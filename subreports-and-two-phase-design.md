# Subreports and Two-Phase Report Design

## 1. Why This Is Needed

Your reporting system is not only:

- one header
- one item table
- one total

It also needs to support:

- subreports
- report inside report
- child lists
- grouped lists
- sale and sale-return together
- multiple related datasets
- functions with hidden dependencies
- complex matching keys such as `SaleId`, `SaleDetailId`, `ReturnId`, `ProductId`, `BatchId`

So the designer should not start directly from HTML layout.

It should work in two clear phases.

## 2. Two-Phase Model

The correct design flow is:

### Phase 1: Data Request Design

In this phase, user defines:

- root entity
- required datasets
- required functions
- joins/match keys
- grouping context
- list vs single record sections
- subreports

### Phase 2: UI Design

In this phase, user only sees the approved data model created in Phase 1 and uses drag-drop to design layout.

This separation is very important.

## 3. Why Two Phases Are Better

If you mix everything together in one canvas:

- data dependencies become unclear
- joins become hidden
- subreports become difficult to manage
- runtime becomes slow
- user may drag fields without understanding data context

With two phases:

- data model is fixed first
- dependencies are expanded first
- layout is built on top of a stable data structure

This is the correct architecture for complex reporting.

## 4. Phase 1: Data Request Designer

This is a dedicated screen or step before the HTML designer.

It should allow the user to build the report data model.

### 4.1 Inputs

The user selects:

- report family
- root entity
- parameter schema
- datasets
- functions
- subreports
- list joins

### 4.2 UI Areas

Recommended panels:

1. root entity selector
2. dataset explorer
3. function explorer
4. selected datasets area
5. required fields area
6. join/mapping area
7. subreport area
8. generated model preview

## 5. Root Entity First

Everything should begin from a root entity.

Examples:

- `Sale`
- `Purchase`
- `Stock`
- `Ledger`
- `Tax`

If root entity is `Sale`, then the user can add related datasets such as:

- `SaleHeader`
- `SaleItems`
- `SaleTax`
- `SaleReturnHeader`
- `SaleReturnItems`
- `PartyInfo`

This gives structure to complexity.

## 6. Dataset Selection

The user chooses business datasets, not raw tables.

Example:

- `Sale Header`
- `Sale Items`
- `Sale Return Header`
- `Sale Return Items`

Internally, these map to real tables and joins.

## 7. Join / Match Model

This is where complexity must be controlled.

The user may need to connect:

- `SaleHeader.SaleId` -> `SaleItems.SaleId`
- `SaleItems.SaleDetailId` -> `SaleReturnItems.SaleDetailId`
- `SaleReturnHeader.ReturnId` -> `SaleReturnItems.ReturnId`

The system should support explicit relationship mapping.

## 8. Relationship Registry

Do not let every report build joins from scratch.

Instead create a central relationship registry.

Example:

```json
[
  {
    "name": "SaleHeader_To_SaleItems",
    "parentDataset": "SaleHeader",
    "childDataset": "SaleItems",
    "parentKey": "SaleId",
    "childKey": "SaleId",
    "cardinality": "OneToMany"
  },
  {
    "name": "SaleItems_To_SaleReturnItems",
    "parentDataset": "SaleItems",
    "childDataset": "SaleReturnItems",
    "parentKey": "SaleDetailId",
    "childKey": "SaleDetailId",
    "cardinality": "OneToMany"
  }
]
```

This gives safe controlled joins.

## 9. Subreport Concept

A subreport should be treated as a nested report block with its own:

- dataset scope
- parameters
- join condition
- layout

Examples:

- sale invoice with subreport for return history
- stock report with subreport for batch details
- ledger report with subreport for voucher lines

## 10. How Subreport Should Work

A subreport should have:

1. parent context
2. child dataset or child report
3. mapping keys
4. optional own template section

Example:

- parent report: `Sale Header`
- parent row key: `SaleId`
- subreport dataset: `SaleReturnItems`
- link condition: `SaleItems.SaleDetailId = SaleReturnItems.SaleDetailId`

At runtime:

- parent data loads first
- child data loads using compiled mapping
- renderer injects child block inside parent block

## 11. Subreport Types

Support at least these types:

### 11.1 Child List Subreport

Example:

- invoice line items
- tax rows
- batch rows

### 11.2 Linked Summary Subreport

Example:

- sale return summary inside sale invoice
- ledger opening/closing block

### 11.3 Independent Linked Report

Example:

- customer outstanding summary inside sale report
- stock movement chart inside product report

## 12. Data Request Output

After Phase 1 is complete, the system should generate a stable intermediate model.

Example output:

```json
{
  "rootEntity": "Sale",
  "parameters": ["saleId"],
  "datasets": [
    "SaleHeader",
    "SaleItems",
    "SaleReturnItems"
  ],
  "relationships": [
    {
      "parent": "SaleHeader",
      "child": "SaleItems",
      "parentKey": "SaleId",
      "childKey": "SaleId"
    },
    {
      "parent": "SaleItems",
      "child": "SaleReturnItems",
      "parentKey": "SaleDetailId",
      "childKey": "SaleDetailId"
    }
  ],
  "functions": [
    "SALE_GRAND_TOTAL",
    "RETURN_TOTAL_QTY"
  ],
  "subreports": [
    {
      "name": "SaleReturnHistory",
      "parentDataset": "SaleItems",
      "childDataset": "SaleReturnItems",
      "join": "SaleDetailId"
    }
  ]
}
```

This is the data contract that Phase 2 should use.

## 13. Phase 2: HTML Designer

Only after Phase 1 is complete should the UI designer open.

At that point, the designer should show:

- root fields
- list fields
- subreport blocks
- function outputs
- grouping nodes

The user should not need to rebuild joins there.

## 14. What User Drags in Phase 2

The user can drag:

- scalar fields
- list/table blocks
- subreport blocks
- function outputs
- grouped sections
- totals sections

Example:

- `saleHeader.CustomerName`
- `saleItems[]`
- `SaleReturnHistory[]`
- `computed.GrandTotal`

## 15. Compiler Responsibility

At compile time, the compiler must:

1. expand function dependencies
2. validate dataset relationships
3. validate subreport mappings
4. freeze join graph
5. generate compiled execution plan
6. generate UI binding metadata

This means runtime does not rediscover complex relationships.

## 16. Runtime Execution for Complex Reports

For complex reports:

1. load compiled plan
2. fetch root dataset
3. fetch child datasets
4. apply relationship mapping
5. apply functions
6. build nested report model
7. render HTML and subreport sections

## 17. Nested Output Model Example

```json
{
  "data": {
    "saleHeader": {
      "SaleId": 101,
      "CustomerName": "ABC Traders"
    },
    "saleItems": [
      {
        "SaleDetailId": 1001,
        "ProductName": "Item A",
        "Qty": 5,
        "returns": [
          {
            "ReturnId": 501,
            "ReturnQty": 1
          }
        ]
      }
    ]
  }
}
```

This is how subreport-like nested rendering should work in HTML.

## 18. Complexity Control Rule

Do not let users create unrestricted raw joins across the whole schema.

Instead:

- expose approved datasets
- expose approved relationships
- expose approved functions
- allow advanced mode only for admins if needed

This is how you support complexity without losing control.

## 19. Final Recommendation

Your designer should be built as:

### Step 1

Data Request Designer:

- choose root
- choose datasets
- choose functions
- choose subreports
- define approved joins

### Step 2

HTML Layout Designer:

- drag approved fields
- drag list blocks
- drag subreports
- drag computed values

### Step 3

Compile:

- generate template
- generate definition
- generate compiled runtime plan

This is the right structure for supporting:

- Crystal-like formula behavior
- subreports
- sale/sale-return linking
- nested list rendering
- complex customer-specific reports
