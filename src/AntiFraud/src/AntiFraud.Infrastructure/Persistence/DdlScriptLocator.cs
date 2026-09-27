namespace AntiFraud.Infrastructure.Persistence;

/// <summary>
/// Localiza <c>src/AntiFraud/scripts/ddl.sql</c> (ou <c>scripts/ddl.sql</c> a partir da pasta da solução).
/// </summary>
public static class DdlScriptLocator
{
    public const string CanonicalRelativePath = "src/AntiFraud/scripts/ddl.sql";

    private static readonly string[] RepoRelativeParts = ["src", "AntiFraud", "scripts", "ddl.sql"];
    private static readonly string[] SolutionRelativeParts = ["scripts", "ddl.sql"];

    public static string? FindDdlScriptPath()
    {
        foreach (var root in GetSearchRoots())
        {
            var dir = root;
            for (var depth = 0; depth < 10 && !string.IsNullOrEmpty(dir); depth++)
            {
                var fromRepo = Path.Combine(new[] { dir }.Concat(RepoRelativeParts).ToArray());
                if (File.Exists(fromRepo))
                {
                    return fromRepo;
                }

                var fromSolution = Path.Combine(new[] { dir }.Concat(SolutionRelativeParts).ToArray());
                if (File.Exists(fromSolution))
                {
                    return fromSolution;
                }

                dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
            if (!string.IsNullOrEmpty(dir))
            {
                yield return dir;
            }
        }
    }
}
