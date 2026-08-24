using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadaTik.Migrations
{
    /// <inheritdoc />
    public partial class AddReportPrintedColumnKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrintedColumnKeys",
                table: "NetworkReportTemplates",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrintedColumnKeys",
                table: "NetworkReportTemplates");
        }
    }
}
