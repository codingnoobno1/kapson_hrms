using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeAppSettings",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AllowAppPunch = table.Column<bool>(type: "bit", nullable: false),
                    RequireLocationForPunch = table.Column<bool>(type: "bit", nullable: false),
                    AllowGatePassRequests = table.Column<bool>(type: "bit", nullable: false),
                    AllowAttendanceCorrections = table.Column<bool>(type: "bit", nullable: false),
                    ShowSalaryDetails = table.Column<bool>(type: "bit", nullable: false),
                    ShowBankDetails = table.Column<bool>(type: "bit", nullable: false),
                    ShowOnboarding = table.Column<bool>(type: "bit", nullable: false),
                    AllowProfileChangeRequests = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAppSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAppSettings_TenantKey",
                schema: "platform",
                table: "EmployeeAppSettings",
                column: "TenantKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeAppSettings",
                schema: "platform");
        }
    }
}
