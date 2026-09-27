namespace AntiFraud.AppHost;

/// <summary>
/// Lê PGADMIN_* de src/docker/pgadmin/.env (dev local, alinhado aos User Secrets).
/// </summary>
internal static class PgAdminDockerEnvReader
{
    public static bool TryRead(out string email, out string password)
    {
        email = string.Empty;
        password = string.Empty;

        foreach (var path in ResolveCandidatePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            string? fileEmail = null;
            string? filePassword = null;

            foreach (var line in File.ReadAllLines(path))
            {
                if (TryParseAssignment(line, "PGADMIN_DEFAULT_EMAIL", out var value))
                {
                    fileEmail = value;
                }
                else if (TryParseAssignment(line, "PGADMIN_DEFAULT_PASSWORD", out value))
                {
                    filePassword = value;
                }
            }

            if (!string.IsNullOrWhiteSpace(fileEmail) && !string.IsNullOrWhiteSpace(filePassword))
            {
                email = fileEmail;
                password = filePassword;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> ResolveCandidatePaths()
    {
        yield return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "docker", "pgadmin", ".env"));
        yield return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "docker", "pgadmin", ".env"));
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docker", "pgadmin", ".env"));
    }

    private static bool TryParseAssignment(string line, string key, out string value)
    {
        value = string.Empty;
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return false;
        }

        if (!trimmed.StartsWith(key + "=", StringComparison.Ordinal))
        {
            return false;
        }

        value = trimmed[(key.Length + 1)..].Trim();
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        return !string.IsNullOrWhiteSpace(value);
    }
}
