# Reporting System POC (Raw Table / Function Driven)

## Purpose

This proof-of-concept demonstrates a runtime report pipeline suitable for migration from Crystal-style designs:

- Design JSON describes only required tables and columns.
- Functions are selected from a catalog and auto-expanded into formula dependencies.
- Compile creates a runtime execution plan.
- Preview renders with a user-editable HTML template.
- Dynamic definitions can be sent directly through API for rapid testing.
- Browser UI supports a public-facing drag/drop workflow (data extractor + report zone designer + function catalog).

## Run

```powershell
cd C:\Work\lerarn\print-system\ReportPoc
dotnet run
```

Open:

- `http://localhost:5244` (or the shown host URL)

## UI workflow

1. Open the home page to launch the Crystal-like visual designer.
2. Select a report variant and fill parameter values (default `saleId`).
3. Use **Load Database** in Database Explorer (left side) to fetch DB schema/columns.
4. Drag fields from left to right canvas zones:
   - Header (single row)
   - Items (list rows)
   - Tax (list rows)
   - Footer (single row)
   - Subreport (list rows)
5. Choose catalog functions (dependencies are auto-added by compile).
6. Add optional custom formulas in the dedicated formula area.
7. Click **Compile & preview** to generate HTML preview and runtime artifacts.

## API

### Core

- `GET /api/reports`
- `GET /api/reports/{reportCode}/definition?version=v1`
- `GET /api/data/tables?connectionString=...&schema=dbo`
- `GET /api/function-catalog`
- `POST /api/reports/{reportCode}/compile`
- `POST /api/reports/{reportCode}/preview`

### Request body (compile/preview)

```json
{
  "version": "v1",
  "connectionString": "optional override",
  "parameters": { "saleId": 101 },
  "definition": {
    "reportCode": "CUSTOM",
    "rootEntity": "Sale",
    "parameters": [{ "name": "saleId", "type": "int", "required": true }],
    "datasets": [
      { "name": "saleItems", "provider": "SaleItems", "fields": ["ProductName", "Qty"], "isSingleRow": false, "filterField": "saleId", "orderBy": "SaleDetailId" }
    ],
    "bindings": [],
    "formulas": [],
    "functionNames": ["TOTAL_QTY"]
  },
  "templateHtml": "<html>{{computed.fn_TOTAL_QTY}}</html>",
  "templateCss": "body{}"
}
```

- `definition` is optional. If omitted, existing stored definition is used.
- `templateHtml` / `templateCss` are optional.
- If `definition` is present, or template is overridden, a runtime design workspace is generated automatically for preview/compile.

## Runtime artifacts

- `compiled-plan.bin` is the runtime execution model consumed by rendering.
- `compiled-plan.debug.json` is a human-readable copy of the same model.
- Runtime edits using the UI are stored under `Reports/Templates/_Runtime/<run-id>/<version>/`.
- `template.html` and `template.css` inside runtime folder are generated from your canvas.

## Files of interest

- `Models/ReportDefinition.cs` (definition schema, runtime metadata)
- `Services/DataProviders.cs` (known providers + dynamic SQL provider)
- `Services/ReportCompiler.cs` (dependencies + formula/function injection)
- `Services/FunctionCatalog.cs` (function library)
- `Services/DatabaseMetadataService.cs` (`/api/data/tables`)
- `Program.cs` (endpoint flow + runtime report workspace)
- `wwwroot/index.html` (drag-like schema+dataset builder + function/template UI)
- `Reports/Templates/SaleInvoice/STANDARD_A4/v1` (sample definition/template)

## Validation completed

- `dotnet build` passes.
- `GET /api/reports` works and feeds report picker.
- `GET /api/function-catalog` works and powers public function picker.
- `POST /api/reports/SALE_INVOICE_STANDARD_A4/compile` works (catalog + runtime definition).
- `POST /api/reports/SALE_INVOICE_STANDARD_A4/preview` works (with fallback sample data).
- `POST /api/reports/SALE_INVOICE_STANDARD_A4/compile` with runtime definition works.
- Custom runtime definition + functions compile and render via preview.
