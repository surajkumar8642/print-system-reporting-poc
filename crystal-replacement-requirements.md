# Crystal Replacement Requirements

## 1. Objective

The future reporting system is not a simple print engine.

It is an alternative to Crystal Reports for your product.

So it must handle the core capabilities that Crystal currently gives you, while improving:

- performance
- maintainability
- client customization
- future server deployment

## 2. Mandatory Capabilities

The new system must support all of these areas.

### 2.1 Layout Design

- drag-drop report design
- labels
- text fields
- tables/grids
- headers
- footers
- page settings
- conditional visibility
- grouping
- totals blocks

### 2.2 Data Selection

- select required datasets
- select required columns
- select required parameters
- fixed root entity
- controlled joins

### 2.3 Functions / Formulas

- predefined business functions
- aggregate functions
- calculated expressions
- formatting functions
- dependency expansion

### 2.4 Subreports

- report inside report
- nested list block
- linked child datasets
- parent-child key mapping
- summary subreports

### 2.5 Runtime Execution

- compiled report plan
- fast parameter-based execution
- only required dataset loading
- standard report model output

### 2.6 Versioning

- draft
- publish
- redesign
- republish
- rollback
- multiple templates per business entity

### 2.7 Output

- HTML preview
- browser print
- PDF export
- local PC mode
- future server mode

## 3. Required Alternative Structure

The correct replacement architecture is:

1. dataset registry
2. relationship registry
3. function registry
4. two-phase designer
5. HTML layout designer
6. compiled runtime executor
7. versioned template storage

## 4. Two-Phase Designer Is Mandatory

If this system is supposed to replace Crystal properly, then the designer must be split into:

### 4.1 Data Request Phase

Here the user chooses:

- root entity
- datasets
- fields
- functions
- subreports
- relationships

### 4.2 UI Design Phase

Here the user designs:

- header
- body
- table layouts
- nested sections
- totals blocks
- print arrangement

This is required for handling complexity safely.

## 5. Crystal Parity Areas

The replacement must handle these Crystal-like features:

- formulas
- functions
- subreports
- grouping
- summary fields
- conditional formatting
- repeated detail sections
- multi-table data mapping
- customer-specific layouts

## 6. Performance Requirements

The replacement must be better than current Crystal flow in these ways:

- do not prepare unnecessary tables
- do not pass giant datasets to the layout engine
- do not rediscover dependencies every print
- compile dependencies once
- execute only required providers

## 7. Recommended Runtime Artifact

For strong runtime performance, the report should generate:

- `compiled-plan.bin`

Optional support file:

- `compiled-plan.debug.json`

This is better than direct DLL generation in the first phase.

## 8. Complexity Handling Rule

Because customers can ask for many report types, the system must not depend on:

- unrestricted ad hoc SQL by end user
- direct whole-schema table browsing for everyone

Instead it must depend on:

- approved datasets
- approved relationships
- approved functions
- compiled plans

## 9. Phased Build Recommendation

### Phase 1

Build parity foundation:

- dataset registry
- function registry
- relationship registry
- one sample executor
- one sample HTML preview

### Phase 2

Build designer:

- data request phase
- layout phase
- compile/publish flow

### Phase 3

Add advanced parity:

- subreports
- nested lists
- conditional sections
- function dependency graph

### Phase 4

Add enterprise polish:

- binary compiled plan
- caching
- audit/versioning UI
- template sharing/import/export

## 10. Final Requirement Statement

If this project is meant to become a real alternative to Crystal Reports, then the system must fully support:

- data-driven report definition
- function-driven derived fields
- subreports
- nested linked datasets
- drag-drop layout design
- compiled fast runtime execution
- redesign and republish
- client-side and server-side deployment

Anything less than this would only be a partial print module, not a true Crystal alternative.
