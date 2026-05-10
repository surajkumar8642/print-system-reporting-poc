# Reporting Blueprint

## 1. Purpose

This document turns the architecture into a practical structure that can be implemented in a `.NET Core MVC` application.

It focuses on:

- report storage structure
- runtime execution flow
- compiled-plan usage
- provider registry design
- redesign and republish flow
- one concrete sample report

## 2. Recommended Project Structure

```text
PrintSystem/
  src/
    PrintSystem.Web/
      Controllers/
      Views/
      wwwroot/
    PrintSystem.Reporting/
      Catalog/
      Compiler/
      Designer/
      Execution/
      Providers/
      Rendering/
      Models/
  Reports/
    Catalog/
    Templates/
```

## 3. Reporting Module Structure

```text
PrintSystem.Reporting/
  Catalog/
    IReportCatalog.cs
    FileReportCatalog.cs
  Compiler/
    IReportCompiler.cs
    ReportCompiler.cs
  Designer/
    ReportDefinitionValidator.cs
  Execution/
    ICompiledReportExecutor.cs
    CompiledReportExecutor.cs
    ProviderRequestBuilder.cs
  Providers/
    IDataProviderRegistry.cs
    DataProviderRegistry.cs
    SaleHeaderProvider.cs
    SaleItemsProvider.cs
    TaxBreakupProvider.cs
  Rendering/
    IHtmlReportRenderer.cs
    HtmlReportRenderer.cs
  Models/
    ReportCatalogEntry.cs
    ReportDefinition.cs
    CompiledReportPlan.cs
    ReportExecutionResult.cs
    ProviderRequest.cs
```

## 4. Storage Structure for Templates

```text
Reports/
  Catalog/
    reports.json
  Templates/
    SaleInvoice/
      STANDARD_A4/
        v1/
          template.html
          template.css
          definition.json
          compiled-plan.json
          sample-data.json
        v2/
          template.html
          template.css
          definition.json
          compiled-plan.json
```

## 5. Runtime Flow

### 5.1 Print Request

User action:

- selects module record such as `saleId = 101`
- selects report such as `SALE_INVOICE_STANDARD_A4`
- clicks preview or print

System flow:

1. load report catalog entry
2. find active published version
3. load `compiled-plan.json`
4. validate parameters
5. call providers in plan order
6. build report model
7. render HTML
8. preview or print

## 6. Compile Flow

When a report is edited:

1. save draft definition
2. validate datasets and fields
3. validate formulas
4. resolve provider dependencies
5. generate `compiled-plan.json`
6. mark version as published
7. optionally switch active version

## 7. Provider Registry Model

The registry maps logical provider names to reusable code.

Example:

```text
SaleHeader -> SaleHeaderProvider
SaleItems -> SaleItemsProvider
TaxBreakup -> TaxBreakupProvider
```

Each provider:

- accepts connection or DB context
- accepts parameters
- accepts required field list
- returns standard object data

## 8. Sample Runtime Call

```csharp
var result = await executor.ExecuteAsync(
    plan,
    connectionString,
    new Dictionary<string, object>
    {
        ["saleId"] = 101
    });
```

## 9. Sample Output Model

```json
{
  "reportCode": "SALE_INVOICE_STANDARD_A4",
  "data": {
    "saleHeader": {
      "SaleNo": "S-101",
      "CustomerName": "ABC Traders",
      "NetAmount": 2000.00,
      "TotalTax": 360.00
    },
    "saleItems": [
      {
        "ProductName": "Item A",
        "Qty": 2,
        "Rate": 100.00,
        "MRP": 150.00,
        "LineTotal": 200.00
      }
    ]
  },
  "computed": {
    "GrandTotal": 2360.00
  }
}
```

## 10. Why This Model Is Fast

It avoids:

- reading all database tables every time
- rebuilding dataset shape every time
- rediscovering field mappings every time

It only:

- loads a published compiled plan
- executes fixed providers
- returns only required data

## 11. Sample Set

The sample files created with this blueprint show one report end to end:

- template
- definition
- compiled plan
- sample data

These should be used as the starting reference for implementation.
