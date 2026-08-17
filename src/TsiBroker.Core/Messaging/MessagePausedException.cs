namespace TsiBroker.Core.Messaging;

// Thrown by a partition handler to signal that processing of its partition should pause and
// that the message currently being handled must be requeued for redelivery once processing
// resumes — unlike a normal handler exception, this does not count toward
// RabbitMqOptions.MaxDeliveryAttempts and never results in the message being dead-lettered.
// The handler itself is responsible for persisting whatever "paused" state it needs and for
// actually stopping the partition (e.g. via IMessageConsumer.StopPartitionAsync) — throwing
// this only controls how the *current* delivery is acknowledged.
public sealed class MessagePausedException : Exception;
