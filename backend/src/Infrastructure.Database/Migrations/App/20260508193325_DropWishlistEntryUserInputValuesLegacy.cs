using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class DropWishlistEntryUserInputValuesLegacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE wishlist_entries DROP COLUMN IF EXISTS user_input_values_legacy;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort schema rollback only. Original row data is unrecoverable
            // because the encrypted store profile is now the authoritative source.
            migrationBuilder.Sql(
                @"ALTER TABLE wishlist_entries ADD COLUMN IF NOT EXISTS user_input_values_legacy jsonb NOT NULL DEFAULT '[]';"
            );
        }
    }
}
