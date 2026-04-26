-- إضافة أعمدة مكان السكن والإحداثيات لجدول Clients (آمن - يضيف فقط المفقود)
-- نفّذ هذا السكربت يدوياً إذا فشل dotnet ef database update

IF COL_LENGTH('Clients', 'ResidenceAddress') IS NULL
BEGIN
    ALTER TABLE [Clients] ADD [ResidenceAddress] nvarchar(500) NULL;
END

IF COL_LENGTH('Clients', 'Latitude') IS NULL
BEGIN
    ALTER TABLE [Clients] ADD [Latitude] float NULL;
END

IF COL_LENGTH('Clients', 'Longitude') IS NULL
BEGIN
    ALTER TABLE [Clients] ADD [Longitude] float NULL;
END

-- تسجيل Migration في EF (لتجنب إعادة المحاولة)
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260214173238_AddClientResidenceLocation')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260214173238_AddClientResidenceLocation', '9.0.0');
END
