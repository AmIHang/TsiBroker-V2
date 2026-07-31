namespace TsiBroker.Im.Api.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync(
        BrokerMessage message,
        CancellationToken cancellationToken = default);
}
