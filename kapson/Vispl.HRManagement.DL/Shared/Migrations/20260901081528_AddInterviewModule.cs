using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewEvaluations",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    InterviewId = table.Column<long>(type: "bigint", nullable: false),
                    PanelMemberId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ScoresJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewEvaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterviewPanelists",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    InterviewId = table.Column<long>(type: "bigint", nullable: false),
                    PanelMemberId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsLead = table.Column<bool>(type: "bit", nullable: false),
                    AddedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPanelists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Interviews",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    CandidateId = table.Column<long>(type: "bigint", nullable: false),
                    JobOpeningId = table.Column<long>(type: "bigint", nullable: false),
                    RoundCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RoundLabel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LocationOrLink = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CancellationReasonCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CancellationComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ScheduledBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewEvaluations_InterviewId_PanelMemberId",
                schema: "recruitment",
                table: "InterviewEvaluations",
                columns: new[] { "InterviewId", "PanelMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPanelists_InterviewId_PanelMemberId",
                schema: "recruitment",
                table: "InterviewPanelists",
                columns: new[] { "InterviewId", "PanelMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPanelists_TenantKey_InterviewId",
                schema: "recruitment",
                table: "InterviewPanelists",
                columns: new[] { "TenantKey", "InterviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPanelists_TenantKey_PanelMemberId",
                schema: "recruitment",
                table: "InterviewPanelists",
                columns: new[] { "TenantKey", "PanelMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_TenantKey_ApplicationId",
                schema: "recruitment",
                table: "Interviews",
                columns: new[] { "TenantKey", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_TenantKey_StartUtc",
                schema: "recruitment",
                table: "Interviews",
                columns: new[] { "TenantKey", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_TenantKey_Status",
                schema: "recruitment",
                table: "Interviews",
                columns: new[] { "TenantKey", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewEvaluations",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "InterviewPanelists",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Interviews",
                schema: "recruitment");
        }
    }
}
