# Function Registry and Derived Fields Model

## 1. Problem

Many reports do not use only direct columns such as:

- `SaleNo`
- `CustomerName`
- `Qty`
- `MRP`

They also use derived values such as:

- total amount
- total rate
- taxable amount
- discount amount
- stock valuation
- amount in words
- gross total
- round off

In Crystal Reports, these are often handled through formulas/functions.

Your concern is correct:

when the user drags a function into the design, the system must automatically know:

- which datasets are needed
- which fields are needed
- which child datasets are needed
- which dependencies are indirect

The user should not manually discover all of that.

## 2. Correct Model

The reporting designer should support three kinds of items:

1. direct fields
2. dataset blocks
3. registered functions

A function is not just text.
It is a pre-defined executable metadata object.

## 3. Function Registry

Create a central `Function Registry`.

This registry stores 100 to 200 predefined functions if needed.

Each function definition should contain:

- function code
- display name
- category
- return type
- root entity
- required datasets
- required fields
- optional parameters
- expression or execution logic
- formatting rules

## 4. Example Function Definition

```json
{
  "code": "SALE_GRAND_TOTAL",
  "name": "Sale Grand Total",
  "category": "Sales Totals",
  "rootEntity": "Sale",
  "returnType": "decimal",
  "requiredDatasets": ["SaleHeader"],
  "requiredFields": ["NetAmount", "TotalTax"],
  "expression": "saleHeader.NetAmount + saleHeader.TotalTax",
  "format": "N2"
}
```

## 5. Example with Indirect Dependencies

```json
{
  "code": "SALE_ITEM_TOTAL_QTY",
  "name": "Sale Item Total Quantity",
  "category": "Sales Totals",
  "rootEntity": "Sale",
  "returnType": "decimal",
  "requiredDatasets": ["SaleItems"],
  "requiredFields": ["Qty"],
  "expression": "sum(saleItems.Qty)"
}
```

If the user drags this function:

- `SaleItems` must be included automatically
- field `Qty` must be included automatically

The user does not need to know that internally.

## 6. Example of a Complex Business Function

```json
{
  "code": "STOCK_CLOSING_VALUE",
  "name": "Stock Closing Value",
  "category": "Stock",
  "rootEntity": "Stock",
  "returnType": "decimal",
  "requiredDatasets": ["StockBatchDetail"],
  "requiredFields": ["ClosingQty", "Rate"],
  "expression": "sum(stockBatchDetail.ClosingQty * stockBatchDetail.Rate)"
}
```

This function automatically pulls:

- dataset `StockBatchDetail`
- fields `ClosingQty` and `Rate`

## 7. Designer Behavior

When the user drags a function:

1. designer reads function metadata
2. designer adds the function to the layout
3. designer auto-adds required datasets
4. designer auto-adds required fields
5. designer marks those dependencies as system-required
6. compile step includes them even if user never dragged raw fields manually

This is the correct behavior.

## 8. Required Area Model

The designer should have two concepts:

### 8.1 User-Selected Fields

Fields the user directly dragged.

### 8.2 System-Required Fields

Fields automatically included because:

- a function depends on them
- a grouping depends on them
- a sort depends on them
- a visibility rule depends on them

This means the required area should show both:

- direct requirements
- indirect requirements

## 9. Dependency Expansion

When a function is selected, the system should expand dependencies recursively.

Example:

Function:

- `NET_AFTER_DISCOUNT_AND_TAX`

Depends on:

- `GROSS_AMOUNT`
- `DISCOUNT_AMOUNT`
- `TAX_AMOUNT`

And those may depend on:

- `SaleItems.Qty`
- `SaleItems.Rate`
- `SaleItems.Discount`
- `TaxBreakup.TaxAmount`

The compiler should resolve all of this automatically.

## 10. Function Types

The system should support several function classes.

### 10.1 Field Aggregate Functions

Examples:

- `sum(Qty)`
- `avg(Rate)`
- `max(MRP)`

### 10.2 Header Calculation Functions

Examples:

- `NetAmount`
- `GrandTotal`
- `RoundOff`

### 10.3 Conditional Functions

Examples:

- `if(TotalTax > 0, 'Tax Invoice', 'Bill')`

### 10.4 Formatting Functions

Examples:

- amount in words
- date formatting
- decimal formatting

### 10.5 Business Logic Functions

Examples:

- stock closing value
- pending balance
- taxable value
- margin percent

### 10.6 Lookup Functions

Examples:

- customer full address
- product display name
- GST label by tax type

## 11. Implementation Strategy

Do not let every function be handwritten in report layout text.

Instead build a structured function registry with:

- metadata
- dependency declarations
- execution rules

Recommended internal models:

1. simple expression-based functions
2. provider-backed functions
3. composite functions

## 12. Expression-Based Functions

These are simple formulas based on already available fields.

Example:

- `saleHeader.NetAmount + saleHeader.TotalTax`

These are the easiest.

## 13. Provider-Backed Functions

Some functions may require extra data retrieval.

Example:

- `GetPreviousBalance`
- `GetOpeningStock`
- `GetBatchWiseValue`

These should declare:

- required provider
- required parameter
- expected return type

So dragging that function can also auto-include provider dependency.

## 14. Composite Functions

Some functions depend on other functions.

Example:

- `FinalPayableAmount`

depends on:

- `GrossAmount`
- `DiscountTotal`
- `TaxTotal`
- `RoundOff`

The compiler should resolve these dependencies like a graph.

## 15. Function Compilation

At compile time:

1. collect direct fields
2. collect selected functions
3. expand function dependencies
4. collect all required datasets and fields
5. resolve provider dependencies
6. build final compiled plan

This ensures the runtime executor already knows all dependencies.

## 16. Runtime Behavior

At runtime:

- user does not re-pick dependencies
- runtime does not rediscover formulas from scratch
- compiled plan already contains expanded dependency graph

That means runtime stays fast even with complex functions.

## 17. Example Compiled Plan Section

```json
{
  "functions": [
    {
      "code": "SALE_GRAND_TOTAL",
      "requiredDatasets": ["SaleHeader"],
      "requiredFields": ["NetAmount", "TotalTax"],
      "expression": "saleHeader.NetAmount + saleHeader.TotalTax"
    },
    {
      "code": "SALE_ITEM_TOTAL_QTY",
      "requiredDatasets": ["SaleItems"],
      "requiredFields": ["Qty"],
      "expression": "sum(saleItems.Qty)"
    }
  ]
}
```

## 18. UI Recommendation

The designer should show a separate `Functions` panel with categories:

- Sales
- Purchase
- Stock
- Tax
- Ledger
- Formatting
- Totals

When a function is dragged:

- the UI should visibly indicate auto-added dependencies
- optionally show a small dependency preview

Example:

`Sale Grand Total`
Uses:
- `SaleHeader.NetAmount`
- `SaleHeader.TotalTax`

## 19. Best Rule

Do not force users to manually understand internal function data requirements.

The system should do that automatically through metadata.

This is one of the biggest advantages of a modern designer over old report engines.

## 20. Final Recommendation

Your designer should include:

- field registry
- dataset registry
- function registry
- dependency resolver
- compile-time dependency expansion

This will allow:

- 100 to 200 predefined business functions
- drag-drop function usage
- automatic required data collection
- fast compiled runtime execution

That is the correct architecture for supporting Crystal-like function power in a cleaner modern reporting platform.
