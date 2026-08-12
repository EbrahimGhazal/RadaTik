-- Company business module
IF OBJECT_ID(N'dbo.WarehouseItems', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[WarehouseItems] (
    [Id] int NOT NULL IDENTITY,
    [CompanyNetworkId] int NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [Unit] nvarchar(40) NULL,
    [Sku] nvarchar(60) NULL,
    [ModelNumber] nvarchar(60) NULL,
    [PurchasePrice] decimal(18,2) NULL,
    [WholesalePrice] decimal(18,2) NULL,
    [RetailPrice] decimal(18,2) NULL,
    [Notes] nvarchar(500) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WarehouseItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseItems_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION
);
END
GO
IF OBJECT_ID(N'dbo.MaterialPurchaseInvoices', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[MaterialPurchaseInvoices] (
    [Id] int NOT NULL IDENTITY,
    [CompanyNetworkId] int NOT NULL,
    [InvoiceDate] datetime2 NOT NULL,
    [SupplierName] nvarchar(120) NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [IsPaid] bit NOT NULL,
    [PaidAt] datetime2 NULL,
    [IsCancelled] bit NOT NULL,
    [CancelledAt] datetime2 NULL,
    [WalletTransactionId] int NULL,
    [MoneyDiaryEntryId] int NULL,
    [CashBoxWithdrawalId] int NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedByUserId] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_MaterialPurchaseInvoices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaterialPurchaseInvoices_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_MaterialPurchaseInvoices_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION
);
END
GO
IF OBJECT_ID(N'dbo.MaterialSalesInvoices', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[MaterialSalesInvoices] (
    [Id] int NOT NULL IDENTITY,
    [CompanyNetworkId] int NOT NULL,
    [InvoiceDate] datetime2 NOT NULL,
    [CustomerName] nvarchar(120) NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [IsPaid] bit NOT NULL,
    [PaidAt] datetime2 NULL,
    [IsCancelled] bit NOT NULL,
    [CancelledAt] datetime2 NULL,
    [WalletTransactionId] int NULL,
    [MoneyDiaryEntryId] int NULL,
    [CashBoxDepositId] int NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedByUserId] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_MaterialSalesInvoices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaterialSalesInvoices_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_MaterialSalesInvoices_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION
);
END
GO
IF OBJECT_ID(N'dbo.PayrollEmployees', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[PayrollEmployees] (
    [Id] int NOT NULL IDENTITY,
    [CompanyNetworkId] int NOT NULL,
    [ApplicationUserId] nvarchar(450) NULL,
    [FullName] nvarchar(120) NOT NULL,
    [JobTitle] nvarchar(80) NULL,
    [Phone] nvarchar(30) NULL,
    [EmploymentType] int NOT NULL,
    [WeeklyWorkHours] decimal(8,2) NOT NULL,
    [MonthlySalary] decimal(18,2) NOT NULL,
    [HireDate] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayrollEmployees] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollEmployees_AspNetUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_PayrollEmployees_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION
);
END
GO
IF OBJECT_ID(N'dbo.MoneyDiaryEntries', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[MoneyDiaryEntries] (
    [Id] int NOT NULL IDENTITY,
    [CompanyNetworkId] int NOT NULL,
    [EntryType] int NOT NULL,
    [CategoryKey] nvarchar(64) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [EntryDate] datetime2 NOT NULL,
    [Description] nvarchar(500) NULL,
    [CreatedByUserId] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [MaterialPurchaseInvoiceId] int NULL,
    [MaterialSalesInvoiceId] int NULL,
    [PayrollPaymentId] int NULL,
    CONSTRAINT [PK_MoneyDiaryEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MoneyDiaryEntries_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_MoneyDiaryEntries_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION
);
END
GO
IF OBJECT_ID(N'dbo.PayrollPayments', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[PayrollPayments] (
    [Id] int NOT NULL IDENTITY,
    [CompanyNetworkId] int NOT NULL,
    [PayrollEmployeeId] int NOT NULL,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [BaseAmount] decimal(18,2) NOT NULL,
    [Bonus] decimal(18,2) NOT NULL,
    [Deduction] decimal(18,2) NOT NULL,
    [Notes] nvarchar(500) NULL,
    [IsPaid] bit NOT NULL,
    [PaidAt] datetime2 NULL,
    [CreatedByUserId] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayrollPayments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollPayments_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_PayrollPayments_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PayrollPayments_PayrollEmployees_PayrollEmployeeId] FOREIGN KEY ([PayrollEmployeeId]) REFERENCES [PayrollEmployees] ([Id]) ON DELETE CASCADE
);
END
GO
IF OBJECT_ID(N'dbo.PayrollSalaryRevisions', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[PayrollSalaryRevisions] (
    [Id] int NOT NULL IDENTITY,
    [CompanyNetworkId] int NOT NULL,
    [PayrollEmployeeId] int NOT NULL,
    [PreviousSalary] decimal(18,2) NOT NULL,
    [NewSalary] decimal(18,2) NOT NULL,
    [AdjustmentType] int NOT NULL,
    [AdjustmentValue] decimal(18,4) NOT NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedByUserId] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayrollSalaryRevisions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollSalaryRevisions_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_PayrollSalaryRevisions_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PayrollSalaryRevisions_PayrollEmployees_PayrollEmployeeId] FOREIGN KEY ([PayrollEmployeeId]) REFERENCES [PayrollEmployees] ([Id]) ON DELETE CASCADE
);
END
GO
IF OBJECT_ID(N'dbo.PayrollTransactions', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[PayrollTransactions] (
    [Id] int NOT NULL IDENTITY,
    [CompanyNetworkId] int NOT NULL,
    [PayrollEmployeeId] int NOT NULL,
    [Type] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [TransactionDate] datetime2 NOT NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedByUserId] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayrollTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollTransactions_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_PayrollTransactions_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PayrollTransactions_PayrollEmployees_PayrollEmployeeId] FOREIGN KEY ([PayrollEmployeeId]) REFERENCES [PayrollEmployees] ([Id]) ON DELETE CASCADE
);
END
GO
IF OBJECT_ID(N'dbo.MaterialPurchaseInvoiceLines', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[MaterialPurchaseInvoiceLines] (
    [Id] int NOT NULL IDENTITY,
    [MaterialPurchaseInvoiceId] int NOT NULL,
    [WarehouseItemId] int NULL,
    [ItemName] nvarchar(120) NOT NULL,
    [ModelNumber] nvarchar(60) NULL,
    [PackageUnit] int NOT NULL,
    [UnitsPerPackage] int NOT NULL,
    [PackageQuantity] decimal(18,3) NOT NULL,
    [BaseQuantity] decimal(18,3) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    [WholesalePrice] decimal(18,2) NULL,
    [RetailPrice] decimal(18,2) NULL,
    CONSTRAINT [PK_MaterialPurchaseInvoiceLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaterialPurchaseInvoiceLines_MaterialPurchaseInvoices_MaterialPurchaseInvoiceId] FOREIGN KEY ([MaterialPurchaseInvoiceId]) REFERENCES [MaterialPurchaseInvoices] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaterialPurchaseInvoiceLines_WarehouseItems_WarehouseItemId] FOREIGN KEY ([WarehouseItemId]) REFERENCES [WarehouseItems] ([Id]) ON DELETE SET NULL
);
END
GO
IF OBJECT_ID(N'dbo.MaterialSalesInvoiceLines', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[MaterialSalesInvoiceLines] (
    [Id] int NOT NULL IDENTITY,
    [MaterialSalesInvoiceId] int NOT NULL,
    [WarehouseItemId] int NOT NULL,
    [PriceMode] int NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_MaterialSalesInvoiceLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MaterialSalesInvoiceLines_MaterialSalesInvoices_MaterialSalesInvoiceId] FOREIGN KEY ([MaterialSalesInvoiceId]) REFERENCES [MaterialSalesInvoices] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MaterialSalesInvoiceLines_WarehouseItems_WarehouseItemId] FOREIGN KEY ([WarehouseItemId]) REFERENCES [WarehouseItems] ([Id]) ON DELETE NO ACTION
);
END
GO
IF OBJECT_ID(N'dbo.WarehouseStocktakes', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[WarehouseStocktakes] (
    [Id] int NOT NULL IDENTITY,
    [CompanyNetworkId] int NOT NULL,
    [StocktakeDate] datetime2 NOT NULL,
    [PeriodFrom] datetime2 NULL,
    [PeriodTo] datetime2 NULL,
    [WarehouseItemId] int NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedByUserId] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WarehouseStocktakes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseStocktakes_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_WarehouseStocktakes_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseStocktakes_WarehouseItems_WarehouseItemId] FOREIGN KEY ([WarehouseItemId]) REFERENCES [WarehouseItems] ([Id]) ON DELETE SET NULL
);
END
GO
IF OBJECT_ID(N'dbo.WarehouseMovements', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[WarehouseMovements] (
    [Id] int NOT NULL IDENTITY,
    [CompanyNetworkId] int NOT NULL,
    [WarehouseItemId] int NOT NULL,
    [MovementType] int NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [MovementDate] datetime2 NOT NULL,
    [Notes] nvarchar(500) NULL,
    [MaterialPurchaseInvoiceId] int NULL,
    [MaterialSalesInvoiceId] int NULL,
    [WarehouseStocktakeId] int NULL,
    [CreatedByUserId] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WarehouseMovements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseMovements_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_WarehouseMovements_MaterialPurchaseInvoices_MaterialPurchaseInvoiceId] FOREIGN KEY ([MaterialPurchaseInvoiceId]) REFERENCES [MaterialPurchaseInvoices] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseMovements_MaterialSalesInvoices_MaterialSalesInvoiceId] FOREIGN KEY ([MaterialSalesInvoiceId]) REFERENCES [MaterialSalesInvoices] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseMovements_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseMovements_WarehouseItems_WarehouseItemId] FOREIGN KEY ([WarehouseItemId]) REFERENCES [WarehouseItems] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WarehouseMovements_WarehouseStocktakes_WarehouseStocktakeId] FOREIGN KEY ([WarehouseStocktakeId]) REFERENCES [WarehouseStocktakes] ([Id]) ON DELETE NO ACTION
);
END
GO
IF OBJECT_ID(N'dbo.WarehouseStocktakeLines', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[WarehouseStocktakeLines] (
    [Id] int NOT NULL IDENTITY,
    [WarehouseStocktakeId] int NOT NULL,
    [WarehouseItemId] int NOT NULL,
    [SystemQuantity] decimal(18,3) NOT NULL,
    [CountedQuantity] decimal(18,3) NOT NULL,
    [Difference] decimal(18,3) NOT NULL,
    CONSTRAINT [PK_WarehouseStocktakeLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WarehouseStocktakeLines_WarehouseItems_WarehouseItemId] FOREIGN KEY ([WarehouseItemId]) REFERENCES [WarehouseItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseStocktakeLines_WarehouseStocktakes_WarehouseStocktakeId] FOREIGN KEY ([WarehouseStocktakeId]) REFERENCES [WarehouseStocktakes] ([Id]) ON DELETE CASCADE
);
END
GO
IF OBJECT_ID(N'dbo.MaterialPurchaseInvoiceLines', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialPurchaseInvoiceLines_MaterialPurchaseInvoiceId' AND object_id = OBJECT_ID(N'dbo.MaterialPurchaseInvoiceLines'))
CREATE INDEX [IX_MaterialPurchaseInvoiceLines_MaterialPurchaseInvoiceId] ON [dbo].[MaterialPurchaseInvoiceLines] ([MaterialPurchaseInvoiceId]);
GO
IF OBJECT_ID(N'dbo.MaterialPurchaseInvoiceLines', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialPurchaseInvoiceLines_WarehouseItemId' AND object_id = OBJECT_ID(N'dbo.MaterialPurchaseInvoiceLines'))
CREATE INDEX [IX_MaterialPurchaseInvoiceLines_WarehouseItemId] ON [dbo].[MaterialPurchaseInvoiceLines] ([WarehouseItemId]);
GO
IF OBJECT_ID(N'dbo.MaterialPurchaseInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialPurchaseInvoices_CashBoxWithdrawalId' AND object_id = OBJECT_ID(N'dbo.MaterialPurchaseInvoices'))
CREATE INDEX [IX_MaterialPurchaseInvoices_CashBoxWithdrawalId] ON [dbo].[MaterialPurchaseInvoices] ([CashBoxWithdrawalId]);
GO
IF OBJECT_ID(N'dbo.MaterialPurchaseInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialPurchaseInvoices_CompanyNetworkId' AND object_id = OBJECT_ID(N'dbo.MaterialPurchaseInvoices'))
CREATE INDEX [IX_MaterialPurchaseInvoices_CompanyNetworkId] ON [dbo].[MaterialPurchaseInvoices] ([CompanyNetworkId]);
GO
IF OBJECT_ID(N'dbo.MaterialPurchaseInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialPurchaseInvoices_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.MaterialPurchaseInvoices'))
CREATE INDEX [IX_MaterialPurchaseInvoices_CreatedByUserId] ON [dbo].[MaterialPurchaseInvoices] ([CreatedByUserId]);
GO
IF OBJECT_ID(N'dbo.MaterialPurchaseInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialPurchaseInvoices_InvoiceDate' AND object_id = OBJECT_ID(N'dbo.MaterialPurchaseInvoices'))
CREATE INDEX [IX_MaterialPurchaseInvoices_InvoiceDate] ON [dbo].[MaterialPurchaseInvoices] ([InvoiceDate]);
GO
IF OBJECT_ID(N'dbo.MaterialPurchaseInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialPurchaseInvoices_MoneyDiaryEntryId' AND object_id = OBJECT_ID(N'dbo.MaterialPurchaseInvoices'))
CREATE INDEX [IX_MaterialPurchaseInvoices_MoneyDiaryEntryId] ON [dbo].[MaterialPurchaseInvoices] ([MoneyDiaryEntryId]);
GO
IF OBJECT_ID(N'dbo.MaterialSalesInvoiceLines', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialSalesInvoiceLines_MaterialSalesInvoiceId' AND object_id = OBJECT_ID(N'dbo.MaterialSalesInvoiceLines'))
CREATE INDEX [IX_MaterialSalesInvoiceLines_MaterialSalesInvoiceId] ON [dbo].[MaterialSalesInvoiceLines] ([MaterialSalesInvoiceId]);
GO
IF OBJECT_ID(N'dbo.MaterialSalesInvoiceLines', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialSalesInvoiceLines_WarehouseItemId' AND object_id = OBJECT_ID(N'dbo.MaterialSalesInvoiceLines'))
CREATE INDEX [IX_MaterialSalesInvoiceLines_WarehouseItemId] ON [dbo].[MaterialSalesInvoiceLines] ([WarehouseItemId]);
GO
IF OBJECT_ID(N'dbo.MaterialSalesInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialSalesInvoices_CashBoxDepositId' AND object_id = OBJECT_ID(N'dbo.MaterialSalesInvoices'))
CREATE INDEX [IX_MaterialSalesInvoices_CashBoxDepositId] ON [dbo].[MaterialSalesInvoices] ([CashBoxDepositId]);
GO
IF OBJECT_ID(N'dbo.MaterialSalesInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialSalesInvoices_CompanyNetworkId' AND object_id = OBJECT_ID(N'dbo.MaterialSalesInvoices'))
CREATE INDEX [IX_MaterialSalesInvoices_CompanyNetworkId] ON [dbo].[MaterialSalesInvoices] ([CompanyNetworkId]);
GO
IF OBJECT_ID(N'dbo.MaterialSalesInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialSalesInvoices_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.MaterialSalesInvoices'))
CREATE INDEX [IX_MaterialSalesInvoices_CreatedByUserId] ON [dbo].[MaterialSalesInvoices] ([CreatedByUserId]);
GO
IF OBJECT_ID(N'dbo.MaterialSalesInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialSalesInvoices_InvoiceDate' AND object_id = OBJECT_ID(N'dbo.MaterialSalesInvoices'))
CREATE INDEX [IX_MaterialSalesInvoices_InvoiceDate] ON [dbo].[MaterialSalesInvoices] ([InvoiceDate]);
GO
IF OBJECT_ID(N'dbo.MaterialSalesInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaterialSalesInvoices_MoneyDiaryEntryId' AND object_id = OBJECT_ID(N'dbo.MaterialSalesInvoices'))
CREATE INDEX [IX_MaterialSalesInvoices_MoneyDiaryEntryId] ON [dbo].[MaterialSalesInvoices] ([MoneyDiaryEntryId]);
GO
IF OBJECT_ID(N'dbo.MoneyDiaryEntries', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MoneyDiaryEntries_CompanyNetworkId' AND object_id = OBJECT_ID(N'dbo.MoneyDiaryEntries'))
CREATE INDEX [IX_MoneyDiaryEntries_CompanyNetworkId] ON [dbo].[MoneyDiaryEntries] ([CompanyNetworkId]);
GO
IF OBJECT_ID(N'dbo.MoneyDiaryEntries', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MoneyDiaryEntries_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.MoneyDiaryEntries'))
CREATE INDEX [IX_MoneyDiaryEntries_CreatedByUserId] ON [dbo].[MoneyDiaryEntries] ([CreatedByUserId]);
GO
IF OBJECT_ID(N'dbo.MoneyDiaryEntries', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MoneyDiaryEntries_EntryDate' AND object_id = OBJECT_ID(N'dbo.MoneyDiaryEntries'))
CREATE INDEX [IX_MoneyDiaryEntries_EntryDate] ON [dbo].[MoneyDiaryEntries] ([EntryDate]);
GO
IF OBJECT_ID(N'dbo.MoneyDiaryEntries', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MoneyDiaryEntries_MaterialPurchaseInvoiceId' AND object_id = OBJECT_ID(N'dbo.MoneyDiaryEntries'))
CREATE INDEX [IX_MoneyDiaryEntries_MaterialPurchaseInvoiceId] ON [dbo].[MoneyDiaryEntries] ([MaterialPurchaseInvoiceId]);
GO
IF OBJECT_ID(N'dbo.MoneyDiaryEntries', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MoneyDiaryEntries_MaterialSalesInvoiceId' AND object_id = OBJECT_ID(N'dbo.MoneyDiaryEntries'))
CREATE INDEX [IX_MoneyDiaryEntries_MaterialSalesInvoiceId] ON [dbo].[MoneyDiaryEntries] ([MaterialSalesInvoiceId]);
GO
IF OBJECT_ID(N'dbo.PayrollEmployees', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollEmployees_ApplicationUserId' AND object_id = OBJECT_ID(N'dbo.PayrollEmployees'))
CREATE INDEX [IX_PayrollEmployees_ApplicationUserId] ON [dbo].[PayrollEmployees] ([ApplicationUserId]);
GO
IF OBJECT_ID(N'dbo.PayrollEmployees', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollEmployees_CompanyNetworkId' AND object_id = OBJECT_ID(N'dbo.PayrollEmployees'))
CREATE INDEX [IX_PayrollEmployees_CompanyNetworkId] ON [dbo].[PayrollEmployees] ([CompanyNetworkId]);
GO
IF OBJECT_ID(N'dbo.PayrollPayments', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollPayments_CompanyNetworkId' AND object_id = OBJECT_ID(N'dbo.PayrollPayments'))
CREATE INDEX [IX_PayrollPayments_CompanyNetworkId] ON [dbo].[PayrollPayments] ([CompanyNetworkId]);
GO
IF OBJECT_ID(N'dbo.PayrollPayments', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollPayments_CompanyNetworkId_Year_Month' AND object_id = OBJECT_ID(N'dbo.PayrollPayments'))
CREATE INDEX [IX_PayrollPayments_CompanyNetworkId_Year_Month] ON [dbo].[PayrollPayments] ([CompanyNetworkId], [Year], [Month]);
GO
IF OBJECT_ID(N'dbo.PayrollPayments', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollPayments_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.PayrollPayments'))
CREATE INDEX [IX_PayrollPayments_CreatedByUserId] ON [dbo].[PayrollPayments] ([CreatedByUserId]);
GO
IF OBJECT_ID(N'dbo.PayrollPayments', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollPayments_PayrollEmployeeId_Year_Month' AND object_id = OBJECT_ID(N'dbo.PayrollPayments'))
CREATE UNIQUE INDEX [IX_PayrollPayments_PayrollEmployeeId_Year_Month] ON [dbo].[PayrollPayments] ([PayrollEmployeeId], [Year], [Month]);
GO
IF OBJECT_ID(N'dbo.PayrollSalaryRevisions', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollSalaryRevisions_CompanyNetworkId' AND object_id = OBJECT_ID(N'dbo.PayrollSalaryRevisions'))
CREATE INDEX [IX_PayrollSalaryRevisions_CompanyNetworkId] ON [dbo].[PayrollSalaryRevisions] ([CompanyNetworkId]);
GO
IF OBJECT_ID(N'dbo.PayrollSalaryRevisions', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollSalaryRevisions_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.PayrollSalaryRevisions'))
CREATE INDEX [IX_PayrollSalaryRevisions_CreatedByUserId] ON [dbo].[PayrollSalaryRevisions] ([CreatedByUserId]);
GO
IF OBJECT_ID(N'dbo.PayrollSalaryRevisions', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollSalaryRevisions_PayrollEmployeeId' AND object_id = OBJECT_ID(N'dbo.PayrollSalaryRevisions'))
CREATE INDEX [IX_PayrollSalaryRevisions_PayrollEmployeeId] ON [dbo].[PayrollSalaryRevisions] ([PayrollEmployeeId]);
GO
IF OBJECT_ID(N'dbo.PayrollTransactions', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollTransactions_CompanyNetworkId' AND object_id = OBJECT_ID(N'dbo.PayrollTransactions'))
CREATE INDEX [IX_PayrollTransactions_CompanyNetworkId] ON [dbo].[PayrollTransactions] ([CompanyNetworkId]);
GO
IF OBJECT_ID(N'dbo.PayrollTransactions', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollTransactions_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.PayrollTransactions'))
CREATE INDEX [IX_PayrollTransactions_CreatedByUserId] ON [dbo].[PayrollTransactions] ([CreatedByUserId]);
GO
IF OBJECT_ID(N'dbo.PayrollTransactions', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollTransactions_PayrollEmployeeId_Year_Month' AND object_id = OBJECT_ID(N'dbo.PayrollTransactions'))
CREATE INDEX [IX_PayrollTransactions_PayrollEmployeeId_Year_Month] ON [dbo].[PayrollTransactions] ([PayrollEmployeeId], [Year], [Month]);
GO
IF OBJECT_ID(N'dbo.WarehouseItems', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseItems_CompanyNetworkId' AND object_id = OBJECT_ID(N'dbo.WarehouseItems'))
CREATE INDEX [IX_WarehouseItems_CompanyNetworkId] ON [dbo].[WarehouseItems] ([CompanyNetworkId]);
GO
IF OBJECT_ID(N'dbo.WarehouseItems', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseItems_CompanyNetworkId_Name' AND object_id = OBJECT_ID(N'dbo.WarehouseItems'))
CREATE INDEX [IX_WarehouseItems_CompanyNetworkId_Name] ON [dbo].[WarehouseItems] ([CompanyNetworkId], [Name]);
GO
IF OBJECT_ID(N'dbo.WarehouseItems', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseItems_CompanyNetworkId_Name_ModelNumber' AND object_id = OBJECT_ID(N'dbo.WarehouseItems'))
CREATE INDEX [IX_WarehouseItems_CompanyNetworkId_Name_ModelNumber] ON [dbo].[WarehouseItems] ([CompanyNetworkId], [Name], [ModelNumber]);
GO
IF OBJECT_ID(N'dbo.WarehouseMovements', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseMovements_CompanyNetworkId' AND object_id = OBJECT_ID(N'dbo.WarehouseMovements'))
CREATE INDEX [IX_WarehouseMovements_CompanyNetworkId] ON [dbo].[WarehouseMovements] ([CompanyNetworkId]);
GO
IF OBJECT_ID(N'dbo.WarehouseMovements', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseMovements_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.WarehouseMovements'))
CREATE INDEX [IX_WarehouseMovements_CreatedByUserId] ON [dbo].[WarehouseMovements] ([CreatedByUserId]);
GO
IF OBJECT_ID(N'dbo.WarehouseMovements', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseMovements_MaterialPurchaseInvoiceId' AND object_id = OBJECT_ID(N'dbo.WarehouseMovements'))
CREATE INDEX [IX_WarehouseMovements_MaterialPurchaseInvoiceId] ON [dbo].[WarehouseMovements] ([MaterialPurchaseInvoiceId]);
GO
IF OBJECT_ID(N'dbo.WarehouseMovements', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseMovements_MaterialSalesInvoiceId' AND object_id = OBJECT_ID(N'dbo.WarehouseMovements'))
CREATE INDEX [IX_WarehouseMovements_MaterialSalesInvoiceId] ON [dbo].[WarehouseMovements] ([MaterialSalesInvoiceId]);
GO
IF OBJECT_ID(N'dbo.WarehouseMovements', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseMovements_MovementDate' AND object_id = OBJECT_ID(N'dbo.WarehouseMovements'))
CREATE INDEX [IX_WarehouseMovements_MovementDate] ON [dbo].[WarehouseMovements] ([MovementDate]);
GO
IF OBJECT_ID(N'dbo.WarehouseMovements', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseMovements_WarehouseItemId' AND object_id = OBJECT_ID(N'dbo.WarehouseMovements'))
CREATE INDEX [IX_WarehouseMovements_WarehouseItemId] ON [dbo].[WarehouseMovements] ([WarehouseItemId]);
GO
IF OBJECT_ID(N'dbo.WarehouseMovements', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseMovements_WarehouseStocktakeId' AND object_id = OBJECT_ID(N'dbo.WarehouseMovements'))
CREATE INDEX [IX_WarehouseMovements_WarehouseStocktakeId] ON [dbo].[WarehouseMovements] ([WarehouseStocktakeId]);
GO
IF OBJECT_ID(N'dbo.WarehouseStocktakeLines', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseStocktakeLines_WarehouseItemId' AND object_id = OBJECT_ID(N'dbo.WarehouseStocktakeLines'))
CREATE INDEX [IX_WarehouseStocktakeLines_WarehouseItemId] ON [dbo].[WarehouseStocktakeLines] ([WarehouseItemId]);
GO
IF OBJECT_ID(N'dbo.WarehouseStocktakeLines', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseStocktakeLines_WarehouseStocktakeId' AND object_id = OBJECT_ID(N'dbo.WarehouseStocktakeLines'))
CREATE INDEX [IX_WarehouseStocktakeLines_WarehouseStocktakeId] ON [dbo].[WarehouseStocktakeLines] ([WarehouseStocktakeId]);
GO
IF OBJECT_ID(N'dbo.WarehouseStocktakes', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseStocktakes_CompanyNetworkId' AND object_id = OBJECT_ID(N'dbo.WarehouseStocktakes'))
CREATE INDEX [IX_WarehouseStocktakes_CompanyNetworkId] ON [dbo].[WarehouseStocktakes] ([CompanyNetworkId]);
GO
IF OBJECT_ID(N'dbo.WarehouseStocktakes', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseStocktakes_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.WarehouseStocktakes'))
CREATE INDEX [IX_WarehouseStocktakes_CreatedByUserId] ON [dbo].[WarehouseStocktakes] ([CreatedByUserId]);
GO
IF OBJECT_ID(N'dbo.WarehouseStocktakes', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseStocktakes_StocktakeDate' AND object_id = OBJECT_ID(N'dbo.WarehouseStocktakes'))
CREATE INDEX [IX_WarehouseStocktakes_StocktakeDate] ON [dbo].[WarehouseStocktakes] ([StocktakeDate]);
GO
IF OBJECT_ID(N'dbo.WarehouseStocktakes', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WarehouseStocktakes_WarehouseItemId' AND object_id = OBJECT_ID(N'dbo.WarehouseStocktakes'))
CREATE INDEX [IX_WarehouseStocktakes_WarehouseItemId] ON [dbo].[WarehouseStocktakes] ([WarehouseItemId]);
GO

