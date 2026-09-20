using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RadaTik.Data;

#nullable disable

namespace RadaTik.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260920080000_DropAntennaCalibrationSessions")]
    public partial class DropAntennaCalibrationSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.AntennaCalibrationSessions', N'U') IS NOT NULL
                    DROP TABLE [dbo].[AntennaCalibrationSessions];
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.AntennaCalibrationSessions', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[AntennaCalibrationSessions] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [Code] nvarchar(8) NOT NULL,
                        [NetworkId] int NOT NULL,
                        [SectorId] int NOT NULL,
                        [ReceiverId] int NULL,
                        [SectorName] nvarchar(120) NOT NULL,
                        [ReceiverName] nvarchar(120) NOT NULL,
                        [ReceiverLatitude] float NOT NULL,
                        [ReceiverLongitude] float NOT NULL,
                        [ReceiverIp] nvarchar(64) NULL,
                        [ReceiverMac] nvarchar(32) NULL,
                        [SectorAntennaMsl] float NOT NULL,
                        [ReceiverAntennaMsl] float NOT NULL,
                        [AlignmentJson] nvarchar(max) NOT NULL,
                        [PathJson] nvarchar(max) NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [LastActivityUtc] datetime2 NOT NULL,
                        [ExpiresAtUtc] datetime2 NOT NULL,
                        [Workflow] nvarchar(20) NOT NULL CONSTRAINT [DF_AntennaCalibrationSessions_Workflow] DEFAULT (N'signal'),
                        CONSTRAINT [PK_AntennaCalibrationSessions] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_AntennaCalibrationSessions_Code]
                        ON [dbo].[AntennaCalibrationSessions] ([Code]);
                    CREATE INDEX [IX_AntennaCalibrationSessions_NetworkId_LastActivityUtc]
                        ON [dbo].[AntennaCalibrationSessions] ([NetworkId], [LastActivityUtc]);
                    CREATE INDEX [IX_AntennaCalibrationSessions_ExpiresAtUtc]
                        ON [dbo].[AntennaCalibrationSessions] ([ExpiresAtUtc]);
                END
                """);
        }
    }
}
