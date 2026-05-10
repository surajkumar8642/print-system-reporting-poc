# Report Versioning and Redesign Model

## 1. Goal

The reporting system must support:

- new report creation
- redesign of existing reports
- multiple templates for the same business entity
- draft and published versions
- safe switching between versions

The compiled-plan model must support all of this.

## 2. Core Concepts

### 2.1 Report Family

A business report group such as:

- `SaleInvoice`
- `StockSummary`
- `TaxSummary`

### 2.2 Template Variant

A layout type inside the family such as:

- `STANDARD_A4`
- `THERMAL_3INCH`
- `DETAIL_WITH_TAX`

### 2.3 Version

A published revision such as:

- `v1`
- `v2`
- `v3`

## 3. Lifecycle

### 3.1 Draft

User edits:

- fields
- layout
- formulas
- conditions

Draft is not used for live printing.

### 3.2 Publish

When published, the system:

1. validates the definition
2. regenerates compiled plan
3. stores a new version artifact
4. optionally marks that version active

### 3.3 Runtime

Runtime always uses:

- active template variant
- active published version

It does not use in-progress draft files.

## 4. Redesign Flow

When a customer wants changes:

1. open current version as editable draft
2. modify fields/layout/formulas
3. save draft
4. publish new version
5. generate new compiled plan
6. switch active version if approved

## 5. Why This Matters

Without versioning:

- one edit can break live printing
- rollback becomes difficult
- testing old vs new output becomes difficult

With versioning:

- live version remains safe
- redesign is controlled
- rollback is simple

## 6. Recommended Folder Shape

```text
Reports/
  Templates/
    SaleInvoice/
      STANDARD_A4/
        draft/
          template.html
          template.css
          definition.json
        v1/
          template.html
          template.css
          definition.json
          compiled-plan.json
        v2/
          template.html
          template.css
          definition.json
          compiled-plan.json
```

## 7. Active Version Mapping

Keep active version selection in catalog metadata.

Example:

```json
{
  "reportCode": "SALE_INVOICE_STANDARD_A4",
  "family": "SaleInvoice",
  "variant": "STANDARD_A4",
  "activeVersion": "v2"
}
```

## 8. Recompilation Rule

If any of these change:

- fields
- dataset selection
- formulas
- parameters
- bindings

then `compiled-plan.json` must be regenerated.

If only CSS or label styling changes, recompilation may be optional, but republish should still create a new version for consistency.

## 9. Final Rule

Compiled plans are not the report itself.
They are generated runtime artifacts for a specific published version.

If the report is redesigned, the compiled plan must also be replaced for that new version.
