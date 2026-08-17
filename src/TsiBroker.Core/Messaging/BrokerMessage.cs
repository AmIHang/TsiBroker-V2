namespace TsiBroker.Core.Messaging;

public record BrokerMessage(
    string? Id,
    string Sender,
    string Receiver,
    string Content,
    // Groups messages that must be processed in order relative to each other (e.g. the
    // sending RU's master-data name) — see IMessageConsumer.RunPartitionedAsync. Messages
    // without one fall back to the publisher/consumer's single default queue.
    string? PartitionKey = null,
    // The TAF/TAP TSI MessageType (from the message's MessageHeader/MessageReference), used to
    // authorize delivery against IsbAssignment.AllowedMessageTypesEvuToBroker/BrokerToEvu.
    string MessageType = "");
