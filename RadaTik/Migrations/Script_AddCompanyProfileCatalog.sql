-- يدوي: إضافة كتالوج بروفايلات الشركة (إذا فشل dotnet ef database update)
-- شغّل على قاعدة البيانات الصحيحة بعد التأكد من وجود جدول Profiles

IF OBJECT_ID(N'dbo.CompanyProfileCatalogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CompanyProfileCatalogs] (
        [Id] int NOT NULL IDENTITY,
        [CompanyNetworkId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Type] int NOT NULL,
        [BillingCycle] int NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [VATPercentage] decimal(5,2) NOT NULL DEFAULT 15,
        [DownloadSpeed] int NOT NULL,
        [DownloadSpeedUnit] int NOT NULL,
        [UploadSpeed] int NULL,
        [UploadSpeedUnit] int NULL,
        [DataLimit] decimal(18,2) NULL,
        [TimeLimit] int NULL,
        [IPTVDevices] int NULL,
        [IsDataCapped] bit NOT NULL,
        [IsTimeCapped] bit NOT NULL,
        [MaxUsers] int NOT NULL,
        [MinDevices] int NOT NULL,
        [MaxDevices] int NOT NULL,
        [AllowedPorts] nvarchar(max) NULL,
        [AllowedAddresses] nvarchar(max) NULL,
        [Features] nvarchar(max) NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [IsForNewClients] bit NOT NULL DEFAULT 1,
        [DisplayOrder] int NOT NULL,
        [MikroTikLocalAddress] nvarchar(max) NULL,
        [MikroTikRemoteAddress] nvarchar(max) NULL,
        [MikroTikRateLimit] nvarchar(max) NULL,
        [MikroTikOnlyOne] bit NOT NULL DEFAULT 1,
        [MikroTikService] nvarchar(max) NULL DEFAULT N'pppoe',
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_CompanyProfileCatalogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompanyProfileCatalogs_Networks_CompanyNetworkId] FOREIGN KEY ([CompanyNetworkId]) REFERENCES [Networks] ([Id])
    );

    CREATE UNIQUE INDEX [IX_CompanyProfileCatalogs_CompanyNetworkId_Name]
        ON [dbo].[CompanyProfileCatalogs] ([CompanyNetworkId], [Name]);
END
GO

IF OBJECT_ID(N'dbo.Profiles', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Profiles', N'CompanyProfileCatalogId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Profiles] ADD [CompanyProfileCatalogId] int NULL;
END
GO

IF OBJECT_ID(N'dbo.Profiles', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.CompanyProfileCatalogs', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_Profiles_CompanyProfileCatalogId_MikroTikServerId'
          AND object_id = OBJECT_ID(N'dbo.Profiles'))
BEGIN
    CREATE UNIQUE INDEX [IX_Profiles_CompanyProfileCatalogId_MikroTikServerId]
        ON [dbo].[Profiles] ([CompanyProfileCatalogId], [MikroTikServerId])
        WHERE [CompanyProfileCatalogId] IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.Profiles', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.CompanyProfileCatalogs', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_Profiles_CompanyProfileCatalogs_CompanyProfileCatalogId')
BEGIN
    ALTER TABLE [dbo].[Profiles] WITH CHECK ADD CONSTRAINT [FK_Profiles_CompanyProfileCatalogs_CompanyProfileCatalogId]
        FOREIGN KEY([CompanyProfileCatalogId]) REFERENCES [dbo].[CompanyProfileCatalogs] ([Id]);
END
GO
