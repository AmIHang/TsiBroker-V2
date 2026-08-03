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
}
