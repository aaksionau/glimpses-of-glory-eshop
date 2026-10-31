using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlimpsesOfGlory.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddReadyForPickupStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadyForPickupAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            // Pickup orders (FulfillmentMethod 1) used Shipped (1) to mean ready for pickup, and
            // ShippedAt for when they got there. Move them onto ReadyForPickup (4) and its own
            // timestamp - including orders already picked up, so their timeline keeps that step.
            migrationBuilder.Sql("""
                UPDATE "Orders" SET "ReadyForPickupAt" = "ShippedAt", "ShippedAt" = NULL
                WHERE "FulfillmentMethod" = 1 AND "ShippedAt" IS NOT NULL;
                UPDATE "Orders" SET "Status" = 4
                WHERE "FulfillmentMethod" = 1 AND "Status" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Orders" SET "Status" = 1
                WHERE "FulfillmentMethod" = 1 AND "Status" = 4;
                UPDATE "Orders" SET "ShippedAt" = "ReadyForPickupAt"
                WHERE "FulfillmentMethod" = 1;
                """);

            migrationBuilder.DropColumn(
                name: "ReadyForPickupAt",
                table: "Orders");
        }
    }
}
