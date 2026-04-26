/*
  إزالة ميزة تذكير تجديد المشتركين من قاعدة البيانات بشكل آمن (Idempotent).
  - يحذف الجداول إن وجدت
  - يحذف عمود TelegramChatId من Clients إن وجد
  - يسجل الهجرة 20260414172304_RemoveClientRenewalReminderFeature إن كان جدول التاريخ موجوداً
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.ClientRenewalReminderSendLogs', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ClientRenewalReminderSendLogs];
END
GO

IF OBJECT_ID(N'dbo.NetworkClientRenewalReminderSettings', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[NetworkClientRenewalReminderSettings];
END
GO

IF COL_LENGTH(N'dbo.Clients', N'TelegramChatId') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Clients] DROP COLUMN [TelegramChatId];
END
GO

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = N'20260414172304_RemoveClientRenewalReminderFeature'
   )
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260414172304_RemoveClientRenewalReminderFeature', N'9.0.10');
END
GO

PRINT N'RemoveClientRenewalReminderFeature: done.';
GO
