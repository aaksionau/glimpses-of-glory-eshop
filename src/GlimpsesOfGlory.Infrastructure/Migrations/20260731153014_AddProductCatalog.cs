using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GlimpsesOfGlory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    StockQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPhotos_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Description", "Name", "Price", "Slug", "StockQuantity" },
                values: new object[,]
                {
                    { 1, "Placeholder description for sample product one. Replace with real product copy.", "Sample Product One", 19.99m, "sample-product-one", 10 },
                    { 2, "Placeholder description for sample product two. Replace with real product copy.", "Sample Product Two", 29.99m, "sample-product-two", 5 },
                    { 3, "Placeholder description for sample product three. Replace with real product copy.", "Sample Product Three", 39.99m, "sample-product-three", 8 }
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

            migrationBuilder.CreateIndex(
                name: "IX_ProductPhotos_ProductId",
                table: "ProductPhotos",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductPhotos");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
