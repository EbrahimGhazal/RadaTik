/* عمود قالب جسم طلب واتساب — نفّذ إن لم تُطبَّق الهجرة AddWhatsAppApiBodyTemplate */

IF COL_LENGTH(N'dbo.NetworkClientRenewalReminderSettings', N'WhatsAppApiBodyTemplate') IS NULL
BEGIN
    ALTER TABLE [dbo].[NetworkClientRenewalReminderSettings]
    ADD [WhatsAppApiBodyTemplate] NVARCHAR(4000) NULL;
END
GO

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = N'20260413212551_AddWhatsAppApiBodyTemplate'
   )
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260413212551_AddWhatsAppApiBodyTemplate', N'9.0.10');
END
GO

PRINT N'AddWhatsAppApiBodyTemplate: done.';
GO
