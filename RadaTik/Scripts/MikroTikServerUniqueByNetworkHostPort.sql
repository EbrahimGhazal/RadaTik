-- Allow same Host/Port on different networks.
-- Uniqueness becomes: NetworkId + Host + Port

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_MikroTikServers_Host_Port'
      AND object_id = OBJECT_ID(N'dbo.MikroTikServers'))
BEGIN
    DROP INDEX [IX_MikroTikServers_Host_Port] ON [dbo].[MikroTikServers];
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_MikroTikServers_NetworkId_Host_Port'
      AND object_id = OBJECT_ID(N'dbo.MikroTikServers'))
BEGIN
    CREATE UNIQUE INDEX [IX_MikroTikServers_NetworkId_Host_Port]
    ON [dbo].[MikroTikServers] ([NetworkId], [Host], [Port]);
END
GO
