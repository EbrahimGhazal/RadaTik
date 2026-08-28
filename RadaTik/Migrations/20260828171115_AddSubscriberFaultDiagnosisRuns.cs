using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadaTik.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriberFaultDiagnosisRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriberFaultDiagnosisRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: true),
                    MaintenanceRequestId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Cause = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    CauseLabel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    SuggestedAction = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SuggestedMaintenanceType = table.Column<int>(type: "int", nullable: true),
                    HasPppSession = table.Column<bool>(type: "bit", nullable: false),
                    HasMikroTikServer = table.Column<bool>(type: "bit", nullable: false),
                    ServerApiReachable = table.Column<bool>(type: "bit", nullable: false),
                    ServerClientCount = table.Column<int>(type: "int", nullable: false),
                    ServerConnectedCount = table.Column<int>(type: "int", nullable: false),
                    SectorClientCount = table.Column<int>(type: "int", nullable: false),
                    SectorConnectedCount = table.Column<int>(type: "int", nullable: false),
                    ReceiverClientCount = table.Column<int>(type: "int", nullable: false),
                    ReceiverConnectedCount = table.Column<int>(type: "int", nullable: false),
                    SectorIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    SectorPingOk = table.Column<bool>(type: "bit", nullable: true),
                    SectorPingMessage = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ReceiverIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ReceiverPingOk = table.Column<bool>(type: "bit", nullable: true),
                    ReceiverPingMessage = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ClientPingOk = table.Column<bool>(type: "bit", nullable: true),
                    ClientPingMessage = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SectorRadioDegraded = table.Column<bool>(type: "bit", nullable: false),
                    SectorNoiseFloorDbm = table.Column<int>(type: "int", nullable: true),
                    SectorSnrDb = table.Column<int>(type: "int", nullable: true),
                    SectorCcqPercent = table.Column<int>(type: "int", nullable: true),
                    RouterPowerOn = table.Column<bool>(type: "bit", nullable: true),
                    InternetLedOn = table.Column<bool>(type: "bit", nullable: true),
                    WanLedOn = table.Column<bool>(type: "bit", nullable: true),
                    NeighborsOnSwitchDown = table.Column<bool>(type: "bit", nullable: true),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfirmedCause = table.Column<int>(type: "int", nullable: true),
                    ConfirmedMaintenanceType = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SuggestionMatched = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberFaultDiagnosisRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriberFaultDiagnosisRuns_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriberFaultDiagnosisRuns_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubscriberFaultDiagnosisRuns_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberFaultDiagnosisRuns_ClientId_CreatedAt",
                table: "SubscriberFaultDiagnosisRuns",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberFaultDiagnosisRuns_MaintenanceRequestId",
                table: "SubscriberFaultDiagnosisRuns",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberFaultDiagnosisRuns_NetworkId",
                table: "SubscriberFaultDiagnosisRuns",
                column: "NetworkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriberFaultDiagnosisRuns");
        }
    }
}
