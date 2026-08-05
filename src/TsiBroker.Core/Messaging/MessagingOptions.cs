namespace TsiBroker.Core.Messaging;

public class MessagingOptions
{
    public const string SectionName = "Messaging";

    public MessageQueueType QueueType { get; set; } = MessageQueueType.RabbitMq;
}
