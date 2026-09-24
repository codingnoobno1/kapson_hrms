using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddMailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailSettings",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    SenderBaseUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ReceiverBaseUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PublicBaseUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProtectedApiKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailSettings",
                schema: "platform");
        }
    }
}
