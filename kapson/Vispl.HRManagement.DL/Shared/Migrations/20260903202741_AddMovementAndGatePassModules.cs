using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddMovementAndGatePassModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GatePasses",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    PassType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    ToTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DestinationLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VerificationToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecidedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RequestedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatePasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GatePassScans",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    GatePassId = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ScannedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScannedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    GateLabel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatePassScans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovementRecords",
                schema: "workforce",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GoingOutUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpectedReturnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActualReturnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DestinationLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_TenantKey_EmployeeId_FromDate",
                schema: "workforce",
                table: "GatePasses",
                columns: new[] { "TenantKey", "EmployeeId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_TenantKey_State",
                schema: "workforce",
                table: "GatePasses",
                columns: new[] { "TenantKey", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_GatePasses_TenantKey_VerificationToken",
                schema: "workforce",
                table: "GatePasses",
                columns: new[] { "TenantKey", "VerificationToken" },
                unique: true,
                filter: "[VerificationToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GatePassScans_TenantKey_GatePassId",
                schema: "workforce",
                table: "GatePassScans",
                columns: new[] { "TenantKey", "GatePassId" });

            migrationBuilder.CreateIndex(
                name: "IX_MovementRecords_OneOpenPerEmployee",
                schema: "workforce",
                table: "MovementRecords",
                columns: new[] { "TenantKey", "EmployeeId" },
                unique: true,
                filter: "[State] = 'Open' AND [ActualReturnUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MovementRecords_TenantKey_EmployeeId_WorkDate",
                schema: "workforce",
                table: "MovementRecords",
                columns: new[] { "TenantKey", "EmployeeId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MovementRecords_TenantKey_State",
                schema: "workforce",
                table: "MovementRecords",
                columns: new[] { "TenantKey", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GatePasses",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "GatePassScans",
                schema: "workforce");

            migrationBuilder.DropTable(
                name: "MovementRecords",
                schema: "workforce");
        }
    }
}
