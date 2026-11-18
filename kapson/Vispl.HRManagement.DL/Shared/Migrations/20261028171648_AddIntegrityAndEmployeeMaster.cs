using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrityAndEmployeeMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeAddresses",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    AddressType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Line1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Line2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CityCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StateCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Pincode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAddresses", x => x.Id);
                    table.CheckConstraint("CK_EmployeeAddresses_Pincode", "[Pincode] IS NULL OR ([Pincode] NOT LIKE '%[^0-9]%' AND LEN([Pincode]) BETWEEN 4 AND 10)");
                    table.CheckConstraint("CK_EmployeeAddresses_Type", "[AddressType] IN ('Permanent', 'Current')");
                    table.ForeignKey(
                        name: "FK_EmployeeAddresses_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeEducation",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    EducationLevelCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    QualificationCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Specialisation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Institute = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BoardOrUniversity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PassingYear = table.Column<int>(type: "int", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Cgpa = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    GradeLetter = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    IsHighest = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeEducation", x => x.Id);
                    table.CheckConstraint("CK_EmployeeEducation_Cgpa", "[Cgpa] IS NULL OR [Cgpa] BETWEEN 0 AND 10");
                    table.CheckConstraint("CK_EmployeeEducation_Percentage", "[Percentage] IS NULL OR [Percentage] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_EmployeeEducation_Year", "[PassingYear] BETWEEN 1950 AND 2100");
                    table.ForeignKey(
                        name: "FK_EmployeeEducation_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePreviousEmployment",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PreviousEmployeeId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    JoinedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    LeftOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EmploymentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LastAnnualCtc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ReportingManagerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LeavingReasonCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    NoticeServed = table.Column<bool>(type: "bit", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePreviousEmployment", x => x.Id);
                    table.CheckConstraint("CK_EmployeePreviousEmployment_Ctc", "[LastAnnualCtc] IS NULL OR [LastAnnualCtc] >= 0");
                    table.CheckConstraint("CK_EmployeePreviousEmployment_Dates", "[LeftOn] >= [JoinedOn]");
                    table.CheckConstraint("CK_EmployeePreviousEmployment_Type", "[EmploymentType] IS NULL OR [EmploymentType] IN ('Permanent', 'Contract', 'Intern', 'Consultant')");
                    table.ForeignKey(
                        name: "FK_EmployeePreviousEmployment_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeStatutory",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    PanNumber = table.Column<string>(type: "nchar(10)", fixedLength: true, maxLength: 10, nullable: true),
                    AadhaarProtected = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AadhaarLast4 = table.Column<string>(type: "nchar(4)", fixedLength: true, maxLength: 4, nullable: true),
                    VoterId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PfApplicable = table.Column<bool>(type: "bit", nullable: false),
                    PfJoinedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PfScheme = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PfNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    UanNumber = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    EsiApplicable = table.Column<bool>(type: "bit", nullable: false),
                    EsiNumber = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeStatutory", x => x.Id);
                    table.CheckConstraint("CK_EmployeeStatutory_AadhaarLast4", "[AadhaarLast4] IS NULL OR [AadhaarLast4] LIKE '[0-9][0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_EmployeeStatutory_Esi", "[EsiNumber] IS NULL OR ([EsiNumber] NOT LIKE '%[^0-9]%' AND LEN([EsiNumber]) = 17)");
                    table.CheckConstraint("CK_EmployeeStatutory_Pan", "[PanNumber] IS NULL OR [PanNumber] LIKE '[A-Z][A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9][A-Z]'");
                    table.CheckConstraint("CK_EmployeeStatutory_PfScheme", "[PfScheme] IS NULL OR [PfScheme] IN ('Govt', 'Company')");
                    table.CheckConstraint("CK_EmployeeStatutory_Uan", "[UanNumber] IS NULL OR ([UanNumber] NOT LIKE '%[^0-9]%' AND LEN([UanNumber]) = 12)");
                    table.ForeignKey(
                        name: "FK_EmployeeStatutory_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_EmployeeId",
                schema: "workforce",
                table: "ShiftAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftDefinitionId",
                schema: "workforce",
                table: "ShiftAssignments",
                column: "ShiftDefinitionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShiftAssignments_Dates",
                schema: "workforce",
                table: "ShiftAssignments",
                sql: "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollRuns_Month",
                schema: "payroll",
                table: "PayrollRuns",
                sql: "[Month] BETWEEN 1 AND 12");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_EmployeeId",
                schema: "payroll",
                table: "PayrollLines",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_RunId",
                schema: "payroll",
                table: "PayrollLines",
                column: "RunId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollLines_Amounts",
                schema: "payroll",
                table: "PayrollLines",
                sql: "[Basic] >= 0 AND [Gross] >= 0 AND [Deductions] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollLines_PayableDays",
                schema: "payroll",
                table: "PayrollLines",
                sql: "[PayableDays] BETWEEN 0 AND [DaysInMonth]");

            migrationBuilder.CreateIndex(
                name: "IX_OnDutyRequests_EmployeeId",
                schema: "workforce",
                table: "OnDutyRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTasks_OnboardingId",
                schema: "recruitment",
                table: "OnboardingTasks",
                column: "OnboardingId");

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_ApplicationId",
                schema: "recruitment",
                table: "Onboardings",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementRecords_EmployeeId",
                schema: "workforce",
                table: "MovementRecords",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_EmployeeId",
                schema: "payroll",
                table: "Loans",
                column: "EmployeeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Loans_Installments",
                schema: "payroll",
                table: "Loans",
                sql: "[Installments] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Loans_Interest",
                schema: "payroll",
                table: "Loans",
                sql: "[InterestPercent] BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Loans_Principal",
                schema: "payroll",
                table: "Loans",
                sql: "[Principal] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Loans_StartMonth",
                schema: "payroll",
                table: "Loans",
                sql: "[StartMonth] BETWEEN 1 AND 12");

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepayments_LoanId",
                schema: "payroll",
                table: "LoanRepayments",
                column: "LoanId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LoanRepayments_Amount",
                schema: "payroll",
                table: "LoanRepayments",
                sql: "[Amount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LoanRepayments_Month",
                schema: "payroll",
                table: "LoanRepayments",
                sql: "[Month] BETWEEN 1 AND 12");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId",
                schema: "workforce",
                table: "LeaveRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_PolicyId",
                schema: "workforce",
                table: "LeaveRequests",
                column: "PolicyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveRequests_Dates",
                schema: "workforce",
                table: "LeaveRequests",
                sql: "[ToDate] >= [FromDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveRequests_Days",
                schema: "workforce",
                table: "LeaveRequests",
                sql: "[Days] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestDays_RequestId",
                schema: "workforce",
                table: "LeaveRequestDays",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_EmployeeId",
                schema: "workforce",
                table: "LeaveBalances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_PolicyId",
                schema: "workforce",
                table: "LeaveBalances",
                column: "PolicyId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JobOpenings_SalaryBand",
                schema: "recruitment",
                table: "JobOpenings",
                sql: "[SalaryMinimum] IS NULL OR [SalaryMaximum] IS NULL OR [SalaryMaximum] >= [SalaryMinimum]");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_ApplicationId",
                schema: "recruitment",
                table: "Interviews",
                column: "ApplicationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GradeSalaryStructures_Basic",
                schema: "platform",
                table: "GradeSalaryStructures",
                sql: "[BasicMonthly] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_GatePassScans_GatePassId",
                schema: "workforce",
                table: "GatePassScans",
                column: "GatePassId");

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_EmployeeId",
                schema: "workforce",
                table: "GatePasses",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTimeline_EmployeeId",
                schema: "employee",
                table: "EmployeeTimeline",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ManagerEmployeeId",
                schema: "employee",
                table: "Employees",
                column: "ManagerEmployeeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_LeavingAfterJoining",
                schema: "employee",
                table: "Employees",
                sql: "[LeavingOn] IS NULL OR [LeavingOn] >= [JoinedOn]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_NotOwnManager",
                schema: "employee",
                table: "Employees",
                sql: "[ManagerEmployeeId] IS NULL OR [ManagerEmployeeId] <> [Id]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_ProbationAfterJoining",
                schema: "employee",
                table: "Employees",
                sql: "[ProbationEndsOn] IS NULL OR [ProbationEndsOn] >= [JoinedOn]");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependants_EmployeeId",
                schema: "employee",
                table: "EmployeeDependants",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeContacts_EmployeeId",
                schema: "employee",
                table: "EmployeeContacts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeChangeRequests_EmployeeId",
                schema: "employee",
                table: "EmployeeChangeRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankAccounts_EmployeeId",
                schema: "employee",
                table: "EmployeeBankAccounts",
                column: "EmployeeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeBankAccounts_Ifsc",
                schema: "employee",
                table: "EmployeeBankAccounts",
                sql: "[Ifsc] LIKE '[A-Z][A-Z][A-Z][A-Z]0[A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeBankAccounts_Last4",
                schema: "employee",
                table: "EmployeeBankAccounts",
                sql: "[AccountLast4] LIKE '[0-9][0-9][0-9][0-9]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompanyBankAccounts_Ifsc",
                schema: "platform",
                table: "CompanyBankAccounts",
                sql: "[Ifsc] LIKE '[A-Z][A-Z][A-Z][A-Z]0[A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompanyBankAccounts_Last4",
                schema: "platform",
                table: "CompanyBankAccounts",
                sql: "[AccountLast4] LIKE '[0-9][0-9][0-9][0-9]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Candidates_Experience",
                schema: "recruitment",
                table: "Candidates",
                sql: "[TotalExperienceYears] BETWEEN 0 AND 60");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunches_EmployeeId",
                schema: "workforce",
                table: "AttendancePunches",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExceptions_EmployeeId",
                schema: "workforce",
                table: "AttendanceExceptions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDays_EmployeeId",
                schema: "workforce",
                table: "AttendanceDays",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_EmployeeId",
                schema: "workforce",
                table: "AttendanceCorrections",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CandidateId",
                schema: "recruitment",
                table: "Applications",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_JobOpeningId",
                schema: "recruitment",
                table: "Applications",
                column: "JobOpeningId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Applications_ExpectedCtc",
                schema: "recruitment",
                table: "Applications",
                sql: "[ExpectedCtc] IS NULL OR [ExpectedCtc] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Applications_Rating",
                schema: "recruitment",
                table: "Applications",
                sql: "[Rating] IS NULL OR [Rating] BETWEEN 0 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_ApplicationId",
                schema: "recruitment",
                table: "ApplicationEvents",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAddresses_EmployeeId",
                schema: "employee",
                table: "EmployeeAddresses",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAddresses_TenantKey_EmployeeId_AddressType",
                schema: "employee",
                table: "EmployeeAddresses",
                columns: new[] { "TenantKey", "EmployeeId", "AddressType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEducation_EmployeeId",
                schema: "employee",
                table: "EmployeeEducation",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEducation_TenantKey_EmployeeId",
                schema: "employee",
                table: "EmployeeEducation",
                columns: new[] { "TenantKey", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePreviousEmployment_EmployeeId",
                schema: "employee",
                table: "EmployeePreviousEmployment",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePreviousEmployment_TenantKey_EmployeeId",
                schema: "employee",
                table: "EmployeePreviousEmployment",
                columns: new[] { "TenantKey", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeStatutory_EmployeeId",
                schema: "employee",
                table: "EmployeeStatutory",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeStatutory_TenantKey_EmployeeId",
                schema: "employee",
                table: "EmployeeStatutory",
                columns: new[] { "TenantKey", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeStatutory_TenantKey_PanNumber",
                schema: "employee",
                table: "EmployeeStatutory",
                columns: new[] { "TenantKey", "PanNumber" },
                unique: true,
                filter: "[PanNumber] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationEvents_Applications_ApplicationId",
                schema: "recruitment",
                table: "ApplicationEvents",
                column: "ApplicationId",
                principalSchema: "recruitment",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Candidates_CandidateId",
                schema: "recruitment",
                table: "Applications",
                column: "CandidateId",
                principalSchema: "recruitment",
                principalTable: "Candidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_JobOpenings_JobOpeningId",
                schema: "recruitment",
                table: "Applications",
                column: "JobOpeningId",
                principalSchema: "recruitment",
                principalTable: "JobOpenings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceCorrections_Employees_EmployeeId",
                schema: "workforce",
                table: "AttendanceCorrections",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceDays_Employees_EmployeeId",
                schema: "workforce",
                table: "AttendanceDays",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceExceptions_Employees_EmployeeId",
                schema: "workforce",
                table: "AttendanceExceptions",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendancePunches_Employees_EmployeeId",
                schema: "workforce",
                table: "AttendancePunches",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeBankAccounts_Employees_EmployeeId",
                schema: "employee",
                table: "EmployeeBankAccounts",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeChangeRequests_Employees_EmployeeId",
                schema: "employee",
                table: "EmployeeChangeRequests",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeContacts_Employees_EmployeeId",
                schema: "employee",
                table: "EmployeeContacts",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDependants_Employees_EmployeeId",
                schema: "employee",
                table: "EmployeeDependants",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Employees_ManagerEmployeeId",
                schema: "employee",
                table: "Employees",
                column: "ManagerEmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeTimeline_Employees_EmployeeId",
                schema: "employee",
                table: "EmployeeTimeline",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GatePasses_Employees_EmployeeId",
                schema: "workforce",
                table: "GatePasses",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GatePassScans_GatePasses_GatePassId",
                schema: "workforce",
                table: "GatePassScans",
                column: "GatePassId",
                principalSchema: "workforce",
                principalTable: "GatePasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InterviewEvaluations_Interviews_InterviewId",
                schema: "recruitment",
                table: "InterviewEvaluations",
                column: "InterviewId",
                principalSchema: "recruitment",
                principalTable: "Interviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InterviewPanelists_Interviews_InterviewId",
                schema: "recruitment",
                table: "InterviewPanelists",
                column: "InterviewId",
                principalSchema: "recruitment",
                principalTable: "Interviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Interviews_Applications_ApplicationId",
                schema: "recruitment",
                table: "Interviews",
                column: "ApplicationId",
                principalSchema: "recruitment",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveBalances_Employees_EmployeeId",
                schema: "workforce",
                table: "LeaveBalances",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveBalances_LeavePolicies_PolicyId",
                schema: "workforce",
                table: "LeaveBalances",
                column: "PolicyId",
                principalSchema: "workforce",
                principalTable: "LeavePolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequestDays_LeaveRequests_RequestId",
                schema: "workforce",
                table: "LeaveRequestDays",
                column: "RequestId",
                principalSchema: "workforce",
                principalTable: "LeaveRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_Employees_EmployeeId",
                schema: "workforce",
                table: "LeaveRequests",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_LeavePolicies_PolicyId",
                schema: "workforce",
                table: "LeaveRequests",
                column: "PolicyId",
                principalSchema: "workforce",
                principalTable: "LeavePolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoanRepayments_Loans_LoanId",
                schema: "payroll",
                table: "LoanRepayments",
                column: "LoanId",
                principalSchema: "payroll",
                principalTable: "Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Loans_Employees_EmployeeId",
                schema: "payroll",
                table: "Loans",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovementRecords_Employees_EmployeeId",
                schema: "workforce",
                table: "MovementRecords",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Onboardings_Applications_ApplicationId",
                schema: "recruitment",
                table: "Onboardings",
                column: "ApplicationId",
                principalSchema: "recruitment",
                principalTable: "Applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OnboardingTasks_Onboardings_OnboardingId",
                schema: "recruitment",
                table: "OnboardingTasks",
                column: "OnboardingId",
                principalSchema: "recruitment",
                principalTable: "Onboardings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OnDutyRequests_Employees_EmployeeId",
                schema: "workforce",
                table: "OnDutyRequests",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollLines_Employees_EmployeeId",
                schema: "payroll",
                table: "PayrollLines",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollLines_PayrollRuns_RunId",
                schema: "payroll",
                table: "PayrollLines",
                column: "RunId",
                principalSchema: "payroll",
                principalTable: "PayrollRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Employees_EmployeeId",
                schema: "workforce",
                table: "ShiftAssignments",
                column: "EmployeeId",
                principalSchema: "employee",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_ShiftDefinitions_ShiftDefinitionId",
                schema: "workforce",
                table: "ShiftAssignments",
                column: "ShiftDefinitionId",
                principalSchema: "workforce",
                principalTable: "ShiftDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationEvents_Applications_ApplicationId",
                schema: "recruitment",
                table: "ApplicationEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Candidates_CandidateId",
                schema: "recruitment",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_JobOpenings_JobOpeningId",
                schema: "recruitment",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceCorrections_Employees_EmployeeId",
                schema: "workforce",
                table: "AttendanceCorrections");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceDays_Employees_EmployeeId",
                schema: "workforce",
                table: "AttendanceDays");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceExceptions_Employees_EmployeeId",
                schema: "workforce",
                table: "AttendanceExceptions");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendancePunches_Employees_EmployeeId",
                schema: "workforce",
                table: "AttendancePunches");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeBankAccounts_Employees_EmployeeId",
                schema: "employee",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeChangeRequests_Employees_EmployeeId",
                schema: "employee",
                table: "EmployeeChangeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeContacts_Employees_EmployeeId",
                schema: "employee",
                table: "EmployeeContacts");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDependants_Employees_EmployeeId",
                schema: "employee",
                table: "EmployeeDependants");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Employees_ManagerEmployeeId",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTimeline_Employees_EmployeeId",
                schema: "employee",
                table: "EmployeeTimeline");

            migrationBuilder.DropForeignKey(
                name: "FK_GatePasses_Employees_EmployeeId",
                schema: "workforce",
                table: "GatePasses");

            migrationBuilder.DropForeignKey(
                name: "FK_GatePassScans_GatePasses_GatePassId",
                schema: "workforce",
                table: "GatePassScans");

            migrationBuilder.DropForeignKey(
                name: "FK_InterviewEvaluations_Interviews_InterviewId",
                schema: "recruitment",
                table: "InterviewEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_InterviewPanelists_Interviews_InterviewId",
                schema: "recruitment",
                table: "InterviewPanelists");

            migrationBuilder.DropForeignKey(
                name: "FK_Interviews_Applications_ApplicationId",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveBalances_Employees_EmployeeId",
                schema: "workforce",
                table: "LeaveBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveBalances_LeavePolicies_PolicyId",
                schema: "workforce",
                table: "LeaveBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequestDays_LeaveRequests_RequestId",
                schema: "workforce",
                table: "LeaveRequestDays");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_Employees_EmployeeId",
                schema: "workforce",
                table: "LeaveRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_LeavePolicies_PolicyId",
                schema: "workforce",
                table: "LeaveRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LoanRepayments_Loans_LoanId",
                schema: "payroll",
                table: "LoanRepayments");

            migrationBuilder.DropForeignKey(
                name: "FK_Loans_Employees_EmployeeId",
                schema: "payroll",
                table: "Loans");

            migrationBuilder.DropForeignKey(
                name: "FK_MovementRecords_Employees_EmployeeId",
                schema: "workforce",
                table: "MovementRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Onboardings_Applications_ApplicationId",
                schema: "recruitment",
                table: "Onboardings");

            migrationBuilder.DropForeignKey(
                name: "FK_OnboardingTasks_Onboardings_OnboardingId",
                schema: "recruitment",
                table: "OnboardingTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_OnDutyRequests_Employees_EmployeeId",
                schema: "workforce",
                table: "OnDutyRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollLines_Employees_EmployeeId",
                schema: "payroll",
                table: "PayrollLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollLines_PayrollRuns_RunId",
                schema: "payroll",
                table: "PayrollLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Employees_EmployeeId",
                schema: "workforce",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_ShiftDefinitions_ShiftDefinitionId",
                schema: "workforce",
                table: "ShiftAssignments");

            migrationBuilder.DropTable(
                name: "EmployeeAddresses",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "EmployeeEducation",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "EmployeePreviousEmployment",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "EmployeeStatutory",
                schema: "employee");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_EmployeeId",
                schema: "workforce",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_ShiftDefinitionId",
                schema: "workforce",
                table: "ShiftAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShiftAssignments_Dates",
                schema: "workforce",
                table: "ShiftAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollRuns_Month",
                schema: "payroll",
                table: "PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_PayrollLines_EmployeeId",
                schema: "payroll",
                table: "PayrollLines");

            migrationBuilder.DropIndex(
                name: "IX_PayrollLines_RunId",
                schema: "payroll",
                table: "PayrollLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollLines_Amounts",
                schema: "payroll",
                table: "PayrollLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollLines_PayableDays",
                schema: "payroll",
                table: "PayrollLines");

            migrationBuilder.DropIndex(
                name: "IX_OnDutyRequests_EmployeeId",
                schema: "workforce",
                table: "OnDutyRequests");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingTasks_OnboardingId",
                schema: "recruitment",
                table: "OnboardingTasks");

            migrationBuilder.DropIndex(
                name: "IX_Onboardings_ApplicationId",
                schema: "recruitment",
                table: "Onboardings");

            migrationBuilder.DropIndex(
                name: "IX_MovementRecords_EmployeeId",
                schema: "workforce",
                table: "MovementRecords");

            migrationBuilder.DropIndex(
                name: "IX_Loans_EmployeeId",
                schema: "payroll",
                table: "Loans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Loans_Installments",
                schema: "payroll",
                table: "Loans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Loans_Interest",
                schema: "payroll",
                table: "Loans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Loans_Principal",
                schema: "payroll",
                table: "Loans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Loans_StartMonth",
                schema: "payroll",
                table: "Loans");

            migrationBuilder.DropIndex(
                name: "IX_LoanRepayments_LoanId",
                schema: "payroll",
                table: "LoanRepayments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LoanRepayments_Amount",
                schema: "payroll",
                table: "LoanRepayments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LoanRepayments_Month",
                schema: "payroll",
                table: "LoanRepayments");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_EmployeeId",
                schema: "workforce",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_PolicyId",
                schema: "workforce",
                table: "LeaveRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveRequests_Dates",
                schema: "workforce",
                table: "LeaveRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveRequests_Days",
                schema: "workforce",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequestDays_RequestId",
                schema: "workforce",
                table: "LeaveRequestDays");

            migrationBuilder.DropIndex(
                name: "IX_LeaveBalances_EmployeeId",
                schema: "workforce",
                table: "LeaveBalances");

            migrationBuilder.DropIndex(
                name: "IX_LeaveBalances_PolicyId",
                schema: "workforce",
                table: "LeaveBalances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JobOpenings_SalaryBand",
                schema: "recruitment",
                table: "JobOpenings");

            migrationBuilder.DropIndex(
                name: "IX_Interviews_ApplicationId",
                schema: "recruitment",
                table: "Interviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GradeSalaryStructures_Basic",
                schema: "platform",
                table: "GradeSalaryStructures");

            migrationBuilder.DropIndex(
                name: "IX_GatePassScans_GatePassId",
                schema: "workforce",
                table: "GatePassScans");

            migrationBuilder.DropIndex(
                name: "IX_GatePasses_EmployeeId",
                schema: "workforce",
                table: "GatePasses");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTimeline_EmployeeId",
                schema: "employee",
                table: "EmployeeTimeline");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ManagerEmployeeId",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_LeavingAfterJoining",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_NotOwnManager",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_ProbationAfterJoining",
                schema: "employee",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDependants_EmployeeId",
                schema: "employee",
                table: "EmployeeDependants");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeContacts_EmployeeId",
                schema: "employee",
                table: "EmployeeContacts");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeChangeRequests_EmployeeId",
                schema: "employee",
                table: "EmployeeChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeBankAccounts_EmployeeId",
                schema: "employee",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeBankAccounts_Ifsc",
                schema: "employee",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeBankAccounts_Last4",
                schema: "employee",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompanyBankAccounts_Ifsc",
                schema: "platform",
                table: "CompanyBankAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompanyBankAccounts_Last4",
                schema: "platform",
                table: "CompanyBankAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Candidates_Experience",
                schema: "recruitment",
                table: "Candidates");

            migrationBuilder.DropIndex(
                name: "IX_AttendancePunches_EmployeeId",
                schema: "workforce",
                table: "AttendancePunches");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceExceptions_EmployeeId",
                schema: "workforce",
                table: "AttendanceExceptions");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceDays_EmployeeId",
                schema: "workforce",
                table: "AttendanceDays");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceCorrections_EmployeeId",
                schema: "workforce",
                table: "AttendanceCorrections");

            migrationBuilder.DropIndex(
                name: "IX_Applications_CandidateId",
                schema: "recruitment",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_JobOpeningId",
                schema: "recruitment",
                table: "Applications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Applications_ExpectedCtc",
                schema: "recruitment",
                table: "Applications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Applications_Rating",
                schema: "recruitment",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationEvents_ApplicationId",
                schema: "recruitment",
                table: "ApplicationEvents");
        }
    }
}
