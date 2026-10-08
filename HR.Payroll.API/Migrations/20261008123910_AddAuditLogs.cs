using System;
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
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Action = table.Column<byte>(type: "smallint", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubjectEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Operation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Changes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_Action", "\"Action\" BETWEEN 1 AND 3");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_At",
                schema: "payroll",
                table: "AuditLogs",
                columns: new[] { "TenantId", "At" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_CorrelationId",
                schema: "payroll",
                table: "AuditLogs",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_Entity",
                schema: "payroll",
                table: "AuditLogs",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_Subject",
                schema: "payroll",
                table: "AuditLogs",
                columns: new[] { "TenantId", "SubjectEmployeeId" },
                filter: "\"SubjectEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_UserId_At",
                schema: "payroll",
                table: "AuditLogs",
                columns: new[] { "TenantId", "UserId", "At" });

            // Audit sirf likha / parha jata hai: app role ke paas UPDATE / DELETE nahi
            migrationBuilder.Sql(@"
    REVOKE ALL ON payroll.""AuditLogs"" FROM PUBLIC;
    GRANT SELECT, INSERT ON payroll.""AuditLogs"" TO hr_payroll_app;
    REVOKE UPDATE, DELETE, TRUNCATE ON payroll.""AuditLogs"" FROM hr_payroll_app;
    ALTER TABLE payroll.""AuditLogs"" ENABLE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS tenant_isolation ON payroll.""AuditLogs"";
    CREATE POLICY tenant_isolation ON payroll.""AuditLogs"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
");
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
