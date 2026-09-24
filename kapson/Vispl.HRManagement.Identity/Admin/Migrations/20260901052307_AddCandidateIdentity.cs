using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.Identity.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CandidateId",
                schema: "identity",
                table: "Users",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CandidateActivations",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TenantKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CandidateId = table.Column<long>(type: "bigint", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InvalidatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateActivations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CandidateId",
                schema: "identity",
                table: "Users",
                column: "CandidateId",
                filter: "[CandidateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateActivations_TenantKey_CandidateId",
                schema: "identity",
                table: "CandidateActivations",
                columns: new[] { "TenantKey", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateActivations_TokenHash",
                schema: "identity",
                table: "CandidateActivations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateActivations",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_Users_CandidateId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CandidateId",
                schema: "identity",
                table: "Users");
        }
    }
}
