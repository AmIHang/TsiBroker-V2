using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TsiBroker.Core.Messaging;

public static class MessagePublisherServiceCollectionExtensions
{
    // Registers the IMessagePublisher implementation selected by Messaging:QueueType
    // (env var Messaging__QueueType) — e.g. RabbitMq or Debug. Additional queue backends
    // (e.g. AWS Service Bus) get their own case here and their own Options type,
    // without any change needed in feature code.
    public static IServiceCollection AddMessagePublisher(this IServiceCollection services, IConfiguration configuration)
    {
        switch (GetQueueType(configuration))
        {
            case MessageQueueType.RabbitMq:
                services.AddRabbitMqOptions(configuration);
                services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
                break;
            case MessageQueueType.Debug:
                services.AddSingleton<IMessagePublisher, DebugMessagePublisher>();
                break;
            case var queueType:
                throw new NotSupportedException($"Unsupported message queue type '{queueType}'.");
        }

        return services;
    }

    // Registers the IMessageConsumer implementation selected by Messaging:QueueType, the
    // same switch as AddMessagePublisher. Consuming is a separate concern from publishing
    // (a process may only need one of the two), so it's opt-in via its own call.
    public static IServiceCollection AddMessageConsumer(this IServiceCollection services, IConfiguration configuration)
    {
        switch (GetQueueType(configuration))
        {
            case MessageQueueType.RabbitMq:
                services.AddRabbitMqOptions(configuration);
                services.AddSingleton<IMessageConsumer, RabbitMqMessageConsumer>();
                break;
            case MessageQueueType.Debug:
                services.AddSingleton<IMessageConsumer, DebugMessageConsumer>();
                break;
            case var queueType:
                throw new NotSupportedException($"Unsupported message queue type '{queueType}'.");
        }

        return services;
    }

    private static MessageQueueType GetQueueType(IConfiguration configuration) =>
        configuration
            .GetSection(MessagingOptions.SectionName)
            .Get<MessagingOptions>()?.QueueType ?? MessageQueueType.RabbitMq;

    // Safe to call more than once (e.g. a host registers both a publisher and a
    // consumer) — options binding composes rather than conflicting.
    private static void AddRabbitMqOptions(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName));
}
