namespace TsiBroker.Core.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    // Whether to connect via TLS (amqps) instead of plain TCP (amqp).
    public bool UseTls { get; set; }

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "tsibroker";

    public string Password { get; set; } = "devpassword123";

    // Used for messages published/consumed without a PartitionKey. Partitioned messages
    // instead get their own queue, named by RabbitMqQueueNaming from the partition key.
    public string QueueName { get; set; } = "tsi-messages";

    // How many times a failing message is retried (in place, blocking that queue) before
    // it is moved to its dead-letter queue ("{queue}.dead-letter") and processing moves on.
    public int MaxDeliveryAttempts { get; set; } = 3;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}
