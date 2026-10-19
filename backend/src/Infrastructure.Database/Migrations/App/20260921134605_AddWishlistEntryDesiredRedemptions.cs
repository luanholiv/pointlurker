using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddWishlistEntryDesiredRedemptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DesiredRedemptions",
                table: "wishlist_entries",
                type: "integer",
                nullable: true
            );

            // Existing auto-redeem entries redeem without bound; null would carry that forward,
            // because null is unlimited here. They are backfilled to a single redemption instead:
            // this column authorises spending, so it fails closed, and anyone who wants the old
            // behaviour asks for it.
            migrationBuilder.Sql("""UPDATE wishlist_entries SET "DesiredRedemptions" = 1;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DesiredRedemptions", table: "wishlist_entries");
        }
    }
}
