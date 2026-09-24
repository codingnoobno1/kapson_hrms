using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddLeavePolicyTone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Emoji",
                schema: "workforce",
                table: "LeavePolicies",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "🗓️");

            migrationBuilder.AddColumn<string>(
                name: "ToneKey",
                schema: "workforce",
                table: "LeavePolicies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "blue");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Emoji",
                schema: "workforce",
                table: "LeavePolicies");

            migrationBuilder.DropColumn(
                name: "ToneKey",
                schema: "workforce",
                table: "LeavePolicies");
        }
    }
}
