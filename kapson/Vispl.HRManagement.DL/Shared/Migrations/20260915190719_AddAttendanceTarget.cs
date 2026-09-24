using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttendanceTargetPercent",
                schema: "platform",
                table: "EmployeeAppSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttendanceTargetPercent",
                schema: "platform",
                table: "EmployeeAppSettings");
        }
    }
}
