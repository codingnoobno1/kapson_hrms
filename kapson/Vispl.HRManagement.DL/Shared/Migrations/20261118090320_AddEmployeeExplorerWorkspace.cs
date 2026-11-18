using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeExplorerWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EpfAccountCode",
                schema: "employee",
                table: "EmployeeStatutory",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EsiAccountCode",
                schema: "employee",
                table: "EmployeeStatutory",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodGroupCode",
                schema: "employee",
                table: "Employees",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeFromPayroll",
                schema: "employee",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FatherName",
                schema: "employee",
                table: "Employees",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherOccupationCode",
                schema: "employee",
                table: "Employees",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                schema: "employee",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MotherName",
                schema: "employee",
                table: "Employees",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherOccupationCode",
                schema: "employee",
                table: "Employees",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherReligion",
                schema: "employee",
                table: "Employees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReligionCode",
                schema: "employee",
                table: "Employees",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpouseName",
                schema: "employee",
                table: "Employees",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpouseOccupationCode",
                schema: "employee",
                table: "Employees",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubDepartmentCode",
                schema: "employee",
                table: "Employees",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificationName",
                schema: "employee",
                table: "EmployeeEducation",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CertificationValidTill",
                schema: "employee",
                table: "EmployeeEducation",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdvancedAccountCode",
                schema: "employee",
                table: "EmployeeBankAccounts",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                schema: "employee",
                table: "EmployeeBankAccounts",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalaryAccountCode",
                schema: "employee",
                table: "EmployeeBankAccounts",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalaryFormat",
                schema: "employee",
                table: "EmployeeBankAccounts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferSalarySlipBy",
                schema: "employee",
                table: "EmployeeBankAccounts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeDocuments",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MediaAssetId = table.Column<long>(type: "bigint", nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    PublicUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLeavingDetails",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    HasLeftOrganization = table.Column<bool>(type: "bit", nullable: false),
                    ResignationSubmitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FinalSettlementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LeavingReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LeavingRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDismissed = table.Column<bool>(type: "bit", nullable: false),
                    DismissDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DismissRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SettlementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeavingDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLeavingDetails_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSalaryConfigs",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApplyMaxLimit = table.Column<bool>(type: "bit", nullable: false),
                    EpfEmployerShareRule = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EpfOnBasicOnly = table.Column<bool>(type: "bit", nullable: false),
                    ComponentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSalaryConfigs", x => x.Id);
                    table.CheckConstraint("CK_EmployeeSalaryConfigs_Basic", "[BasicSalary] >= 0");
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryConfigs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId",
                schema: "employee",
                table: "EmployeeDocuments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_TenantKey_EmployeeId",
                schema: "employee",
                table: "EmployeeDocuments",
                columns: new[] { "TenantKey", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeavingDetails_EmployeeId",
                schema: "employee",
                table: "EmployeeLeavingDetails",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeavingDetails_TenantKey_EmployeeId",
                schema: "employee",
                table: "EmployeeLeavingDetails",
                columns: new[] { "TenantKey", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryConfigs_EmployeeId",
                schema: "employee",
                table: "EmployeeSalaryConfigs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryConfigs_TenantKey_EmployeeId",
                schema: "employee",
                table: "EmployeeSalaryConfigs",
                columns: new[] { "TenantKey", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeDocuments",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "EmployeeLeavingDetails",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "EmployeeSalaryConfigs",
                schema: "employee");

            migrationBuilder.DropColumn(
                name: "EpfAccountCode",
                schema: "employee",
                table: "EmployeeStatutory");

            migrationBuilder.DropColumn(
                name: "EsiAccountCode",
                schema: "employee",
                table: "EmployeeStatutory");

            migrationBuilder.DropColumn(
                name: "BloodGroupCode",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ExcludeFromPayroll",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FatherName",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FatherOccupationCode",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "MotherName",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "MotherOccupationCode",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "OtherReligion",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ReligionCode",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SpouseName",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SpouseOccupationCode",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SubDepartmentCode",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CertificationName",
                schema: "employee",
                table: "EmployeeEducation");

            migrationBuilder.DropColumn(
                name: "CertificationValidTill",
                schema: "employee",
                table: "EmployeeEducation");

            migrationBuilder.DropColumn(
                name: "AdvancedAccountCode",
                schema: "employee",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropColumn(
                name: "BranchName",
                schema: "employee",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropColumn(
                name: "SalaryAccountCode",
                schema: "employee",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropColumn(
                name: "SalaryFormat",
                schema: "employee",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropColumn(
                name: "TransferSalarySlipBy",
                schema: "employee",
                table: "EmployeeBankAccounts");
        }
    }
}
