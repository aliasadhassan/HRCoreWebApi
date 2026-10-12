namespace HR.Shared.Library.Persistence;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Naye tenant tables ke liye wahi cheezein jo tenancy audit 03/04 ne purane tables par lagayi thin:
/// (TenantId, Id) unique key, composite tenant FK (doosre tenant ki row point na ho sake), app role grants aur RLS.
/// Sab idempotent hain taake idempotent SQL script dobara chale to toote nahi.
/// </summary>
public static class TenantMigrations
{
    /// <summary>Composite FK ka nishana banne ke liye (TenantId, Id) unique index.</summary>
    public static void AddTenantKey(this MigrationBuilder migrationBuilder, string schema, string table)
        => migrationBuilder.Sql(
            $"CREATE UNIQUE INDEX IF NOT EXISTS \"UX_{table}_TenantId_Id\" ON {schema}.\"{table}\" (\"TenantId\", \"Id\");");

    /// <summary>(TenantId, column) → principal (TenantId, Id). onDelete: "RESTRICT" ya "CASCADE".</summary>
    public static void AddTenantForeignKey(
        this MigrationBuilder migrationBuilder, string schema, string table, string column, string principalTable, string onDelete = "RESTRICT")
    {
        var name = $"FK_{table}_{column}_Tenant";
        migrationBuilder.Sql(
            $"DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = '{name}' AND conrelid = '{schema}.\"{table}\"'::regclass) THEN " +
            $"ALTER TABLE {schema}.\"{table}\" ADD CONSTRAINT \"{name}\" FOREIGN KEY (\"TenantId\", \"{column}\") " +
            $"REFERENCES {schema}.\"{principalTable}\" (\"TenantId\", \"Id\") ON DELETE {onDelete}; END IF; END $$;");
    }

    /// <summary>App role ko CRUD grant + tenant_isolation RLS policy.</summary>
    public static void EnableTenantIsolation(this MigrationBuilder migrationBuilder, string schema, string appRole, params string[] tables)
    {
        foreach (var t in tables)
        {
            var table = $"{schema}.\"{t}\"";
            migrationBuilder.Sql($"GRANT SELECT, INSERT, UPDATE, DELETE ON {table} TO {appRole};");
            migrationBuilder.Sql($"ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON {table};");
            migrationBuilder.Sql(
                $"CREATE POLICY tenant_isolation ON {table} TO {appRole} " +
                "USING (\"TenantId\" = tenancy.current_tenant_id()) WITH CHECK (\"TenantId\" = tenancy.current_tenant_id());");
        }
    }
}
