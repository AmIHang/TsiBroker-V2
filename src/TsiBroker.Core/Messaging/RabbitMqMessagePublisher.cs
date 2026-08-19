using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace TsiBroker.Core.Messaging;

public sealed class RabbitMqMessagePublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqMessagePublisher> logger) : IMessagePublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _declaredQueues = new();
    private IConnection? _connection;
    private IChannel? _channel;

    // Opens (or reuses) the connection so callers — e.g. a startup check — can verify
    // the broker is reachable without publishing a message.
    public Task EnsureConnectedAsync(CancellationToken cancellationToken = default) =>
        GetChannelAsync(cancellationToken);

    public async Task PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default)
    {
        var queueName = string.IsNullOrWhiteSpace(message.PartitionKey)
            ? _options.QueueName
            : RabbitMqQueueNaming.ForPartition(message.PartitionKey);

        var channel = await GetChannelAsync(cancellationToken);
        await EnsureQueueDeclaredAsync(channel, queueName, cancellationToken);
        await PublishToQueueAsync(channel, queueName, message, cancellationToken);

        logger.LogInformation(
            "Published message {MessageId} from {Sender} to {Receiver} on queue '{QueueName}'",
            message.Id,
            message.Sender,
            message.Receiver,
            queueName);
    }

    public async Task PublishToDeadLetterAsync(string partitionKey, BrokerMessage message, CancellationToken cancellationToken = default)
    {
        // Declare topology via the *main* queue name — that's what pairs the dead-letter queue
        // with matching arguments; declaring the dead-letter queue name directly here would
        // conflict with its existing (argument-less) declaration from RabbitMqTopology.DeclareAsync.
        var queueName = RabbitMqQueueNaming.ForPartition(partitionKey);
        var channel = await GetChannelAsync(cancellationToken);
        await EnsureQueueDeclaredAsync(channel, queueName, cancellationToken);

        var deadLetterQueueName = RabbitMqTopology.DeadLetterQueueNameFor(queueName);
        await PublishToQueueAsync(channel, deadLetterQueueName, message, cancellationToken);

        logger.LogInformation(
            "Published message {MessageId} from {Sender} to {Receiver} directly to dead-letter queue '{QueueName}'",
            message.Id,
            message.Sender,
            message.Receiver,
            deadLetterQueueName);
    }

    // Raw publish only — callers must ensure the target queue's topology is already declared
    // (via EnsureQueueDeclaredAsync on its *main* queue name) before calling this.
    private static async Task PublishToQueueAsync(IChannel channel, string queueName, BrokerMessage message, CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/xml",
            MessageId = message.Id,
            Headers = new Dictionary<string, object?>
            {
                ["Sender"] = message.Sender,
                ["Receiver"] = message.Receiver,
                // Round-trip format so RabbitMqMessageConsumer.ToBrokerMessage can parse it back
                // exactly — see BrokerMessage.CreatedAt.
                ["CreatedAt"] = message.CreatedAt.ToString("o"),
            },
        };

        var body = Encoding.UTF8.GetBytes(message.Content);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureQueueDeclaredAsync(IChannel channel, string queueName, CancellationToken cancellationToken)
    {
        if (_declaredQueues.ContainsKey(queueName))
        {
            return;
        }

        await RabbitMqTopology.DeclareAsync(
            channel,
            queueName,
            RabbitMqTopology.DeadLetterQueueNameFor(queueName),
            cancellationToken);

        _declaredQueues[queueName] = 0;
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

            logger.LogInformation(
                "Connected to RabbitMQ at {HostName}:{Port} (vhost '{VirtualHost}')",
                _options.HostName,
                _options.Port,
                _options.VirtualHost);

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
