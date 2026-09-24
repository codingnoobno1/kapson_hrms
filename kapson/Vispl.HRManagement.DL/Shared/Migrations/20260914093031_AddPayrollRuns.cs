using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payroll");

            migrationBuilder.CreateTable(
                name: "BankTransferBatches",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CompanyBankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TransferMode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployeeCount = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransferBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLines",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    DivisionCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DepartmentCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    GradeCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ShiftCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    WorkedDays = table.Column<int>(type: "int", nullable: false),
                    ExpectedDays = table.Column<int>(type: "int", nullable: false),
                    PayableDays = table.Column<int>(type: "int", nullable: false),
                    DaysInMonth = table.Column<int>(type: "int", nullable: false),
                    Basic = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Hra = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Conveyance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherAllowances = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Gross = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Pf = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Esi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Tds = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Deductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPayable = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BreakdownJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TransferBatchId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    SalaryType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GeneratedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ApprovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransferBatches_TenantKey_ReferenceNo",
                schema: "payroll",
                table: "BankTransferBatches",
                columns: new[] { "TenantKey", "ReferenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransferBatches_TenantKey_RunId",
                schema: "payroll",
                table: "BankTransferBatches",
                columns: new[] { "TenantKey", "RunId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_TenantKey_RunId_EmployeeId",
                schema: "payroll",
                table: "PayrollLines",
                columns: new[] { "TenantKey", "RunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_TenantKey_RunId_Status",
                schema: "payroll",
                table: "PayrollLines",
                columns: new[] { "TenantKey", "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantKey_Year_Month_SalaryType",
                schema: "payroll",
                table: "PayrollRuns",
                columns: new[] { "TenantKey", "Year", "Month", "SalaryType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTransferBatches",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "PayrollLines",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "PayrollRuns",
                schema: "payroll");
        }
    }
}
