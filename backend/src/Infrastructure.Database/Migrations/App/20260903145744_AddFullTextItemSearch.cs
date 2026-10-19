using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddFullTextItemSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, and left where the scaffolder put it: the configuration below maps through the
            // `unaccent` dictionary, which does not exist until this has run.
            migrationBuilder
                .AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            // `simple` plus `unaccent`, not a language dictionary: item text is streamer-written
            // and mixes languages within one row, so there is no per-row language to stem by, and
            // the same word arrives accented in one row and bare in the next. The full rationale
            // lives on StoreItemTextSearch.Configuration.
            migrationBuilder.Sql(
                """
                CREATE TEXT SEARCH CONFIGURATION se_search (COPY = simple);

                ALTER TEXT SEARCH CONFIGURATION se_search
                    ALTER MAPPING FOR asciiword, asciihword, hword_asciipart,
                                      word, hword, hword_part,
                                      numword, numhword, hword_numpart
                    WITH unaccent, simple;
                """
            );

            // Covers the word-matching arms of the item search. Written as raw SQL because EF
            // cannot declare an index over an expression, so this one is intentionally absent from
            // the model snapshot. The two expressions must read exactly as the repository builds
            // them, or the planner will not recognise them and will silently fall back to a scan.
            // `to_tsvector(regconfig, text)` is immutable even though `se_search` folds accents
            // with a dictionary — that is what permits it in an index at all, where a bare
            // `unaccent(...)` would be rejected.
            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_store_items_Search"
                ON store_items USING GIN (
                    to_tsvector('se_search', "Name"),
                    to_tsvector('se_search', coalesce("Description", ''))
                );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mirrored: the index depends on the configuration and the configuration depends on the
            // dictionary, so both go before the extensions they were built out of.
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_store_items_Search";
                """
            );

            migrationBuilder.Sql(
                """
                DROP TEXT SEARCH CONFIGURATION IF EXISTS se_search;
                """
            );

            migrationBuilder
                .AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");
        }
    }
}
