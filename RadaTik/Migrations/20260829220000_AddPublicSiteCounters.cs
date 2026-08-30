using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RadaTik.Data;

#nullable disable

namespace RadaTik.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260829220000_AddPublicSiteCounters")]
    public partial class AddPublicSiteCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.PublicSiteCounters', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[PublicSiteCounters] (
                        [Key] nvarchar(64) NOT NULL,
                        [Count] bigint NOT NULL CONSTRAINT [DF_PublicSiteCounters_Count] DEFAULT (0),
                        [UpdatedUtc] datetime2 NOT NULL CONSTRAINT [DF_PublicSiteCounters_UpdatedUtc] DEFAULT (SYSUTCDATETIME()),
                        CONSTRAINT [PK_PublicSiteCounters] PRIMARY KEY ([Key])
                    );
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicSiteCounters");
        }
    }
}
