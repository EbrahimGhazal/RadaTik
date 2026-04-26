using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadTik.Migrations
{
    /// <inheritdoc />
    public partial class AddClientTrafficTestSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientTrafficTestSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    ChargeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientTrafficTestSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientTrafficTestSessions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientTrafficTestSessions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientTrafficTestSessions_ClientId_StartedAtUtc",
                table: "ClientTrafficTestSessions",
                columns: new[] { "ClientId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientTrafficTestSessions_CreatedByUserId",
                table: "ClientTrafficTestSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTrafficTestSessions_StartedAtUtc",
                table: "ClientTrafficTestSessions",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientTrafficTestSessions");
        }
    }
}
