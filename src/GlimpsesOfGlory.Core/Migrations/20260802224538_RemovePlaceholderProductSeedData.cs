using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GlimpsesOfGlory.Core.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlaceholderProductSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ProductPhotos",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ProductPhotos",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ProductPhotos",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ProductPhotos",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "ProductPhotos",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "ProductPhotos",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Description", "IsActive", "Name", "Price", "Slug", "StockQuantity" },
                values: new object[,]
                {
                    { 1, "Placeholder description for sample product one. Replace with real product copy.", true, "Sample Product One", 19.99m, "sample-product-one", 10 },
                    { 2, "Placeholder description for sample product two. Replace with real product copy.", true, "Sample Product Two", 29.99m, "sample-product-two", 5 },
                    { 3, "Placeholder description for sample product three. Replace with real product copy.", true, "Sample Product Three", 39.99m, "sample-product-three", 8 }
                });

            migrationBuilder.InsertData(
                table: "ProductPhotos",
                columns: new[] { "Id", "DisplayOrder", "FileName", "ProductId" },
                values: new object[,]
                {
                    { 1, 1, "sample-product-one-1.svg", 1 },
                    { 2, 2, "sample-product-one-2.svg", 1 },
                    { 3, 1, "sample-product-two-1.svg", 2 },
                    { 4, 2, "sample-product-two-2.svg", 2 },
                    { 5, 1, "sample-product-three-1.svg", 3 },
                    { 6, 2, "sample-product-three-2.svg", 3 }
                });
        }
    }
}
