namespace TsiBroker.Im.Api.Messaging;

public record BrokerMessage(
    string? Id,
    string Sender,
    string Receiver,
    string Content);
