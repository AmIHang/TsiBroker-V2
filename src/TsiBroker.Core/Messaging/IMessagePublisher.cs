namespace TsiBroker.Core.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync(
        BrokerMessage message,
        CancellationToken cancellationToken = default);

    // Publishes directly into partitionKey's dead-letter queue rather than its main queue —
    // used to route a message that was rejected (e.g. failed authorization) or that a consumer
    // gave up on into that partition's own error queue, for manual inspection, without going
    // through the normal consume-then-nack path.
    Task PublishToDeadLetterAsync(
        string partitionKey,
        BrokerMessage message,
        CancellationToken cancellationToken = default);
}
