using System.Text.Json;
using ReportPoc.Models;

namespace ReportPoc.Services;

public interface IReportExecutorService
{
    Task<ReportExecutionOutput> ExecuteAndRenderAsync(
        ReportCatalogEntry entry,
        string version,
        Dictionary<string, JsonElement>? parameters,
        string? connectionString,
        CancellationToken cancellationToken = default);
}

public sealed class ReportExecutorService(
    IReportCatalogService catalog,
    IDataProviderRegistry providers,
    IReportRenderer renderer,
    IRootPathResolver resolver) : IReportExecutorService
{
    private readonly IReportCatalogService _catalog = catalog;
    private readonly IDataProviderRegistry _providers = providers;
    private readonly IReportRenderer _renderer = renderer;
    private readonly IRootPathResolver _resolver = resolver;

    public async Task<ReportExecutionOutput> ExecuteAndRenderAsync(
        ReportCatalogEntry entry,
        string version,
        Dictionary<string, JsonElement>? parameters,
        string? connectionString,
        CancellationToken cancellationToken = default)
    {
        var folder = _catalog.ResolveTemplateDirectory(entry, version);
        var planPath = Path.Combine(folder, "compiled-plan.bin");
        if (!File.Exists(planPath))
        {
            throw new InvalidOperationException($"Compiled plan for report {entry.ReportCode} version {version} is missing. Compile before preview.");
        }

        await using var stream = File.OpenRead(planPath);
        var plan = await JsonSerializer.DeserializeAsync<CompiledReportPlan>(stream, cancellationToken: cancellationToken)
                   ?? throw new InvalidOperationException("Compiled plan is invalid.");
        plan.CompiledPlanPath = planPath;

        var runtimeParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (parameters is not null)
        {
            foreach (var item in parameters)
            {
                runtimeParams[item.Key] = JsonElementUtilities.ToClrObject(item.Value);
            }
        }

        var dataModel = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var execution in plan.ExecutionPlan)
        {
            var provider = _providers.Get(execution.Provider, execution.SingleRow, execution.PrimaryFilterField, execution.OrderBy);
            if (provider is null)
            {
                throw new InvalidOperationException($"Missing provider '{execution.Provider}'.");
            }

            var request = new ProviderRequest(
                entry.ReportCode ?? string.Empty,
                execution.Alias,
                execution.Fields,
                runtimeParams,
                execution.Filters,
                execution.PrimaryFilterField,
                execution.SingleRow);

            var result = await provider.ExecuteAsync(request, connectionString, cancellationToken);
            dataModel[execution.Alias] = result ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var computed = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var evaluator = new ReportFormulaEvaluator();
        var formulaContext = evaluator.BuildFormulaContext(dataModel);
        foreach (var formula in plan.FormulaPlan)
        {
            var value = evaluator.Evaluate(formula.Expression, formulaContext, computed);
            computed[formula.Name] = value;
        }

        var templatePath = Path.Combine(folder, "template.html");
        var templateText = await File.ReadAllTextAsync(templatePath, cancellationToken);
        var html = _renderer.Render(templateText, dataModel, computed);

        return new ReportExecutionOutput
        {
            CompiledVersion = plan.CompiledVersion,
            DataModel = dataModel,
            Computed = computed,
            Html = html
        };
    }
}
