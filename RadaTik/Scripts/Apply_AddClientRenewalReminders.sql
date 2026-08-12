/*
  تطبيق يدوي لهجرة AddClientRenewalReminders عندما لا يعمل dotnet ef database update
  (مثلاً عدم تطابق __EFMigrationsHistory مع قاعدة البيانات الفعلية).

  نفّذ هذا الملف على قاعدة بيانات المشروع مرة واحدة.
*/

SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.Clients', N'TelegramChatId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Clients] ADD [TelegramChatId] NVARCHAR(64) NULL;
END
GO

IF OBJECT_ID(N'dbo.ClientRenewalReminderSendLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ClientRenewalReminderSendLogs] (
        [Id] INT NOT NULL IDENTITY(1, 1),
        [ClientId] INT NOT NULL,
        [CompanyNetworkId] INT NOT NULL,
        [ExpirationDate] DATE NOT NULL,
        [DaysBefore] TINYINT NOT NULL,
        [Channel] TINYINT NOT NULL,
        [SentAtUtc] DATETIME2 NOT NULL,
        [Success] BIT NOT NULL,
        [ErrorMessage] NVARCHAR(500) NULL,
        CONSTRAINT [PK_ClientRenewalReminderSendLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClientRenewalReminderSendLogs_Clients_ClientId]
            FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_ClientRenewalReminderSendLogs_ClientId_ExpirationDate_DaysBefore_Channel]
        ON [dbo].[ClientRenewalReminderSendLogs] ([ClientId], [ExpirationDate], [DaysBefore], [Channel]);

    CREATE INDEX [IX_ClientRenewalReminderSendLogs_CompanyNetworkId]
        ON [dbo].[ClientRenewalReminderSendLogs] ([CompanyNetworkId]);

    CREATE INDEX [IX_ClientRenewalReminderSendLogs_ClientId]
        ON [dbo].[ClientRenewalReminderSendLogs] ([ClientId]);
END
GO

IF OBJECT_ID(N'dbo.NetworkClientRenewalReminderSettings', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NetworkClientRenewalReminderSettings] (
        [NetworkId] INT NOT NULL,
        [IsEnabled] BIT NOT NULL,
        [RemindDaysBefore5] BIT NOT NULL,
        [RemindDaysBefore4] BIT NOT NULL,
        [RemindDaysBefore3] BIT NOT NULL,
        [MessageTemplate] NVARCHAR(4000) NOT NULL,
        [SendWhatsApp] BIT NOT NULL,
        [WhatsAppDisplayNumber] NVARCHAR(32) NULL,
        [WhatsAppVerifiedAt] DATETIME2 NULL,
        [WhatsAppApiUrl] NVARCHAR(1000) NULL,
        [WhatsAppApiAuthorizationHeader] NVARCHAR(500) NULL,
        [WhatsAppApiBodyTemplate] NVARCHAR(4000) NULL,
        [SendTelegram] BIT NOT NULL,
        [TelegramBotToken] NVARCHAR(256) NULL,
        [TelegramVerifiedAt] DATETIME2 NULL,
        [TelegramTestChatId] NVARCHAR(64) NULL,
        [WhatsAppTestPhone] NVARCHAR(32) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        [UpdatedAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [PK_NetworkClientRenewalReminderSettings] PRIMARY KEY ([NetworkId]),
        CONSTRAINT [FK_NetworkClientRenewalReminderSettings_Networks_NetworkId]
            FOREIGN KEY ([NetworkId]) REFERENCES [dbo].[Networks] ([Id]) ON DELETE CASCADE
    );
END
GO

IF COL_LENGTH(N'dbo.NetworkClientRenewalReminderSettings', N'WhatsAppApiBodyTemplate') IS NULL
   AND OBJECT_ID(N'dbo.NetworkClientRenewalReminderSettings', N'U') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[NetworkClientRenewalReminderSettings]
        ADD [WhatsAppApiBodyTemplate] NVARCHAR(4000) NULL;
END
GO

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = N'20260725101346_AddClientRenewalReminders'
   )
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260725101346_AddClientRenewalReminders', N'9.0.10');
END
GO

-- Legacy history id from older manual script (ignore if present).
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND EXISTS (
       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = N'20260413210724_AddClientRenewalReminders'
   )
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = N'20260725101346_AddClientRenewalReminders'
   )
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260725101346_AddClientRenewalReminders', N'9.0.10');
END
GO

PRINT N'Apply_AddClientRenewalReminders: done.';
GO
