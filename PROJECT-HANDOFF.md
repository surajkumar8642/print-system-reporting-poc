# Report System POC Handoff

Last active date (UTC+05:30): 2026-05-10

This repository contains a working proof-of-concept replacing Crystal-style report design with a two-phase HTML reporting architecture:
- **Design time:** drag/drop dataset + function based designer in browser
- **Runtime:** compile to a compact execution plan and execute only requested data

## 1) What is implemented

- ASP.NET Core MVC (minimal API style) project in `ReportPoc/`
- Crystal-like web designer at `ReportPoc/wwwroot/index.html`
- Runtime endpoints for:
  - report catalog
  - data explorer
  - function catalog
  - compile
  - preview
- report definition model and compiler:
  - `ReportPoc/Models/ReportDefinition.cs`
  - `ReportPoc/Models/CompiledReportPlan.cs`
  - `ReportPoc/Services/ReportCompiler.cs`
  - `ReportPoc/Services/ReportExecutorService.cs`
- function registry and function dependency expansion:
  - `ReportPoc/Services/FunctionCatalog.cs`
  - `ReportPoc/Services/FormulaEvaluator.cs`
  - `ReportPoc/Services/FormulaFieldPathExtractor.cs`
- data providers:
  - `ReportPoc/Services/DataProviders.cs`
  - `ReportPoc/Services/DatabaseMetadataService.cs`
- report catalog and sample assets:
  - `ReportPoc/Reports/Catalog/reports.json`
  - `ReportPoc/Reports/Templates/SaleInvoice/STANDARD_A4/v1/definition.json`
  - `ReportPoc/Reports/Templates/SaleInvoice/STANDARD_A4/v1/template.html`
  - `ReportPoc/Reports/Templates/SaleInvoice/STANDARD_A4/v1/template.css`
  - `ReportPoc/Reports/Templates/SaleInvoice/STANDARD_A4/v1/compiled-plan.bin`
  - `ReportPoc/Reports/Templates/SaleInvoice/STANDARD_A4/v1/compiled-plan.debug.json`
  - `ReportPoc/Reports/Templates/SaleInvoice/STANDARD_A4/v1/sample-data.json`
- runtime SQL demo schema:
  - `ReportPoc/db/ReportPocDemo.sql`

## 2) How to run

```powershell
cd C:\Work\lerarn\print-system\ReportPoc
dotnet restore
dotnet build
dotnet run
```

Open in browser:

- `http://localhost:5090` (or shown port)

Compile and preview from API (example):

- `POST /api/reports/SALE_INVOICE_STANDARD_A4/compile`
- `POST /api/reports/SALE_INVOICE_STANDARD_A4/preview`

## 3) Local database setup

Default connection string in `ReportPoc/appsettings.json`:
`Server=DESKTOP-75T57GH\SQLEXPRESS;Database=ReportPocDemo;Trusted_Connection=True;TrustServerCertificate=True;`

If your local machine differs:
- create `ReportPocDemo` database using `ReportPoc\db\ReportPocDemo.sql`
- update `ConnectionStrings:ReportDatabase` in `ReportPoc/appsettings.Development.json`

The app already has fallback metadata for offline DB discovery in the data endpoint, so UI still works for design exploration when SQL is unavailable.

## 4) Designer and data flow (important)

1. User opens UI (`index.html`) and chooses report.
2. Left panel loads schema via `GET /api/data/tables`.
3. User drags fields/functions into report zones (Header/Detail/Tax/Summary/Subreport).
4. User clicks **Compile**.
5. Backend builds a runtime definition + compile plan.
6. Plan and template are written under `ReportPoc/Reports/Templates/_Runtime/...` unless publishing to fixed template path.
7. Preview endpoint executes compiled plan and renders HTML from bound template.

Runtime behavior is designed to avoid reprocessing full schema on every run; it consumes the compiled shape.

## 5) Compile/runtime artifacts

- Human-readable artifact: `compiled-plan.debug.json`
- Runtime artifact (fast path): `compiled-plan.bin`

## 6) API quick reference

- `GET /api/reports`
- `GET /api/reports/{reportCode}/definition?version=v1`
- `GET /api/data/tables?connectionString=...&schema=dbo`
- `GET /api/function-catalog`
- `POST /api/reports/{reportCode}/compile`
- `POST /api/reports/{reportCode}/preview`

Request payload (compile/preview):

```json
{
  "version": "v1",
  "connectionString": "optional override",
  "parameters": { "saleId": 101 },
  "definition": { ... optional runtime definition ... },
  "templateHtml": "<html>...</html>",
  "templateCss": "body{}",
  "compileOnly": false
}
```

## 7) Validation that was run locally

- `dotnet build` passes
- API smoke checks:
  - `GET /api/reports`
  - `GET /api/function-catalog`
  - `GET /api/data/tables?schema=dbo`
  - `POST /api/reports/SALE_INVOICE_STANDARD_A4/compile`
  - `POST /api/reports/SALE_INVOICE_STANDARD_A4/preview` (with stored/runtime definition path)

## 8) Known assumptions / risks

- Subreport support is represented in model and UI areas, but deep nested parameter propagation for all report families is still evolving.
- SQL extraction still relies on SQL Server metadata provider path; if SQL not available, metadata falls back to sample data.
- Output currently renders as HTML preview; print integration/export steps are intentionally kept lightweight for this POC.

## 9) Existing concept/architecture docs

- `html-reporting-architecture.md`
- `subreports-and-two-phase-design.md`
- `compiled-report-executor.md`
- `direct-executor-and-designer.md`
- `function-registry-and-derived-fields.md`
- `report-versioning-and-redesign.md`
- `full-reporting-flow.md`
- `reporting-blueprint.md`
- `crystal-replacement-requirements.md`
- `database-concept-model.md`
- `verification-status.md`

## 10) Git + GitHub handoff

### Initialize and commit locally

```powershell
cd C:\Work\lerarn\print-system
git init
git add .
git commit -m "feat: reporting web POC with designer and compile/preview pipeline"
```

### Push to public GitHub (create repo automatically)

```powershell
gh repo create print-system-reporting-poc --public --source . --remote origin --push
```

If repo name differs or you already have one:

```powershell
git remote add origin https://github.com/<owner>/<repo>.git
git branch -M main
git push -u origin main
```

To continue on another PC:

- clone repo
- run DB script if needed
- `cd ReportPoc`
- `dotnet run`
