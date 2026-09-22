using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RadaTik.Data;

#nullable disable

namespace RadaTik.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260922093000_AddClientServerPresence")]
    public partial class AddClientServerPresence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server: لا يمكن ADD COLUMN ثم استخدامه في نفس الدفعة — افصل الدفعات.
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.Clients', N'ActiveServingServerId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Clients]
                        ADD [ActiveServingServerId] int NULL;
                END
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_Clients_MikroTikServers_ActiveServingServerId')
                BEGIN
                    ALTER TABLE [dbo].[Clients] WITH CHECK
                    ADD CONSTRAINT [FK_Clients_MikroTikServers_ActiveServingServerId]
                        FOREIGN KEY ([ActiveServingServerId])
                        REFERENCES [dbo].[MikroTikServers] ([Id])
                        ON DELETE SET NULL;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Clients_ActiveServingServerId'
                      AND object_id = OBJECT_ID(N'dbo.Clients'))
                BEGIN
                    CREATE INDEX [IX_Clients_ActiveServingServerId]
                        ON [dbo].[Clients] ([ActiveServingServerId]);
                END
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.ClientServerPresences', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ClientServerPresences] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [ClientId] int NOT NULL,
                        [MikroTikServerId] int NOT NULL,
                        [Role] tinyint NOT NULL CONSTRAINT [DF_ClientServerPresences_Role] DEFAULT (1),
                        [CreatedAtUtc] datetime2 NOT NULL CONSTRAINT [DF_ClientServerPresences_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
                        [LastSeenActiveUtc] datetime2 NULL,
                        CONSTRAINT [PK_ClientServerPresences] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ClientServerPresences_Clients_ClientId]
                            FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Clients] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_ClientServerPresences_MikroTikServers_MikroTikServerId]
                            FOREIGN KEY ([MikroTikServerId]) REFERENCES [dbo].[MikroTikServers] ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_ClientServerPresences_ClientId_MikroTikServerId]
                        ON [dbo].[ClientServerPresences] ([ClientId], [MikroTikServerId]);
                    CREATE INDEX [IX_ClientServerPresences_MikroTikServerId]
                        ON [dbo].[ClientServerPresences] ([MikroTikServerId]);
                END
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO [dbo].[ClientServerPresences] ([ClientId], [MikroTikServerId], [Role], [CreatedAtUtc])
                SELECT c.[Id], c.[MikroTikServerId], 0, SYSUTCDATETIME()
                FROM [dbo].[Clients] c
                WHERE c.[MikroTikServerId] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[ClientServerPresences] p
                      WHERE p.[ClientId] = c.[Id] AND p.[MikroTikServerId] = c.[MikroTikServerId]);
                """);

            migrationBuilder.Sql(
                """
                ;WITH Ranked AS (
                    SELECT
                        c.[Id],
                        c.[NetworkId],
                        LOWER(LTRIM(RTRIM(c.[UserName]))) AS UName,
                        c.[MikroTikServerId],
                        c.[Balance],
                        c.[CreatedDate],
                        ROW_NUMBER() OVER (
                            PARTITION BY c.[NetworkId], LOWER(LTRIM(RTRIM(c.[UserName])))
                            ORDER BY c.[Balance] DESC, c.[CreatedDate] ASC, c.[Id] ASC) AS rn
                    FROM [dbo].[Clients] c
                    WHERE c.[NetworkId] IS NOT NULL
                      AND c.[UserName] IS NOT NULL
                      AND LTRIM(RTRIM(c.[UserName])) <> N''
                      AND c.[MikroTikServerId] IS NOT NULL
                      AND c.[IsCrossServerDuplicate] = 1
                ),
                Keepers AS (
                    SELECT * FROM Ranked WHERE rn = 1
                ),
                SiblingServers AS (
                    SELECT DISTINCT k.[Id] AS KeeperId, r.[MikroTikServerId] AS SiblingServerId
                    FROM Keepers k
                    INNER JOIN Ranked r
                        ON r.[NetworkId] = k.[NetworkId]
                       AND r.[UName] = k.[UName]
                       AND r.[Id] <> k.[Id]
                )
                INSERT INTO [dbo].[ClientServerPresences] ([ClientId], [MikroTikServerId], [Role], [CreatedAtUtc])
                SELECT s.KeeperId, s.SiblingServerId, 1, SYSUTCDATETIME()
                FROM SiblingServers s
                WHERE NOT EXISTS (
                    SELECT 1 FROM [dbo].[ClientServerPresences] p
                    WHERE p.[ClientId] = s.KeeperId AND p.[MikroTikServerId] = s.SiblingServerId);
                """);

            migrationBuilder.Sql(
                """
                ;WITH Ranked AS (
                    SELECT
                        c.[Id],
                        c.[NetworkId],
                        LOWER(LTRIM(RTRIM(c.[UserName]))) AS UName,
                        c.[MikroTikServerId],
                        c.[Balance],
                        c.[CreatedDate],
                        ROW_NUMBER() OVER (
                            PARTITION BY c.[NetworkId], LOWER(LTRIM(RTRIM(c.[UserName])))
                            ORDER BY c.[Balance] DESC, c.[CreatedDate] ASC, c.[Id] ASC) AS rn
                    FROM [dbo].[Clients] c
                    WHERE c.[IsCrossServerDuplicate] = 1
                      AND c.[NetworkId] IS NOT NULL
                      AND c.[UserName] IS NOT NULL
                      AND c.[MikroTikServerId] IS NOT NULL
                ),
                Keepers AS (SELECT * FROM Ranked WHERE rn = 1),
                LatestSibling AS (
                    SELECT k.[Id] AS KeeperId,
                           (
                               SELECT TOP (1) r.[MikroTikServerId]
                               FROM Ranked r
                               WHERE r.[NetworkId] = k.[NetworkId]
                                 AND r.[UName] = k.[UName]
                                 AND r.[Id] <> k.[Id]
                               ORDER BY r.[CreatedDate] DESC, r.[Id] DESC
                           ) AS ActiveServerId
                    FROM Keepers k
                )
                UPDATE c
                SET c.[ActiveServingServerId] = COALESCE(ls.ActiveServerId, c.[MikroTikServerId])
                FROM [dbo].[Clients] c
                INNER JOIN LatestSibling ls ON ls.KeeperId = c.[Id]
                WHERE c.[ActiveServingServerId] IS NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.ClientServerPresences', N'U') IS NOT NULL
                    DROP TABLE [dbo].[ClientServerPresences];
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_Clients_MikroTikServers_ActiveServingServerId')
                    ALTER TABLE [dbo].[Clients] DROP CONSTRAINT [FK_Clients_MikroTikServers_ActiveServingServerId];

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Clients_ActiveServingServerId'
                      AND object_id = OBJECT_ID(N'dbo.Clients'))
                    DROP INDEX [IX_Clients_ActiveServingServerId] ON [dbo].[Clients];
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.Clients', N'ActiveServingServerId') IS NOT NULL
                    ALTER TABLE [dbo].[Clients] DROP COLUMN [ActiveServingServerId];
                """);
        }
    }
}
