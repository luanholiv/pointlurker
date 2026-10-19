using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddFulfillmentCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fulfillment_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RedemptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EncryptedCode = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    SentAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NextRetryAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    LastError = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fulfillment_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fulfillment_codes_redemptions_RedemptionId",
                        column: x => x.RedemptionId,
                        principalTable: "redemptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_fulfillment_codes_RedemptionId",
                table: "fulfillment_codes",
                column: "RedemptionId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_fulfillment_codes_undelivered_next_retry",
                table: "fulfillment_codes",
                column: "NextRetryAt",
                filter: "\"SentAt\" IS NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fulfillment_codes");
        }
    }
}
