using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.App
{
    /// <inheritdoc />
    public partial class HardenRedemptionInputData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "InputSnapshot", table: "redemptions");

            migrationBuilder.AlterColumn<string>(
                name: "RedemptionMessage",
                table: "wishlist_entries",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RedemptionMessage",
                table: "wishlist_entries",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "InputSnapshot",
                table: "redemptions",
                type: "text",
                nullable: false,
                defaultValue: ""
            );
        }
    }
}
