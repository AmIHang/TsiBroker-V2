namespace TsiBroker.Core.Messaging;

// Message types with special handling shared across the authorization services
// (TsiMessageAuthorizationService, IsbMessageAuthorizationService, EvuMessageAuthorizationService).
public static class TafTsiMessageType
{
    // ErrorMessage: sent by the receiver to report that a previously received message failed
    // processing. Always authorized regardless of an assignment's AllowedMessageTypes* list -
    // gating it the same as any other type would mean a partner that's had a message type's
    // permission revoked (or never had it) can't even be told a message of that type failed,
    // including for messages sent back when the permission was still in place.
    public const string ErrorMessage = "9000";
}

public record BrokerMessage(
    string? Id,
    string Sender,
    string Receiver,
    string Content,
    // When the broker first accepted this message (RU submission or IM /ci receipt) — carried
    // through re-publishes (including onto the wire via RabbitMqMessagePublisher/
    // RabbitMqMessageConsumer's "CreatedAt" header) so it still reflects the original submission
    // time, not any later requeue. Used by InfrastructureOperatorConsumerCoordinator to bound how
    // long a message keeps being resent after a delivery timeout (spec 2.3.2 step 2c: "erneut
    // gesendet, bis das maximale Alter der Nachricht erreicht ist").
    DateTimeOffset CreatedAt,
    // Groups messages that must be processed in order relative to each other (e.g. the
    // sending RU's master-data name) — see IMessageConsumer.RunPartitionedAsync. Messages
    // without one fall back to the publisher/consumer's single default queue.
    string? PartitionKey = null,
    // The TAF/TAP TSI MessageType (from the message's MessageHeader/MessageReference), used to
    // authorize delivery against IsbAssignment.AllowedMessageTypesEvuToBroker/BrokerToEvu.
    string MessageType = "");
