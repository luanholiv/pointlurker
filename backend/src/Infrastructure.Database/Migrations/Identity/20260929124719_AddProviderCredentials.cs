using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddProviderCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderCredentials",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    EncryptedSecret = table.Column<string>(
                        type: "character varying(4096)",
                        maxLength: 4096,
                        nullable: false
                    ),
                    SecretSetAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    AccountName = table.Column<string>(type: "text", nullable: true),
                    AccountNameRefreshedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    LastValidatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    LastValidationError = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderCredentials", x => new { x.UserId, x.Provider });
                    table.ForeignKey(
                        name: "FK_ProviderCredentials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            // Expand step: the StreamElements token and username are copied, not moved. The
            // AspNetUsers columns stay, and the application keeps writing them too, so the previous
            // release still finds a current token there after a rollback. Provider 0 is
            // StoreProvider.StreamElements. The ciphertext copies verbatim: it is protected under the
            // same purpose on both sides. Nothing here has ever been validated, so LastValidatedAt
            // stays null until the next account refresh.
            migrationBuilder.Sql(
                """
                INSERT INTO "ProviderCredentials"
                    ("UserId", "Provider", "EncryptedSecret", "SecretSetAt", "AccountName", "AccountNameRefreshedAt")
                SELECT
                    "Id",
                    0,
                    "StreamElementsToken",
                    COALESCE("StreamElementsTokenSetAt", now()),
                    NULLIF(btrim("StreamElementsUsername"), ''),
                    CASE WHEN NULLIF(btrim("StreamElementsUsername"), '') IS NULL THEN NULL
                         ELSE "StreamElementsUsernameRefreshedAt" END
                FROM "AspNetUsers"
                WHERE NULLIF(btrim("StreamElementsToken"), '') IS NOT NULL;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProviderCredentials");
        }
    }
}
