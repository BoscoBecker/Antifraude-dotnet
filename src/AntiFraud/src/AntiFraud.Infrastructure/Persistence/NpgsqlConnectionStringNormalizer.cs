using Npgsql;

namespace AntiFraud.Infrastructure.Persistence;

internal static class NpgsqlConnectionStringNormalizer
{
    /// <summary>
    /// Ajusta user/password (aspas) e evita truncar senha no <c>#</c> quando a string não usa aspas ou %23.
    /// </summary>
    public static string Normalize(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        builder.Username = TrimSecret(builder.Username ?? string.Empty);
        builder.Password = TrimSecret(builder.Password ?? string.Empty);

        if (builder.Password.Contains('#') &&
            !connectionString.Contains("%23", StringComparison.Ordinal) &&
            !connectionString.Contains("Password='", StringComparison.OrdinalIgnoreCase))
        {
            var recovered = TryRecoverPasswordAfterHash(connectionString);
            if (!string.IsNullOrEmpty(recovered))
            {
                builder.Password = recovered;
            }
        }

        return builder.ConnectionString;
    }

    private static string TrimSecret(string value) =>
        value.Trim().Trim('"').Trim('\'');

    private static string? TryRecoverPasswordAfterHash(string connectionString)
    {
        const string prefix = "Password=";
        var idx = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var start = idx + prefix.Length;
        if (start >= connectionString.Length)
        {
            return null;
        }

        if (connectionString[start] is '\'' or '"')
        {
            return null;
        }

        var rest = connectionString[start..];
        var end = rest.IndexOf(';');
        var segment = end >= 0 ? rest[..end] : rest;
        return segment.Trim();
    }
}
