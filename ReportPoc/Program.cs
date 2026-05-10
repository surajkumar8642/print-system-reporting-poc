using ReportPoc.Models;
using ReportPoc.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IRootPathResolver, RootPathResolver>(_ =>
    new RootPathResolver(
        builder.Configuration["Reports:RootPath"] ?? "Reports",
        builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IReportCatalogService, ReportCatalogService>();
builder.Services.AddSingleton<IDataProviderRegistry, DataProviderRegistry>();
builder.Services.AddSingleton<IReportCompiler, ReportCompiler>();
builder.Services.AddSingleton<IReportExecutorService, ReportExecutorService>();
builder.Services.AddSingleton<IReportRenderer, ReportHtmlRenderer>();
builder.Services.AddSingleton<IFunctionCatalogService, FunctionCatalog>();
builder.Services.AddSingleton<IDatabaseMetadataService, DatabaseMetadataService>();

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", () => Results.Redirect("/index.html"));

app.MapGet("/api/reports", async (IReportCatalogService catalog, CancellationToken ct) =>
{
    var reports = await catalog.GetAllAsync(ct);
    return Results.Ok(reports);
});

app.MapGet("/api/reports/{reportCode}/definition", async (
    string reportCode,
    string? version,
    IReportCatalogService catalog,
    CancellationToken ct) =>
{
    var entry = await catalog.GetByCodeAsync(reportCode, ct);
    if (entry is null)
    {
        return Results.NotFound(new { message = $"Report {reportCode} not found" });
    }

    var selectedVersion = version ?? entry.ActiveVersion ?? "v1";
    var definition = await catalog.LoadDefinitionAsync(entry, selectedVersion, ct);
    if (definition is null)
    {
        return Results.NotFound(new { message = $"Definition for report {reportCode} version {selectedVersion} was not found" });
    }

    return Results.Ok(definition);
});

app.MapGet("/api/data/tables", async (
    string? connectionString,
    string? schema,
    IDatabaseMetadataService metadata,
    IConfiguration config,
    CancellationToken ct) =>
{
    var resolvedConnectionString = ResolveConnectionString(config, connectionString);
    if (string.IsNullOrWhiteSpace(resolvedConnectionString))
    {
        return Results.BadRequest(new { message = "Connection string is required and no default connection string is configured." });
    }

    try
    {
        var tables = await metadata.GetTablesAndColumnsAsync(resolvedConnectionString, schema, ct);
        return Results.Ok(tables);
    }
    catch (Exception)
    {
        return Results.Ok(GetFallbackTablesFromSamples());
    }
});

app.MapGet("/api/function-catalog", (IFunctionCatalogService catalog) =>
{
    return Results.Ok(catalog.GetFunctions());
});

app.MapPost("/api/reports/{reportCode}/compile", async (
    string reportCode,
    ReportExecutionOptions request,
    IReportCatalogService catalog,
    IReportCompiler compiler,
    IRootPathResolver resolver,
    CancellationToken ct) =>
{
    var context = await ResolveExecutionContextAsync(
        reportCode,
        request,
        catalog,
        resolver,
        ct);
    if (context.Problem is not null)
    {
        return context.Problem;
    }

    var usedVersion = context.Entry!.ActiveVersion!;
    var plan = await compiler.CompileAsync(context.Entry, context.Definition!, usedVersion, ct);
    return Results.Ok(new
    {
        reportCode = context.Entry.ReportCode,
        version = usedVersion,
        compiledVersion = plan.CompiledVersion,
        compiledPlanPath = plan.CompiledPlanPath,
        isDynamic = context.IsDynamic
    });
});

app.MapPost("/api/reports/{reportCode}/preview", async (
    string reportCode,
    ReportExecutionOptions request,
    IReportCatalogService catalog,
    IRootPathResolver resolver,
    IReportCompiler compiler,
    IReportExecutorService executor,
    IConfiguration config,
    CancellationToken ct) =>
{
    var connectionString = ResolveConnectionString(config, request.ConnectionString);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { message = "Connection string is required." });
    }

    var context = await ResolveExecutionContextAsync(
        reportCode,
        request,
        catalog,
        resolver,
        ct);
    if (context.Problem is not null)
    {
        return context.Problem;
    }

    if (context.IsDynamic)
    {
        await compiler.CompileAsync(context.Entry!, context.Definition!, context.Entry!.ActiveVersion!, ct);
    }

    var result = await executor.ExecuteAndRenderAsync(
        context.Entry!,
        context.Entry!.ActiveVersion!,
        request.Parameters ?? new(),
        connectionString,
        ct);

    return Results.Ok(new
    {
        reportCode = context.Entry.ReportCode,
        version = context.Entry.ActiveVersion,
        compiledVersion = result.CompiledVersion,
        html = result.Html,
        data = result.DataModel,
        isDynamic = context.IsDynamic
    });
});

app.Run();

static async Task<ExecutionContextResolution> ResolveExecutionContextAsync(
    string reportCode,
    ReportExecutionOptions request,
    IReportCatalogService catalog,
    IRootPathResolver resolver,
    CancellationToken cancellationToken)
{
    var entry = await catalog.GetByCodeAsync(reportCode, cancellationToken);
    var version = string.IsNullOrWhiteSpace(request.Version)
        ? entry?.ActiveVersion ?? "v1"
        : request.Version;

    ReportDefinition? definition;
    bool requiresDynamicFolder = request.Definition is not null
        || !string.IsNullOrWhiteSpace(request.TemplateHtml)
        || !string.IsNullOrWhiteSpace(request.TemplateCss);

    if (entry is null)
    {
        if (!requiresDynamicFolder || request.Definition is null)
        {
            return new ExecutionContextResolution(null, null, false, Results.NotFound(new { message = $"Report {reportCode} not found" }));
        }
    }

    if (request.Definition is not null)
    {
        definition = request.Definition;
        if (string.IsNullOrWhiteSpace(definition.ReportCode))
        {
            definition.ReportCode = reportCode;
        }
    }
    else
    {
        if (entry is null)
        {
            return new ExecutionContextResolution(null, null, false, Results.NotFound(new { message = $"Report {reportCode} not found" }));
        }

        definition = await catalog.LoadDefinitionAsync(entry, version, cancellationToken);
        if (definition is null)
        {
            return new ExecutionContextResolution(null, null, false, Results.NotFound(new
            {
                message = $"Definition for report {reportCode} version {version} was not found"
            }));
        }

        if (!string.IsNullOrWhiteSpace(request.TemplateHtml) || !string.IsNullOrWhiteSpace(request.TemplateCss))
        {
            definition = CloneWithTemplateOverrides(definition, request);
        }
    }

    if (requiresDynamicFolder)
    {
        var dynamic = await CreateRuntimeReportFolderAsync(
            entry,
            reportCode,
            version,
            definition!,
            request,
            resolver,
            cancellationToken);

        return dynamic;
    }

    return new ExecutionContextResolution(entry, definition, false, null);
}

static async Task<ExecutionContextResolution> CreateRuntimeReportFolderAsync(
    ReportCatalogEntry? sourceEntry,
    string reportCode,
    string version,
    ReportDefinition definition,
    ReportExecutionOptions request,
    IRootPathResolver resolver,
    CancellationToken cancellationToken)
{
    var variant = $"DESIGN_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
    var dynamicEntry = new ReportCatalogEntry
    {
        ReportCode = $"{reportCode}-RUN-{Guid.NewGuid():N}",
        Family = "_Runtime",
        Variant = variant,
        ActiveVersion = version,
        RootEntity = definition.RootEntity,
        DefinitionMode = "Runtime"
    };

    var runtimeFolder = Path.Combine(
        resolver.ResolveTemplatesPath(),
        dynamicEntry.Family!,
        dynamicEntry.Variant!,
        dynamicEntry.ActiveVersion!);
    Directory.CreateDirectory(runtimeFolder);

    definition.ReportCode = dynamicEntry.ReportCode;
    await File.WriteAllTextAsync(
        Path.Combine(runtimeFolder, "definition.json"),
        System.Text.Json.JsonSerializer.Serialize(definition, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
        cancellationToken);

    var baseTemplate = definition.TemplateHtml ?? string.Empty;
    var baseStyle = definition.TemplateCss ?? string.Empty;

    var templateHtml = string.IsNullOrWhiteSpace(request.TemplateHtml)
        ? (string.IsNullOrWhiteSpace(baseTemplate)
            ? await ReadSourceTemplateAsync(sourceEntry, version, resolver, cancellationToken)
            : baseTemplate)
        : request.TemplateHtml;

    if (string.IsNullOrWhiteSpace(templateHtml))
    {
        templateHtml = $"<html><body><h1>{reportCode}</h1></body></html>";
    }

    var templateCss = string.IsNullOrWhiteSpace(request.TemplateCss)
        ? (string.IsNullOrWhiteSpace(baseStyle)
            ? "body { font-family: Arial, sans-serif; }"
            : baseStyle)
        : request.TemplateCss;

    await File.WriteAllTextAsync(Path.Combine(runtimeFolder, "template.html"), templateHtml, cancellationToken);
    await File.WriteAllTextAsync(Path.Combine(runtimeFolder, "template.css"), templateCss, cancellationToken);

    return new ExecutionContextResolution(dynamicEntry, definition, true, null);
}

static async Task<string> ReadSourceTemplateAsync(
    ReportCatalogEntry? sourceEntry,
    string version,
    IRootPathResolver resolver,
    CancellationToken cancellationToken)
{
    if (sourceEntry is null)
    {
        return string.Empty;
    }

    var sourceFolder = Path.Combine(
        resolver.ResolveTemplatesPath(),
        sourceEntry.Family ?? "General",
        sourceEntry.Variant ?? "STANDARD_A4",
        version);
    var sourceTemplate = Path.Combine(sourceFolder, "template.html");
    if (!File.Exists(sourceTemplate))
    {
        return string.Empty;
    }

    return await File.ReadAllTextAsync(sourceTemplate, cancellationToken);
}

static ReportDefinition CloneWithTemplateOverrides(ReportDefinition original, ReportExecutionOptions request)
{
    var clone = new ReportDefinition
    {
        ReportCode = original.ReportCode,
        RootEntity = original.RootEntity,
        TemplateHtml = original.TemplateHtml,
        TemplateCss = original.TemplateCss,
        Parameters = original.Parameters.Select(x => new DefinitionParameter
        {
            Name = x.Name,
            Type = x.Type,
            Required = x.Required
        }).ToList(),
        Datasets = original.Datasets.Select(x => new DefinitionDataset
        {
            Name = x.Name,
            Provider = x.Provider,
            Fields = x.Fields.Select(f => f).ToList(),
            IsSingleRow = x.IsSingleRow,
            FilterField = x.FilterField,
            OrderBy = x.OrderBy
        }).ToList(),
        Bindings = original.Bindings.Select(x => new ReportBinding
        {
            Control = x.Control,
            Field = x.Field
        }).ToList(),
        Formulas = original.Formulas.Select(x => new ReportFormula
        {
            Name = x.Name,
            Expression = x.Expression
        }).ToList(),
        FunctionNames = original.FunctionNames.Select(x => x).ToList()
    };

    if (!string.IsNullOrWhiteSpace(request.TemplateHtml))
    {
        clone.TemplateHtml = request.TemplateHtml;
    }

    if (!string.IsNullOrWhiteSpace(request.TemplateCss))
    {
        clone.TemplateCss = request.TemplateCss;
    }

    return clone;
}

static string? ResolveConnectionString(IConfiguration config, string? overrideConnectionString)
{
    if (!string.IsNullOrWhiteSpace(overrideConnectionString))
    {
        return overrideConnectionString;
    }

    return config.GetConnectionString("ReportDatabase");
}

static List<SchemaTableInfo> GetFallbackTablesFromSamples()
{
    return new()
    {
        new()
        {
            TableName = "SaleHeader",
            Columns = new List<SchemaColumnInfo>
            {
                new() { ColumnName = "SaleId", DataType = "int" },
                new() { ColumnName = "SaleNo", DataType = "nvarchar" },
                new() { ColumnName = "SaleDate", DataType = "date" },
                new() { ColumnName = "CustomerName", DataType = "nvarchar" },
                new() { ColumnName = "BillingAddress", DataType = "nvarchar" },
                new() { ColumnName = "NetAmount", DataType = "decimal" },
                new() { ColumnName = "TotalTax", DataType = "decimal" }
            }
        },
        new()
        {
            TableName = "SaleItems",
            Columns = new List<SchemaColumnInfo>
            {
                new() { ColumnName = "SaleId", DataType = "int" },
                new() { ColumnName = "SaleDetailId", DataType = "int" },
                new() { ColumnName = "ProductName", DataType = "nvarchar" },
                new() { ColumnName = "Qty", DataType = "decimal" },
                new() { ColumnName = "Rate", DataType = "decimal" },
                new() { ColumnName = "MRP", DataType = "decimal" },
                new() { ColumnName = "LineTotal", DataType = "decimal" }
            }
        },
        new()
        {
            TableName = "TaxBreakup",
            Columns = new List<SchemaColumnInfo>
            {
                new() { ColumnName = "SaleId", DataType = "int" },
                new() { ColumnName = "TaxName", DataType = "nvarchar" },
                new() { ColumnName = "TaxRate", DataType = "decimal" },
                new() { ColumnName = "TaxAmount", DataType = "decimal" }
            }
        }
    };
}

public sealed class ReportExecutionOptions
{
    public string? Version { get; set; }
    public string? ConnectionString { get; set; }
    public Dictionary<string, JsonElement>? Parameters { get; set; }
    public ReportDefinition? Definition { get; set; }
    public string? TemplateHtml { get; set; }
    public string? TemplateCss { get; set; }
}

internal sealed record ExecutionContextResolution(
    ReportCatalogEntry? Entry,
    ReportDefinition? Definition,
    bool IsDynamic,
    IResult? Problem);
