using Microsoft.Extensions.Logging;

namespace TsiBroker.Core.Messaging;

public class DebugMessagePublisher(ILogger<DebugMessagePublisher> logger) : IMessagePublisher
{
    public Task PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Would publish message {MessageId} from {Sender}: {Payload}",
            message.Id,
            message.Sender,
            message.Content);

        return Task.CompletedTask;
    }

    public Task PublishToDeadLetterAsync(string partitionKey, BrokerMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Would publish message {MessageId} from {Sender} directly to dead-letter queue for partition '{PartitionKey}': {Payload}",
            message.Id,
            message.Sender,
            partitionKey,
            message.Content);

        return Task.CompletedTask;
    }
}
