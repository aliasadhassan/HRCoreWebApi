using HR.Shared.Library.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Payroll.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Table, indexes, grants (SELECT + INSERT only) + RLS — HR.Shared.Library AuditLogMigrations
            migrationBuilder.CreateAuditLogs("payroll", "hr_payroll_app", rowLevelSecurity: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "payroll");
        }
    }
}
