IF DB_ID('ReportPocDemo') IS NULL
BEGIN
    CREATE DATABASE ReportPocDemo;
END
GO

USE ReportPocDemo;

IF OBJECT_ID('dbo.TaxBreakup', 'U') IS NOT NULL
    DROP TABLE dbo.TaxBreakup;

IF OBJECT_ID('dbo.SaleItems', 'U') IS NOT NULL
    DROP TABLE dbo.SaleItems;

IF OBJECT_ID('dbo.SaleHeader', 'U') IS NOT NULL
    DROP TABLE dbo.SaleHeader;

CREATE TABLE dbo.SaleHeader
(
    SaleId INT NOT NULL CONSTRAINT PK_SaleHeader PRIMARY KEY,
    SaleNo NVARCHAR(50) NOT NULL,
    SaleDate DATE NOT NULL,
    CustomerName NVARCHAR(200) NOT NULL,
    BillingAddress NVARCHAR(300) NULL,
    NetAmount DECIMAL(18, 2) NOT NULL,
    TotalTax DECIMAL(18, 2) NOT NULL
);

CREATE TABLE dbo.SaleItems
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SaleItems PRIMARY KEY,
    SaleId INT NOT NULL,
    SaleDetailId INT NOT NULL,
    ProductName NVARCHAR(200) NOT NULL,
    Qty DECIMAL(18,2) NOT NULL,
    Rate DECIMAL(18,2) NOT NULL,
    MRP DECIMAL(18,2) NOT NULL,
    LineTotal DECIMAL(18,2) NOT NULL
);

CREATE TABLE dbo.TaxBreakup
(
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TaxBreakup PRIMARY KEY,
    SaleId INT NOT NULL,
    TaxName NVARCHAR(50) NOT NULL,
    TaxRate DECIMAL(18,2) NOT NULL,
    TaxAmount DECIMAL(18,2) NOT NULL
);

INSERT dbo.SaleHeader (SaleId, SaleNo, SaleDate, CustomerName, BillingAddress, NetAmount, TotalTax)
VALUES
(101, N'S-101', '2026-05-10', N'ABC Traders', N'Market Road, Surat', 2000.00, 360.00),
(102, N'S-102', '2026-05-09', N'Knit Store', N'Mansarovar, Ahmedabad', 500.00, 90.00);

INSERT dbo.SaleItems (SaleId, SaleDetailId, ProductName, Qty, Rate, MRP, LineTotal)
VALUES
(101, 201, N'Item A', 2, 100.00, 150.00, 200.00),
(101, 202, N'Item B', 3, 600.00, 700.00, 1800.00),
(102, 203, N'Gadget X', 1, 500.00, 550.00, 500.00);

INSERT dbo.TaxBreakup (SaleId, TaxName, TaxRate, TaxAmount)
VALUES
(101, N'CGST', 9.00, 180.00),
(101, N'SGST', 9.00, 180.00),
(102, N'VAT', 18.00, 90.00);
