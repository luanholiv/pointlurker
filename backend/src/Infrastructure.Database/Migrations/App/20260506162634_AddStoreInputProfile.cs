using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddStoreInputProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserInputValues",
                table: "wishlist_entries",
                newName: "user_input_values_legacy"
            );

            migrationBuilder.AddColumn<string>(
                name: "input_aliases",
                table: "watched_stores",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "input_values",
                table: "watched_stores",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.CreateTable(
                name: "store_input_resolution_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawLabel = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: false
                    ),
                    CanonicalLabel = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                    Confidence = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_input_resolution_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_input_resolution_log_watched_stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "watched_stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_store_input_resolution_log_StoreId_ObservedAt",
                table: "store_input_resolution_log",
                columns: new[] { "StoreId", "ObservedAt" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "store_input_resolution_log");

            migrationBuilder.DropColumn(name: "input_aliases", table: "watched_stores");

            migrationBuilder.DropColumn(name: "input_values", table: "watched_stores");

            migrationBuilder.RenameColumn(
                name: "user_input_values_legacy",
                table: "wishlist_entries",
                newName: "UserInputValues"
            );
        }
    }
}
