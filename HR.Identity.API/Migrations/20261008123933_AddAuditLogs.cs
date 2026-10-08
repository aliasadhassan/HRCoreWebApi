using HR.Shared.Library.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Identity.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Table, indexes, grants (SELECT + INSERT only) — HR.Shared.Library AuditLogMigrations
            migrationBuilder.CreateAuditLogs("identity", "hr_identity_app", rowLevelSecurity: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "identity");
        }
    }
}
