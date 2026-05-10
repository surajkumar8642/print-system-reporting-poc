# Database Concept Model for HTML Reporting

## 1. Goal

This document explains the full database-side concept for the reporting system.

It focuses on:

- how local SQL Server fits into the reporting architecture
- how connection strings should work
- how report data should be fetched
- how dataset providers should be designed
- how report definitions and database logic should stay separate
- how to support dynamic customer reports without reading all tables every time

The target environment is:

- client machine first
- local SQL Server or local database engine
- optional later move to a server
- same report system in both cases

## 2. Main Principle

The report should not directly own the database logic.

The report should only say:

- what business data it needs
- what parameters it needs
- what layout it needs

The application/reporting engine should own:

- connection handling
- query execution
- dataset mapping
- security
- output shaping

This is the central concept.

## 3. Connection Model

In your case, the application can use the local machine SQL Server instance.

Example target pattern:

```text
Server=DESKTOP-75T57GH\SQLEXPRESS02;
Database=YourDatabaseName;
Trusted_Connection=True;
TrustServerCertificate=True;
```

Because you are using Windows Authentication, the main required values are:

- SQL Server instance name
- database name
- trusted connection

If later moved to server, only these change:

- server name
- database name if different

The reporting design itself should not need to change.

## 4. Recommended Connection Ownership

Do not store connection string inside every report file.

Instead:

- application stores database connection config
- reporting engine receives connection from application
- report only receives parameters like `saleId`, `fromDate`, `partyId`

Recommended flow:

1. app knows current customer database
2. app creates DB connection or DB context
3. report executor receives that connection
4. providers run against that connection

This is cleaner and safer than storing DB credentials in template files.

## 5. Database Responsibility Split

Split responsibilities like this:

### 5.1 Database

Stores:

- transaction data
- master data
- stock data
- tax data
- customer/company setup

### 5.2 Report Definition

Stores:

- selected datasets
- selected fields
- formulas
- bindings
- visibility rules

### 5.3 Dataset Provider Layer

Stores:

- actual SQL logic
- joins
- filters
- projection rules

This separation is critical.

## 6. What the Database Layer Should Return

The provider layer should not return random unstructured data.

It should return data in a standard report shape.

Possible output forms:

- typed DTO objects
- dictionary objects
- JSON-ready object graphs
- DataTable/DataSet if required

For HTML-based systems, best output is:

- typed objects or JSON-ready models

Example:

```json
{
  "saleHeader": {
    "SaleNo": "S-101",
    "CustomerName": "ABC Traders"
  },
  "saleItems": [
    {
      "ProductName": "Item A",
      "Qty": 2
    }
  ]
}
```

## 7. Dataset Provider Concept

This is the most important database-side concept.

Instead of allowing every report to create direct SQL against raw tables, create reusable provider functions.

Examples:

- `SaleHeaderProvider`
- `SaleItemsProvider`
- `TaxBreakupProvider`
- `StockSummaryProvider`
- `StockBatchProvider`
- `LedgerSummaryProvider`

Each provider should:

- accept DB connection or DB context
- accept parameters
- accept required field list
- execute optimized query
- return structured data

## 8. Why Provider Model Is Better

Without providers:

- every report writes its own SQL
- logic repeats
- database schema becomes tightly exposed
- performance becomes inconsistent
- support becomes difficult

With providers:

- business logic is centralized
- queries can be optimized once
- reports become easier to build
- schema changes are easier to manage

## 9. Example Provider Contract

Example concept:

```csharp
public interface IReportDataProvider
{
    string Name { get; }

    Task<object> ExecuteAsync(
        SqlConnection connection,
        ProviderRequest request,
        CancellationToken cancellationToken = default);
}
```

Where `ProviderRequest` contains:

- provider name
- required fields
- parameters
- optional sort/filter settings

## 10. Example Provider Request

```json
{
  "provider": "SaleItems",
  "fields": ["ProductName", "Qty", "Rate", "MRP", "LineTotal"],
  "parameters": {
    "saleId": 101
  }
}
```

## 11. Query Strategy

The database layer should use a controlled query strategy.

### 11.1 Do Not

- load 20 to 50 tables for every report
- pass all tables into report engine
- let the layout decide joins at runtime blindly

### 11.2 Do

- start from one root entity
- call only required providers
- fetch only required fields where practical
- use parameterized queries
- keep each provider focused

## 12. Root Entity Model

Every report should start from one root context such as:

- Sale
- Purchase
- Stock
- Tax
- Ledger

That root determines:

- required parameters
- allowed providers
- join direction
- data boundary

Example:

If root entity is `Sale`, then likely allowed providers are:

- `SaleHeader`
- `SaleItems`
- `TaxBreakup`
- `PartyInfo`
- `TransportInfo`

This avoids full-database chaos.

## 13. Field Projection Concept

If a provider supports field projection, it should only select needed columns.

Example:

If report needs:

- `ProductName`
- `Qty`
- `MRP`

then query should avoid extra values like:

- product description
- purchase history
- supplier code
- extra batch metadata

where possible.

This improves:

- speed
- memory usage
- mapping simplicity

## 14. Example Data Retrieval Flow

Assume report:

- `SALE_INVOICE_STANDARD_A4`
- parameter `saleId = 101`

Execution:

1. compiled plan loaded
2. execution plan says:
   - `SaleHeader`
   - `SaleItems`
   - `TaxBreakup`
3. provider registry resolves these providers
4. each provider executes against local SQL Server
5. provider results merged into one report model
6. HTML renderer uses that model

The database is touched only through those required providers.

## 15. Where SQL Should Live

There are 3 options.

### 15.1 SQL in C# Code

Simple and direct.

Pros:

- easy deployment
- no DB dependency beyond tables

Cons:

- larger codebase

### 15.2 Stored Procedures

Good for heavy stable reports.

Pros:

- performance tuning possible in DB
- reusable by many reports

Cons:

- harder customer-by-customer deployment
- DB version drift risk

### 15.3 Hybrid

Best practical option.

Use:

- provider code for most reports
- stored procedures only for selected heavy or complex reports

This is the recommended direction.

## 16. Dynamic Reports and Database Control

Customers may ask for:

- sale detail
- stock expiry
- tax report
- product price list
- custom invoice

Support this by:

1. exposing approved datasets
2. exposing approved fields
3. letting designer bind them
4. compiling the final plan

Do not support this by:

- letting every user write arbitrary SQL against every table

That would become difficult to support at scale.

## 17. Caching Concept

Database caching should be selective.

Good candidates:

- company profile
- branch settings
- static master labels
- tax label setup

Use caution for:

- sale transactions
- stock balances
- ledger balances

because those may change frequently.

## 18. Error Handling Concept

The provider layer should report errors clearly:

- missing parameter
- missing dataset provider
- invalid field selection
- database connection failure
- query timeout

The report template should not hide DB failures.
It should show a controlled preview or log message.

## 19. Local PC and Server Use

This same DB model works in both cases.

### 19.1 Local PC

- app on local machine
- DB on local SQL Server instance
- report engine uses local connection

### 19.2 Server

- app on server
- DB on same server or another server
- same providers run with a different connection

The reporting model remains the same.

## 20. Practical Recommendation

Use this approach:

1. application owns connection
2. reports never directly store raw connection details per template
3. dataset providers own SQL logic
4. compiled plan chooses which providers run
5. provider results return standard object data
6. HTML renderer prints that data

This gives the best balance of:

- speed
- dynamic support
- safety
- maintainability

## 21. Final Summary

The database should be treated as the source of business data, not as the report designer itself.

The right concept is:

- DB stores business records
- provider layer reads business records
- compiled report plan decides what to fetch
- standard report model is produced
- HTML template renders the final print

This is the cleanest way to support:

- local customer installation
- future server deployment
- fast printing
- complex reports
- dynamic template redesign
