using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class RenameStoreIdentityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Contract step for AddStoreProvider: renames only. The release before this one maps its
            // properties to the old names, so rolling back past here needs a database restore.
            migrationBuilder.RenameColumn(
                name: "ChannelId",
                table: "watched_stores",
                newName: "ExternalId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_watched_stores_UserId_Provider_ChannelId",
                table: "watched_stores",
                newName: "IX_watched_stores_UserId_Provider_ExternalId"
            );

            migrationBuilder.RenameColumn(
                name: "StreamElementsId",
                table: "store_items",
                newName: "ProviderItemId"
            );

            migrationBuilder.RenameColumn(
                name: "SeUpdatedAt",
                table: "store_items",
                newName: "ProviderUpdatedAt"
            );

            migrationBuilder.RenameColumn(
                name: "SeCreatedAt",
                table: "store_items",
                newName: "ProviderCreatedAt"
            );

            migrationBuilder.RenameColumn(
                name: "ChannelId",
                table: "store_items",
                newName: "StoreExternalId"
            );

            migrationBuilder.RenameColumn(
                name: "StreamElementsRedemptionId",
                table: "redemptions",
                newName: "ProviderRedemptionId"
            );

            migrationBuilder.RenameColumn(
                name: "StreamElementsItemId",
                table: "redemptions",
                newName: "ProviderItemId"
            );

            migrationBuilder.RenameColumn(
                name: "ChannelId",
                table: "redemptions",
                newName: "StoreExternalId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExternalId",
                table: "watched_stores",
                newName: "ChannelId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_watched_stores_UserId_Provider_ExternalId",
                table: "watched_stores",
                newName: "IX_watched_stores_UserId_Provider_ChannelId"
            );

            migrationBuilder.RenameColumn(
                name: "StoreExternalId",
                table: "store_items",
                newName: "ChannelId"
            );

            migrationBuilder.RenameColumn(
                name: "ProviderUpdatedAt",
                table: "store_items",
                newName: "SeUpdatedAt"
            );

            migrationBuilder.RenameColumn(
                name: "ProviderItemId",
                table: "store_items",
                newName: "StreamElementsId"
            );

            migrationBuilder.RenameColumn(
                name: "ProviderCreatedAt",
                table: "store_items",
                newName: "SeCreatedAt"
            );

            migrationBuilder.RenameColumn(
                name: "StoreExternalId",
                table: "redemptions",
                newName: "ChannelId"
            );

            migrationBuilder.RenameColumn(
                name: "ProviderRedemptionId",
                table: "redemptions",
                newName: "StreamElementsRedemptionId"
            );

            migrationBuilder.RenameColumn(
                name: "ProviderItemId",
                table: "redemptions",
                newName: "StreamElementsItemId"
            );
        }
    }
}
