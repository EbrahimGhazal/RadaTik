using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RadaTik.Data;

#nullable disable

namespace RadaTik.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260920093000_AddCalibrationScenariosAndSessions")]
    public partial class AddCalibrationScenariosAndSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.CalibrationScenarios', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[CalibrationScenarios] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [NetworkId] int NOT NULL,
                        [Name] nvarchar(80) NOT NULL,
                        [Description] nvarchar(240) NULL,
                        [IsDefault] bit NOT NULL CONSTRAINT [DF_CalibrationScenarios_IsDefault] DEFAULT (0),
                        [IsActive] bit NOT NULL CONSTRAINT [DF_CalibrationScenarios_IsActive] DEFAULT (1),
                        [CrewMode] nvarchar(16) NOT NULL,
                        [AimMode] nvarchar(16) NOT NULL,
                        [AuthMode] nvarchar(16) NOT NULL,
                        [DisplayMode] nvarchar(16) NOT NULL,
                        [SuccessMode] nvarchar(16) NOT NULL,
                        [MinSignalDbm] int NULL,
                        [PeakHoldSeconds] int NOT NULL CONSTRAINT [DF_CalibrationScenarios_PeakHold] DEFAULT (3),
                        [RequireIpOrMac] bit NOT NULL CONSTRAINT [DF_CalibrationScenarios_RequireIp] DEFAULT (1),
                        [ShowSnrCcq] bit NOT NULL CONSTRAINT [DF_CalibrationScenarios_ShowSnr] DEFAULT (0),
                        [ShowLosHint] bit NOT NULL CONSTRAINT [DF_CalibrationScenarios_ShowLos] DEFAULT (1),
                        [ShowGeometryTargets] bit NOT NULL CONSTRAINT [DF_CalibrationScenarios_ShowGeo] DEFAULT (0),
                        [SortOrder] int NOT NULL CONSTRAINT [DF_CalibrationScenarios_Sort] DEFAULT (0),
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [UpdatedAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_CalibrationScenarios] PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [IX_CalibrationScenarios_NetworkId_Name]
                        ON [dbo].[CalibrationScenarios] ([NetworkId], [Name]);
                    CREATE INDEX [IX_CalibrationScenarios_NetworkId_IsDefault]
                        ON [dbo].[CalibrationScenarios] ([NetworkId], [IsDefault]);
                END

                IF OBJECT_ID(N'dbo.CalibrationSessions', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[CalibrationSessions] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [Code] nvarchar(8) NOT NULL,
                        [NetworkId] int NOT NULL,
                        [SectorId] int NOT NULL,
                        [ReceiverId] int NULL,
                        [ScenarioId] int NULL,
                        [ScenarioName] nvarchar(80) NOT NULL,
                        [ScenarioJson] nvarchar(max) NOT NULL,
                        [SectorName] nvarchar(120) NOT NULL,
                        [ReceiverName] nvarchar(120) NOT NULL,
                        [ReceiverLatitude] float NOT NULL,
                        [ReceiverLongitude] float NOT NULL,
                        [ReceiverIp] nvarchar(64) NULL,
                        [ReceiverMac] nvarchar(32) NULL,
                        [SectorAntennaMsl] float NOT NULL,
                        [ReceiverAntennaMsl] float NOT NULL,
                        [AlignmentJson] nvarchar(max) NOT NULL,
                        [PathSummary] nvarchar(400) NULL,
                        [PathClear] bit NOT NULL CONSTRAINT [DF_CalibrationSessions_PathClear] DEFAULT (1),
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [LastActivityUtc] datetime2 NOT NULL,
                        [ExpiresAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_CalibrationSessions] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_CalibrationSessions_Code]
                        ON [dbo].[CalibrationSessions] ([Code]);
                    CREATE INDEX [IX_CalibrationSessions_NetworkId_LastActivityUtc]
                        ON [dbo].[CalibrationSessions] ([NetworkId], [LastActivityUtc]);
                    CREATE INDEX [IX_CalibrationSessions_ExpiresAtUtc]
                        ON [dbo].[CalibrationSessions] ([ExpiresAtUtc]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.CalibrationSessions', N'U') IS NOT NULL
                    DROP TABLE [dbo].[CalibrationSessions];
                IF OBJECT_ID(N'dbo.CalibrationScenarios', N'U') IS NOT NULL
                    DROP TABLE [dbo].[CalibrationScenarios];
                """);
        }
    }
}
