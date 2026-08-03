namespace TsiBroker.Core.Messaging;

public record BrokerMessage(
    string? Id,
    string Sender,
    string Receiver,
    string Content);
