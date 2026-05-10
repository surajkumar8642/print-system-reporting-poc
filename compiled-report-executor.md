# Compiled Report Executor Model

## 1. Goal

This document explains an optimization model for HTML-based reporting where:

- report design happens once
- field selection and dataset selection happen once
- the system generates a compiled execution plan
- print-time does not need to re-analyze the full design every time
- print-time only accepts parameters such as `saleId`, `fromDate`, `toDate`, connection string, or tenant DB info
- the executor returns a standard data model quickly

This is possible and is a strong approach for your requirement.

## 2. Main Idea

Instead of doing this on every print:

1. read template
2. read definition
3. inspect all controls
4. inspect all field mappings
5. detect required datasets
6. generate query plan
7. execute queries
8. build report model

you can do a one-time compilation step after the report is designed or modified.

That compilation step generates a `report execution artifact`.

At runtime, the print engine only does this:

1. load compiled artifact
2. pass parameters
3. run prebuilt providers/query plan
4. return standard JSON/data model
5. render HTML

## 3. Is It Possible

Yes.

There are three practical ways to implement it.

### 3.1 Compiled Metadata Plan

This is the best starting option.

The system does not generate a .NET DLL per report at first.
Instead, it generates a compact execution file such as:

- `compiled-plan.json`
- `compiled-plan.bin`

This file contains:

- fixed dataset list
- fixed field list
- prevalidated provider mapping
- prebuilt join/filter plan
- parameter schema
- output shape

This gives most of the speed benefit without the complexity of real code generation.

### 3.2 Generated C# Function / Expression Tree

The system can generate a C# execution class for the report and compile it.

Example idea:

```csharp
public sealed class SaleInvoiceA4Executor : ICompiledReportExecutor
{
    public ReportDataResult Execute(IDbConnection db, ReportParameters parameters)
    {
        // fixed provider calls
        // fixed mapping
        // returns report model
    }
}
```

This is possible, but adds more complexity:

- code generation
- compilation
- DLL loading/unloading
- versioning
- debugging difficulty

This can be useful later for very high-scale or very large reports, but it should not be the first version.

### 3.3 SQL Stored Procedure / View Driven Model

In some cases the report can map to:

- a stored procedure
- a view
- a parameterized query pack

This is fast, but is less flexible for a cross-customer dynamic report designer. It also increases database coupling.

For your use case, this should be used only for selected heavy reports, not as the base architecture.

## 4. Recommended Model

Use this layered strategy:

### 4.1 Design-Time Layer

The customer or admin designs the report:

- chooses datasets
- chooses fields
- chooses grouping
- chooses layout
- defines formulas

### 4.2 Compile-Time Layer

When the report is saved or published, the system generates:

- template HTML
- template CSS
- design definition JSON
- compiled execution plan

### 4.3 Runtime Layer

When print is requested:

- load compiled execution plan
- validate parameters
- call fixed providers
- return data model
- render HTML

This gives a strong balance between flexibility and speed.

## 5. What Gets Compiled

The compile step should convert high-level design into a compact runtime plan.

Example input:

- user selected `SaleHeader`
- user selected `SaleItems`
- user selected `TaxBreakup`
- user used `saleId` parameter
- user added fields `SaleNo`, `CustomerName`, `MRP`, `Qty`, `TaxAmount`

Compiled output should contain:

- report code
- root entity
- allowed parameters
- provider execution order
- selected fields per dataset
- provider filter definitions
- formula plan
- output model schema

Example:

```json
{
  "reportCode": "SALE_INVOICE_A4",
  "rootEntity": "Sale",
  "parameterSchema": {
    "saleId": "int"
  },
  "executionPlan": [
    {
      "provider": "SaleHeader",
      "alias": "saleHeader",
      "fields": ["SaleNo", "SaleDate", "CustomerName", "NetAmount", "TotalTax"],
      "filters": ["saleId"]
    },
    {
      "provider": "SaleItems",
      "alias": "saleItems",
      "fields": ["ProductName", "Qty", "Rate", "MRP", "LineTotal"],
      "filters": ["saleId"]
    },
    {
      "provider": "TaxBreakup",
      "alias": "taxBreakup",
      "fields": ["TaxName", "TaxRate", "TaxAmount"],
      "filters": ["saleId"]
    }
  ],
  "formulaPlan": [
    {
      "name": "grandTotal",
      "expression": "saleHeader.NetAmount + saleHeader.TotalTax"
    }
  ]
}
```

At runtime, there is no need to rediscover mappings from the full designer if this compiled plan already exists.

## 6. Runtime Contract

Your idea is correct: runtime should ideally look like a fixed function call.

Example contract:

```csharp
public interface IReportExecutor
{
    ReportExecutionResult Execute(
        string reportCode,
        string connectionString,
        Dictionary<string, object> parameters);
}
```

Example call:

```csharp
var result = executor.Execute(
    "SALE_INVOICE_A4",
    connectionString,
    new Dictionary<string, object>
    {
        ["saleId"] = 101
    });
```

Output:

- standard JSON model
- or standard `DataSet`
- or typed DTO object graph

Best output format for HTML rendering is a typed object or JSON model.

## 7. Best Standard Output Shape

Use a standard output model like this:

```json
{
  "reportCode": "SALE_INVOICE_A4",
  "parameters": {
    "saleId": 101
  },
  "data": {
    "saleHeader": {
      "SaleNo": "S-101",
      "CustomerName": "ABC Traders",
      "NetAmount": 2000,
      "TotalTax": 360
    },
    "saleItems": [
      {
        "ProductName": "Item A",
        "Qty": 2,
        "MRP": 150,
        "LineTotal": 300
      }
    ],
    "taxBreakup": [
      {
        "TaxName": "CGST",
        "TaxAmount": 180
      },
      {
        "TaxName": "SGST",
        "TaxAmount": 180
      }
    ]
  },
  "computed": {
    "grandTotal": 2360
  }
}
```

This format is simple, portable, and ideal for HTML rendering.

## 8. Do You Need DLL Generation

Usually, no for version 1.

You do not need to generate one DLL per report initially.

Why:

- harder deployment
- harder debugging
- dynamic compilation complexity
- assembly version management
- file locking and update problems on client PCs

A compiled plan plus provider registry is usually enough and much safer.

Recommended progression:

1. metadata definition
2. compiled execution plan
3. optional expression compilation
4. optional generated executor classes only for hot reports

## 9. Stronger Design: Provider Functions

Instead of generating raw SQL per report, define reusable provider functions.

Examples:

```csharp
Task<object> GetSaleHeaderAsync(DbContext db, int saleId, string[] fields);
Task<List<object>> GetSaleItemsAsync(DbContext db, int saleId, string[] fields);
Task<List<object>> GetTaxBreakupAsync(DbContext db, int saleId, string[] fields);
Task<List<object>> GetStockBatchDetailAsync(DbContext db, int productId, string[] fields);
```

Then the compiled plan only needs to say:

- call `GetSaleHeaderAsync`
- call `GetSaleItemsAsync`
- call `GetTaxBreakupAsync`
- project these fields

This is a strong model because:

- logic is reusable
- runtime is fast
- mappings are prevalidated
- no repeated table analysis at print-time

## 10. How Compilation Would Work

### 10.1 Save Draft

During design:

- user drags fields
- user binds sections
- user adds formulas
- system saves draft definition

### 10.2 Publish or Generate Runtime

When the report is finalized:

1. validate selected datasets
2. validate selected fields
3. validate formulas
4. resolve provider dependencies
5. freeze execution order
6. generate compiled plan
7. optionally generate preview data

### 10.3 Runtime Print

At print:

1. report code is selected
2. compiled plan is loaded
3. parameters are validated
4. providers execute
5. output model is returned
6. HTML template renders

## 11. How Fast It Can Be

Yes, this is faster than re-reading everything every time.

Because at runtime you avoid:

- full designer analysis
- re-binding discovery
- repeated field-to-table interpretation
- repeated query-shape decisions

The only runtime work becomes:

- load compact plan
- run fixed provider calls
- shape result
- render HTML

This is the correct optimization direction.

## 12. How to Handle Customer Changes

When a customer changes the design:

1. update draft definition
2. republish report
3. regenerate compiled plan
4. replace old plan version

You do not need to rebuild the whole application.

If versioning is needed, store:

- report code
- version
- published date
- plan hash

## 13. Suggested File Structure

```text
Reports/
  Templates/
    SALE_INVOICE_A4/
      template.html
      template.css
      definition.json
      compiled-plan.json
      sample-data.json
```

If you later want binary optimization:

```text
Reports/
  Templates/
    SALE_INVOICE_A4/
      compiled-plan.bin
```

## 14. Suggested Interfaces

### 14.1 Report Compiler

```csharp
public interface IReportCompiler
{
    CompiledReportPlan Compile(ReportDefinition definition);
}
```

### 14.2 Report Executor

```csharp
public interface ICompiledReportExecutor
{
    Task<ReportExecutionResult> ExecuteAsync(
        CompiledReportPlan plan,
        string connectionString,
        Dictionary<string, object> parameters);
}
```

### 14.3 Provider Registry

```csharp
public interface IDataProviderRegistry
{
    IReportDataProvider Get(string providerName);
}
```

### 14.4 Data Provider

```csharp
public interface IReportDataProvider
{
    Task<object> ExecuteAsync(
        string connectionString,
        ProviderRequest request);
}
```

## 15. Best Practical Recommendation

For your case, implement this in stages.

### Stage 1

Build:

- report definition
- dataset registry
- HTML rendering

### Stage 2

Add:

- compiled execution plan generation
- fixed runtime executor
- provider registry

### Stage 3

Add:

- expression compilation for formulas
- binary compiled plan cache

### Stage 4

Only if needed:

- generated executor classes
- dynamic assembly/DLL generation for hot reports

## 16. Final Conclusion

Yes, your idea is valid.

The best way is not to generate a DLL first for every report.
The best first implementation is:

- design report
- save metadata
- compile metadata into a runtime execution plan
- at print-time call a fixed executor with:
  - report code
  - connection string
  - parameters
- executor returns raw structured report data
- HTML template renders and prints

This gives:

- fast runtime
- lower repeated analysis cost
- easier maintenance
- easier client-side deployment
- easier future server migration

If needed later, this same architecture can be extended to generate real compiled code or DLL-based executors for selected heavy reports.
