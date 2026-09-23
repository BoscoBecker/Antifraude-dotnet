namespace AntiFraud.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "antifraud.events";
    public string QueueName { get; set; } = "antifraud.transactions";
    public string RoutingKey { get; set; } = "transaction.received";
}
