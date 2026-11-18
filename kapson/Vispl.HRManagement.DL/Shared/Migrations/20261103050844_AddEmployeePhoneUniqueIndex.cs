using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vispl.HRManagement.DL.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePhoneUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Same spelling as EmployeePhone.Normalize for the common cases, so the index
            // compares like with like: separators removed, a bare 10-digit number gets +91.
            migrationBuilder.Sql(@"
UPDATE employee.Employees
SET Phone = NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(Phone, ' ', ''), '-', ''), '(', ''), ')', ''), '')
WHERE Phone IS NOT NULL;
UPDATE employee.Employees
SET Phone = '+91' + Phone
WHERE Phone IS NOT NULL AND LEN(Phone) = 10 AND Phone NOT LIKE '%[^0-9]%';");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantKey_Phone",
                schema: "employee",
                table: "Employees",
                columns: new[] { "TenantKey", "Phone" },
                unique: true,
                filter: "[Phone] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantKey_Phone",
                schema: "employee",
                table: "Employees");
        }
    }
}
