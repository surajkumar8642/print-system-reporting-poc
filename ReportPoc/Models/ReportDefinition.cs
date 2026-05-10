namespace ReportPoc.Models;

using System.Text.Json.Serialization;

public sealed class ReportDefinition
{
    public string? ReportCode { get; set; }
    public string? RootEntity { get; set; }
    public List<DefinitionParameter> Parameters { get; set; } = new();
    public List<DefinitionDataset> Datasets { get; set; } = new();
    public List<ReportBinding> Bindings { get; set; } = new();
    public List<ReportFormula> Formulas { get; set; } = new();
    public List<string> FunctionNames { get; set; } = new();
    public string? TemplateHtml { get; set; }
    public string? TemplateCss { get; set; }
}

public sealed class DefinitionParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; } = true;
}

public sealed class DefinitionDataset
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = new();
    public bool IsSingleRow { get; set; }
    public string? FilterField { get; set; }
    public string? OrderBy { get; set; }
}

public sealed class ReportBinding
{
    public string Control { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
}

public sealed class ReportFormula
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

public sealed class ReportLayoutAsset
{
    public string TemplatePath { get; set; } = string.Empty;
    public string CssPath { get; set; } = string.Empty;
}

public sealed class ReportExecutionOptionsPayload
{
    [JsonIgnore]
    public string? Version { get; set; }

    public string? ConnectionString { get; set; }
    public string? TemplateHtml { get; set; }
    public string? TemplateCss { get; set; }
}

public sealed class SqlFunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class SchemaTableInfo
{
    public string TableName { get; set; } = string.Empty;
    public List<SchemaColumnInfo> Columns { get; set; } = new();
}

public sealed class SchemaColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public bool IsNullable { get; set; }
}
