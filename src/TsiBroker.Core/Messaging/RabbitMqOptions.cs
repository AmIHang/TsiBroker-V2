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

    public string QueueName { get; set; } = "tsi-messages";
}
