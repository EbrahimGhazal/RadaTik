-- إنشاء جدول ClientTopUpTransactions (تغذية رصيد العميل)
-- نفّذ هذا السكربت إذا ظهر خطأ: Invalid object name 'ClientTopUpTransactions'

IF OBJECT_ID(N'[dbo].[ClientTopUpTransactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ClientTopUpTransactions] (
        [Id] int NOT NULL IDENTITY(1,1),
        [ClientId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PreviousBalance] decimal(18,2) NOT NULL,
        [NewBalance] decimal(18,2) NOT NULL,
        [SourceType] int NOT NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Notes] nvarchar(500) NULL,
        [NetworkId] int NULL,
        [CollectionPointAccountId] int NULL,
        CONSTRAINT [PK_ClientTopUpTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClientTopUpTransactions_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClientTopUpTransactions_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClientTopUpTransactions_CollectionPointAccounts_CollectionPointAccountId] FOREIGN KEY ([CollectionPointAccountId]) REFERENCES [CollectionPointAccounts] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ClientTopUpTransactions_Networks_NetworkId] FOREIGN KEY ([NetworkId]) REFERENCES [Networks] ([Id]) ON DELETE SET NULL
    );

    CREATE INDEX [IX_ClientTopUpTransactions_ClientId] ON [dbo].[ClientTopUpTransactions] ([ClientId]);
    CREATE INDEX [IX_ClientTopUpTransactions_CollectionPointAccountId] ON [dbo].[ClientTopUpTransactions] ([CollectionPointAccountId]);
    CREATE INDEX [IX_ClientTopUpTransactions_CreatedAt] ON [dbo].[ClientTopUpTransactions] ([CreatedAt]);
    CREATE INDEX [IX_ClientTopUpTransactions_CreatedByUserId] ON [dbo].[ClientTopUpTransactions] ([CreatedByUserId]);
    CREATE INDEX [IX_ClientTopUpTransactions_NetworkId] ON [dbo].[ClientTopUpTransactions] ([NetworkId]);
    CREATE INDEX [IX_ClientTopUpTransactions_SourceType] ON [dbo].[ClientTopUpTransactions] ([SourceType]);

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214120000_AddClientTopUpTransaction', N'9.0.10');

    PRINT 'تم إنشاء جدول ClientTopUpTransactions بنجاح.';
END
ELSE
    PRINT 'الجدول ClientTopUpTransactions موجود مسبقاً.';
