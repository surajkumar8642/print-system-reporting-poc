# Direct Executor and UI Designer Model

## 1. Current Status

The current workspace does **not** contain:

- a real drag-drop report designer UI
- a compile button implementation
- a real generated executor artifact
- a DLL generator
- a binary compiled runtime artifact generator

What exists today is only:

- design documents
- sample report metadata
- sample compiled-plan JSON format
- sample HTML template

So your assumption is correct.

## 2. What the Designer Should Do

The designer should be a proper report-building screen.

When user opens the designer window, it should show:

1. report properties panel
2. dataset tree
3. column tree
4. selected datasets area
5. required fields area
6. design canvas
7. formula editor
8. preview area
9. compile/publish button

## 3. Recommended Designer UI Layout

### 3.1 Left Panel

Show business datasets:

- Sale Header
- Sale Items
- Tax Breakup
- Stock Summary
- Stock Batch Detail
- Purchase Header
- Ledger Summary

Each dataset expands into fields:

- SaleNo
- SaleDate
- CustomerName
- ProductName
- Qty
- Rate
- MRP
- TaxAmount

### 3.2 Middle Panel

Show the report layout canvas:

- page header
- body
- tables
- footer
- tax block
- totals block

The user drags fields here to create the visual layout.

### 3.3 Right Panel

Show selected item properties:

- bound field
- width
- alignment
- format
- font
- visibility condition
- aggregate formula

## 4. Required vs Used Data

You mentioned an important point:

- user drags tables and columns
- system should know exactly what is required
- compile should create a direct executor artifact

That is the correct model.

The designer should internally build three sets:

### 4.1 Available Datasets

All allowed datasets for the root entity.

### 4.2 Selected Datasets

Datasets the report actually depends on.

Example:

- SaleHeader
- SaleItems
- TaxBreakup

### 4.3 Selected Fields

Fields the report actually uses.

Example:

- SaleHeader.SaleNo
- SaleHeader.CustomerName
- SaleItems.ProductName
- SaleItems.Qty
- TaxBreakup.TaxAmount

The compile step should use only `Selected Datasets` and `Selected Fields`.

## 5. What Happens When User Presses Compile

When user presses `Compile`, the system should do this:

1. read current report draft
2. read selected datasets
3. read selected fields
4. read bindings
5. read formulas
6. validate everything
7. resolve provider dependencies
8. freeze execution sequence
9. generate direct executor artifact
10. save publishable runtime package

## 6. What the Compile Step Should Produce

The compile step should generate these files:

- `template.html`
- `template.css`
- `definition.json`
- `compiled-plan.bin`
- optional `compiled-plan.debug.json`

## 7. Direct Executor File

You asked where the direct executable file should be.

Recommended location:

```text
Reports/
  Templates/
    SaleInvoice/
      STANDARD_A4/
        v1/
          template.html
          template.css
          definition.json
          compiled-plan.bin
          compiled-plan.debug.json
```

## 8. Best Format for Direct Executor

For high efficiency, I recommend this:

### 8.1 Runtime File

Use:

- `compiled-plan.bin`

This should be:

- binary
- compact
- optimized for direct loading
- not intended for manual editing

This is the main direct executor artifact.

### 8.2 Debug File

Use:

- `compiled-plan.debug.json`

This should be:

- human-readable
- optional
- only for support/debugging/development

This gives both:

- high runtime speed
- easy developer troubleshooting

## 9. Human Readable or Not

For runtime efficiency, the main direct executor file should preferably be:

- not human-readable

That is acceptable and in your case preferable.

Recommended model:

- runtime artifact: binary
- support artifact: JSON

So the answer is:

- yes, main artifact can be non-human-readable
- yes, that is fine
- that is the better direction for performance

## 10. What Should Be Inside the Binary Artifact

The binary compiled artifact should contain:

- report code
- root entity
- version
- parameter schema
- selected providers
- selected fields
- provider execution order
- formula plan
- output aliases
- control binding map

This means runtime does not need to reopen the full designer structure.

## 11. Should It Be a DLL

There are two options.

### 11.1 Binary Plan File

Example:

- `compiled-plan.bin`

Pros:

- simpler deployment
- easier replacement on redesign
- no assembly loading problems
- very fast
- enough for most client-side systems

### 11.2 Generated DLL

Example:

- `SaleInvoice_STANDARD_A4_v1.Executor.dll`

Pros:

- potentially faster for extreme cases
- allows precompiled code paths

Cons:

- more complex generation
- assembly version management
- file lock/update issues
- harder debugging
- harder client-side maintenance

## 12. Recommendation

For your first strong implementation:

- do **not** generate DLL first
- generate a binary direct executor file first

Recommended main artifact:

- `compiled-plan.bin`

This is the best balance of:

- performance
- maintainability
- client-side deployment simplicity
- redesign friendliness

## 13. How Runtime Uses the Direct Executor

At runtime:

1. user selects report
2. app resolves active version
3. app opens `compiled-plan.bin`
4. app deserializes binary executor plan
5. app validates runtime parameters
6. app calls required providers
7. app receives raw structured data
8. app renders HTML
9. app previews or prints

That means runtime does **not** need to:

- re-read the full designer visually
- rediscover fields from the layout
- re-analyze all table mappings

## 14. Example Runtime Call

Conceptually:

```csharp
var result = executor.Run(
    reportCode: "SALE_INVOICE_STANDARD_A4",
    version: "v1",
    connectionString: connectionString,
    parameters: new Dictionary<string, object>
    {
        ["saleId"] = 101
    });
```

The executor loads:

- `compiled-plan.bin`

and returns:

- structured report data model

## 15. Binary Artifact Creation Strategy

The compile button should build the binary file from:

- selected datasets
- selected fields
- formulas
- bindings
- parameter schema

Recommended internal flow:

1. designer saves draft
2. compiler builds internal object graph
3. object graph is validated
4. object graph is serialized into compact binary format
5. file is saved as `compiled-plan.bin`

## 16. Binary Format Options

Possible formats:

- MessagePack
- protobuf
- custom binary serializer
- compressed binary JSON

Recommended first option:

- MessagePack

Reason:

- compact
- fast
- easy in .NET
- simpler than custom binary format

## 17. What About Redesign

If user redesigns the report:

1. update draft
2. press compile/publish again
3. create new version
4. regenerate new `compiled-plan.bin`
5. keep old version if rollback is needed

So the direct executor file is not permanent.
It is version-specific.

## 18. Recommended Naming

### 18.1 Folder-Based Naming

```text
Reports/Templates/SaleInvoice/STANDARD_A4/v1/compiled-plan.bin
```

This is best for clean version separation.

### 18.2 Optional Explicit File Naming

```text
SaleInvoice_STANDARD_A4_v1.compiled.bin
```

This is also acceptable if you want easier manual inspection.

## 19. Direct Answer

Your required model should be:

1. user opens designer
2. designer shows datasets and columns
3. user drags required fields into design
4. system tracks exactly which datasets and fields are needed
5. user presses compile
6. compiler generates:
   - `template.html`
   - `template.css`
   - `definition.json`
   - `compiled-plan.bin`
7. runtime uses `compiled-plan.bin` directly
8. runtime fetches only required data
9. HTML prints the final output

## 20. Final Recommendation

If your priority is a highly efficient direct executor, the best practical structure is:

- UI designer for dataset and field selection
- publish/compile step
- binary direct executor artifact
- provider-based runtime data loading
- HTML renderer

The direct executor file should be:

- `compiled-plan.bin`

It should be:

- binary
- efficient
- versioned
- not necessarily human-readable

That is the strongest design for your requirement.
