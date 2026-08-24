using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadaTik.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyDocumentAppearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyDocumentAppearances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    HeaderLayout = table.Column<int>(type: "int", nullable: false),
                    ShowLogo = table.Column<bool>(type: "bit", nullable: false),
                    UseNetworkLogo = table.Column<bool>(type: "bit", nullable: false),
                    CustomLogoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrimaryColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    TableHeaderColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    WatermarkMode = table.Column<int>(type: "int", nullable: false),
                    WatermarkText = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    WatermarkOpacityPercent = table.Column<int>(type: "int", nullable: false),
                    TableDensity = table.Column<int>(type: "int", nullable: false),
                    StripedRows = table.Column<bool>(type: "bit", nullable: false),
                    FooterText = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ShowGeneratedAt = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDocumentAppearances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyDocumentAppearances_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CompanyDocumentAppearances_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDocumentAppearances_CompanyNetworkId",
                table: "CompanyDocumentAppearances",
                column: "CompanyNetworkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDocumentAppearances_UpdatedByUserId",
                table: "CompanyDocumentAppearances",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyDocumentAppearances");
        }
    }
}
