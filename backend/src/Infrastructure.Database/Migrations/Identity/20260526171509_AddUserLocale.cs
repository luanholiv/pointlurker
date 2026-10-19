using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddUserLocale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "en-US"
            );

            // CHECK constraint — prevents storing unsupported locale tags.
            // Update this constraint when adding new locales to SupportedLocales.All.
            migrationBuilder.Sql(
                """
                ALTER TABLE "AspNetUsers"
                    ADD CONSTRAINT "CK_AspNetUsers_Locale_Allowlist"
                    CHECK ("Locale" IN ('en-US', 'pt-BR'));
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "AspNetUsers"
                    DROP CONSTRAINT IF EXISTS "CK_AspNetUsers_Locale_Allowlist";
                """
            );

            migrationBuilder.DropColumn(name: "Locale", table: "AspNetUsers");
        }
    }
}
