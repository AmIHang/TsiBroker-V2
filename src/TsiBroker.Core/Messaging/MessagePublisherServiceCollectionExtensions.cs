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
        var queueType = configuration
            .GetSection(MessagingOptions.SectionName)
            .Get<MessagingOptions>()?.QueueType ?? MessageQueueType.RabbitMq;

        switch (queueType)
        {
            case MessageQueueType.RabbitMq:
                services
                    .AddOptions<RabbitMqOptions>()
                    .Bind(configuration.GetSection(RabbitMqOptions.SectionName));
                services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
                break;
            case MessageQueueType.Debug:
                services.AddSingleton<IMessagePublisher, DebugMessagePublisher>();
                break;
            default:
                throw new NotSupportedException($"Unsupported message queue type '{queueType}'.");
        }

        return services;
    }
}
