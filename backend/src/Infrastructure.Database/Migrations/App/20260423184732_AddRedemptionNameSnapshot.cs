using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddRedemptionNameSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StoreItemName",
                table: "redemptions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "StoreName",
                table: "redemptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.Sql(
                """
                UPDATE redemptions r
                SET "StoreItemName" = COALESCE(si."Name", ''),
                    "StoreName"     = COALESCE(ws."DisplayName", '')
                FROM store_items si
                LEFT JOIN watched_stores ws ON ws."Id" = si."WatchedStoreId"
                WHERE si."Id" = r."StoreItemId";
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "StoreItemName", table: "redemptions");

            migrationBuilder.DropColumn(name: "StoreName", table: "redemptions");
        }
    }
}
