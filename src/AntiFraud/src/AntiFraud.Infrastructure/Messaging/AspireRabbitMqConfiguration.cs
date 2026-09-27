using Microsoft.Extensions.Configuration;

namespace AntiFraud.Infrastructure.Messaging;

internal static class AspireRabbitMqConfiguration
{
    /// <summary>
    /// Aspire injeta ConnectionStrings__rabbitmq (amqp://...) quando o AppHost referencia RabbitMQ.
    /// </summary>
    public static void ApplyConnectionStringIfPresent(RabbitMqOptions options, IConfiguration configuration)
    {
        var aspireConnection = configuration.GetConnectionString("rabbitmq");
        if (string.IsNullOrWhiteSpace(aspireConnection))
        {
            return;
        }

        if (!Uri.TryCreate(aspireConnection, UriKind.Absolute, out var uri))
        {
            return;
        }

        options.HostName = uri.Host;
        options.Port = uri.Port > 0 ? uri.Port : 5672;

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            options.UserName = Uri.UnescapeDataString(parts[0]);
            if (parts.Length > 1)
            {
                options.Password = Uri.UnescapeDataString(parts[1]);
            }
        }
    }
}
