using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminEcosystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MasterOptions",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PrimaryDomain = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThemeTemplates",
                schema: "platform",
                columns: table => new
                {
                    TemplateKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PrimaryColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    AccentColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    SurfaceColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    FontFamily = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    HeadingFontFamily = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    BorderRadius = table.Column<int>(type: "int", nullable: false),
                    PreviewGradient = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemeTemplates", x => x.TemplateKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterOptions_TenantKey_Category_Code",
                schema: "platform",
                table: "MasterOptions",
                columns: new[] { "TenantKey", "Category", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterOptions_TenantKey_Category_IsActive",
                schema: "platform",
                table: "MasterOptions",
                columns: new[] { "TenantKey", "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_TenantKey_CreatedUtc",
                schema: "platform",
                table: "MediaAssets",
                columns: new[] { "TenantKey", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_PrimaryDomain",
                schema: "platform",
                table: "Tenants",
                column: "PrimaryDomain",
                unique: true,
                filter: "[PrimaryDomain] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantKey",
                schema: "platform",
                table: "Tenants",
                column: "TenantKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MasterOptions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "MediaAssets",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ThemeTemplates",
                schema: "platform");
        }
    }
}
