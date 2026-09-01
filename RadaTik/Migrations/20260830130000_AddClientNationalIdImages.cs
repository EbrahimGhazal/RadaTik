using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RadaTik.Data;

#nullable disable

namespace RadaTik.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260830130000_AddClientNationalIdImages")]
    public partial class AddClientNationalIdImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.Clients', N'NationalIdFrontPath') IS NULL
                    ALTER TABLE [dbo].[Clients] ADD [NationalIdFrontPath] nvarchar(260) NULL;
                IF COL_LENGTH(N'dbo.Clients', N'NationalIdBackPath') IS NULL
                    ALTER TABLE [dbo].[Clients] ADD [NationalIdBackPath] nvarchar(260) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.Clients', N'NationalIdFrontPath') IS NOT NULL
                    ALTER TABLE [dbo].[Clients] DROP COLUMN [NationalIdFrontPath];
                IF COL_LENGTH(N'dbo.Clients', N'NationalIdBackPath') IS NOT NULL
                    ALTER TABLE [dbo].[Clients] DROP COLUMN [NationalIdBackPath];
                """);
        }
    }
}
