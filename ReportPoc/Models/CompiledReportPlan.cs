namespace ReportPoc.Models;

public sealed class CompiledReportPlan
{
    public string ReportCode { get; set; } = string.Empty;
    public int CompiledVersion { get; set; }
    public string RootEntity { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
    public string CompiledPlanPath { get; set; } = string.Empty;
    public Dictionary<string, string> ParameterSchema { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CompiledProviderExecution> ExecutionPlan { get; set; } = new();
    public List<CompiledFormulaExecution> FormulaPlan { get; set; } = new();
}

public sealed class CompiledProviderExecution
{
    public string Provider { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = new();
    public List<string> Filters { get; set; } = new();
    public string? PrimaryFilterField { get; set; }
    public bool SingleRow { get; set; }
    public string? OrderBy { get; set; }
}

public sealed class CompiledFormulaExecution
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

public sealed class ReportExecutionOutput
{
    public int CompiledVersion { get; set; }
    public Dictionary<string, object?> DataModel { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> Computed { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Html { get; set; } = string.Empty;
}
