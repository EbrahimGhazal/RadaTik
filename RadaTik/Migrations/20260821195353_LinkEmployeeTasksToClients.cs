using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadaTik.Migrations
{
    /// <inheritdoc />
    public partial class LinkEmployeeTasksToClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "CompanyEmployeeTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaintenanceRequestId",
                table: "CompanyEmployeeTasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeTasks_ClientId",
                table: "CompanyEmployeeTasks",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeTasks_MaintenanceRequestId",
                table: "CompanyEmployeeTasks",
                column: "MaintenanceRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyEmployeeTasks_Clients_ClientId",
                table: "CompanyEmployeeTasks",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyEmployeeTasks_MaintenanceRequests_MaintenanceRequestId",
                table: "CompanyEmployeeTasks",
                column: "MaintenanceRequestId",
                principalTable: "MaintenanceRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyEmployeeTasks_Clients_ClientId",
                table: "CompanyEmployeeTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyEmployeeTasks_MaintenanceRequests_MaintenanceRequestId",
                table: "CompanyEmployeeTasks");

            migrationBuilder.DropIndex(
                name: "IX_CompanyEmployeeTasks_ClientId",
                table: "CompanyEmployeeTasks");

            migrationBuilder.DropIndex(
                name: "IX_CompanyEmployeeTasks_MaintenanceRequestId",
                table: "CompanyEmployeeTasks");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "CompanyEmployeeTasks");

            migrationBuilder.DropColumn(
                name: "MaintenanceRequestId",
                table: "CompanyEmployeeTasks");
        }
    }
}
