using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlimpsesOfGlory.Core.Migrations
{
    /// <inheritdoc />
    public partial class DropStaleTotalColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Total" was originally a real NOT NULL column on both tables. A later change made it
            // a computed property (Subtotal + ShippingCost) mapped with Ignore(), but the migration
            // that had already run against deployed databases was edited in place instead of being
            // superseded by a new one - so those databases still have the stale column, which now
            // fails every insert since EF never populates it. IF EXISTS makes this safe on fresh
            // databases too, where the column was never created.
            migrationBuilder.Sql("ALTER TABLE \"Orders\" DROP COLUMN IF EXISTS \"Total\";");
            migrationBuilder.Sql("ALTER TABLE \"PendingCheckouts\" DROP COLUMN IF EXISTS \"Total\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Orders\" ADD COLUMN \"Total\" numeric(10,2) NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE \"PendingCheckouts\" ADD COLUMN \"Total\" numeric(10,2) NOT NULL DEFAULT 0;");
        }
    }
}
