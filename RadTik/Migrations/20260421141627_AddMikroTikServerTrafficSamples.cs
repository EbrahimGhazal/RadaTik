using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadTik.Migrations
{
    /// <inheritdoc />
    public partial class AddMikroTikServerTrafficSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MikroTikServerTrafficSamples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    MikroTikServerId = table.Column<int>(type: "int", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    InterfaceCount = table.Column<int>(type: "int", nullable: false),
                    RxBps = table.Column<double>(type: "float", nullable: false),
                    TxBps = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MikroTikServerTrafficSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MikroTikServerTrafficSamples_MikroTikServers_MikroTikServerId",
                        column: x => x.MikroTikServerId,
                        principalTable: "MikroTikServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MikroTikServerTrafficSamples_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MikroTikServerTrafficSamples_MikroTikServerId_CapturedAtUtc",
                table: "MikroTikServerTrafficSamples",
                columns: new[] { "MikroTikServerId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MikroTikServerTrafficSamples_NetworkId_CapturedAtUtc",
                table: "MikroTikServerTrafficSamples",
                columns: new[] { "NetworkId", "CapturedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MikroTikServerTrafficSamples");
        }
    }
}
