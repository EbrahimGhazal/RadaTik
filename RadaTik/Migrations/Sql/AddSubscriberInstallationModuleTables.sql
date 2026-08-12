-- Subscriber installation invoices module
IF OBJECT_ID(N'dbo.SubscriberInstallationMaterialPrices', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[SubscriberInstallationMaterialPrices] (
    [Id] int NOT NULL IDENTITY,
    [NetworkId] int NOT NULL,
    [MaterialKey] nvarchar(60) NOT NULL,
    [MaterialName] nvarchar(120) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [WarehouseItemId] int NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_SubscriberInstallationMaterialPrices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SubscriberInstallationMaterialPrices_Networks_NetworkId] FOREIGN KEY ([NetworkId]) REFERENCES [Networks] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SubscriberInstallationMaterialPrices_WarehouseItems_WarehouseItemId] FOREIGN KEY ([WarehouseItemId]) REFERENCES [WarehouseItems] ([Id])
);
END
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationMaterialWarehouseLinks', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[SubscriberInstallationMaterialWarehouseLinks] (
    [Id] int NOT NULL IDENTITY,
    [MaterialPriceId] int NOT NULL,
    [WarehouseItemId] int NOT NULL,
    [IsDefault] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SubscriberInstallationMaterialWarehouseLinks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SubscriberInstallationMaterialWarehouseLinks_SubscriberInstallationMaterialPrices_MaterialPriceId] FOREIGN KEY ([MaterialPriceId]) REFERENCES [SubscriberInstallationMaterialPrices] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SubscriberInstallationMaterialWarehouseLinks_WarehouseItems_WarehouseItemId] FOREIGN KEY ([WarehouseItemId]) REFERENCES [WarehouseItems] ([Id]) ON DELETE NO ACTION
);
END
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoices', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[SubscriberInstallationInvoices] (
    [Id] int NOT NULL IDENTITY,
    [ClientId] int NOT NULL,
    [NetworkId] int NOT NULL,
    [CompanyName] nvarchar(120) NOT NULL,
    [ClientName] nvarchar(120) NOT NULL,
    [ReceiverMode] int NOT NULL,
    [Kind] int NOT NULL,
    [Status] int NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [PaidAmount] decimal(18,2) NOT NULL,
    [RemainingAmount] decimal(18,2) NOT NULL,
    [ClientSignature] nvarchar(500) NULL,
    [EmployeeSignature] nvarchar(500) NULL,
    [CreatedByUserId] nvarchar(450) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
    [FinalizedAt] datetime2 NULL,
    [FinalizedByUserId] nvarchar(450) NULL,
    CONSTRAINT [PK_SubscriberInstallationInvoices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SubscriberInstallationInvoices_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_SubscriberInstallationInvoices_AspNetUsers_FinalizedByUserId] FOREIGN KEY ([FinalizedByUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_SubscriberInstallationInvoices_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SubscriberInstallationInvoices_Networks_NetworkId] FOREIGN KEY ([NetworkId]) REFERENCES [Networks] ([Id]) ON DELETE NO ACTION
);
END
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoiceItems', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[SubscriberInstallationInvoiceItems] (
    [Id] int NOT NULL IDENTITY,
    [SubscriberInstallationInvoiceId] int NOT NULL,
    [ItemName] nvarchar(120) NOT NULL,
    [MaterialKey] nvarchar(60) NULL,
    [IsStockItem] bit NOT NULL,
    [WarehouseItemId] int NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [Quantity] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_SubscriberInstallationInvoiceItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SubscriberInstallationInvoiceItems_SubscriberInstallationInvoices_SubscriberInstallationInvoiceId] FOREIGN KEY ([SubscriberInstallationInvoiceId]) REFERENCES [SubscriberInstallationInvoices] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SubscriberInstallationInvoiceItems_WarehouseItems_WarehouseItemId] FOREIGN KEY ([WarehouseItemId]) REFERENCES [WarehouseItems] ([Id])
);
END
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoicePayments', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[SubscriberInstallationInvoicePayments] (
    [Id] int NOT NULL IDENTITY,
    [SubscriberInstallationInvoiceId] int NOT NULL,
    [PaymentTransactionId] int NULL,
    [PaymentMethod] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [ReceivedByUserId] nvarchar(450) NOT NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_SubscriberInstallationInvoicePayments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SubscriberInstallationInvoicePayments_AspNetUsers_ReceivedByUserId] FOREIGN KEY ([ReceivedByUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_SubscriberInstallationInvoicePayments_PaymentTransactions_PaymentTransactionId] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [PaymentTransactions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SubscriberInstallationInvoicePayments_SubscriberInstallationInvoices_SubscriberInstallationInvoiceId] FOREIGN KEY ([SubscriberInstallationInvoiceId]) REFERENCES [SubscriberInstallationInvoices] ([Id]) ON DELETE CASCADE
);
END
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoiceItems', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoiceItems_SubscriberInstallationInvoiceId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoiceItems'))
CREATE INDEX [IX_SubscriberInstallationInvoiceItems_SubscriberInstallationInvoiceId] ON [dbo].[SubscriberInstallationInvoiceItems] ([SubscriberInstallationInvoiceId]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoiceItems', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoiceItems_WarehouseItemId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoiceItems'))
CREATE INDEX [IX_SubscriberInstallationInvoiceItems_WarehouseItemId] ON [dbo].[SubscriberInstallationInvoiceItems] ([WarehouseItemId]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoicePayments', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoicePayments_CreatedAt' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoicePayments'))
CREATE INDEX [IX_SubscriberInstallationInvoicePayments_CreatedAt] ON [dbo].[SubscriberInstallationInvoicePayments] ([CreatedAt]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoicePayments', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoicePayments_PaymentTransactionId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoicePayments'))
BEGIN
    SET QUOTED_IDENTIFIER ON;
    CREATE UNIQUE INDEX [IX_SubscriberInstallationInvoicePayments_PaymentTransactionId] ON [dbo].[SubscriberInstallationInvoicePayments] ([PaymentTransactionId]) WHERE [PaymentTransactionId] IS NOT NULL;
END
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoicePayments', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoicePayments_ReceivedByUserId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoicePayments'))
CREATE INDEX [IX_SubscriberInstallationInvoicePayments_ReceivedByUserId] ON [dbo].[SubscriberInstallationInvoicePayments] ([ReceivedByUserId]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoicePayments', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoicePayments_SubscriberInstallationInvoiceId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoicePayments'))
CREATE INDEX [IX_SubscriberInstallationInvoicePayments_SubscriberInstallationInvoiceId] ON [dbo].[SubscriberInstallationInvoicePayments] ([SubscriberInstallationInvoiceId]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoices_ClientId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoices'))
CREATE INDEX [IX_SubscriberInstallationInvoices_ClientId] ON [dbo].[SubscriberInstallationInvoices] ([ClientId]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoices_ClientId_Kind' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoices'))
CREATE INDEX [IX_SubscriberInstallationInvoices_ClientId_Kind] ON [dbo].[SubscriberInstallationInvoices] ([ClientId], [Kind]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoices_CreatedAt' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoices'))
CREATE INDEX [IX_SubscriberInstallationInvoices_CreatedAt] ON [dbo].[SubscriberInstallationInvoices] ([CreatedAt]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoices_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoices'))
CREATE INDEX [IX_SubscriberInstallationInvoices_CreatedByUserId] ON [dbo].[SubscriberInstallationInvoices] ([CreatedByUserId]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoices_FinalizedByUserId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoices'))
CREATE INDEX [IX_SubscriberInstallationInvoices_FinalizedByUserId] ON [dbo].[SubscriberInstallationInvoices] ([FinalizedByUserId]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoices_NetworkId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoices'))
CREATE INDEX [IX_SubscriberInstallationInvoices_NetworkId] ON [dbo].[SubscriberInstallationInvoices] ([NetworkId]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationInvoices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationInvoices_Status' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationInvoices'))
CREATE INDEX [IX_SubscriberInstallationInvoices_Status] ON [dbo].[SubscriberInstallationInvoices] ([Status]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationMaterialPrices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationMaterialPrices_IsActive' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationMaterialPrices'))
CREATE INDEX [IX_SubscriberInstallationMaterialPrices_IsActive] ON [dbo].[SubscriberInstallationMaterialPrices] ([IsActive]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationMaterialPrices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationMaterialPrices_NetworkId_MaterialKey' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationMaterialPrices'))
CREATE UNIQUE INDEX [IX_SubscriberInstallationMaterialPrices_NetworkId_MaterialKey] ON [dbo].[SubscriberInstallationMaterialPrices] ([NetworkId], [MaterialKey]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationMaterialPrices', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationMaterialPrices_WarehouseItemId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationMaterialPrices'))
CREATE INDEX [IX_SubscriberInstallationMaterialPrices_WarehouseItemId] ON [dbo].[SubscriberInstallationMaterialPrices] ([WarehouseItemId]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationMaterialWarehouseLinks', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationMaterialWarehouseLinks_MaterialPriceId_WarehouseItemId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationMaterialWarehouseLinks'))
CREATE UNIQUE INDEX [IX_SubscriberInstallationMaterialWarehouseLinks_MaterialPriceId_WarehouseItemId] ON [dbo].[SubscriberInstallationMaterialWarehouseLinks] ([MaterialPriceId], [WarehouseItemId]);
GO
IF OBJECT_ID(N'dbo.SubscriberInstallationMaterialWarehouseLinks', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubscriberInstallationMaterialWarehouseLinks_WarehouseItemId' AND object_id = OBJECT_ID(N'dbo.SubscriberInstallationMaterialWarehouseLinks'))
CREATE INDEX [IX_SubscriberInstallationMaterialWarehouseLinks_WarehouseItemId] ON [dbo].[SubscriberInstallationMaterialWarehouseLinks] ([WarehouseItemId]);
GO

