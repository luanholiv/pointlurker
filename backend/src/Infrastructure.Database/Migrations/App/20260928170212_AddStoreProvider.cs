using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddStoreProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Expand step, additive only, so the release before this one still runs against the
            // result: it never writes Provider, the default files its rows under StreamElements
            // (value 0), and the wider unique index is the old one with that constant prepended.
            // The column renames are the contract step and ship in their own migration.
            migrationBuilder.DropIndex(
                name: "IX_watched_stores_UserId_ChannelId",
                table: "watched_stores"
            );

            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "watched_stores",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SeCreatedAt",
                table: "store_items",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone"
            );

            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "store_items",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "redemptions",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateIndex(
                name: "IX_watched_stores_UserId_Provider_ChannelId",
                table: "watched_stores",
                columns: new[] { "UserId", "Provider", "ChannelId" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_watched_stores_UserId_Provider_ChannelId",
                table: "watched_stores"
            );

            migrationBuilder.DropColumn(name: "Provider", table: "watched_stores");

            migrationBuilder.DropColumn(name: "Provider", table: "store_items");

            migrationBuilder.DropColumn(name: "Provider", table: "redemptions");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SeCreatedAt",
                table: "store_items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(
                    new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    new TimeSpan(0, 0, 0, 0, 0)
                ),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_watched_stores_UserId_ChannelId",
                table: "watched_stores",
                columns: new[] { "UserId", "ChannelId" },
                unique: true
            );
        }
    }
}
