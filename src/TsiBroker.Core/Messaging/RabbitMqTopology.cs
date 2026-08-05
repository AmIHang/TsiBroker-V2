using RabbitMQ.Client;

namespace TsiBroker.Core.Messaging;

// Declares a queue + its dead-letter queue, shared by RabbitMqMessagePublisher and
// RabbitMqMessageConsumer. Both may open the same queue, so they must declare it with
// identical arguments — RabbitMQ rejects a re-declare whose arguments differ from the
// queue's original declaration.
internal static class RabbitMqTopology
{
    public static string DeadLetterQueueNameFor(string queueName) => $"{queueName}.dead-letter";

    public static async Task DeclareAsync(
        IChannel channel,
        string queueName,
        string deadLetterQueueName,
        CancellationToken cancellationToken)
    {
        // Declared first so it exists before the main queue can ever dead-letter into it.
        await channel.QueueDeclareAsync(
            queue: deadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Using the default exchange ("") as the dead-letter exchange routes a nacked
        // (requeue: false) message straight to deadLetterQueueName by routing key, with
        // no extra exchange/binding to declare.
        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = deadLetterQueueName,
        };

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellationToken);
    }
}
