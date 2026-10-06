using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Payroll.API.Migrations
{
    /// <summary>
    /// Tenancy hardening step 04 (payroll hissa). Live par SQL Editor se 04_rls_child_tenantid.sql chalta hai, phir yeh history mein record hoti hai.
    ///   - Child tables (PayslipLines, EmployeeSalaryComponents, SalaryTemplateLines) ko apna TenantId: parent se backfill, NOT NULL, composite FKs.
    ///   - tenancy.current_tenant_id() (app.tenant_id GUC) + hr_payroll_app role (LOGIN, NOBYPASSRLS, password manually).
    ///   - RLS policies sirf hr_payroll_app par; postgres / service_role (migrations, admin) pe asar nahi.
    /// Sab idempotent hai (IF NOT EXISTS / DROP POLICY IF EXISTS), isliye 04 ke baad dobara chalna safe hai.
    /// </summary>
    public partial class TenantRlsChildTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_EmployeeSalaries_TenantId_Id"" ON payroll.""EmployeeSalaries"" (""TenantId"", ""Id"");
ALTER TABLE payroll.""PayslipLines""             ADD COLUMN IF NOT EXISTS ""TenantId"" uuid;
ALTER TABLE payroll.""EmployeeSalaryComponents"" ADD COLUMN IF NOT EXISTS ""TenantId"" uuid;
ALTER TABLE payroll.""SalaryTemplateLines""      ADD COLUMN IF NOT EXISTS ""TenantId"" uuid;
UPDATE payroll.""PayslipLines"" c SET ""TenantId"" = p.""TenantId"" FROM payroll.""Payslips"" p WHERE p.""Id"" = c.""PayslipId"" AND c.""TenantId"" IS NULL;
UPDATE payroll.""EmployeeSalaryComponents"" c SET ""TenantId"" = p.""TenantId"" FROM payroll.""EmployeeSalaries"" p WHERE p.""Id"" = c.""EmployeeSalaryId"" AND c.""TenantId"" IS NULL;
UPDATE payroll.""SalaryTemplateLines"" c SET ""TenantId"" = p.""TenantId"" FROM payroll.""SalaryTemplates"" p WHERE p.""Id"" = c.""SalaryTemplateId"" AND c.""TenantId"" IS NULL;
ALTER TABLE payroll.""PayslipLines""             ALTER COLUMN ""TenantId"" SET NOT NULL;
ALTER TABLE payroll.""EmployeeSalaryComponents"" ALTER COLUMN ""TenantId"" SET NOT NULL;
ALTER TABLE payroll.""SalaryTemplateLines""      ALTER COLUMN ""TenantId"" SET NOT NULL;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PayslipLines_PayslipId_Tenant' AND conrelid = 'payroll.""PayslipLines""'::regclass) THEN
  ALTER TABLE payroll.""PayslipLines"" ADD CONSTRAINT ""FK_PayslipLines_PayslipId_Tenant"" FOREIGN KEY (""TenantId"", ""PayslipId"") REFERENCES payroll.""Payslips"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PayslipLines_PayComponentId_Tenant' AND conrelid = 'payroll.""PayslipLines""'::regclass) THEN
  ALTER TABLE payroll.""PayslipLines"" ADD CONSTRAINT ""FK_PayslipLines_PayComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""PayComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeSalaryComponents_EmployeeSalaryId_Tenant' AND conrelid = 'payroll.""EmployeeSalaryComponents""'::regclass) THEN
  ALTER TABLE payroll.""EmployeeSalaryComponents"" ADD CONSTRAINT ""FK_EmployeeSalaryComponents_EmployeeSalaryId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeSalaryId"") REFERENCES payroll.""EmployeeSalaries"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeSalaryComponents_PayComponentId_Tenant' AND conrelid = 'payroll.""EmployeeSalaryComponents""'::regclass) THEN
  ALTER TABLE payroll.""EmployeeSalaryComponents"" ADD CONSTRAINT ""FK_EmployeeSalaryComponents_PayComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""PayComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeSalaryComponents_BaseComponentId_Tenant' AND conrelid = 'payroll.""EmployeeSalaryComponents""'::regclass) THEN
  ALTER TABLE payroll.""EmployeeSalaryComponents"" ADD CONSTRAINT ""FK_EmployeeSalaryComponents_BaseComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""BaseComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryTemplateLines_SalaryTemplateId_Tenant' AND conrelid = 'payroll.""SalaryTemplateLines""'::regclass) THEN
  ALTER TABLE payroll.""SalaryTemplateLines"" ADD CONSTRAINT ""FK_SalaryTemplateLines_SalaryTemplateId_Tenant"" FOREIGN KEY (""TenantId"", ""SalaryTemplateId"") REFERENCES payroll.""SalaryTemplates"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryTemplateLines_PayComponentId_Tenant' AND conrelid = 'payroll.""SalaryTemplateLines""'::regclass) THEN
  ALTER TABLE payroll.""SalaryTemplateLines"" ADD CONSTRAINT ""FK_SalaryTemplateLines_PayComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""PayComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryTemplateLines_BaseComponentId_Tenant' AND conrelid = 'payroll.""SalaryTemplateLines""'::regclass) THEN
  ALTER TABLE payroll.""SalaryTemplateLines"" ADD CONSTRAINT ""FK_SalaryTemplateLines_BaseComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""BaseComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
CREATE SCHEMA IF NOT EXISTS tenancy;
CREATE OR REPLACE FUNCTION tenancy.current_tenant_id() RETURNS uuid
  LANGUAGE sql STABLE PARALLEL SAFE
  AS $$ SELECT nullif(current_setting('app.tenant_id', true), '')::uuid $$;
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'hr_payroll_app') THEN
    CREATE ROLE hr_payroll_app LOGIN NOINHERIT NOBYPASSRLS;
  END IF;
END $$;
GRANT USAGE ON SCHEMA tenancy TO hr_payroll_app;
GRANT EXECUTE ON FUNCTION tenancy.current_tenant_id() TO hr_payroll_app;
GRANT USAGE ON SCHEMA payroll TO hr_payroll_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA payroll TO hr_payroll_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA payroll TO hr_payroll_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA payroll GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO hr_payroll_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA payroll GRANT USAGE, SELECT ON SEQUENCES TO hr_payroll_app;
ALTER TABLE payroll.""ContributionRules"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_or_global_read ON payroll.""ContributionRules"";
CREATE POLICY tenant_or_global_read ON payroll.""ContributionRules"" FOR SELECT TO hr_payroll_app USING (""TenantId"" IS NULL OR ""TenantId"" = tenancy.current_tenant_id());
DROP POLICY IF EXISTS tenant_write ON payroll.""ContributionRules"";
CREATE POLICY tenant_write ON payroll.""ContributionRules"" FOR ALL TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""EmployeeLoans"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""EmployeeLoans"";
CREATE POLICY tenant_isolation ON payroll.""EmployeeLoans"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""EmployeeSalaries"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""EmployeeSalaries"";
CREATE POLICY tenant_isolation ON payroll.""EmployeeSalaries"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""EmployeeSalaryComponents"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""EmployeeSalaryComponents"";
CREATE POLICY tenant_isolation ON payroll.""EmployeeSalaryComponents"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""EmployeeTaxOpeningBalances"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""EmployeeTaxOpeningBalances"";
CREATE POLICY tenant_isolation ON payroll.""EmployeeTaxOpeningBalances"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""EmployeeUnpaidLeaveDays"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""EmployeeUnpaidLeaveDays"";
CREATE POLICY tenant_isolation ON payroll.""EmployeeUnpaidLeaveDays"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""InboxState"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS app_infrastructure ON payroll.""InboxState"";
CREATE POLICY app_infrastructure ON payroll.""InboxState"" TO hr_payroll_app USING (true) WITH CHECK (true);  -- infrastructure, no tenant data
ALTER TABLE payroll.""LoanPolicies"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""LoanPolicies"";
CREATE POLICY tenant_isolation ON payroll.""LoanPolicies"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""LoanRepayments"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""LoanRepayments"";
CREATE POLICY tenant_isolation ON payroll.""LoanRepayments"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""LoanRequests"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""LoanRequests"";
CREATE POLICY tenant_isolation ON payroll.""LoanRequests"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""OutboxMessage"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS app_infrastructure ON payroll.""OutboxMessage"";
CREATE POLICY app_infrastructure ON payroll.""OutboxMessage"" TO hr_payroll_app USING (true) WITH CHECK (true);  -- infrastructure, no tenant data
ALTER TABLE payroll.""OutboxState"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS app_infrastructure ON payroll.""OutboxState"";
CREATE POLICY app_infrastructure ON payroll.""OutboxState"" TO hr_payroll_app USING (true) WITH CHECK (true);  -- infrastructure, no tenant data
ALTER TABLE payroll.""PayComponents"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayComponents"";
CREATE POLICY tenant_isolation ON payroll.""PayComponents"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""PayGroups"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayGroups"";
CREATE POLICY tenant_isolation ON payroll.""PayGroups"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""PayPeriods"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayPeriods"";
CREATE POLICY tenant_isolation ON payroll.""PayPeriods"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""PaymentBatches"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PaymentBatches"";
CREATE POLICY tenant_isolation ON payroll.""PaymentBatches"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""Payments"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""Payments"";
CREATE POLICY tenant_isolation ON payroll.""Payments"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""PayrollEmployees"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayrollEmployees"";
CREATE POLICY tenant_isolation ON payroll.""PayrollEmployees"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""PayrollInputs"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayrollInputs"";
CREATE POLICY tenant_isolation ON payroll.""PayrollInputs"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""PayrollRuns"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayrollRuns"";
CREATE POLICY tenant_isolation ON payroll.""PayrollRuns"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""PayrollSettings"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayrollSettings"";
CREATE POLICY tenant_isolation ON payroll.""PayrollSettings"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""PayslipLines"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayslipLines"";
CREATE POLICY tenant_isolation ON payroll.""PayslipLines"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""Payslips"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""Payslips"";
CREATE POLICY tenant_isolation ON payroll.""Payslips"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""SalaryGrades"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""SalaryGrades"";
CREATE POLICY tenant_isolation ON payroll.""SalaryGrades"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""SalaryTemplateLines"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""SalaryTemplateLines"";
CREATE POLICY tenant_isolation ON payroll.""SalaryTemplateLines"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""SalaryTemplates"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""SalaryTemplates"";
CREATE POLICY tenant_isolation ON payroll.""SalaryTemplates"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""TaxRegimes"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_or_global_read ON payroll.""TaxRegimes"";
CREATE POLICY tenant_or_global_read ON payroll.""TaxRegimes"" FOR SELECT TO hr_payroll_app USING (""TenantId"" IS NULL OR ""TenantId"" = tenancy.current_tenant_id());
DROP POLICY IF EXISTS tenant_write ON payroll.""TaxRegimes"";
CREATE POLICY tenant_write ON payroll.""TaxRegimes"" FOR ALL TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""TaxSlabs"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS via_parent_read ON payroll.""TaxSlabs"";
CREATE POLICY via_parent_read ON payroll.""TaxSlabs"" FOR SELECT TO hr_payroll_app USING (EXISTS (SELECT 1 FROM payroll.""TaxRegimes"" p WHERE p.""Id"" = ""TaxRegimeId"" AND (p.""TenantId"" IS NULL OR p.""TenantId"" = tenancy.current_tenant_id())));
DROP POLICY IF EXISTS via_parent_write ON payroll.""TaxSlabs"";
CREATE POLICY via_parent_write ON payroll.""TaxSlabs"" FOR ALL TO hr_payroll_app USING (EXISTS (SELECT 1 FROM payroll.""TaxRegimes"" p WHERE p.""Id"" = ""TaxRegimeId"" AND p.""TenantId"" = tenancy.current_tenant_id())) WITH CHECK (EXISTS (SELECT 1 FROM payroll.""TaxRegimes"" p WHERE p.""Id"" = ""TaxRegimeId"" AND p.""TenantId"" = tenancy.current_tenant_id()));
ALTER TABLE payroll.""__EFMigrationsHistory"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS app_infrastructure ON payroll.""__EFMigrationsHistory"";
CREATE POLICY app_infrastructure ON payroll.""__EFMigrationsHistory"" TO hr_payroll_app USING (true) WITH CHECK (true);  -- infrastructure, no tenant data
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS tenant_or_global_read ON payroll.""ContributionRules"";
DROP POLICY IF EXISTS tenant_write ON payroll.""ContributionRules"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""EmployeeLoans"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""EmployeeSalaries"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""EmployeeSalaryComponents"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""EmployeeTaxOpeningBalances"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""EmployeeUnpaidLeaveDays"";
DROP POLICY IF EXISTS app_infrastructure ON payroll.""InboxState"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""LoanPolicies"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""LoanRepayments"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""LoanRequests"";
DROP POLICY IF EXISTS app_infrastructure ON payroll.""OutboxMessage"";
DROP POLICY IF EXISTS app_infrastructure ON payroll.""OutboxState"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayComponents"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayGroups"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayPeriods"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PaymentBatches"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""Payments"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayrollEmployees"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayrollInputs"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayrollRuns"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayrollSettings"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""PayslipLines"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""Payslips"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""SalaryGrades"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""SalaryTemplateLines"";
DROP POLICY IF EXISTS tenant_isolation ON payroll.""SalaryTemplates"";
DROP POLICY IF EXISTS tenant_or_global_read ON payroll.""TaxRegimes"";
DROP POLICY IF EXISTS tenant_write ON payroll.""TaxRegimes"";
DROP POLICY IF EXISTS via_parent_read ON payroll.""TaxSlabs"";
DROP POLICY IF EXISTS via_parent_write ON payroll.""TaxSlabs"";
DROP POLICY IF EXISTS app_infrastructure ON payroll.""__EFMigrationsHistory"";
ALTER TABLE payroll.""ContributionRules"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""EmployeeLoans"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""EmployeeSalaries"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""EmployeeSalaryComponents"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""EmployeeTaxOpeningBalances"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""EmployeeUnpaidLeaveDays"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""InboxState"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""LoanPolicies"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""LoanRepayments"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""LoanRequests"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""OutboxMessage"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""OutboxState"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""PayComponents"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""PayGroups"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""PayPeriods"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""PaymentBatches"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""Payments"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""PayrollEmployees"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""PayrollInputs"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""PayrollRuns"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""PayrollSettings"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""PayslipLines"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""Payslips"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""SalaryGrades"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""SalaryTemplateLines"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""SalaryTemplates"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""TaxRegimes"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""TaxSlabs"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""__EFMigrationsHistory"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE payroll.""PayslipLines"" DROP CONSTRAINT IF EXISTS ""FK_PayslipLines_PayslipId_Tenant"";
ALTER TABLE payroll.""PayslipLines"" DROP CONSTRAINT IF EXISTS ""FK_PayslipLines_PayComponentId_Tenant"";
ALTER TABLE payroll.""EmployeeSalaryComponents"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeSalaryComponents_EmployeeSalaryId_Tenant"";
ALTER TABLE payroll.""EmployeeSalaryComponents"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeSalaryComponents_PayComponentId_Tenant"";
ALTER TABLE payroll.""EmployeeSalaryComponents"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeSalaryComponents_BaseComponentId_Tenant"";
ALTER TABLE payroll.""SalaryTemplateLines"" DROP CONSTRAINT IF EXISTS ""FK_SalaryTemplateLines_SalaryTemplateId_Tenant"";
ALTER TABLE payroll.""SalaryTemplateLines"" DROP CONSTRAINT IF EXISTS ""FK_SalaryTemplateLines_PayComponentId_Tenant"";
ALTER TABLE payroll.""SalaryTemplateLines"" DROP CONSTRAINT IF EXISTS ""FK_SalaryTemplateLines_BaseComponentId_Tenant"";
ALTER TABLE payroll.""PayslipLines"" DROP COLUMN IF EXISTS ""TenantId"";
ALTER TABLE payroll.""EmployeeSalaryComponents"" DROP COLUMN IF EXISTS ""TenantId"";
ALTER TABLE payroll.""SalaryTemplateLines"" DROP COLUMN IF EXISTS ""TenantId"";
DROP INDEX IF EXISTS payroll.""UX_EmployeeSalaries_TenantId_Id"";
ALTER DEFAULT PRIVILEGES IN SCHEMA payroll REVOKE ALL ON TABLES FROM hr_payroll_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA payroll REVOKE ALL ON SEQUENCES FROM hr_payroll_app;
-- Role cluster-wide hai (password Key Vault mein): sirf is DB ke grants hatao. Role khud R04 se drop hota hai.
DROP OWNED BY hr_payroll_app;
-- tenancy schema dono services share karti hain: sirf tab drop jab doosri service ki policies bhi na hon
DO $$ BEGIN
  DROP FUNCTION IF EXISTS tenancy.current_tenant_id();
  DROP SCHEMA IF EXISTS tenancy;
EXCEPTION WHEN dependent_objects_still_exist THEN NULL;
END $$;
");
        }
    }
}
