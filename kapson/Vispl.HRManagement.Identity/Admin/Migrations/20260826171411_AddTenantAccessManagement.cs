using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.Identity.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAccessManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                schema: "identity",
                table: "Users",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNumber",
                schema: "identity",
                table: "Users",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "identity",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginUtc",
                schema: "identity",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Portal",
                schema: "identity",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TenantKey",
                schema: "identity",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedUtc",
                schema: "identity",
                table: "Users",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedUtc",
                schema: "identity",
                table: "Roles",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "identity",
                table: "Roles",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "identity",
                table: "Roles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "identity",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                schema: "identity",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Portal",
                schema: "identity",
                table: "Roles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RoleCode",
                schema: "identity",
                table: "Roles",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantKey",
                schema: "identity",
                table: "Roles",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedUtc",
                schema: "identity",
                table: "Roles",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql("""
                UPDATE [identity].[Users]
                SET [IsActive] = 1,
                    [UpdatedUtc] = CASE
                        WHEN [CreatedUtc] > '1900-01-01' THEN [CreatedUtc]
                        ELSE SYSUTCDATETIME()
                    END;

                UPDATE [identity].[Roles]
                SET [RoleCode] = CASE
                        WHEN [Name] = 'VertexAdministrator' THEN 'VERTEX_ADMINISTRATOR'
                        ELSE UPPER(REPLACE(COALESCE([Name], 'LEGACY_ROLE'), ' ', '_'))
                    END,
                    [DisplayName] = CASE
                        WHEN [Name] = 'VertexAdministrator' THEN 'Vertex Administrator'
                        ELSE COALESCE([Name], 'Legacy role')
                    END,
                    [Description] = CASE
                        WHEN [Name] = 'VertexAdministrator' THEN 'Protected platform administrator with access to every licensed client.'
                        ELSE [Description]
                    END,
                    [IsSystem] = CASE WHEN [Name] = 'VertexAdministrator' THEN 1 ELSE [IsSystem] END,
                    [IsActive] = 1,
                    [CreatedUtc] = SYSUTCDATETIME(),
                    [UpdatedUtc] = SYSUTCDATETIME();
                """);

            migrationBuilder.CreateTable(
                name: "IdentityAudit",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SubjectType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubjectId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantKey_EmployeeNumber",
                schema: "identity",
                table: "Users",
                columns: new[] { "TenantKey", "EmployeeNumber" },
                filter: "[EmployeeNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantKey_Portal",
                schema: "identity",
                table: "Users",
                columns: new[] { "TenantKey", "Portal" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantKey_Portal_RoleCode",
                schema: "identity",
                table: "Roles",
                columns: new[] { "TenantKey", "Portal", "RoleCode" },
                unique: true,
                filter: "[TenantKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityAudit_TenantKey_OccurredUtc",
                schema: "identity",
                table: "IdentityAudit",
                columns: new[] { "TenantKey", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionKey",
                schema: "identity",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentityAudit",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantKey_EmployeeNumber",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantKey_Portal",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantKey_Portal_RoleCode",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmployeeNumber",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginUtc",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Portal",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantKey",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Portal",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "RoleCode",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "TenantKey",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                schema: "identity",
                table: "Roles");
        }
    }
}
