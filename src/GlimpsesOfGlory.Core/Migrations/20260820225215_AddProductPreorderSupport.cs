using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlimpsesOfGlory.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPreorderSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpectedAvailabilityDate",
                table: "Products",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPreorder",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PreorderedQuantity",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpectedAvailabilityDate",
                table: "PendingCheckoutLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPreorder",
                table: "PendingCheckoutLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpectedAvailabilityDate",
                table: "OrderLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPreorder",
                table: "OrderLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedAvailabilityDate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsPreorder",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PreorderedQuantity",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ExpectedAvailabilityDate",
                table: "PendingCheckoutLines");

            migrationBuilder.DropColumn(
                name: "IsPreorder",
                table: "PendingCheckoutLines");

            migrationBuilder.DropColumn(
                name: "ExpectedAvailabilityDate",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "IsPreorder",
                table: "OrderLines");
        }
    }
}
