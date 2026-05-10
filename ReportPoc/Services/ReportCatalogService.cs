using System.Text.Json;
using ReportPoc.Models;

namespace ReportPoc.Services;

public interface IReportCatalogService
{
    Task<List<ReportCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ReportCatalogEntry?> GetByCodeAsync(string reportCode, CancellationToken cancellationToken = default);
    Task<ReportDefinition?> LoadDefinitionAsync(ReportCatalogEntry entry, string version, CancellationToken cancellationToken = default);
    string ResolveTemplateDirectory(ReportCatalogEntry entry, string? version = null);
}

public sealed class ReportCatalogService(IRootPathResolver resolver) : IReportCatalogService
{
    private readonly IRootPathResolver _resolver = resolver;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<ReportCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(_resolver.GetCatalogPath());
        var records = await JsonSerializer.DeserializeAsync<List<ReportCatalogEntry>>(stream, _jsonOptions, cancellationToken)
                      ?? [];
        return records;
    }

    public async Task<ReportCatalogEntry?> GetByCodeAsync(string reportCode, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.FirstOrDefault(x => string.Equals(x.ReportCode, reportCode, StringComparison.OrdinalIgnoreCase));
    }

    public string ResolveTemplateDirectory(ReportCatalogEntry entry, string? version = null)
    {
        var templatesRoot = _resolver.ResolveTemplatesPath();
        var variant = entry.Variant ?? "DEFAULT";
        var family = entry.Family ?? "General";
        var selectedVersion = version ?? entry.ActiveVersion ?? "v1";
        return Path.Combine(templatesRoot, family, variant, selectedVersion);
    }

    public async Task<ReportDefinition?> LoadDefinitionAsync(ReportCatalogEntry entry, string version, CancellationToken cancellationToken = default)
    {
        var directory = ResolveTemplateDirectory(entry, version);
        var definitionPath = Path.Combine(directory, "definition.json");
        if (!File.Exists(definitionPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(definitionPath);
        return await JsonSerializer.DeserializeAsync<ReportDefinition>(stream, _jsonOptions, cancellationToken);
    }
}
