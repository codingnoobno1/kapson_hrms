using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RecipientId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActionRoute = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SubjectType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SubjectId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantKey_RecipientId_OccurredUtc",
                schema: "platform",
                table: "Notifications",
                columns: new[] { "TenantKey", "RecipientId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantKey_RecipientId_ReadUtc",
                schema: "platform",
                table: "Notifications",
                columns: new[] { "TenantKey", "RecipientId", "ReadUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantKey_RecipientId_SubjectType_SubjectId",
                schema: "platform",
                table: "Notifications",
                columns: new[] { "TenantKey", "RecipientId", "SubjectType", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "platform");
        }
    }
}
