using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaveAdjustments",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreditIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveAdjustments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveBalances",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    PolicyId = table.Column<long>(type: "bigint", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Entitled = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Accrued = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    CarriedForward = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    AdjustmentDays = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveCredits",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    PolicyId = table.Column<long>(type: "bigint", nullable: false),
                    SourceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EarnedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    IsEncashable = table.Column<bool>(type: "bit", nullable: false),
                    EncashmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EncashmentCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ConsumedByAdjustmentId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveCredits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveHolidays",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LocationCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveHolidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeavePolicies",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AnnualEntitlement = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    AccrualMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CarryForwardCap = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    NoticeDays = table.Column<int>(type: "int", nullable: false),
                    DocumentRequiredAboveDays = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    AllowsHalfDay = table.Column<bool>(type: "bit", nullable: false),
                    AllowsNegativeBalance = table.Column<bool>(type: "bit", nullable: false),
                    CountsNonWorkingDays = table.Column<bool>(type: "bit", nullable: false),
                    EligibilityText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AccrualRuleText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CarryForwardRuleText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NoticeRuleText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DocumentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeavePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequestComments",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequestComments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequestDays",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Portion = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequestDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    PolicyId = table.Column<long>(type: "bigint", nullable: false),
                    LeaveTypeCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsFirstDayPartial = table.Column<bool>(type: "bit", nullable: false),
                    IsLastDayPartial = table.Column<bool>(type: "bit", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AttachmentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecidedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecisionReasonCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveAdjustments_TenantKey_EmployeeId_State",
                schema: "workforce",
                table: "LeaveAdjustments",
                columns: new[] { "TenantKey", "EmployeeId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_TenantKey_EmployeeId_PolicyId_Year",
                schema: "workforce",
                table: "LeaveBalances",
                columns: new[] { "TenantKey", "EmployeeId", "PolicyId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCredits_TenantKey_EmployeeId_ExpiresOn",
                schema: "workforce",
                table: "LeaveCredits",
                columns: new[] { "TenantKey", "EmployeeId", "ExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveHolidays_TenantKey_Date_LocationCode",
                schema: "workforce",
                table: "LeaveHolidays",
                columns: new[] { "TenantKey", "Date", "LocationCode" },
                unique: true,
                filter: "[LocationCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePolicies_TenantKey_Code",
                schema: "workforce",
                table: "LeavePolicies",
                columns: new[] { "TenantKey", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestComments_TenantKey_RequestId",
                schema: "workforce",
                table: "LeaveRequestComments",
                columns: new[] { "TenantKey", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestDays_TenantKey_EmployeeId_Date",
                schema: "workforce",
                table: "LeaveRequestDays",
                columns: new[] { "TenantKey", "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequestDays_TenantKey_RequestId",
                schema: "workforce",
                table: "LeaveRequestDays",
                columns: new[] { "TenantKey", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_TenantKey_EmployeeId_FromDate",
                schema: "workforce",
                table: "LeaveRequests",
                columns: new[] { "TenantKey", "EmployeeId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_TenantKey_State",
                schema: "workforce",
                table: "LeaveRequests",
                columns: new[] { "TenantKey", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveAdjustments",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "LeaveBalances",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "LeaveCredits",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "LeaveHolidays",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "LeavePolicies",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "LeaveRequestComments",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "LeaveRequestDays",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "LeaveRequests",
                schema: "workforce");
        }
    }
}
