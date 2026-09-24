using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workforce");

            migrationBuilder.CreateTable(
                name: "AttendanceCorrections",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OriginalPunchesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedPunchesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecisionReasonCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceCorrections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDays",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WorkedMinutes = table.Column<int>(type: "int", nullable: false),
                    ExpectedMinutes = table.Column<int>(type: "int", nullable: false),
                    ExceptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceExceptions",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PolicyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    EmployeeJustification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApprovedMinutes = table.Column<int>(type: "int", nullable: true),
                    DecisionReasonCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DetectedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceExceptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendancePunches",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LocationLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    ClientEventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupersededByCorrectionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendancePunches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_TenantKey_EmployeeId_WorkDate",
                schema: "workforce",
                table: "AttendanceCorrections",
                columns: new[] { "TenantKey", "EmployeeId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_TenantKey_State",
                schema: "workforce",
                table: "AttendanceCorrections",
                columns: new[] { "TenantKey", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDays_TenantKey_EmployeeId_WorkDate",
                schema: "workforce",
                table: "AttendanceDays",
                columns: new[] { "TenantKey", "EmployeeId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDays_TenantKey_WorkDate_Status",
                schema: "workforce",
                table: "AttendanceDays",
                columns: new[] { "TenantKey", "WorkDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExceptions_TenantKey_EmployeeId_WorkDate",
                schema: "workforce",
                table: "AttendanceExceptions",
                columns: new[] { "TenantKey", "EmployeeId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExceptions_TenantKey_EmployeeId_WorkDate_ExceptionType",
                schema: "workforce",
                table: "AttendanceExceptions",
                columns: new[] { "TenantKey", "EmployeeId", "WorkDate", "ExceptionType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExceptions_TenantKey_State",
                schema: "workforce",
                table: "AttendanceExceptions",
                columns: new[] { "TenantKey", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunches_TenantKey_EmployeeId_ClientEventId",
                schema: "workforce",
                table: "AttendancePunches",
                columns: new[] { "TenantKey", "EmployeeId", "ClientEventId" },
                unique: true,
                filter: "[ClientEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendancePunches_TenantKey_EmployeeId_WorkDate",
                schema: "workforce",
                table: "AttendancePunches",
                columns: new[] { "TenantKey", "EmployeeId", "WorkDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceCorrections",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "AttendanceDays",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "AttendanceExceptions",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "AttendancePunches",
                schema: "workforce");
        }
    }
}
