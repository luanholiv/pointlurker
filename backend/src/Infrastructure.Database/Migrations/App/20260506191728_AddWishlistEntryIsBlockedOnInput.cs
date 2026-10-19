using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddWishlistEntryIsBlockedOnInput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // user_input_values_legacy column is intentionally retained for one release for backfill safety.
            // EF does not map it, so add a default so future inserts succeed without the column.
            migrationBuilder.Sql(
                @"ALTER TABLE wishlist_entries ALTER COLUMN user_input_values_legacy SET DEFAULT '[]';"
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsBlockedOnInput",
                table: "wishlist_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsBlockedOnInput", table: "wishlist_entries");

            migrationBuilder.Sql(
                @"ALTER TABLE wishlist_entries ALTER COLUMN user_input_values_legacy DROP DEFAULT;"
            );
        }
    }
}
