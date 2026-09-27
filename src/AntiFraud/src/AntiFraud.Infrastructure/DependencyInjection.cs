using AntiFraud.Application.Abstractions;
using AntiFraud.Domain.Repositories;
using AntiFraud.Infrastructure.Audit;
using AntiFraud.Infrastructure.Messaging;
using AntiFraud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AntiFraud.Infrastructure;

public enum InfrastructureHostRole
{
    Api,
    Worker
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        InfrastructureHostRole hostRole)
    {
        services.AddOptions<RabbitMqOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection(RabbitMqOptions.SectionName).Bind(options);
                AspireRabbitMqConfiguration.ApplyConnectionStringIfPresent(options, config);
            });
        var connectionString = configuration.GetConnectionString("AntiFraud");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:AntiFraud não configurada. Use dotnet user-secrets ou variáveis de ambiente " +
                "(ver src/AntiFraud/docs/user-secrets.md).");
        }

        connectionString = NpgsqlConnectionStringNormalizer.Normalize(connectionString);

        services.AddDbContext<AntiFraudDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AntiFraudDbContext>());
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();

        AddRabbitMqMessaging(services, hostRole);

        return services;
    }

    /// <summary>
    /// Outbox (Postgres) → relay na API → RabbitMQ → consumer no Worker → avaliação de fraude.
    /// </summary>
    private static void AddRabbitMqMessaging(
        IServiceCollection services,
        InfrastructureHostRole hostRole)
    {
        services.AddSingleton<RabbitMqTransactionQueuePublisher>();
        services.AddSingleton<ITransactionQueuePublisher>(sp =>
            sp.GetRequiredService<RabbitMqTransactionQueuePublisher>());

        if (hostRole == InfrastructureHostRole.Api)
        {
            services.AddHostedService<OutboxRabbitRelayWorker>();
        }
        else
        {
            services.AddHostedService<RabbitMqTransactionEvaluationConsumer>();
        }
    }
}
