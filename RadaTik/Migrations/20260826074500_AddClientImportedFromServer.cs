using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadaTik.Migrations
{
    /// <inheritdoc />
    public partial class AddClientImportedFromServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsImportedFromServer",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing MikroTik imports have no initial-setup invoice; treat them as already installed.
            migrationBuilder.Sql("""
                UPDATE c
                SET c.IsImportedFromServer = 1
                FROM Clients AS c
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM SubscriberInstallationInvoices AS i
                    WHERE i.ClientId = c.Id
                      AND i.Kind = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsImportedFromServer",
                table: "Clients");
        }
    }
}
