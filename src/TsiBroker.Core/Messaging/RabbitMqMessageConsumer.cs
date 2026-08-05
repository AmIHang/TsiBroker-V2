using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TsiBroker.Core.Messaging;

public sealed class RabbitMqMessageConsumer(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqMessageConsumer> logger) : IMessageConsumer, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task RunAsync(
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
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
                catch (Exception ex) when (attempt < _options.MaxDeliveryAttempts)
                {
                    logger.LogWarning(
                        ex,
                        "Failed processing message {MessageId} from {Sender} to {Receiver} (attempt {Attempt}/{MaxAttempts}) — retrying in {RetryDelay}",
                        message.Id,
                        message.Sender,
                        message.Receiver,
                        attempt,
                        _options.MaxDeliveryAttempts,
                        _options.RetryDelay);
                    await Task.Delay(_options.RetryDelay, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Giving up on message {MessageId} from {Sender} to {Receiver} after {Attempts} attempts — moving to '{DeadLetterQueueName}'",
                        message.Id,
                        message.Sender,
                        message.Receiver,
                        attempt,
                        _options.DeadLetterQueueName);
                    await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                    return;
                }
            }
        };

        await channel.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
    }

    private static BrokerMessage ToBrokerMessage(BasicDeliverEventArgs delivery)
    {
        var content = Encoding.UTF8.GetString(delivery.Body.ToArray());
        var sender = GetHeader(delivery, "Sender");
        var receiver = GetHeader(delivery, "Receiver");

        return new BrokerMessage(delivery.BasicProperties.MessageId, sender, receiver, content);
    }

    private static string GetHeader(BasicDeliverEventArgs delivery, string key)
    {
        if (delivery.BasicProperties.Headers?.TryGetValue(key, out var value) == true && value is byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return string.Empty;
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
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
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await RabbitMqTopology.DeclareAsync(_channel, _options, cancellationToken);

            // One unacked (in-flight) message at a time: the broker won't push the next
            // message until this one is ack'd/nack'd, which is what keeps processing —
            // including retries of a failing message — strictly in queue order.
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken);

            logger.LogInformation(
                "Connected to RabbitMQ at {HostName}:{Port} (vhost '{VirtualHost}'), consuming queue '{QueueName}' (dead-letter: '{DeadLetterQueueName}')",
                _options.HostName,
                _options.Port,
                _options.VirtualHost,
                _options.QueueName,
                _options.DeadLetterQueueName);

            return _channel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _connectionLock.Dispose();
    }
}
