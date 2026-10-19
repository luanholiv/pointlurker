using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.Identity
{
    /// <inheritdoc />
    public partial class DropUserStreamElementsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Contract step: the AspNetUsers columns were a write-only mirror of the StreamElements
            // ProviderCredential. The mirror only covered rollback. A token pasted while rolled back
            // to a release that predates credentials reached these columns alone, so carry any such
            // newer token across before dropping them. During the dual write both sides were
            // stamped with the same instant, so "newer" singles out exactly those rolled-back
            // writes. Provider 0 is StoreProvider.StreamElements.
            migrationBuilder.Sql(
                """
                INSERT INTO "ProviderCredentials"
                    ("UserId", "Provider", "EncryptedSecret", "SecretSetAt", "AccountName", "AccountNameRefreshedAt")
                SELECT
                    u."Id",
                    0,
                    u."StreamElementsToken",
                    COALESCE(u."StreamElementsTokenSetAt", now()),
                    NULLIF(btrim(u."StreamElementsUsername"), ''),
                    CASE WHEN NULLIF(btrim(u."StreamElementsUsername"), '') IS NULL THEN NULL
                         ELSE u."StreamElementsUsernameRefreshedAt" END
                FROM "AspNetUsers" u
                LEFT JOIN "ProviderCredentials" c ON c."UserId" = u."Id" AND c."Provider" = 0
                WHERE NULLIF(btrim(u."StreamElementsToken"), '') IS NOT NULL
                  AND (c."UserId" IS NULL OR u."StreamElementsTokenSetAt" > c."SecretSetAt")
                ON CONFLICT ("UserId", "Provider") DO UPDATE SET
                    "EncryptedSecret" = EXCLUDED."EncryptedSecret",
                    "SecretSetAt" = EXCLUDED."SecretSetAt",
                    "AccountName" = COALESCE(EXCLUDED."AccountName", "ProviderCredentials"."AccountName"),
                    "AccountNameRefreshedAt" = COALESCE(EXCLUDED."AccountNameRefreshedAt", "ProviderCredentials"."AccountNameRefreshedAt"),
                    "LastValidatedAt" = NULL,
                    "LastValidationError" = NULL;
                """
            );

            migrationBuilder.DropColumn(name: "StreamElementsToken", table: "AspNetUsers");

            migrationBuilder.DropColumn(name: "StreamElementsTokenSetAt", table: "AspNetUsers");

            migrationBuilder.DropColumn(name: "StreamElementsUsername", table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StreamElementsUsernameRefreshedAt",
                table: "AspNetUsers"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the columns empty: the dropped values are gone. Rolling back past this
            // migration needs a database restore, not this.
            migrationBuilder.AddColumn<string>(
                name: "StreamElementsToken",
                table: "AspNetUsers",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StreamElementsTokenSetAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "StreamElementsUsername",
                table: "AspNetUsers",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StreamElementsUsernameRefreshedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true
            );
        }
    }
}
