using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class RenameRedemptionSettlementNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SECompletedAt",
                table: "redemptions",
                newName: "SettledAt"
            );

            // Status is stored as the enum member name, so the renamed members need their rows rewritten.
            migrationBuilder.Sql(
                """
                UPDATE redemptions SET "Status" = 'CompletionTimedOut' WHERE "Status" = 'SECompletionTimedOut';
                UPDATE redemptions SET "Status" = 'Rejected' WHERE "Status" = 'SERejected';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE redemptions SET "Status" = 'SECompletionTimedOut' WHERE "Status" = 'CompletionTimedOut';
                UPDATE redemptions SET "Status" = 'SERejected' WHERE "Status" = 'Rejected';
                """
            );

            migrationBuilder.RenameColumn(
                name: "SettledAt",
                table: "redemptions",
                newName: "SECompletedAt"
            );
        }
    }
}
