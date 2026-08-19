using TsiBroker.Core.Messaging;

namespace TsiBroker.ApiService.Tests;

// Stands in for the real RabbitMqMessageConsumer: instead of actually consuming a queue, it just
// captures the handler InfrastructureOperatorConsumerCoordinator.StartAsync registers for a
// partition, so a test can invoke that handler directly against a hand-built BrokerMessage.
internal sealed class CapturingMessageConsumer : IMessageConsumer
{
    private readonly Dictionary<string, Func<BrokerMessage, CancellationToken, Task>> _handlers = [];

    public Func<BrokerMessage, CancellationToken, Task> HandlerFor(string partitionKey) => _handlers[partitionKey];

    public Task RunAsync(Func<BrokerMessage, CancellationToken, Task> handler, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task StartPartitionAsync(
        string partitionKey,
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        _handlers[partitionKey] = handler;
        return Task.CompletedTask;
    }

    public Task StopPartitionAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        _handlers.Remove(partitionKey);
        return Task.CompletedTask;
    }

    public bool IsPartitionActive(string partitionKey) => _handlers.ContainsKey(partitionKey);

    public Task<int?> GetMessageCountAsync(string partitionKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<int?>(0);

    public Task<int?> GetDeadLetterMessageCountAsync(string partitionKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<int?>(0);
}

// Stands in for the real RabbitMqMessagePublisher, just recording what would have been published.
internal sealed class CapturingMessagePublisher : IMessagePublisher
{
    public List<BrokerMessage> DeadLettered { get; } = [];

    public Task PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishToDeadLetterAsync(string partitionKey, BrokerMessage message, CancellationToken cancellationToken = default)
    {
        DeadLettered.Add(message);
        return Task.CompletedTask;
    }
}
