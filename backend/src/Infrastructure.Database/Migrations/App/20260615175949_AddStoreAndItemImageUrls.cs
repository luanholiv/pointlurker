using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddStoreAndItemImageUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "watched_stores",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PreviewUrl",
                table: "store_items",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "store_items",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AvatarUrl", table: "watched_stores");

            migrationBuilder.DropColumn(name: "PreviewUrl", table: "store_items");

            migrationBuilder.DropColumn(name: "ThumbnailUrl", table: "store_items");
        }
    }
}
