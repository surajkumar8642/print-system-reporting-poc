using Microsoft.Data.SqlClient;

namespace ReportPoc.Services;

public interface IReportDataProvider
{
    string ProviderName { get; }
    Task<object?> ExecuteAsync(ProviderRequest request, string? connectionString, CancellationToken ct = default);
}

public sealed record ProviderRequest(
    string ReportCode,
    string Alias,
    IReadOnlyList<string> Fields,
    IReadOnlyDictionary<string, object?> Parameters,
    IReadOnlyList<string> Filters,
    string? PrimaryFilterField,
    bool SingleRow);

public interface IDataProviderRegistry
{
    IReportDataProvider Get(string providerName, bool singleRow, string? primaryFilterField, string? orderBy);
}

public sealed class DataProviderRegistry : IDataProviderRegistry
{
    private readonly Dictionary<string, IReportDataProvider> _providers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SaleHeader"] = new SaleHeaderProvider(),
        ["SaleItems"] = new SaleItemsProvider(),
        ["TaxBreakup"] = new TaxBreakupProvider()
    };

    public IReportDataProvider Get(string providerName, bool singleRow, string? primaryFilterField, string? orderBy)
    {
        if (_providers.GetValueOrDefault(providerName) is { } known)
        {
            return known;
        }

        return new SqlTableProvider(providerName, singleRow, primaryFilterField, orderBy);
    }
}

public sealed class SaleHeaderProvider : SqlBackedReportDataProvider
{
    public override string ProviderName => "SaleHeader";

    protected override string TableName => "SaleHeader";
    protected override string PrimaryFilterField => "SaleId";
    protected override bool SingleRow => true;

    protected override object ResolveFallback(int saleId, IReadOnlyList<string> fields)
    {
        var row = SampleSalesData.Headers.FirstOrDefault(x => ToInt(x["SaleId"]) == saleId);
        if (row is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        return ProjectRow(row, fields);
    }
}

public sealed class SaleItemsProvider : SqlBackedReportDataProvider
{
    public override string ProviderName => "SaleItems";

    protected override string TableName => "SaleItems";
    protected override string PrimaryFilterField => "SaleId";
    protected override string? OrderBy => "SaleDetailId";

    protected override object ResolveFallback(int saleId, IReadOnlyList<string> fields)
    {
        var rows = SampleSalesData.Items
            .Where(x => ToInt(x["SaleId"]) == saleId)
            .Select(x => ProjectRow(x, fields))
            .Cast<object>()
            .ToList();
        return rows;
    }
}

public sealed class TaxBreakupProvider : SqlBackedReportDataProvider
{
    public override string ProviderName => "TaxBreakup";

    protected override string TableName => "TaxBreakup";
    protected override string PrimaryFilterField => "SaleId";
    protected override string? OrderBy => "TaxName";

    protected override object ResolveFallback(int saleId, IReadOnlyList<string> fields)
    {
        var rows = SampleSalesData.Taxes
            .Where(x => ToInt(x["SaleId"]) == saleId)
            .Select(x => ProjectRow(x, fields))
            .Cast<object>()
            .ToList();
        return rows;
    }
}

public sealed class SqlTableProvider(string tableName, bool singleRow, string? primaryFilterField, string? orderBy)
    : SqlBackedReportDataProvider
{
    public override string ProviderName => tableName;
    protected override string TableName => tableName;
    protected override string PrimaryFilterField => primaryFilterField ?? "SaleId";
    protected override bool SingleRow => singleRow;
    protected override string? OrderBy => orderBy;
    protected override bool EnableFallback => false;
}

public abstract class SqlBackedReportDataProvider : IReportDataProvider
{
    public abstract string ProviderName { get; }
    protected abstract string TableName { get; }
    protected abstract string PrimaryFilterField { get; }
    protected virtual bool SingleRow => false;
    protected virtual string? OrderBy => null;
    protected virtual bool EnableFallback => true;

    public virtual async Task<object?> ExecuteAsync(
        ProviderRequest request,
        string? connectionString,
        CancellationToken ct = default)
    {
        var singleRow = request.SingleRow;
        var filterField = request.PrimaryFilterField ?? PrimaryFilterField;
        var hasFilter = TryGetFilterValue(request.Parameters, request.Filters, out var filterValue);
        if (!hasFilter || filterValue is null)
        {
            return singleRow
                ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                : [];
        }

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                return await ExecuteFromSqlAsync(
                    filterValue.Value,
                    filterField,
                    singleRow,
                    request.Fields,
                    requestedOrderBy: OrderBy,
                    connectionString,
                    ct);
            }
            catch
            {
                return ResolveFallback(filterValue.Value, request.Fields);
            }
        }

        if (!EnableFallback)
        {
            return singleRow
                ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                : [];
        }

        return ResolveFallback(filterValue.Value, request.Fields);
    }

    protected virtual object ResolveFallback(int filterValue, IReadOnlyList<string> fields)
    {
        return SingleRow ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) : new List<object>();
    }

    protected static Dictionary<string, object?> ProjectRow(Dictionary<string, object?> row, IReadOnlyList<string> fields)
    {
        var projection = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (row.TryGetValue(field, out var value))
            {
                projection[field] = value;
            }
        }
        return projection;
    }

    private async Task<object?> ExecuteFromSqlAsync(
        int filterValue,
        string? filterField,
        bool singleRow,
        IReadOnlyList<string> fields,
        string? requestedOrderBy,
        string connectionString,
        CancellationToken ct)
    {
        var projectionFields = fields.Count == 0
            ? "*"
            : string.Join(", ", fields.Select(QuoteField));

        var sql = $"SELECT {projectionFields} FROM dbo.{QuoteField(TableName)}";
        var hasWhere = !string.IsNullOrWhiteSpace(filterField);
        if (hasWhere)
        {
            sql += $" WHERE {QuoteField(filterField!)} = @FilterValue";
        }

        if (!string.IsNullOrWhiteSpace(requestedOrderBy))
        {
            sql += $" ORDER BY {QuoteField(requestedOrderBy)}";
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        if (hasWhere)
        {
            command.Parameters.AddWithValue("@FilterValue", filterValue);
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (singleRow)
        {
            if (!await reader.ReadAsync(ct))
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            }

            return ReadSingleRow(reader);
        }

        var rows = new List<object>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(ReadSingleRow(reader));
        }

        return rows;
    }

    protected static string QuoteField(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field name cannot be empty.", nameof(field));
        }

        if (field.Contains(']') || field.Contains('['))
        {
            throw new ArgumentException("Invalid field name.", nameof(field));
        }

        return $"[{field}]";
    }

    private static bool TryGetFilterValue(
        IReadOnlyDictionary<string, object?> values,
        IReadOnlyList<string> filterHints,
        out int? filterValue)
    {
        filterValue = null;

        foreach (var filterName in filterHints)
        {
            if (values.TryGetValue(filterName, out var raw))
            {
                if (raw is not null)
                {
                    if (TryParseInt(raw, out var parsed))
                    {
                        filterValue = parsed;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryParseInt(object? value, out int parsed)
    {
        switch (value)
        {
            case int intValue:
                parsed = intValue;
                return true;
            case long longValue:
                parsed = (int)longValue;
                return true;
            case decimal decimalValue:
                parsed = (int)decimalValue;
                return true;
            case double doubleValue:
                parsed = (int)doubleValue;
                return true;
            case string s when int.TryParse(s, out parsed):
                return true;
            default:
                parsed = 0;
                return false;
        }
    }

    private static Dictionary<string, object?> ReadSingleRow(SqlDataReader reader)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }
        return row;
    }

    protected static int ToInt(object? value)
    {
        return value switch
        {
            int v => v,
            long v => (int)v,
            decimal v => (int)v,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => 0
        };
    }
}

internal static class SampleSalesData
{
    public static readonly List<Dictionary<string, object?>> Headers = new()
    {
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SaleId"] = 101,
            ["SaleNo"] = "S-101",
            ["SaleDate"] = "2026-05-10",
            ["CustomerName"] = "ABC Traders",
            ["BillingAddress"] = "Market Road, Surat",
            ["NetAmount"] = 2000.00m,
            ["TotalTax"] = 360.00m
        },
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SaleId"] = 102,
            ["SaleNo"] = "S-102",
            ["SaleDate"] = "2026-05-09",
            ["CustomerName"] = "Knit Store",
            ["BillingAddress"] = "Mansarovar, Ahmedabad",
            ["NetAmount"] = 500.00m,
            ["TotalTax"] = 90.00m
        }
    };

    public static readonly List<Dictionary<string, object?>> Items = new()
    {
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SaleId"] = 101,
            ["SaleDetailId"] = 201,
            ["ProductName"] = "Item A",
            ["Qty"] = 2m,
            ["Rate"] = 100m,
            ["MRP"] = 150m,
            ["LineTotal"] = 200m
        },
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SaleId"] = 101,
            ["SaleDetailId"] = 202,
            ["ProductName"] = "Item B",
            ["Qty"] = 3m,
            ["Rate"] = 600m,
            ["MRP"] = 700m,
            ["LineTotal"] = 1800m
        },
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SaleId"] = 102,
            ["SaleDetailId"] = 203,
            ["ProductName"] = "Gadget X",
            ["Qty"] = 1m,
            ["Rate"] = 500m,
            ["MRP"] = 550m,
            ["LineTotal"] = 500m
        }
    };

    public static readonly List<Dictionary<string, object?>> Taxes = new()
    {
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SaleId"] = 101,
            ["TaxName"] = "CGST",
            ["TaxRate"] = 9.0m,
            ["TaxAmount"] = 180m
        },
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SaleId"] = 101,
            ["TaxName"] = "SGST",
            ["TaxRate"] = 9.0m,
            ["TaxAmount"] = 180m
        },
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SaleId"] = 102,
            ["TaxName"] = "VAT",
            ["TaxRate"] = 18.0m,
            ["TaxAmount"] = 90m
        }
    };
}
