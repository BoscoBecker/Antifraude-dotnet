using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AntiFraud.Infrastructure.Persistence;

/// <summary>
/// Schema é criado apenas pelo DDL (src/docker/pgadmin/scripts/ddl.sql). Sem migrations / EnsureCreated.
/// </summary>
public static class DatabaseSchemaBootstrap
{
    private static readonly string[] RequiredTables =
    [
        "transactions",
        "fraud_evaluations",
        "outbox_messages",
        "audit_logs"
    ];

    public static async Task EnsureReadyAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseSchemaBootstrap));

        var db = scope.ServiceProvider.GetRequiredService<AntiFraudDbContext>();

        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            const string message =
                "Não foi possível conectar ao PostgreSQL. Suba src/docker/pgadmin e confira ConnectionStrings:AntiFraud.";
            logger.LogCritical(message);
            throw new InvalidOperationException(message);
        }

        foreach (var table in RequiredTables)
        {
            // SqlQuery<int> exige coluna "Value" no result set (EF Core + Npgsql).
            var count = await db.Database
                .SqlQuery<int>(
                    $"""
                     SELECT COUNT(*)::int AS "Value"
                     FROM information_schema.tables
                     WHERE table_schema = 'public' AND table_name = {table}
                     """)
                .SingleAsync(cancellationToken);

            if (count == 0)
            {
                var message =
                    $"Tabela public.{table} não encontrada. Aplique o DDL em src/docker/pgadmin/scripts/ddl.sql " +
                    "(init do volume na 1ª subida do Postgres ou execução manual).";
                logger.LogCritical(message);
                throw new InvalidOperationException(message);
            }
        }

        logger.LogInformation(
            "PostgreSQL pronto (schema via DDL: transactions, fraud_evaluations, outbox_messages, audit_logs).");
    }
}
