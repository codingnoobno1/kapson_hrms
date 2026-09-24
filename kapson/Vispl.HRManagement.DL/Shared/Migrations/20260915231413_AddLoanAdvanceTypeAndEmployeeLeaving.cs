using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanAdvanceTypeAndEmployeeLeaving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetirementAgeYears",
                schema: "platform",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 58);

            migrationBuilder.AddColumn<string>(
                name: "AdvanceTypeCode",
                schema: "payroll",
                table: "Loans",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LeavingOn",
                schema: "employee",
                table: "Employees",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetirementAgeYears",
                schema: "platform",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AdvanceTypeCode",
                schema: "payroll",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "LeavingOn",
                schema: "employee",
                table: "Employees");
        }
    }
}
