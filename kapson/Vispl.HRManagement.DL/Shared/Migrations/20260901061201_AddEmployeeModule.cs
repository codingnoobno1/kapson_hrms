using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "employee");

            migrationBuilder.CreateTable(
                name: "EmployeeChangeRequests",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Section = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SubmittedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeChangeRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeContacts",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeContacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDependants",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RelationCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    IsBenefitEligible = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDependants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LegalFirstName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LegalMiddleName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LegalLastName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PreferredName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    GenderCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MaritalStatusCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    NationalityCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    JoinedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EmploymentTypeCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EmploymentStatusCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GradeCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    ProbationEndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DivisionCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DepartmentCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LocationCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ManagerEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    CostCentre = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AccountId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CandidateId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeTimeline",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTimeline", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeChangeRequests_TenantKey_EmployeeId",
                schema: "employee",
                table: "EmployeeChangeRequests",
                columns: new[] { "TenantKey", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeChangeRequests_TenantKey_Status",
                schema: "employee",
                table: "EmployeeChangeRequests",
                columns: new[] { "TenantKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeContacts_TenantKey_EmployeeId",
                schema: "employee",
                table: "EmployeeContacts",
                columns: new[] { "TenantKey", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependants_TenantKey_EmployeeId",
                schema: "employee",
                table: "EmployeeDependants",
                columns: new[] { "TenantKey", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CandidateId",
                schema: "employee",
                table: "Employees",
                column: "CandidateId",
                filter: "[CandidateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantKey_DepartmentCode",
                schema: "employee",
                table: "Employees",
                columns: new[] { "TenantKey", "DepartmentCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantKey_EmployeeNumber",
                schema: "employee",
                table: "Employees",
                columns: new[] { "TenantKey", "EmployeeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantKey_EmploymentStatusCode",
                schema: "employee",
                table: "Employees",
                columns: new[] { "TenantKey", "EmploymentStatusCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantKey_LocationCode",
                schema: "employee",
                table: "Employees",
                columns: new[] { "TenantKey", "LocationCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantKey_ManagerEmployeeId",
                schema: "employee",
                table: "Employees",
                columns: new[] { "TenantKey", "ManagerEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTimeline_TenantKey_EmployeeId_OccurredUtc",
                schema: "employee",
                table: "EmployeeTimeline",
                columns: new[] { "TenantKey", "EmployeeId", "OccurredUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeChangeRequests",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "EmployeeContacts",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "EmployeeDependants",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "EmployeeTimeline",
                schema: "employee");
        }
    }
}
