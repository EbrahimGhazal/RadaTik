using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadaTik.Migrations
{
    /// <inheritdoc />
    public partial class AddVipSubscriberFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VipDiscountPercent",
                table: "Networks",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VipGraceDays",
                table: "Networks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "VipSkipAutoDisable",
                table: "Networks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVip",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VipNote",
                table: "Clients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VipSince",
                table: "Clients",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VipDiscountPercent",
                table: "Networks");

            migrationBuilder.DropColumn(
                name: "VipGraceDays",
                table: "Networks");

            migrationBuilder.DropColumn(
                name: "VipSkipAutoDisable",
                table: "Networks");

            migrationBuilder.DropColumn(
                name: "IsVip",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "VipNote",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "VipSince",
                table: "Clients");
        }
    }
}
