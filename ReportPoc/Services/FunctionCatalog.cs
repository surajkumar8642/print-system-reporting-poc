using ReportPoc.Models;

namespace ReportPoc.Services;

public interface IFunctionCatalogService
{
    IReadOnlyList<SqlFunctionDefinition> GetFunctions();
}

public sealed class FunctionCatalog : IFunctionCatalogService
{
    public static readonly IReadOnlyList<SqlFunctionDefinition> All = new[]
    {
        new SqlFunctionDefinition
        {
            Name = "GRAND_TOTAL",
            Description = "Total = Header NetAmount + Header TotalTax",
            Expression = "SaleHeader.NetAmount + SaleHeader.TotalTax"
        },
        new SqlFunctionDefinition
        {
            Name = "TOTAL_QTY",
            Description = "Total quantity from sale items",
            Expression = "sum(SaleItems.Qty)"
        },
        new SqlFunctionDefinition
        {
            Name = "TOTAL_AMOUNT",
            Description = "Line total from sale items",
            Expression = "sum(SaleItems.LineTotal)"
        },
        new SqlFunctionDefinition
        {
            Name = "TOTAL_TAX",
            Description = "Tax amount total",
            Expression = "sum(TaxBreakup.TaxAmount)"
        },
        new SqlFunctionDefinition
        {
            Name = "ITEM_COUNT",
            Description = "Total count of item rows",
            Expression = "count(SaleItems.SaleDetailId)"
        }
    };

    public IReadOnlyList<SqlFunctionDefinition> GetFunctions()
    {
        return All;
    }
}
