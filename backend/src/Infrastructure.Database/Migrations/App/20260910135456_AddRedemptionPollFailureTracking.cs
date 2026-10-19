using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class AddRedemptionPollFailureTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastPollError",
                table: "redemptions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "PollFailures",
                table: "redemptions",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LastPollError", table: "redemptions");

            migrationBuilder.DropColumn(name: "PollFailures", table: "redemptions");
        }
    }
}
