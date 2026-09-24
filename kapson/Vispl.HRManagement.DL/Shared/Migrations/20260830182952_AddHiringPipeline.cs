using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddHiringPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiresOn",
                schema: "recruitment",
                table: "JobOpenings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredDocumentCodesJson",
                schema: "recruitment",
                table: "JobOpenings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SalaryCurrencyCode",
                schema: "recruitment",
                table: "JobOpenings",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMaximum",
                schema: "recruitment",
                table: "JobOpenings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMinimum",
                schema: "recruitment",
                table: "JobOpenings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScreeningQuestionsJson",
                schema: "recruitment",
                table: "JobOpenings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "recruitment",
                table: "JobOpenings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusChangedUtc",
                schema: "recruitment",
                table: "JobOpenings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationDocuments",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ScanState = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UploadedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationEvents",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    FromStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    JobOpeningId = table.Column<long>(type: "bigint", nullable: false),
                    CandidateId = table.Column<long>(type: "bigint", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NoticePeriodDays = table.Column<int>(type: "int", nullable: false),
                    ExpectedCtc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AnswersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConsentGranted = table.Column<bool>(type: "bit", nullable: false),
                    ConsentPolicyVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ConsentUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AppliedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StageChangedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StageChangedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Candidates",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentLocation = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    TotalExperienceYears = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Onboardings",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    CandidateId = table.Column<long>(type: "bigint", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    JoiningOn = table.Column<DateOnly>(type: "date", nullable: true),
                    StartedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Onboardings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingTasks",
                schema: "recruitment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OnboardingId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TaskType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
                    FormValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobOpenings_TenantKey_Status",
                schema: "recruitment",
                table: "JobOpenings",
                columns: new[] { "TenantKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDocuments_TenantKey_ApplicationId",
                schema: "recruitment",
                table: "ApplicationDocuments",
                columns: new[] { "TenantKey", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_TenantKey_ApplicationId",
                schema: "recruitment",
                table: "ApplicationEvents",
                columns: new[] { "TenantKey", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantKey_CandidateId",
                schema: "recruitment",
                table: "Applications",
                columns: new[] { "TenantKey", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantKey_JobOpeningId_CandidateId",
                schema: "recruitment",
                table: "Applications",
                columns: new[] { "TenantKey", "JobOpeningId", "CandidateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_TenantKey_Stage",
                schema: "recruitment",
                table: "Applications",
                columns: new[] { "TenantKey", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_TenantKey_Email",
                schema: "recruitment",
                table: "Candidates",
                columns: new[] { "TenantKey", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_TenantKey_ApplicationId",
                schema: "recruitment",
                table: "Onboardings",
                columns: new[] { "TenantKey", "ApplicationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_TenantKey_Stage",
                schema: "recruitment",
                table: "Onboardings",
                columns: new[] { "TenantKey", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTasks_TenantKey_OnboardingId",
                schema: "recruitment",
                table: "OnboardingTasks",
                columns: new[] { "TenantKey", "OnboardingId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationDocuments",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "ApplicationEvents",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Applications",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Candidates",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "Onboardings",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "OnboardingTasks",
                schema: "recruitment");

            migrationBuilder.DropIndex(
                name: "IX_JobOpenings_TenantKey_Status",
                schema: "recruitment",
                table: "JobOpenings");

            migrationBuilder.DropColumn(
                name: "ExpiresOn",
                schema: "recruitment",
                table: "JobOpenings");

            migrationBuilder.DropColumn(
                name: "RequiredDocumentCodesJson",
                schema: "recruitment",
                table: "JobOpenings");

            migrationBuilder.DropColumn(
                name: "SalaryCurrencyCode",
                schema: "recruitment",
                table: "JobOpenings");

            migrationBuilder.DropColumn(
                name: "SalaryMaximum",
                schema: "recruitment",
                table: "JobOpenings");

            migrationBuilder.DropColumn(
                name: "SalaryMinimum",
                schema: "recruitment",
                table: "JobOpenings");

            migrationBuilder.DropColumn(
                name: "ScreeningQuestionsJson",
                schema: "recruitment",
                table: "JobOpenings");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "recruitment",
                table: "JobOpenings");

            migrationBuilder.DropColumn(
                name: "StatusChangedUtc",
                schema: "recruitment",
                table: "JobOpenings");
        }
    }
}
