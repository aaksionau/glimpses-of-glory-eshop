using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlimpsesOfGlory.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress_Phone",
                table: "PendingCheckouts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress_Phone",
                table: "Orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingAddress_Phone",
                table: "PendingCheckouts");

            migrationBuilder.DropColumn(
                name: "ShippingAddress_Phone",
                table: "Orders");
        }
    }
}
