using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlimpsesOfGlory.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingCheckoutReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemindedAt",
                table: "PendingCheckouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestoreToken",
                table: "PendingCheckouts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingCheckouts_RestoreToken",
                table: "PendingCheckouts",
                column: "RestoreToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingCheckouts_RestoreToken",
                table: "PendingCheckouts");

            migrationBuilder.DropColumn(
                name: "RemindedAt",
                table: "PendingCheckouts");

            migrationBuilder.DropColumn(
                name: "RestoreToken",
                table: "PendingCheckouts");
        }
    }
}
