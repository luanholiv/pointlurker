using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class MakeItemQuantityQueryable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add nullable, backfill, verify, tighten, then drop. The scaffolded order dropped the
            // jsonb column before the integer columns existed, which would have discarded the data.
            migrationBuilder.AddColumn<int>(
                name: "QuantityTotal",
                table: "store_items",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "QuantityCurrent",
                table: "store_items",
                type: "integer",
                nullable: true
            );

            // The payload was written with JsonSerializerDefaults.Web, so its keys are camelCase.
            // Reading ->>'Total' would yield NULL rather than fail — the verification below is what
            // turns that silent miss into a loud one.
            migrationBuilder.Sql(
                """
                UPDATE store_items
                SET "QuantityTotal" = ("Quantity" ->> 'total')::int,
                    "QuantityCurrent" = ("Quantity" ->> 'current')::int;
                """
            );

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE unmigrated bigint;
                BEGIN
                    SELECT count(*) INTO unmigrated FROM store_items
                    WHERE "QuantityTotal" IS NULL OR "QuantityCurrent" IS NULL;

                    IF unmigrated > 0 THEN
                        RAISE EXCEPTION
                            'ItemQuantity backfill left % store_items row(s) unmigrated; refusing to drop the jsonb column',
                            unmigrated;
                    END IF;
                END $$;
                """
            );

            migrationBuilder.AlterColumn<int>(
                name: "QuantityTotal",
                table: "store_items",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<int>(
                name: "QuantityCurrent",
                table: "store_items",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );

            migrationBuilder.DropColumn(name: "Quantity", table: "store_items");

            // Superseded by IX_store_items_WatchedStoreId_CategoryName, which leads on the same column.
            migrationBuilder.DropIndex(name: "IX_store_items_WatchedStoreId", table: "store_items");

            migrationBuilder.CreateIndex(
                name: "IX_store_items_WatchedStoreId_CategoryName",
                table: "store_items",
                columns: new[] { "WatchedStoreId", "CategoryName" }
            );

            // Covers the item grid's default view: one store, availability, then stock. Written as
            // raw SQL because EF cannot declare an index over a member of a complex type, so this
            // one is intentionally absent from the model snapshot.
            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_store_items_WatchedStoreId_Enabled_QuantityCurrent"
                ON store_items ("WatchedStoreId", "Enabled", "QuantityCurrent");
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_store_items_WatchedStoreId_Enabled_QuantityCurrent";
                """
            );

            migrationBuilder.DropIndex(
                name: "IX_store_items_WatchedStoreId_CategoryName",
                table: "store_items"
            );

            migrationBuilder.CreateIndex(
                name: "IX_store_items_WatchedStoreId",
                table: "store_items",
                column: "WatchedStoreId"
            );

            migrationBuilder.AddColumn<string>(
                name: "Quantity",
                table: "store_items",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE store_items
                SET "Quantity" = jsonb_build_object(
                    'total', "QuantityTotal",
                    'current', "QuantityCurrent");
                """
            );

            migrationBuilder.AlterColumn<string>(
                name: "Quantity",
                table: "store_items",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true
            );

            migrationBuilder.DropColumn(name: "QuantityCurrent", table: "store_items");

            migrationBuilder.DropColumn(name: "QuantityTotal", table: "store_items");
        }
    }
}
