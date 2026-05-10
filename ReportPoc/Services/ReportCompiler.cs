using System.Text.Json;
using ReportPoc.Models;

namespace ReportPoc.Services;

public interface IReportCompiler
{
    Task<CompiledReportPlan> CompileAsync(
        ReportCatalogEntry entry,
        ReportDefinition definition,
        string version,
        CancellationToken cancellationToken = default);
}

public sealed class ReportCompiler(IRootPathResolver resolver) : IReportCompiler
{
    private readonly IRootPathResolver _resolver = resolver;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task<CompiledReportPlan> CompileAsync(
        ReportCatalogEntry entry,
        ReportDefinition definition,
        string version,
        CancellationToken cancellationToken = default)
    {
        var reportRoot = Path.Combine(
            _resolver.ResolveTemplatesPath(),
            entry.Family ?? "General",
            entry.Variant ?? "DEFAULT",
            version);

        if (!Directory.Exists(reportRoot))
        {
            throw new DirectoryNotFoundException($"Template version folder '{reportRoot}' not found.");
        }

        var priorPlan = await ReadExistingPlanAsync(reportRoot, cancellationToken);
        var nextVersion = priorPlan is null ? 1 : priorPlan.CompiledVersion + 1;

        var plan = new CompiledReportPlan
        {
            ReportCode = definition.ReportCode ?? entry.ReportCode ?? string.Empty,
            RootEntity = definition.RootEntity ?? string.Empty,
            SourceVersion = version,
            CompiledVersion = nextVersion,
            CompiledPlanPath = Path.Combine(reportRoot, "compiled-plan.bin"),
            ParameterSchema = definition.Parameters
                .ToDictionary(x => x.Name, x => x.Type, StringComparer.OrdinalIgnoreCase)
        };

        var datasetMap = definition.Datasets
            .ToDictionary(x => x.Name, x => new HashSet<string>(x.Fields, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        AddDependenciesFromBindings(plan, definition, datasetMap);
        AddDependenciesFromFormulaLibrary(plan, definition, datasetMap);
        AddDependenciesFromFormulas(plan, definition, datasetMap);

        foreach (var dataset in definition.Datasets)
        {
            if (!datasetMap.TryGetValue(dataset.Name, out var fields))
            {
                continue;
            }

            var requiredFields = fields.ToList();
            var primaryFilterField = ResolveFilterField(dataset.FilterField, definition.Parameters);
            var providerExecution = new CompiledProviderExecution
            {
                Provider = dataset.Provider,
                Alias = dataset.Name,
                Fields = requiredFields,
                Filters = definition.Parameters.Select(x => x.Name).ToList(),
                PrimaryFilterField = primaryFilterField,
                SingleRow = dataset.IsSingleRow,
                OrderBy = dataset.OrderBy
            };
            if (string.IsNullOrWhiteSpace(providerExecution.Provider))
            {
                throw new InvalidOperationException($"Dataset '{dataset.Name}' has no provider.");
            }

            if (!string.IsNullOrWhiteSpace(providerExecution.Provider))
            {
                plan.ExecutionPlan.Add(providerExecution);
            }
        }

        foreach (var formula in definition.Formulas)
        {
            if (!string.IsNullOrWhiteSpace(formula.Name) && !string.IsNullOrWhiteSpace(formula.Expression))
            {
                plan.FormulaPlan.Add(new CompiledFormulaExecution
                {
                    Name = formula.Name,
                    Expression = formula.Expression
                });
            }
        }

        await WritePlanAsync(plan, reportRoot, cancellationToken);
        return plan;
    }

    private static void AddDependenciesFromBindings(
        CompiledReportPlan plan,
        ReportDefinition definition,
        Dictionary<string, HashSet<string>> datasetMap)
    {
        foreach (var binding in definition.Bindings)
        {
            if (string.IsNullOrWhiteSpace(binding.Field))
            {
                continue;
            }

            var parsed = TryParseBindingPath(binding.Field);
            if (!parsed.HasValue)
            {
                continue;
            }

            var (alias, field) = parsed.Value;
            if (!datasetMap.ContainsKey(alias))
            {
                throw new InvalidOperationException($"Binding '{binding.Control}' uses unknown dataset alias '{alias}'.");
            }

            datasetMap[alias].Add(field);
        }
    }

    private static void AddDependenciesFromFormulas(
        CompiledReportPlan plan,
        ReportDefinition definition,
        Dictionary<string, HashSet<string>> datasetMap)
    {
        foreach (var formula in definition.Formulas)
        {
            foreach (var path in FormulaFieldPathExtractor.ExtractPaths(formula.Expression))
            {
                if (path.StartsWith("computed.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parsed = TryParseBindingPath(path);
                if (!parsed.HasValue)
                {
                    continue;
                }

                var (alias, field) = parsed.Value;
                if (!datasetMap.ContainsKey(alias))
                {
                    throw new InvalidOperationException($"Formula '{formula.Name}' uses unknown dataset alias '{alias}'.");
                }

                datasetMap[alias].Add(field);
            }
        }
    }

    private static void AddDependenciesFromFormulaLibrary(
        CompiledReportPlan plan,
        ReportDefinition definition,
        Dictionary<string, HashSet<string>> datasetMap)
    {
        foreach (var functionName in definition.FunctionNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var function = FunctionCatalog.All.FirstOrDefault(x =>
                string.Equals(x.Name, functionName, StringComparison.OrdinalIgnoreCase));
            if (function is null)
            {
                continue;
            }

            var formulaName = $"fn_{function.Name}";
            if (!plan.FormulaPlan.Any(f => string.Equals(f.Name, formulaName, StringComparison.OrdinalIgnoreCase)))
            {
                plan.FormulaPlan.Add(new CompiledFormulaExecution
                {
                    Name = formulaName,
                    Expression = function.Expression
                });
            }

            foreach (var path in FormulaFieldPathExtractor.ExtractPaths(function.Expression))
            {
                var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                if (!datasetMap.TryGetValue(parts[0], out var fields))
                {
                    continue;
                }

                fields.Add(parts[1]);
            }
        }
    }

    private static string? ResolveFilterField(string? configuredFilter, List<DefinitionParameter> parameters)
    {
        if (!string.IsNullOrWhiteSpace(configuredFilter))
        {
            return configuredFilter;
        }

        var firstId = parameters.FirstOrDefault(x =>
            x.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("id", StringComparison.OrdinalIgnoreCase));

        if (firstId is null)
        {
            return null;
        }

        return ToPascal(firstId.Name);
    }

    private static string ToPascal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Length == 1)
        {
            return value.ToUpperInvariant();
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static (string alias, string field)? TryParseBindingPath(string path)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        return (parts[0], parts[1]);
    }

    private static async Task<CompiledReportPlan?> ReadExistingPlanAsync(string reportRoot, CancellationToken cancellationToken)
    {
        var planPath = Path.Combine(reportRoot, "compiled-plan.bin");
        if (!File.Exists(planPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(planPath);
        return await JsonSerializer.DeserializeAsync<CompiledReportPlan>(stream, cancellationToken: cancellationToken);
    }

    private async Task WritePlanAsync(CompiledReportPlan plan, string reportRoot, CancellationToken cancellationToken)
    {
        var planPath = Path.Combine(reportRoot, "compiled-plan.bin");
        var debugPath = Path.Combine(reportRoot, "compiled-plan.debug.json");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(plan, _jsonOptions);
        await File.WriteAllBytesAsync(planPath, bytes, cancellationToken);
        await File.WriteAllTextAsync(debugPath, JsonSerializer.Serialize(plan, _jsonOptions), cancellationToken);
    }
}
