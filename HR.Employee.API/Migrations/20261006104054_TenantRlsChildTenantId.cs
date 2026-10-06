using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <summary>
    /// Tenancy hardening step 04 (employee hissa). Live par SQL Editor se 04_rls_child_tenantid.sql chalta hai, phir yeh history mein record hoti hai.
    ///   - Child tables (LeavePolicyRules, AttendancePunches) ko apna TenantId: parent se backfill, NOT NULL, composite FKs.
    ///   - tenancy.current_tenant_id() (app.tenant_id GUC) + hr_employee_app role (LOGIN, NOBYPASSRLS, password manually).
    ///   - RLS policies sirf hr_employee_app par; postgres / service_role (migrations, admin) pe asar nahi.
    /// Sab idempotent hai (IF NOT EXISTS / DROP POLICY IF EXISTS), isliye 04 ke baad dobara chalna safe hai.
    /// </summary>
    public partial class TenantRlsChildTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_LeavePolicies_TenantId_Id""    ON employee.""LeavePolicies"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_AttendanceDevices_TenantId_Id"" ON attendance.""AttendanceDevices"" (""TenantId"", ""Id"");
ALTER TABLE employee.""LeavePolicyRules""        ADD COLUMN IF NOT EXISTS ""TenantId"" uuid;
ALTER TABLE attendance.""AttendancePunches""     ADD COLUMN IF NOT EXISTS ""TenantId"" uuid;
UPDATE employee.""LeavePolicyRules"" c SET ""TenantId"" = p.""TenantId"" FROM employee.""LeavePolicies"" p WHERE p.""Id"" = c.""LeavePolicyId"" AND c.""TenantId"" IS NULL;
UPDATE attendance.""AttendancePunches"" c SET ""TenantId"" = p.""TenantId"" FROM attendance.""AttendanceDays"" p WHERE p.""Id"" = c.""AttendanceDayId"" AND c.""TenantId"" IS NULL;
ALTER TABLE employee.""LeavePolicyRules""        ALTER COLUMN ""TenantId"" SET NOT NULL;
ALTER TABLE attendance.""AttendancePunches""     ALTER COLUMN ""TenantId"" SET NOT NULL;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeavePolicyRules_LeavePolicyId_Tenant' AND conrelid = 'employee.""LeavePolicyRules""'::regclass) THEN
  ALTER TABLE employee.""LeavePolicyRules"" ADD CONSTRAINT ""FK_LeavePolicyRules_LeavePolicyId_Tenant"" FOREIGN KEY (""TenantId"", ""LeavePolicyId"") REFERENCES employee.""LeavePolicies"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeavePolicyRules_LeaveTypeId_Tenant' AND conrelid = 'employee.""LeavePolicyRules""'::regclass) THEN
  ALTER TABLE employee.""LeavePolicyRules"" ADD CONSTRAINT ""FK_LeavePolicyRules_LeaveTypeId_Tenant"" FOREIGN KEY (""TenantId"", ""LeaveTypeId"") REFERENCES employee.""LeaveTypes"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AttendancePunches_AttendanceDayId_Tenant' AND conrelid = 'attendance.""AttendancePunches""'::regclass) THEN
  ALTER TABLE attendance.""AttendancePunches"" ADD CONSTRAINT ""FK_AttendancePunches_AttendanceDayId_Tenant"" FOREIGN KEY (""TenantId"", ""AttendanceDayId"") REFERENCES attendance.""AttendanceDays"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AttendancePunches_DeviceId_Tenant' AND conrelid = 'attendance.""AttendancePunches""'::regclass) THEN
  ALTER TABLE attendance.""AttendancePunches"" ADD CONSTRAINT ""FK_AttendancePunches_DeviceId_Tenant"" FOREIGN KEY (""TenantId"", ""DeviceId"") REFERENCES attendance.""AttendanceDevices"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
CREATE SCHEMA IF NOT EXISTS tenancy;
CREATE OR REPLACE FUNCTION tenancy.current_tenant_id() RETURNS uuid
  LANGUAGE sql STABLE PARALLEL SAFE
  AS $$ SELECT nullif(current_setting('app.tenant_id', true), '')::uuid $$;
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'hr_employee_app') THEN
    CREATE ROLE hr_employee_app LOGIN NOINHERIT NOBYPASSRLS;
  END IF;
END $$;
GRANT USAGE ON SCHEMA tenancy TO hr_employee_app;
GRANT EXECUTE ON FUNCTION tenancy.current_tenant_id() TO hr_employee_app;
GRANT USAGE ON SCHEMA employee, attendance TO hr_employee_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA employee, attendance TO hr_employee_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA employee, attendance TO hr_employee_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA employee, attendance GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO hr_employee_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA employee, attendance GRANT USAGE, SELECT ON SEQUENCES TO hr_employee_app;
ALTER TABLE attendance.""AttendanceDays"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON attendance.""AttendanceDays"";
CREATE POLICY tenant_isolation ON attendance.""AttendanceDays"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE attendance.""AttendanceDevices"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON attendance.""AttendanceDevices"";
CREATE POLICY tenant_isolation ON attendance.""AttendanceDevices"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE attendance.""AttendancePolicies"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON attendance.""AttendancePolicies"";
CREATE POLICY tenant_isolation ON attendance.""AttendancePolicies"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE attendance.""AttendancePunches"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON attendance.""AttendancePunches"";
CREATE POLICY tenant_isolation ON attendance.""AttendancePunches"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE attendance.""AttendanceRequests"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON attendance.""AttendanceRequests"";
CREATE POLICY tenant_isolation ON attendance.""AttendanceRequests"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE attendance.""RosterEntries"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON attendance.""RosterEntries"";
CREATE POLICY tenant_isolation ON attendance.""RosterEntries"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE attendance.""ShiftAssignments"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON attendance.""ShiftAssignments"";
CREATE POLICY tenant_isolation ON attendance.""ShiftAssignments"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE attendance.""Shifts"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON attendance.""Shifts"";
CREATE POLICY tenant_isolation ON attendance.""Shifts"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""Departments"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""Departments"";
CREATE POLICY tenant_isolation ON employee.""Departments"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""Designations"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""Designations"";
CREATE POLICY tenant_isolation ON employee.""Designations"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""EmployeeDocuments"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""EmployeeDocuments"";
CREATE POLICY tenant_isolation ON employee.""EmployeeDocuments"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""EmployeeEmergencyContacts"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS via_parent ON employee.""EmployeeEmergencyContacts"";
CREATE POLICY via_parent ON employee.""EmployeeEmergencyContacts"" TO hr_employee_app USING (EXISTS (SELECT 1 FROM employee.""Employees"" p WHERE p.""Id"" = ""EmployeeId"" AND p.""TenantId"" = tenancy.current_tenant_id())) WITH CHECK (EXISTS (SELECT 1 FROM employee.""Employees"" p WHERE p.""Id"" = ""EmployeeId"" AND p.""TenantId"" = tenancy.current_tenant_id()));
ALTER TABLE employee.""EmployeeJobHistory"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""EmployeeJobHistory"";
CREATE POLICY tenant_isolation ON employee.""EmployeeJobHistory"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""Employees"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""Employees"";
CREATE POLICY tenant_isolation ON employee.""Employees"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""Holidays"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""Holidays"";
CREATE POLICY tenant_isolation ON employee.""Holidays"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""InboxState"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS app_infrastructure ON employee.""InboxState"";
CREATE POLICY app_infrastructure ON employee.""InboxState"" TO hr_employee_app USING (true) WITH CHECK (true);  -- infrastructure, no tenant data
ALTER TABLE employee.""LeaveApprovalSettings"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeaveApprovalSettings"";
CREATE POLICY tenant_isolation ON employee.""LeaveApprovalSettings"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""LeaveBalances"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeaveBalances"";
CREATE POLICY tenant_isolation ON employee.""LeaveBalances"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""LeavePolicies"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeavePolicies"";
CREATE POLICY tenant_isolation ON employee.""LeavePolicies"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""LeavePolicyRules"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeavePolicyRules"";
CREATE POLICY tenant_isolation ON employee.""LeavePolicyRules"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""LeaveRequestApprovals"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS via_parent ON employee.""LeaveRequestApprovals"";
CREATE POLICY via_parent ON employee.""LeaveRequestApprovals"" TO hr_employee_app USING (EXISTS (SELECT 1 FROM employee.""LeaveRequests"" p WHERE p.""Id"" = ""LeaveRequestId"" AND p.""TenantId"" = tenancy.current_tenant_id())) WITH CHECK (EXISTS (SELECT 1 FROM employee.""LeaveRequests"" p WHERE p.""Id"" = ""LeaveRequestId"" AND p.""TenantId"" = tenancy.current_tenant_id()));
ALTER TABLE employee.""LeaveRequests"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeaveRequests"";
CREATE POLICY tenant_isolation ON employee.""LeaveRequests"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""LeaveTypes"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeaveTypes"";
CREATE POLICY tenant_isolation ON employee.""LeaveTypes"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""Locations"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""Locations"";
CREATE POLICY tenant_isolation ON employee.""Locations"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""OutboxMessage"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS app_infrastructure ON employee.""OutboxMessage"";
CREATE POLICY app_infrastructure ON employee.""OutboxMessage"" TO hr_employee_app USING (true) WITH CHECK (true);  -- infrastructure, no tenant data
ALTER TABLE employee.""OutboxState"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS app_infrastructure ON employee.""OutboxState"";
CREATE POLICY app_infrastructure ON employee.""OutboxState"" TO hr_employee_app USING (true) WITH CHECK (true);  -- infrastructure, no tenant data
ALTER TABLE employee.""__EFMigrationsHistory"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS app_infrastructure ON employee.""__EFMigrationsHistory"";
CREATE POLICY app_infrastructure ON employee.""__EFMigrationsHistory"" TO hr_employee_app USING (true) WITH CHECK (true);  -- infrastructure, no tenant data
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP POLICY IF EXISTS tenant_isolation ON attendance.""AttendanceDays"";
DROP POLICY IF EXISTS tenant_isolation ON attendance.""AttendanceDevices"";
DROP POLICY IF EXISTS tenant_isolation ON attendance.""AttendancePolicies"";
DROP POLICY IF EXISTS tenant_isolation ON attendance.""AttendancePunches"";
DROP POLICY IF EXISTS tenant_isolation ON attendance.""AttendanceRequests"";
DROP POLICY IF EXISTS tenant_isolation ON attendance.""RosterEntries"";
DROP POLICY IF EXISTS tenant_isolation ON attendance.""ShiftAssignments"";
DROP POLICY IF EXISTS tenant_isolation ON attendance.""Shifts"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""Departments"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""Designations"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""EmployeeDocuments"";
DROP POLICY IF EXISTS via_parent ON employee.""EmployeeEmergencyContacts"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""EmployeeJobHistory"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""Employees"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""Holidays"";
DROP POLICY IF EXISTS app_infrastructure ON employee.""InboxState"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeaveApprovalSettings"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeaveBalances"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeavePolicies"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeavePolicyRules"";
DROP POLICY IF EXISTS via_parent ON employee.""LeaveRequestApprovals"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeaveRequests"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""LeaveTypes"";
DROP POLICY IF EXISTS tenant_isolation ON employee.""Locations"";
DROP POLICY IF EXISTS app_infrastructure ON employee.""OutboxMessage"";
DROP POLICY IF EXISTS app_infrastructure ON employee.""OutboxState"";
DROP POLICY IF EXISTS app_infrastructure ON employee.""__EFMigrationsHistory"";
ALTER TABLE attendance.""AttendanceDays"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE attendance.""AttendanceDevices"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE attendance.""AttendancePolicies"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE attendance.""AttendancePunches"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE attendance.""AttendanceRequests"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE attendance.""RosterEntries"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE attendance.""ShiftAssignments"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE attendance.""Shifts"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""Departments"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""Designations"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""EmployeeDocuments"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""EmployeeEmergencyContacts"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""EmployeeJobHistory"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""Employees"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""Holidays"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""InboxState"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""LeaveApprovalSettings"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""LeaveBalances"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""LeavePolicies"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""LeavePolicyRules"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""LeaveRequestApprovals"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""LeaveRequests"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""LeaveTypes"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""Locations"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""OutboxMessage"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""OutboxState"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""__EFMigrationsHistory"" DISABLE ROW LEVEL SECURITY;
ALTER TABLE employee.""LeavePolicyRules"" DROP CONSTRAINT IF EXISTS ""FK_LeavePolicyRules_LeavePolicyId_Tenant"";
ALTER TABLE employee.""LeavePolicyRules"" DROP CONSTRAINT IF EXISTS ""FK_LeavePolicyRules_LeaveTypeId_Tenant"";
ALTER TABLE attendance.""AttendancePunches"" DROP CONSTRAINT IF EXISTS ""FK_AttendancePunches_AttendanceDayId_Tenant"";
ALTER TABLE attendance.""AttendancePunches"" DROP CONSTRAINT IF EXISTS ""FK_AttendancePunches_DeviceId_Tenant"";
ALTER TABLE employee.""LeavePolicyRules"" DROP COLUMN IF EXISTS ""TenantId"";
ALTER TABLE attendance.""AttendancePunches"" DROP COLUMN IF EXISTS ""TenantId"";
DROP INDEX IF EXISTS employee.""UX_LeavePolicies_TenantId_Id"";
DROP INDEX IF EXISTS attendance.""UX_AttendanceDevices_TenantId_Id"";
ALTER DEFAULT PRIVILEGES IN SCHEMA employee, attendance REVOKE ALL ON TABLES FROM hr_employee_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA employee, attendance REVOKE ALL ON SEQUENCES FROM hr_employee_app;
-- Role cluster-wide hai (password Key Vault mein): sirf is DB ke grants hatao. Role khud R04 se drop hota hai.
DROP OWNED BY hr_employee_app;
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
