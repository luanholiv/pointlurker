using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddRedemptionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StreamElementsRedemptionId",
                table: "redemptions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "redemptions",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAt",
                table: "redemptions",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SECompletedAt",
                table: "redemptions",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "redemptions",
                type: "timestamp with time zone",
                nullable: true
            );

            // Migrate existing status values
            migrationBuilder.Sql(
                """
                UPDATE redemptions
                SET "Status" = 'Completed',
                    "SubmittedAt" = "AttemptedAt",
                    "SECompletedAt" = "AttemptedAt",
                    "Attempts" = 1
                WHERE "Status" = 'Succeeded';
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE redemptions
                SET "Status" = 'SubmissionFailed',
                    "Attempts" = 1
                WHERE "Status" = 'Failed';
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_redemptions_status_next_retry",
                table: "redemptions",
                columns: new[] { "Status", "NextRetryAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_redemptions_status_submitted",
                table: "redemptions",
                columns: new[] { "Status", "SubmittedAt" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_redemptions_status_next_retry",
                table: "redemptions"
            );

            migrationBuilder.DropIndex(
                name: "IX_redemptions_status_submitted",
                table: "redemptions"
            );

            migrationBuilder.DropColumn(name: "Attempts", table: "redemptions");

            migrationBuilder.DropColumn(name: "NextRetryAt", table: "redemptions");

            migrationBuilder.DropColumn(name: "SECompletedAt", table: "redemptions");

            migrationBuilder.DropColumn(name: "SubmittedAt", table: "redemptions");

            migrationBuilder.AlterColumn<string>(
                name: "StreamElementsRedemptionId",
                table: "redemptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true
            );
        }
    }
}
