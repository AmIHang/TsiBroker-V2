using RabbitMQ.Client;

namespace TsiBroker.Core.Messaging;

// Declares the queue topology shared by RabbitMqMessagePublisher and RabbitMqMessageConsumer.
// Both open the same queue, so they must declare it with identical arguments — RabbitMQ
// rejects a re-declare whose arguments differ from the queue's original declaration.
internal static class RabbitMqTopology
{
    public static async Task DeclareAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken cancellationToken)
    {
        // Declared first so it exists before the main queue can ever dead-letter into it.
        await channel.QueueDeclareAsync(
            queue: options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Using the default exchange ("") as the dead-letter exchange routes a nacked
        // (requeue: false) message straight to DeadLetterQueueName by routing key, with
        // no extra exchange/binding to declare.
        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = options.DeadLetterQueueName,
        };

        await channel.QueueDeclareAsync(
            queue: options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellationToken);
    }
}
