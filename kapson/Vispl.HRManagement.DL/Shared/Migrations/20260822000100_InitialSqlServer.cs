using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Vispl.HRManagement.DL.Shared.Migrations;

[DbContext(typeof(HrManagementDbContext))]
[Migration("20260822000100_InitialSqlServer")]
public sealed class InitialSqlServer : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "platform");
        migrationBuilder.EnsureSchema(name: "recruitment");

        migrationBuilder.CreateTable(
            name: "CareerSites",
            schema: "platform",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                DraftJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PublishedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PublishedVersion = table.Column<int>(type: "int", nullable: false),
                PublishedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CareerSites", item => item.Id));

        migrationBuilder.CreateTable(
            name: "JobOpenings",
            schema: "recruitment",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Slug = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Department = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Location = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                EmploymentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                WorkMode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Experience = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Summary = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                ResponsibilitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                RequirementsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                IsPublished = table.Column<bool>(type: "bit", nullable: false),
                PublishedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_JobOpenings", item => item.Id));

        migrationBuilder.CreateIndex(
            name: "IX_CareerSites_TenantKey",
            schema: "platform",
            table: "CareerSites",
            column: "TenantKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_JobOpenings_TenantKey_IsPublished",
            schema: "recruitment",
            table: "JobOpenings",
            columns: ["TenantKey", "IsPublished"]);

        migrationBuilder.CreateIndex(
            name: "IX_JobOpenings_TenantKey_Slug",
            schema: "recruitment",
            table: "JobOpenings",
            columns: ["TenantKey", "Slug"],
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CareerSites", schema: "platform");
        migrationBuilder.DropTable(name: "JobOpenings", schema: "recruitment");
    }

}
