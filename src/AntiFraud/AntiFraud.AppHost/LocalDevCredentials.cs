using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AntiFraud.AppHost;

internal sealed record LocalDevCredentials(
    string PostgresUser,
    string PostgresPassword,
    string PostgresDatabase,
    string RabbitUser,
    string RabbitPassword,
    string PgAdminEmail,
    string PgAdminPassword)
{
    public static LocalDevCredentials FromConfiguration(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AntiFraud")
            ?? configuration["ConnectionStrings:AntiFraud"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:AntiFraud não configurada no AppHost. " +
                "Configure User Secrets da API (mesmo UserSecretsId) — docs/user-secrets.md.");
        }

        string pgUser;
        string pgPassword;
        string pgDatabase;

        try
        {
            var npgsql = new NpgsqlConnectionStringBuilder(connectionString);
            pgUser = NormalizeSecret(npgsql.Username ?? "postgres");
            pgPassword = NormalizeSecret(npgsql.Password ?? string.Empty);
            pgDatabase = NormalizeSecret(npgsql.Database ?? "antifraud");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:AntiFraud inválida nos User Secrets. Use formato Npgsql (Password=...).",
                ex);
        }

        if (string.IsNullOrEmpty(pgPassword))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:AntiFraud deve incluir Password (mesmo valor dos User Secrets / .env local). " +
                "Senha com # use Password='...' na connection string ou %23.");
        }

        var overridePassword = configuration["Postgres:Password"];
        if (!string.IsNullOrWhiteSpace(overridePassword))
        {
            pgPassword = NormalizeSecret(overridePassword);
        }

        if (pgPassword.Contains('#') &&
            !connectionString.Contains("%23", StringComparison.Ordinal) &&
            !connectionString.Contains("Password='", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "[AppHost] AVISO: senha Postgres contém '#'. Se a autenticação falhar, use Password='...' ou %23 nos User Secrets.");
        }

        var rabbitUser = NormalizeSecret(configuration["RabbitMq:UserName"] ?? string.Empty);
        var rabbitPassword = NormalizeSecret(configuration["RabbitMq:Password"] ?? string.Empty);

        if (string.IsNullOrEmpty(rabbitUser) || string.IsNullOrEmpty(rabbitPassword))
        {
            throw new InvalidOperationException(
                "RabbitMq:UserName e RabbitMq:Password são obrigatórios nos User Secrets (mesmos da API).");
        }

        var pgAdminEmail = NormalizeSecret(configuration["PgAdmin:DefaultEmail"] ?? string.Empty);
        var pgAdminPassword = NormalizeSecret(configuration["PgAdmin:DefaultPassword"] ?? string.Empty);

        if (PgAdminDockerEnvReader.TryRead(out var envEmail, out var envPassword))
        {
            if (string.IsNullOrEmpty(pgAdminEmail))
            {
                pgAdminEmail = NormalizeSecret(envEmail);
            }

            if (string.IsNullOrEmpty(pgAdminPassword))
            {
                pgAdminPassword = NormalizeSecret(envPassword);
            }
        }

        if (string.IsNullOrEmpty(pgAdminEmail))
        {
            pgAdminEmail = "admin@example.com";
        }

        if (string.IsNullOrEmpty(pgAdminPassword))
        {
            pgAdminPassword = pgPassword;
        }

        if (IsInvalidPgAdminEmail(pgAdminEmail))
        {
            if (PgAdminDockerEnvReader.TryRead(out envEmail, out envPassword)
                && !IsInvalidPgAdminEmail(envEmail))
            {
                Console.WriteLine(
                    "[AppHost] PgAdmin:DefaultEmail inválido nos secrets; usando PGADMIN_DEFAULT_EMAIL do .env local.");
                pgAdminEmail = NormalizeSecret(envEmail);
                if (string.IsNullOrEmpty(configuration["PgAdmin:DefaultPassword"]))
                {
                    pgAdminPassword = NormalizeSecret(envPassword);
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "PgAdmin:DefaultEmail inválido (pgAdmin rejeita @*.local e domínios reservados). " +
                    "Defina PgAdmin:DefaultEmail nos User Secrets ou PGADMIN_DEFAULT_EMAIL no .env local " +
                    "(ex.: testenetrin@gmail.com).");
            }
        }

        return new LocalDevCredentials(
            pgUser, pgPassword, pgDatabase, rabbitUser, rabbitPassword, pgAdminEmail, pgAdminPassword);
    }

    private static string NormalizeSecret(string value) =>
        value.Trim().Trim('"').Trim('\'');

    private static bool IsInvalidPgAdminEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return true;
        }

        var domain = email.Split('@', 2)[1];
        return domain.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
               || domain.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }
}
