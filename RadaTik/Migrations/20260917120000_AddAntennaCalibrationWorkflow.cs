using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RadaTik.Data;

#nullable disable

namespace RadaTik.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260917120000_AddAntennaCalibrationWorkflow")]
    public partial class AddAntennaCalibrationWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.AntennaCalibrationSessions', N'Workflow') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[AntennaCalibrationSessions]
                    ADD [Workflow] nvarchar(20) NOT NULL
                        CONSTRAINT [DF_AntennaCalibrationSessions_Workflow] DEFAULT (N'signal');
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.AntennaCalibrationSessions', N'Workflow') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[AntennaCalibrationSessions]
                    DROP CONSTRAINT [DF_AntennaCalibrationSessions_Workflow];
                    ALTER TABLE [dbo].[AntennaCalibrationSessions]
                    DROP COLUMN [Workflow];
                END
                """);
        }
    }
}
