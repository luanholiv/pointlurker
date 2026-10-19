using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddSeUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "StreamElementsUsername", table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StreamElementsUsernameRefreshedAt",
                table: "AspNetUsers"
            );
        }
    }
}
