namespace HR.Shared.Library.Persistence;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Teeno services ki "AddAuditLogs" migration yahi chalati hai — table ek jaisa, sirf schema / app role alag.
/// Shape AuditLogConfiguration jaisa hi rakhna (snapshot wahan se banta hai).
/// </summary>
public static class AuditLogMigrations
{
    /// <param name="rowLevelSecurity">Employee / Payroll: tenant_isolation policy. Identity: nahi (filter API mein).</param>
    public static void CreateAuditLogs(this MigrationBuilder migrationBuilder, string schema, string appRole, bool rowLevelSecurity)
    {
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            schema: schema,
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
            schema: schema,
            table: "AuditLogs",
            columns: new[] { "TenantId", "At" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_TenantId_CorrelationId",
            schema: schema,
            table: "AuditLogs",
            columns: new[] { "TenantId", "CorrelationId" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_TenantId_Entity",
            schema: schema,
            table: "AuditLogs",
            columns: new[] { "TenantId", "EntityType", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_TenantId_Subject",
            schema: schema,
            table: "AuditLogs",
            columns: new[] { "TenantId", "SubjectEmployeeId" },
            filter: "\"SubjectEmployeeId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_TenantId_UserId_At",
            schema: schema,
            table: "AuditLogs",
            columns: new[] { "TenantId", "UserId", "At" });

        // Audit sirf likha / parha jata hai: app role ke paas UPDATE / DELETE nahi
        var table = $"{schema}.\"AuditLogs\"";
        migrationBuilder.Sql($"REVOKE ALL ON {table} FROM PUBLIC;");
        migrationBuilder.Sql($"GRANT SELECT, INSERT ON {table} TO {appRole};");
        migrationBuilder.Sql($"REVOKE UPDATE, DELETE, TRUNCATE ON {table} FROM {appRole};");
        if (!rowLevelSecurity)
            return;
        migrationBuilder.Sql($"ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON {table};");
        migrationBuilder.Sql(
            $"CREATE POLICY tenant_isolation ON {table} TO {appRole} " +
            "USING (\"TenantId\" = tenancy.current_tenant_id()) WITH CHECK (\"TenantId\" = tenancy.current_tenant_id());");
    }
}
