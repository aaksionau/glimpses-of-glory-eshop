using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GlimpsesOfGlory.Core.Migrations
{
    /// <inheritdoc />
    public partial class PerProductShippingRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAdditionalItemAmount",
                table: "Products",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFirstItemAmount",
                table: "Products",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShippingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstItemAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    AdditionalItemAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ShippingSettings",
                columns: new[] { "Id", "AdditionalItemAmount", "FirstItemAmount" },
                values: new object[] { 1, 1.00m, 5.00m });

            // Keep the store owner's edited base rate: the old lowest tier becomes the first-item rate.
            migrationBuilder.Sql("""
                UPDATE "ShippingSettings"
                SET "FirstItemAmount" = (SELECT "Amount" FROM "ShippingTiers" ORDER BY "MinQuantity" LIMIT 1)
                WHERE "Id" = 1 AND EXISTS (SELECT 1 FROM "ShippingTiers");
                """);

            migrationBuilder.DropTable(
                name: "ShippingTiers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShippingSettings");

            migrationBuilder.DropColumn(
                name: "ShippingAdditionalItemAmount",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShippingFirstItemAmount",
                table: "Products");

            migrationBuilder.CreateTable(
                name: "ShippingTiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    MinQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingTiers", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ShippingTiers",
                columns: new[] { "Id", "Amount", "MinQuantity" },
                values: new object[,]
                {
                    { 1, 5.00m, 1 },
                    { 2, 9.00m, 5 }
                });
        }
    }
}
