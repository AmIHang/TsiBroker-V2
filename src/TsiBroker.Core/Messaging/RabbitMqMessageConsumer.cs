using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace TsiBroker.Core.Messaging;

public sealed class RabbitMqMessageConsumer(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqMessageConsumer> logger) : IMessageConsumer, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, PartitionHandle> _partitions = new();
    private IConnection? _connection;

    public async Task RunAsync(
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var (channel, _) = await SetupConsumerAsync(
            connection, _options.QueueName, handler, cancellationToken, cancellationToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
        finally
        {
            await channel.CloseAsync();
            channel.Dispose();
        }
    }

    // Starts a background consume loop for partitionKey that keeps running (independently of
    // cancellationToken, which only bounds this setup call) until StopPartitionAsync is called
    // or this consumer is disposed. No-op if partitionKey is already running.
    public async Task StartPartitionAsync(
        string partitionKey,
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var queueName = RabbitMqQueueNaming.ForPartition(partitionKey);
        if (_partitions.ContainsKey(queueName))
        {
            return;
        }

        var connection = await GetConnectionAsync(cancellationToken);
        var runCts = new CancellationTokenSource();
        var (channel, deadLetterQueueName) = await SetupConsumerAsync(
            connection, queueName, handler, runCts.Token, cancellationToken);

        var handle = new PartitionHandle(channel, runCts, WaitUntilCancelledAsync(runCts.Token));

        if (!_partitions.TryAdd(queueName, handle))
        {
            // Lost a race with a concurrent StartPartitionAsync for the same key — the other
            // call's channel/consumer wins; tear down the redundant one we just opened.
            await handle.DisposeAsync();
            return;
        }

        logger.LogInformation(
            "Started consuming queue '{QueueName}' for partition '{PartitionKey}' (dead-letter: '{DeadLetterQueueName}')",
            queueName,
            partitionKey,
            deadLetterQueueName);
    }

    public async Task StopPartitionAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        var queueName = RabbitMqQueueNaming.ForPartition(partitionKey);
        if (!_partitions.TryRemove(queueName, out var handle))
        {
            return;
        }

        await handle.DisposeAsync();

        logger.LogInformation(
            "Stopped consuming queue '{QueueName}' for partition '{PartitionKey}'",
            queueName,
            partitionKey);
    }

    public bool IsPartitionActive(string partitionKey) =>
        _partitions.ContainsKey(RabbitMqQueueNaming.ForPartition(partitionKey));

    // A passive declare only asks the broker for the queue's current stats — it never
    // creates the queue, so a partition that has never been started correctly reports "no
    // such queue" (404, surfaced as OperationInterruptedException) rather than an empty one.
    public Task<int?> GetMessageCountAsync(string partitionKey, CancellationToken cancellationToken = default) =>
        GetQueueMessageCountAsync(RabbitMqQueueNaming.ForPartition(partitionKey), cancellationToken);

    public Task<int?> GetDeadLetterMessageCountAsync(string partitionKey, CancellationToken cancellationToken = default) =>
        GetQueueMessageCountAsync(
            RabbitMqTopology.DeadLetterQueueNameFor(RabbitMqQueueNaming.ForPartition(partitionKey)),
            cancellationToken);

    private async Task<int?> GetQueueMessageCountAsync(string queueName, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        try
        {
            var result = await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);
            return (int)result.MessageCount;
        }
        catch (OperationInterruptedException)
        {
            return null;
        }
        finally
        {
            if (channel.IsOpen)
            {
                await channel.CloseAsync(cancellationToken);
            }

            channel.Dispose();
        }
    }

    private async Task<(IChannel Channel, string DeadLetterQueueName)> SetupConsumerAsync(
        IConnection connection,
        string queueName,
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken processingCancellationToken,
        CancellationToken setupCancellationToken)
    {
        var deadLetterQueueName = RabbitMqTopology.DeadLetterQueueNameFor(queueName);

        var channel = await connection.CreateChannelAsync(cancellationToken: setupCancellationToken);
        await RabbitMqTopology.DeclareAsync(channel, queueName, deadLetterQueueName, setupCancellationToken);

        // One unacked (in-flight) message at a time: the broker won't push the next
        // message on this queue until this one is ack'd/nack'd, which is what keeps
        // processing — including retries of a failing message — strictly in queue order.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, setupCancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) =>
            ProcessDeliveryAsync(channel, queueName, deadLetterQueueName, delivery, handler, processingCancellationToken);

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: setupCancellationToken);

        logger.LogInformation(
            "Consuming queue '{QueueName}' (dead-letter: '{DeadLetterQueueName}')",
            queueName,
            deadLetterQueueName);

        return (channel, deadLetterQueueName);
    }

    private async Task ProcessDeliveryAsync(
        IChannel channel,
        string queueName,
        string deadLetterQueueName,
        BasicDeliverEventArgs delivery,
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var message = ToBrokerMessage(delivery);
        var attempt = 0;

        while (true)
        {
            attempt++;
            try
            {
                await handler(message, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
                return;
            }
            catch (MessagePausedException)
            {
                // Not a normal failure: the handler wants this message put back for redelivery
                // once it resumes processing, without counting toward MaxDeliveryAttempts or
                // being dead-lettered.
                logger.LogInformation(
                    "Pausing consumption of '{QueueName}' — requeuing message {MessageId} from {Sender} to {Receiver} for redelivery once resumed",
                    queueName,
                    message.Id,
                    message.Sender,
                    message.Receiver);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxDeliveryAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Failed processing message {MessageId} from {Sender} to {Receiver} on '{QueueName}' (attempt {Attempt}/{MaxAttempts}) — retrying in {RetryDelay}",
                    message.Id,
                    message.Sender,
                    message.Receiver,
                    queueName,
                    attempt,
                    _options.MaxDeliveryAttempts,
                    _options.RetryDelay);
                await Task.Delay(_options.RetryDelay, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Giving up on message {MessageId} from {Sender} to {Receiver} on '{QueueName}' after {Attempts} attempts — moving to '{DeadLetterQueueName}'",
                    message.Id,
                    message.Sender,
                    message.Receiver,
                    queueName,
                    attempt,
                    deadLetterQueueName);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                return;
            }
        }
    }

    private static BrokerMessage ToBrokerMessage(BasicDeliverEventArgs delivery)
    {
        var content = Encoding.UTF8.GetString(delivery.Body.ToArray());
        var sender = GetHeader(delivery, "Sender");
        var receiver = GetHeader(delivery, "Receiver");
        var createdAt = ParseCreatedAtHeader(GetHeader(delivery, "CreatedAt"));

        return new BrokerMessage(delivery.BasicProperties.MessageId, sender, receiver, content, createdAt);
    }

    // Falls back to "now" for a message published before this header existed (a pre-deploy
    // message still sitting on the queue) rather than failing it outright — worst case it gets
    // its own full max-age budget starting from consumption instead of original submission.
    private static DateTimeOffset ParseCreatedAtHeader(string value) =>
        DateTimeOffset.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var createdAt)
            ? createdAt
            : DateTimeOffset.UtcNow;

    private static string GetHeader(BasicDeliverEventArgs delivery, string key)
    {
        if (delivery.BasicProperties.Headers?.TryGetValue(key, out var value) == true && value is byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return string.Empty;
    }

    private static async Task WaitUntilCancelledAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on stop.
        }
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,
                Ssl = new SslOption
                {
                    Enabled = _options.UseTls,
                    ServerName = _options.HostName,
                },
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            logger.LogInformation(
                "Connected to RabbitMQ at {HostName}:{Port} (vhost '{VirtualHost}')",
                _options.HostName,
                _options.Port,
                _options.VirtualHost);

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var queueName in _partitions.Keys.ToList())
        {
            if (_partitions.TryRemove(queueName, out var handle))
            {
                await handle.DisposeAsync();
            }
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _connectionLock.Dispose();
    }

    private sealed record PartitionHandle(IChannel Channel, CancellationTokenSource Cts, Task RunTask)
    {
        public async ValueTask DisposeAsync()
        {
            await Cts.CancelAsync();
            try
            {
                await RunTask;
            }
            catch (OperationCanceledException)
            {
            }

            await Channel.CloseAsync();
            Channel.Dispose();
            Cts.Dispose();
        }
    }
}
