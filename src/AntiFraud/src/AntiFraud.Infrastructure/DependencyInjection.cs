using AntiFraud.Application.Abstractions;
using AntiFraud.Domain.Repositories;
using AntiFraud.Infrastructure.Audit;
using AntiFraud.Infrastructure.Messaging;
using AntiFraud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        var connectionString = configuration.GetConnectionString("AntiFraud");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:AntiFraud não configurada. Use dotnet user-secrets ou variáveis de ambiente " +
                "(ver src/AntiFraud/docs/user-secrets.md).");
        }

        services.AddDbContext<AntiFraudDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AntiFraudDbContext>());
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();

        var useRabbitMq = configuration.GetValue("Features:UseRabbitMq", false);
        AddMessaging(services, hostRole, useRabbitMq);

        return services;
    }

    private static void AddMessaging(
        IServiceCollection services,
        InfrastructureHostRole hostRole,
        bool useRabbitMq)
    {
        if (useRabbitMq)
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

            return;
        }

        if (hostRole == InfrastructureHostRole.Worker)
        {
            services.AddSingleton<InMemoryTransactionQueuePublisher>();
            services.AddSingleton<ITransactionQueuePublisher>(sp =>
                sp.GetRequiredService<InMemoryTransactionQueuePublisher>());
            services.AddHostedService<OutboxTransactionDispatchWorker>();
            return;
        }

        // API sem Rabbit: outbox é processada pelo Worker (processo separado).
        services.AddSingleton<InMemoryTransactionQueuePublisher>();
        services.AddSingleton<ITransactionQueuePublisher>(sp =>
            sp.GetRequiredService<InMemoryTransactionQueuePublisher>());
    }
}
