using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class RenameItemSearchConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The configuration is raw SQL, absent from the model snapshot. IX_store_items_Search holds
            // the configuration's OID rather than its name, so the index stays valid across the rename.
            migrationBuilder.Sql(
                "ALTER TEXT SEARCH CONFIGURATION se_search RENAME TO item_search;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TEXT SEARCH CONFIGURATION item_search RENAME TO se_search;"
            );
        }
    }
}
