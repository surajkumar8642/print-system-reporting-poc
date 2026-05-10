namespace ReportPoc.Services;

public interface IRootPathResolver
{
    string ResolveReportsRootPath();
    string ResolveTemplatesPath();
    string GetCatalogPath();
}

public sealed class RootPathResolver(string configuredPath, string contentRoot) : IRootPathResolver
{
    private readonly string _configuredPath = configuredPath;
    private readonly string _contentRoot = contentRoot;

    public string ResolveReportsRootPath()
    {
        if (Path.IsPathRooted(_configuredPath))
        {
            if (Directory.Exists(_configuredPath))
            {
                return _configuredPath;
            }

            throw new DirectoryNotFoundException($"Configured report path '{_configuredPath}' does not exist.");
        }

        var candidate = Path.GetFullPath(Path.Combine(_contentRoot, _configuredPath));
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        var legacyCandidate = Path.GetFullPath(Path.Combine(_contentRoot, "..", "..", "Reports"));
        if (Directory.Exists(legacyCandidate))
        {
            return legacyCandidate;
        }

        throw new DirectoryNotFoundException($"Report path not found. Checked '{candidate}' and '{legacyCandidate}'.");
    }

    public string GetCatalogPath()
    {
        return Path.Combine(ResolveReportsRootPath(), "Catalog", "reports.json");
    }

    public string ResolveTemplatesPath()
    {
        return Path.Combine(ResolveReportsRootPath(), "Templates");
    }
}
