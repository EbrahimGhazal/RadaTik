using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadaTik.Migrations
{
    /// <inheritdoc />
    public partial class AddClientOccupationAndWorkplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "Clients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Workplace",
                table: "Clients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Workplace",
                table: "Clients");
        }
    }
}
