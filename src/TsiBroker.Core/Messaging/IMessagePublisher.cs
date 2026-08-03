namespace TsiBroker.Core.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync(
        BrokerMessage message,
        CancellationToken cancellationToken = default);
}
