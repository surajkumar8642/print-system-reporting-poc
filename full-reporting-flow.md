# Full Reporting Flow

## 1. Purpose

This document explains the complete end-to-end flow of the proposed reporting system.

It covers:

- design-time flow
- publish flow
- runtime flow
- redesign flow
- database flow
- output flow

This is the full operational structure.

## 2. Main Actors

The system has five main actors:

1. application
2. report designer
3. report compiler
4. report executor
5. HTML renderer

## 3. Main Assets

The system uses these main assets:

- `reports.json`
- `definition.json`
- `compiled-plan.json`
- `template.html`
- `template.css`
- database connection
- runtime parameters

## 4. Full Lifecycle

The report lifecycle has four main stages:

1. design
2. publish
3. runtime execute
4. redesign/version update

## 5. Design Stage

At design time, the user or admin creates a report layout.

### 5.1 Inputs

- report family such as `SaleInvoice`
- template variant such as `STANDARD_A4`
- root entity such as `Sale`
- business datasets such as `SaleHeader`, `SaleItems`, `TaxBreakup`

### 5.2 Designer Actions

The designer:

- selects datasets
- selects fields
- drags fields into the layout
- creates tables and sections
- defines formulas
- sets visibility rules
- saves the definition

### 5.3 Output of Design Stage

The design stage creates:

- `template.html`
- `template.css`
- `definition.json`

## 6. Publish Stage

This stage converts a design into a runtime-ready published artifact.

### 6.1 Publish Inputs

- current `definition.json`
- selected datasets
- selected fields
- formula definitions

### 6.2 Compiler Work

The compiler:

1. validates parameters
2. validates dataset names
3. validates fields
4. validates bindings
5. validates formulas
6. resolves provider sequence
7. generates `compiled-plan.json`

### 6.3 Publish Output

The publish stage creates:

- published template version
- published definition
- published compiled plan

## 7. Runtime Execution Stage

This is what happens when user clicks preview or print.

### 7.1 User Action

Example:

- open sale entry
- choose `Sale Invoice Standard A4`
- click print

### 7.2 Runtime Inputs

- active report code
- active published version
- database connection
- runtime parameters such as `saleId`

### 7.3 Runtime Steps

1. load report catalog
2. resolve active version
3. load `compiled-plan.json`
4. validate runtime parameters
5. create provider requests
6. call required providers
7. merge provider results into report model
8. apply formulas
9. render HTML using template
10. show preview or print

## 8. Database Flow

The database does not directly talk to the template.

The flow is:

1. executor reads `compiled-plan.json`
2. executor sees required providers
3. executor calls provider registry
4. provider registry returns provider implementations
5. providers execute SQL against customer database
6. providers return structured data

This is important because it prevents the template from owning raw DB logic.

## 9. Data Flow Example

Example for `saleId = 101`:

### 9.1 Compiled Plan Says

- `SaleHeader`
- `SaleItems`
- `TaxBreakup`

### 9.2 Providers Return

- one sale header object
- many sale item rows
- many tax rows

### 9.3 Executor Builds

```json
{
  "data": {
    "saleHeader": {},
    "saleItems": [],
    "taxBreakup": []
  },
  "computed": {}
}
```

### 9.4 Renderer Uses

- `template.html`
- `template.css`
- report model

and produces printable HTML.

## 10. Output Flow

The renderer output can be used for:

- screen preview
- browser print
- PDF export

This gives the same basic print structure in:

- local PC mode
- server mode

## 11. Redesign Flow

Reports must support future changes.

When customer wants layout changes:

1. current version is copied to draft
2. draft is modified
3. draft is revalidated
4. new version is published
5. new `compiled-plan.json` is generated
6. active version can be switched

This keeps runtime safe while allowing change.

## 12. Multiple Template Flow

One business entity can have many templates.

Example for `SaleInvoice`:

- standard A4
- A5
- thermal
- detail with tax
- retail format

All of them can share the same provider layer, but have different:

- templates
- definitions
- compiled plans

## 13. Performance Flow

The speed comes from these rules:

1. do not load all tables
2. do not rebuild mapping every time
3. do not analyze whole design every time
4. only load compiled plan
5. only call required providers
6. only return required report data

## 14. Error Flow

If something fails:

- invalid parameter
- missing provider
- SQL connection failure
- invalid field in definition

the executor should stop early and return a controlled error to preview/logging.

## 15. Local and Server Flow

### 15.1 Local PC Mode

- app runs on customer PC
- SQL Server is local
- templates are local
- report preview is local

### 15.2 Server Mode

- same app copied to server
- same templates copied to server
- DB connection changed
- browser accesses server-hosted app

The full report structure remains the same.

## 16. Final Structure Summary

The complete model is:

### 16.1 Design Layer

- template editor
- dataset explorer
- field binding
- formula editor

### 16.2 Publish Layer

- validation
- compilation
- versioning

### 16.3 Runtime Layer

- compiled plan loader
- provider execution
- model builder
- formula evaluation

### 16.4 Output Layer

- HTML rendering
- preview
- print
- PDF

## 17. Actual Status in This Workspace

Currently available:

- concept documents
- sample template files
- sample definition
- sample compiled plan
- sample report data

Currently not available:

- real executor code
- real provider code
- real MVC app
- real database execution
- real preview output

## 18. Correct Next Step

If you want this to move from concept to working system, the next build should be:

1. create ASP.NET Core MVC solution
2. create report executor library
3. create provider registry
4. create one SQL-backed sample provider set
5. create one preview page
6. run one real sale invoice from database to HTML

That will be the first true runtime proof.
