using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadTik.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceInvoicesAndPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaintenanceRequestId = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    IssuedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FaultExplanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FixExplanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ServiceBasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransportFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CommissionMode = table.Column<int>(type: "int", nullable: false),
                    CommissionValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetAmountToCompany = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentTransactionId = table.Column<int>(type: "int", nullable: true),
                    PaidByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreviousClientBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NewClientBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_AspNetUsers_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_AspNetUsers_PaidByUserId",
                        column: x => x.PaidByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NetworkMaintenancePrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    MaintenanceType = table.Column<int>(type: "int", nullable: false),
                    AmountSYP = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkMaintenancePrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkMaintenancePrices_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkMaintenancePrices_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_ClientId",
                table: "MaintenanceInvoices",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_CreatedAt",
                table: "MaintenanceInvoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_IssuedByUserId",
                table: "MaintenanceInvoices",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_MaintenanceRequestId",
                table: "MaintenanceInvoices",
                column: "MaintenanceRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_NetworkId",
                table: "MaintenanceInvoices",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_PaidByUserId",
                table: "MaintenanceInvoices",
                column: "PaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_PaymentTransactionId",
                table: "MaintenanceInvoices",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_Status",
                table: "MaintenanceInvoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkMaintenancePrices_IsActive",
                table: "NetworkMaintenancePrices",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkMaintenancePrices_NetworkId_MaintenanceType",
                table: "NetworkMaintenancePrices",
                columns: new[] { "NetworkId", "MaintenanceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkMaintenancePrices_UpdatedByUserId",
                table: "NetworkMaintenancePrices",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceInvoices");

            migrationBuilder.DropTable(
                name: "NetworkMaintenancePrices");
        }
    }
}
