-- إضافة أعمدة مواعيد لوحة الموظف (تشغيل مرة واحدة على قاعدة البيانات الحالية)
-- Use when EF reports Invalid column name 'ScheduledInstallationDate' / 'ScheduledVisitDate'

IF COL_LENGTH(N'dbo.Clients', N'ScheduledInstallationDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[Clients] ADD [ScheduledInstallationDate] datetime2 NULL;
    PRINT N'Added Clients.ScheduledInstallationDate';
END
ELSE
    PRINT N'Clients.ScheduledInstallationDate already exists';

IF COL_LENGTH(N'dbo.MaintenanceRequests', N'ScheduledVisitDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[MaintenanceRequests] ADD [ScheduledVisitDate] datetime2 NULL;
    PRINT N'Added MaintenanceRequests.ScheduledVisitDate';
END
ELSE
    PRINT N'MaintenanceRequests.ScheduledVisitDate already exists';

-- اختياري: تسجيل الهجرة حتى لا يحاول EF إعادة تطبيقها لاحقاً (إن وُجد جدول السجل)
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = N'20260410120000_AddEmployeeDashboardScheduledDates')
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260410120000_AddEmployeeDashboardScheduledDates', N'9.0.10');
    PRINT N'Recorded migration 20260410120000_AddEmployeeDashboardScheduledDates in __EFMigrationsHistory';
END
