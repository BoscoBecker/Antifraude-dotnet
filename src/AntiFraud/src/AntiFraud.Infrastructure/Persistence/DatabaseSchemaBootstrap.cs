using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AntiFraud.Infrastructure.Persistence;

/// <summary>
/// Schema via DDL idempotente (<see cref="DdlScriptLocator"/> → scripts/ddl.sql). Sem EF migrations.
/// </summary>
public static class DatabaseSchemaBootstrap
{
    /// <summary>Serializa apply do DDL entre API, Worker e evita corrida com CREATE TABLE paralelo.</summary>
    private const int AdvisoryLockNamespace = 0x414E_5449; // ANTI
    private const int AdvisoryLockId = 0x4641_5544; // FAUD

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
            var detail = await TryGetConnectionFailureDetailAsync(db, cancellationToken);
            var message =
                "Não foi possível conectar ao PostgreSQL. Suba o AntiFraud.AppHost (Aspire) ou confira ConnectionStrings:AntiFraud nos User Secrets. " +
                "e confira ConnectionStrings:AntiFraud nos User Secrets (senha com # exige Password='...' ou %23). " +
                detail;
            logger.LogCritical("{Message}", message);
            throw new InvalidOperationException(message);
        }

        if (!await AllTablesExistAsync(db, cancellationToken))
        {
            await EnsureSchemaUnderAdvisoryLockAsync(db, logger, cancellationToken);

            if (!await AllTablesExistAsync(db, cancellationToken))
            {
                const string message =
                    "Tabelas AntiFraud ausentes após DDL. Verifique src/AntiFraud/scripts/ddl.sql ou init Postgres no AppHost.";
                logger.LogCritical("{Message} Path={DdlPath}", message, DdlScriptLocator.CanonicalRelativePath);
                throw new InvalidOperationException(message);
            }
        }

        logger.LogInformation(
            "PostgreSQL pronto (schema via DDL: transactions, fraud_evaluations, outbox_messages, audit_logs).");
    }

    private static async Task<string> TryGetConnectionFailureDetailAsync(
        AntiFraudDbContext db,
        CancellationToken cancellationToken)
    {
        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "Detalhe: connection string indisponível no DbContext.";
        }

        connectionString = NpgsqlConnectionStringNormalizer.Normalize(connectionString);

        try
        {
            var csb = new NpgsqlConnectionStringBuilder(connectionString);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return $"Conectou após normalização (Host={csb.Host}, Port={csb.Port}, Database={csb.Database}).";
        }
        catch (Exception ex)
        {
            try
            {
                var csb = new NpgsqlConnectionStringBuilder(connectionString);
                return $"Detalhe: Host={csb.Host}, Port={csb.Port}, Database={csb.Database}, User={csb.Username}. Erro: {ex.Message}";
            }
            catch
            {
                return $"Detalhe: {ex.Message}";
            }
        }
    }

    private static async Task<bool> AllTablesExistAsync(AntiFraudDbContext db, CancellationToken cancellationToken)
    {
        foreach (var table in RequiredTables)
        {
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
                return false;
            }
        }

        return true;
    }

    private static async Task EnsureSchemaUnderAdvisoryLockAsync(
        AntiFraudDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Connection string AntiFraud indisponível.");

        connectionString = NpgsqlConnectionStringNormalizer.Normalize(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var lockCommand = new NpgsqlCommand(
                         "SELECT pg_advisory_lock(@ns, @id);",
                         connection))
        {
            lockCommand.Parameters.AddWithValue("ns", AdvisoryLockNamespace);
            lockCommand.Parameters.AddWithValue("id", AdvisoryLockId);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            if (await AllTablesExistAsync(db, cancellationToken))
            {
                logger.LogInformation(
                    "Schema AntiFraud já presente (init Postgres ou outro host aplicou o DDL).");
                return;
            }

            await ApplyDdlScriptOnConnectionAsync(connection, logger, cancellationToken);
        }
        finally
        {
            await using var unlockCommand = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(@ns, @id);",
                connection);
            unlockCommand.Parameters.AddWithValue("ns", AdvisoryLockNamespace);
            unlockCommand.Parameters.AddWithValue("id", AdvisoryLockId);
            await unlockCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ApplyDdlScriptOnConnectionAsync(
        NpgsqlConnection connection,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var ddlPath = DdlScriptLocator.FindDdlScriptPath();
        if (ddlPath is null)
        {
            logger.LogWarning(
                "DDL não encontrado ({Path}). Pulando apply automático.",
                DdlScriptLocator.CanonicalRelativePath);
            return;
        }

        var sql = await File.ReadAllTextAsync(ddlPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(sql))
        {
            logger.LogWarning("Arquivo DDL vazio: {Path}", ddlPath);
            return;
        }

        logger.LogInformation("Aplicando DDL idempotente: {Path}", ddlPath);

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (IsBenignSchemaRace(ex))
        {
            logger.LogWarning(
                ex,
                "DDL concorrente (primeira subida Aspire/API+Worker ou init Docker): {SqlState}. Revalidando tabelas.",
                ex.SqlState);
        }
    }

    private static bool IsBenignSchemaRace(PostgresException ex) =>
        ex.SqlState is "23505" or "42P07" or "42710"
        || ex.Message.Contains("pg_type_typname_nsp_index", StringComparison.Ordinal);
}
