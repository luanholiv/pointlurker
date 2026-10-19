using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddWishlistRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_wishlist_entries_StoreItemId",
                table: "wishlist_entries",
                column: "StoreItemId",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_wishlist_entries_store_items_StoreItemId",
                table: "wishlist_entries",
                column: "StoreItemId",
                principalTable: "store_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_wishlist_entries_store_items_StoreItemId",
                table: "wishlist_entries"
            );

            migrationBuilder.DropIndex(
                name: "IX_wishlist_entries_StoreItemId",
                table: "wishlist_entries"
            );
        }
    }
}
