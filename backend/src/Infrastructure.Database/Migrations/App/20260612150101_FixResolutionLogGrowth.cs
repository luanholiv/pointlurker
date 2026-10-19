using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class FixResolutionLogGrowth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the never-read column first. In PostgreSQL this is a
            // metadata-only operation (no table rewrite), so it is instant
            // even on a multi-million-row table, and it keeps the SELECT *
            // dedup below aligned with the final table shape.
            migrationBuilder.DropColumn(name: "StoreItemId", table: "store_input_resolution_log");

            // Build the lookup index before the dedup. The index build is the
            // single sort of the table; the DISTINCT ON below then reads the
            // newest row per (StoreId, RawLabel) via an ordered index scan
            // instead of sorting the whole wide table a second time, which on a
            // production-sized table blows past the command timeout.
            migrationBuilder.CreateIndex(
                name: "IX_store_input_resolution_log_StoreId_RawLabel_ObservedAt",
                table: "store_input_resolution_log",
                columns: new[] { "StoreId", "RawLabel", "ObservedAt" }
            );

            // One-time backlog cleanup: the table accumulated one row per
            // item x label per sync tick (millions of rows). Transition-only
            // logging needs only the newest row per (StoreId, RawLabel).
            // Snapshot the survivors and truncate instead of deleting in
            // place: a mass DELETE leaves the table full of dead tuples,
            // while truncate + reinsert reclaims the disk space immediately.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE resolution_log_keep AS
                SELECT DISTINCT ON ("StoreId", "RawLabel") *
                FROM store_input_resolution_log
                ORDER BY "StoreId", "RawLabel", "ObservedAt" DESC;

                TRUNCATE store_input_resolution_log;

                INSERT INTO store_input_resolution_log
                SELECT * FROM resolution_log_keep;

                DROP TABLE resolution_log_keep;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_store_input_resolution_log_StoreId_RawLabel_ObservedAt",
                table: "store_input_resolution_log"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "StoreItemId",
                table: "store_input_resolution_log",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );
        }
    }
}
