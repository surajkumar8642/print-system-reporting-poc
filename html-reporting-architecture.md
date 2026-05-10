# HTML Reporting Architecture for Client-Side and Server-Side Use

## 1. Goal

This document defines a future reporting structure for a business application that:

- runs first on a client PC
- can later be copied to a server with minimal change
- supports many different customer print formats
- avoids loading unnecessary data
- allows fast report execution
- allows easy report modification
- supports complex reporting such as sales, purchase, stock, tax, ledger, credit/debit, expiry, and summary reports

The main objective is to replace the old pattern:

- application prepares many tables in memory
- application sends large datasets to Crystal Reports
- report uses only a small part of the data

with a new pattern:

- selected report tells the system what data it needs
- system loads only that data
- HTML template renders the output
- browser print or PDF export produces the final report

## 2. Main Problem in the Current Crystal Model

In the current pattern, the code often does this:

1. read many related tables
2. combine data in code
3. create large DataSet/DataTable structures
4. pass everything into the report engine
5. let the report use only some of those values

This creates several problems:

- slower reporting
- high memory usage
- difficult maintenance
- duplicate logic between code and report
- hard to understand dependencies
- difficult client-specific customization
- report engine becomes tightly coupled to current database and current code

## 3. Recommended Future Model

Use a metadata-driven HTML reporting engine.

The report system should be divided into separate layers:

1. report catalog
2. report template
3. report definition
4. dataset registry
5. query/data resolver
6. formula engine
7. renderer
8. print/export layer

This separation is the most important design improvement.

## 4. High-Level Architecture

```text
User selects report
    ->
Load report catalog entry
    ->
Load report definition
    ->
Determine required datasets, fields, parameters
    ->
Run dataset providers only for those datasets
    ->
Build report model JSON
    ->
Apply model to HTML template
    ->
Render preview / print / export PDF
```

## 5. Core Components

### 5.1 Report Catalog

The report catalog is the master list of all reports.

It stores:

- report code
- report name
- report category
- business module
- paper size
- orientation
- template version
- active/inactive status
- target entity type such as Sale, Purchase, Stock
- required parameters

Example:

```json
{
  "reportCode": "SALE_INVOICE_A4",
  "name": "Sale Invoice A4",
  "module": "Sales",
  "rootEntity": "Sale",
  "templatePath": "Reports/Templates/SALE_INVOICE_A4/template.html",
  "definitionPath": "Reports/Templates/SALE_INVOICE_A4/definition.json",
  "paperSize": "A4",
  "orientation": "Portrait",
  "isActive": true
}
```

### 5.2 Report Template

The template is the visual design.

It contains:

- header layout
- footer layout
- labels
- tables
- row sections
- tax summary blocks
- totals
- fonts
- spacing
- conditional sections
- barcode or QR areas if needed

This can be stored as:

- `template.html`
- `template.css`
- optional layout JSON if a custom drag-drop designer is used

The template should not contain heavy business logic.

### 5.3 Report Definition

The report definition is the metadata behind the design.

It defines:

- root entity
- available parameters
- required datasets
- required fields
- groupings
- sorting
- formulas
- visibility rules
- control-to-field bindings

This is the key file that makes the system dynamic.

Example:

```json
{
  "reportCode": "SALE_INVOICE_A4",
  "rootEntity": "Sale",
  "parameters": ["saleId"],
  "datasets": [
    {
      "name": "saleHeader",
      "provider": "SaleHeader",
      "fields": ["SaleNo", "SaleDate", "CustomerName", "BillingAddress", "NetAmount", "TotalTax"]
    },
    {
      "name": "saleItems",
      "provider": "SaleItems",
      "fields": ["ProductName", "Qty", "Rate", "MRP", "DiscountAmount", "LineTotal"]
    },
    {
      "name": "taxBreakup",
      "provider": "TaxBreakup",
      "fields": ["TaxName", "TaxRate", "TaxAmount"]
    }
  ],
  "bindings": [
    { "control": "txtSaleNo", "field": "saleHeader.SaleNo" },
    { "control": "txtCustomerName", "field": "saleHeader.CustomerName" },
    { "control": "tblItems", "field": "saleItems" },
    { "control": "tblTax", "field": "taxBreakup" }
  ]
}
```

### 5.4 Dataset Registry

This is a controlled mapping layer between business report fields and actual database queries.

This should not expose raw tables first.

Instead of showing:

- `tbl_sale`
- `tbl_sale_item`
- `tbl_product`
- `tbl_stock`

the designer should show business-friendly datasets:

- `Sale Header`
- `Sale Items`
- `Sale Tax Breakup`
- `Stock Detail`
- `Stock Batch Detail`
- `Purchase Header`
- `Ledger Summary`

Internally, each dataset provider knows:

- source tables
- joins
- filters
- supported parameters
- allowed output fields

Example registry:

```json
{
  "provider": "SaleItems",
  "rootEntity": "Sale",
  "parameters": ["saleId"],
  "sourceTables": ["Sale", "SaleItem", "Product"],
  "allowedFields": ["ProductName", "Qty", "Rate", "MRP", "Amount", "Discount", "TaxPercent"]
}
```

### 5.5 Query/Data Resolver

This is the execution layer.

It reads the selected report definition and decides:

- which providers are required
- which parameters are required
- which fields are required
- which queries should run

This layer solves the main optimization problem.

If a report only uses:

- `Sale.CustomerName`
- `Sale.SaleNo`
- `SaleItems.ProductName`
- `SaleItems.Qty`

then only those datasets should execute.

The system should not automatically read:

- stock tables
- tax history tables
- unrelated purchase tables
- account ledgers
- extra product metadata

unless the report definition actually needs them.

### 5.6 Formula Engine

Some customers need additional calculations.

Examples:

- line total
- total quantity
- gross amount
- discount percent
- tax total
- round off
- amount in words
- conditional labels

These should be supported by a formula layer, not by raw template scripting everywhere.

Supported formula types:

- aggregate formulas such as `sum(items.LineTotal)`
- arithmetic formulas such as `Gross - Discount + Tax`
- conditional formulas such as `if(NetAmount > 10000, 'High Value', '')`
- formatting formulas such as number/date formatting
- display formulas such as `AmountInWords`

### 5.7 HTML Renderer

This layer takes a final report model and injects it into the template.

Input:

- template HTML
- style CSS
- report model JSON

Output:

- browser preview
- printable HTML
- PDF export if required

The renderer should support:

- single value fields
- repeated row sections
- grouping
- page headers and footers
- tax summary blocks
- optional sections
- conditional visibility

### 5.8 Print and Export Layer

This layer handles:

- browser print
- A4/A5/custom sizes
- page margins
- portrait/landscape
- PDF export
- printer-specific adjustments if required

For most modern systems, browser print and PDF are enough.

## 6. Dynamics Support

The system must support many different customer requests without changing core code every time.

This should be achieved through metadata and controlled datasets, not through unrestricted raw SQL.

### 6.1 What Should Be Dynamic

The following should be configurable:

- report layout
- visible fields
- column order
- labels and captions
- logo and branding
- grouping
- sorting
- page size
- showing or hiding tax blocks
- showing or hiding MRP, expiry, batch, barcode
- choosing one of multiple templates for the same entity

### 6.2 What Should Not Be Fully Open

The following should not be completely unrestricted for end users:

- arbitrary joins across the whole database
- arbitrary SQL execution
- unrestricted update/delete statements
- unrestricted script execution inside reports

Reason:

- high support cost
- security risk
- performance risk
- schema coupling
- inconsistent output

### 6.3 Best Dynamic Model

Use a controlled dynamic model:

1. define approved business datasets
2. expose approved fields from those datasets
3. allow drag-drop binding from those fields
4. allow formulas and visibility rules
5. allow optional advanced mode for expert/admin users only

This gives flexibility without losing control.

## 7. How the Designer Should Work

The designer is the most important user-facing part.

It should not be just an HTML editor.

It should be a report builder with separate zones.

### 7.1 Designer UI Structure

Recommended panels:

1. report list / report properties
2. dataset explorer
3. field explorer
4. design canvas
5. properties panel
6. formula editor
7. preview panel

### 7.2 Dataset Explorer

The left panel should show datasets such as:

- Sale Header
- Sale Items
- Sale Tax
- Stock Summary
- Stock Batch Detail
- Purchase Header
- Ledger Summary
- Party Address

Each dataset expands to fields:

- `SaleNo`
- `SaleDate`
- `CustomerName`
- `NetAmount`
- `ProductName`
- `MRP`
- `Qty`
- `ExpiryDate`

### 7.3 Design Canvas

The user drags items to the design area.

Supported elements:

- text field
- label
- image/logo
- line
- rectangle
- table/grid
- group section
- header section
- footer section
- totals block
- tax summary block

### 7.4 Properties Panel

For each selected control:

- bound field
- font
- size
- alignment
- color
- format
- visibility condition
- border
- width and height

### 7.5 Formula Editor

The user can define:

- `sum(saleItems.LineTotal)`
- `sum(saleItems.Qty)`
- `saleHeader.NetAmount + saleHeader.TotalTax`
- `if(saleHeader.TotalTax > 0, true, false)`

### 7.6 Preview

The designer should provide preview with sample parameter values so the user can verify layout before saving.

## 8. Saved Artifacts

When a report is saved, the system should store both design metadata and execution metadata.

Recommended structure:

```text
Reports/
  Catalog/
    reports.json
  Templates/
    SALE_INVOICE_A4/
      template.html
      template.css
      definition.json
      sample-data.json
    STOCK_SUMMARY_A4/
      template.html
      template.css
      definition.json
```

### 8.1 template.html

Contains layout structure.

### 8.2 template.css

Contains print styling.

### 8.3 definition.json

Contains datasets, bindings, formulas, parameters, and conditions.

### 8.4 sample-data.json

Optional sample preview data for design-time rendering.

## 9. Execution Flow During Print

When the user clicks print:

1. select report
2. system loads report catalog entry
3. system loads report definition
4. system validates parameters
5. system determines required datasets
6. system executes only those dataset providers
7. system builds final report model
8. system renders HTML
9. system opens preview
10. user prints or exports PDF

## 10. Example Scenarios

### 10.1 Sale Detail Print

User wants:

- sale number
- date
- customer name
- item table
- tax breakup

Required datasets:

- `SaleHeader`
- `SaleItems`
- `TaxBreakup`

No need to load:

- stock summary
- purchase tables
- ledger history

### 10.2 Stock Expiry Report

User wants:

- product name
- batch
- expiry date
- MRP
- available stock

Required datasets:

- `StockBatchDetail`

No need to load:

- sale item data
- invoice tax tables
- payment vouchers

### 10.3 Tax Summary Report

User wants:

- from date
- to date
- tax type
- tax amount
- taxable value

Required datasets:

- `TaxSummary`

No need to load item-level datasets unless the report definition asks for them.

## 11. How Complexity Is Handled

Complex reports should be supported by composition, not by one giant query.

Use these strategies:

### 11.1 Root Entity Model

Each report begins from one root:

- Sale
- Purchase
- Stock
- Ledger
- Tax

This avoids uncontrolled joins.

### 11.2 Dataset Provider Isolation

Each dataset provider owns one business concern.

Examples:

- `SaleHeaderProvider`
- `SaleItemsProvider`
- `TaxBreakupProvider`
- `StockBatchProvider`

This makes maintenance easier.

### 11.3 Optional Advanced Datasets

For advanced customers, create approved complex datasets:

- `SaleWithBatchAndTax`
- `LedgerWithOpeningClosing`
- `StockValuationByBatch`

This gives complexity support without opening full SQL freedom.

### 11.4 Formula Layer for Presentation Logic

Keep display calculations in formulas, not in repeated copy-paste code.

## 12. Client-Side and Server-Side Deployment

The same architecture should support both modes.

### 12.1 Local Client PC Mode

- application runs on local machine
- database is local
- templates are local files
- browser is local
- printing happens locally

### 12.2 Server Mode

- same application files are copied to server
- same templates are copied to server
- database points to server database
- clients access via browser

This is why HTML-based reporting is stronger than desktop-bound report engines.

## 13. Where Data and Design Should Be Stored

Recommended:

### 13.1 File Storage

Store:

- HTML templates
- CSS
- report definitions
- preview samples

Benefits:

- easy backup
- easy copy from local to server
- versionable
- simple packaging

### 13.2 Database Storage

Store:

- report catalog metadata
- user-to-report mapping
- report access permissions
- selected default template per transaction type
- report usage history

For very flexible systems, either files or DB can work. For your use case, file-based templates plus DB metadata is a practical balance.

## 14. Security and Governance

If clients can edit reports, apply control levels.

### 14.1 Standard User

- choose from predefined templates
- change labels, logo, and visible fields

### 14.2 Power User

- rearrange layout
- add approved fields
- modify grouping and totals

### 14.3 Admin/Developer

- create new templates
- enable advanced datasets
- edit formulas
- manage registry and provider mappings

This is important because not all customers should be allowed to build unrestricted reports.

## 15. Performance Optimization Rules

These rules are mandatory if performance is important.

### 15.1 Field-Level Fetching

Only fetch required columns where practical.

### 15.2 Dataset-Level Fetching

Only execute dataset providers referenced in the report definition.

### 15.3 Parameterized Queries

All providers should use parameterized queries such as:

- `saleId`
- `fromDate`
- `toDate`
- `partyId`
- `productId`

### 15.4 Query Reuse

Common datasets should be reused across reports instead of rewriting SQL many times.

### 15.5 Caching

Optional for lookup data:

- company profile
- branch details
- static tax labels

Avoid caching transactional data too aggressively unless needed.

### 15.6 Pagination and Large Reports

For very large list reports:

- support page chunking
- support streamed rendering if required
- support summary vs detail modes

## 16. Recommended Starting Structure

Start with a limited but powerful foundation.

### 16.1 Phase 1

Build 10 to 20 sample reports:

- sale invoice
- sale detail
- purchase invoice
- stock summary
- stock batch expiry
- tax summary
- ledger statement
- debit note
- credit note
- product price list

### 16.2 Phase 2

Build dataset registry for these modules:

- sales
- purchase
- stock
- ledger
- tax

### 16.3 Phase 3

Build designer with:

- drag-drop fields
- report sections
- grid/repeater
- formulas
- preview

### 16.4 Phase 4

Add advanced customer customization:

- branding
- alternate layouts
- custom labels
- multi-template support per transaction

## 17. Recommendation Summary

The best future structure is:

- HTML/CSS template for layout
- JSON definition for metadata
- controlled dataset registry for data access
- resolver that executes only required datasets
- formula engine for display logic
- browser print/PDF for output
- local-first deployment with later server migration

This model gives the best balance of:

- speed
- flexibility
- maintainability
- migration readiness
- support for complex customer-specific designs

## 18. Final Decision Guidance

If the goal is:

- keep using old report-engine style design
- allow limited modernization

then RDLC can be considered.

If the goal is:

- support client-side first
- support future server hosting
- allow fast customization
- reduce over-fetching
- support many different layouts and data combinations

then HTML-based metadata-driven reporting is the better long-term architecture.
