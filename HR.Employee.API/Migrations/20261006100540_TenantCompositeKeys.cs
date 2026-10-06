using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <summary>
    /// Tenant-aware relationships (tenancy hardening step 03, live par 2026-10-06 ko SQL Editor se chal chuka hai).
    /// Har parent par UNIQUE ("TenantId", "Id"), aur har child par composite FK (TenantId, XId) -> Parent(TenantId, Id),
    /// taake Tenant A ki row kabhi Tenant B ki row ko point na kar sake. Purane single-column FKs waise hi rehte hain.
    /// Sab statements idempotent hain (IF NOT EXISTS), isliye live par dobara chalna safe hai.
    /// EF model in FKs ko track nahi karta (alternate keys nahi bani), isliye raw SQL.
    /// </summary>
    public partial class TenantCompositeKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_AttendanceDays_TenantId_Id"" ON attendance.""AttendanceDays"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Shifts_TenantId_Id"" ON attendance.""Shifts"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Departments_TenantId_Id"" ON employee.""Departments"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Designations_TenantId_Id"" ON employee.""Designations"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_EmployeeDocuments_TenantId_Id"" ON employee.""EmployeeDocuments"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Employees_TenantId_Id"" ON employee.""Employees"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_LeaveRequests_TenantId_Id"" ON employee.""LeaveRequests"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_LeaveTypes_TenantId_Id"" ON employee.""LeaveTypes"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Locations_TenantId_Id"" ON employee.""Locations"" (""TenantId"", ""Id"");
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AttendanceDays_EmployeeId_Tenant' AND conrelid = 'attendance.""AttendanceDays""'::regclass) THEN
  ALTER TABLE attendance.""AttendanceDays"" ADD CONSTRAINT ""FK_AttendanceDays_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AttendanceDays_LeaveRequestId_Tenant' AND conrelid = 'attendance.""AttendanceDays""'::regclass) THEN
  ALTER TABLE attendance.""AttendanceDays"" ADD CONSTRAINT ""FK_AttendanceDays_LeaveRequestId_Tenant"" FOREIGN KEY (""TenantId"", ""LeaveRequestId"") REFERENCES employee.""LeaveRequests"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AttendanceDays_ShiftId_Tenant' AND conrelid = 'attendance.""AttendanceDays""'::regclass) THEN
  ALTER TABLE attendance.""AttendanceDays"" ADD CONSTRAINT ""FK_AttendanceDays_ShiftId_Tenant"" FOREIGN KEY (""TenantId"", ""ShiftId"") REFERENCES attendance.""Shifts"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AttendanceDevices_LocationId_Tenant' AND conrelid = 'attendance.""AttendanceDevices""'::regclass) THEN
  ALTER TABLE attendance.""AttendanceDevices"" ADD CONSTRAINT ""FK_AttendanceDevices_LocationId_Tenant"" FOREIGN KEY (""TenantId"", ""LocationId"") REFERENCES employee.""Locations"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AttendancePolicies_LocationId_Tenant' AND conrelid = 'attendance.""AttendancePolicies""'::regclass) THEN
  ALTER TABLE attendance.""AttendancePolicies"" ADD CONSTRAINT ""FK_AttendancePolicies_LocationId_Tenant"" FOREIGN KEY (""TenantId"", ""LocationId"") REFERENCES employee.""Locations"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AttendanceRequests_AttendanceDayId_Tenant' AND conrelid = 'attendance.""AttendanceRequests""'::regclass) THEN
  ALTER TABLE attendance.""AttendanceRequests"" ADD CONSTRAINT ""FK_AttendanceRequests_AttendanceDayId_Tenant"" FOREIGN KEY (""TenantId"", ""AttendanceDayId"") REFERENCES attendance.""AttendanceDays"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AttendanceRequests_EmployeeId_Tenant' AND conrelid = 'attendance.""AttendanceRequests""'::regclass) THEN
  ALTER TABLE attendance.""AttendanceRequests"" ADD CONSTRAINT ""FK_AttendanceRequests_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_RosterEntries_EmployeeId_Tenant' AND conrelid = 'attendance.""RosterEntries""'::regclass) THEN
  ALTER TABLE attendance.""RosterEntries"" ADD CONSTRAINT ""FK_RosterEntries_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_RosterEntries_ShiftId_Tenant' AND conrelid = 'attendance.""RosterEntries""'::regclass) THEN
  ALTER TABLE attendance.""RosterEntries"" ADD CONSTRAINT ""FK_RosterEntries_ShiftId_Tenant"" FOREIGN KEY (""TenantId"", ""ShiftId"") REFERENCES attendance.""Shifts"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ShiftAssignments_EmployeeId_Tenant' AND conrelid = 'attendance.""ShiftAssignments""'::regclass) THEN
  ALTER TABLE attendance.""ShiftAssignments"" ADD CONSTRAINT ""FK_ShiftAssignments_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ShiftAssignments_ShiftId_Tenant' AND conrelid = 'attendance.""ShiftAssignments""'::regclass) THEN
  ALTER TABLE attendance.""ShiftAssignments"" ADD CONSTRAINT ""FK_ShiftAssignments_ShiftId_Tenant"" FOREIGN KEY (""TenantId"", ""ShiftId"") REFERENCES attendance.""Shifts"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Departments_HeadEmployeeId_Tenant' AND conrelid = 'employee.""Departments""'::regclass) THEN
  ALTER TABLE employee.""Departments"" ADD CONSTRAINT ""FK_Departments_HeadEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""HeadEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Departments_ParentDepartmentId_Tenant' AND conrelid = 'employee.""Departments""'::regclass) THEN
  ALTER TABLE employee.""Departments"" ADD CONSTRAINT ""FK_Departments_ParentDepartmentId_Tenant"" FOREIGN KEY (""TenantId"", ""ParentDepartmentId"") REFERENCES employee.""Departments"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeDocuments_EmployeeId_Tenant' AND conrelid = 'employee.""EmployeeDocuments""'::regclass) THEN
  ALTER TABLE employee.""EmployeeDocuments"" ADD CONSTRAINT ""FK_EmployeeDocuments_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeJobHistory_EmployeeId_Tenant' AND conrelid = 'employee.""EmployeeJobHistory""'::regclass) THEN
  ALTER TABLE employee.""EmployeeJobHistory"" ADD CONSTRAINT ""FK_EmployeeJobHistory_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Employees_DepartmentId_Tenant' AND conrelid = 'employee.""Employees""'::regclass) THEN
  ALTER TABLE employee.""Employees"" ADD CONSTRAINT ""FK_Employees_DepartmentId_Tenant"" FOREIGN KEY (""TenantId"", ""DepartmentId"") REFERENCES employee.""Departments"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Employees_DesignationId_Tenant' AND conrelid = 'employee.""Employees""'::regclass) THEN
  ALTER TABLE employee.""Employees"" ADD CONSTRAINT ""FK_Employees_DesignationId_Tenant"" FOREIGN KEY (""TenantId"", ""DesignationId"") REFERENCES employee.""Designations"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Employees_LocationId_Tenant' AND conrelid = 'employee.""Employees""'::regclass) THEN
  ALTER TABLE employee.""Employees"" ADD CONSTRAINT ""FK_Employees_LocationId_Tenant"" FOREIGN KEY (""TenantId"", ""LocationId"") REFERENCES employee.""Locations"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Employees_ManagerId_Tenant' AND conrelid = 'employee.""Employees""'::regclass) THEN
  ALTER TABLE employee.""Employees"" ADD CONSTRAINT ""FK_Employees_ManagerId_Tenant"" FOREIGN KEY (""TenantId"", ""ManagerId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Holidays_LocationId_Tenant' AND conrelid = 'employee.""Holidays""'::regclass) THEN
  ALTER TABLE employee.""Holidays"" ADD CONSTRAINT ""FK_Holidays_LocationId_Tenant"" FOREIGN KEY (""TenantId"", ""LocationId"") REFERENCES employee.""Locations"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeaveBalances_EmployeeId_Tenant' AND conrelid = 'employee.""LeaveBalances""'::regclass) THEN
  ALTER TABLE employee.""LeaveBalances"" ADD CONSTRAINT ""FK_LeaveBalances_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeaveBalances_LeaveTypeId_Tenant' AND conrelid = 'employee.""LeaveBalances""'::regclass) THEN
  ALTER TABLE employee.""LeaveBalances"" ADD CONSTRAINT ""FK_LeaveBalances_LeaveTypeId_Tenant"" FOREIGN KEY (""TenantId"", ""LeaveTypeId"") REFERENCES employee.""LeaveTypes"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeavePolicies_LocationId_Tenant' AND conrelid = 'employee.""LeavePolicies""'::regclass) THEN
  ALTER TABLE employee.""LeavePolicies"" ADD CONSTRAINT ""FK_LeavePolicies_LocationId_Tenant"" FOREIGN KEY (""TenantId"", ""LocationId"") REFERENCES employee.""Locations"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeaveRequests_AttachmentDocumentId_Tenant' AND conrelid = 'employee.""LeaveRequests""'::regclass) THEN
  ALTER TABLE employee.""LeaveRequests"" ADD CONSTRAINT ""FK_LeaveRequests_AttachmentDocumentId_Tenant"" FOREIGN KEY (""TenantId"", ""AttachmentDocumentId"") REFERENCES employee.""EmployeeDocuments"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeaveRequests_EmployeeId_Tenant' AND conrelid = 'employee.""LeaveRequests""'::regclass) THEN
  ALTER TABLE employee.""LeaveRequests"" ADD CONSTRAINT ""FK_LeaveRequests_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeaveRequests_LeaveTypeId_Tenant' AND conrelid = 'employee.""LeaveRequests""'::regclass) THEN
  ALTER TABLE employee.""LeaveRequests"" ADD CONSTRAINT ""FK_LeaveRequests_LeaveTypeId_Tenant"" FOREIGN KEY (""TenantId"", ""LeaveTypeId"") REFERENCES employee.""LeaveTypes"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
ALTER TABLE attendance.""AttendanceDays"" VALIDATE CONSTRAINT ""FK_AttendanceDays_EmployeeId_Tenant"";
ALTER TABLE attendance.""AttendanceDays"" VALIDATE CONSTRAINT ""FK_AttendanceDays_LeaveRequestId_Tenant"";
ALTER TABLE attendance.""AttendanceDays"" VALIDATE CONSTRAINT ""FK_AttendanceDays_ShiftId_Tenant"";
ALTER TABLE attendance.""AttendanceDevices"" VALIDATE CONSTRAINT ""FK_AttendanceDevices_LocationId_Tenant"";
ALTER TABLE attendance.""AttendancePolicies"" VALIDATE CONSTRAINT ""FK_AttendancePolicies_LocationId_Tenant"";
ALTER TABLE attendance.""AttendanceRequests"" VALIDATE CONSTRAINT ""FK_AttendanceRequests_AttendanceDayId_Tenant"";
ALTER TABLE attendance.""AttendanceRequests"" VALIDATE CONSTRAINT ""FK_AttendanceRequests_EmployeeId_Tenant"";
ALTER TABLE attendance.""RosterEntries"" VALIDATE CONSTRAINT ""FK_RosterEntries_EmployeeId_Tenant"";
ALTER TABLE attendance.""RosterEntries"" VALIDATE CONSTRAINT ""FK_RosterEntries_ShiftId_Tenant"";
ALTER TABLE attendance.""ShiftAssignments"" VALIDATE CONSTRAINT ""FK_ShiftAssignments_EmployeeId_Tenant"";
ALTER TABLE attendance.""ShiftAssignments"" VALIDATE CONSTRAINT ""FK_ShiftAssignments_ShiftId_Tenant"";
ALTER TABLE employee.""Departments"" VALIDATE CONSTRAINT ""FK_Departments_HeadEmployeeId_Tenant"";
ALTER TABLE employee.""Departments"" VALIDATE CONSTRAINT ""FK_Departments_ParentDepartmentId_Tenant"";
ALTER TABLE employee.""EmployeeDocuments"" VALIDATE CONSTRAINT ""FK_EmployeeDocuments_EmployeeId_Tenant"";
ALTER TABLE employee.""EmployeeJobHistory"" VALIDATE CONSTRAINT ""FK_EmployeeJobHistory_EmployeeId_Tenant"";
ALTER TABLE employee.""Employees"" VALIDATE CONSTRAINT ""FK_Employees_DepartmentId_Tenant"";
ALTER TABLE employee.""Employees"" VALIDATE CONSTRAINT ""FK_Employees_DesignationId_Tenant"";
ALTER TABLE employee.""Employees"" VALIDATE CONSTRAINT ""FK_Employees_LocationId_Tenant"";
ALTER TABLE employee.""Employees"" VALIDATE CONSTRAINT ""FK_Employees_ManagerId_Tenant"";
ALTER TABLE employee.""Holidays"" VALIDATE CONSTRAINT ""FK_Holidays_LocationId_Tenant"";
ALTER TABLE employee.""LeaveBalances"" VALIDATE CONSTRAINT ""FK_LeaveBalances_EmployeeId_Tenant"";
ALTER TABLE employee.""LeaveBalances"" VALIDATE CONSTRAINT ""FK_LeaveBalances_LeaveTypeId_Tenant"";
ALTER TABLE employee.""LeavePolicies"" VALIDATE CONSTRAINT ""FK_LeavePolicies_LocationId_Tenant"";
ALTER TABLE employee.""LeaveRequests"" VALIDATE CONSTRAINT ""FK_LeaveRequests_AttachmentDocumentId_Tenant"";
ALTER TABLE employee.""LeaveRequests"" VALIDATE CONSTRAINT ""FK_LeaveRequests_EmployeeId_Tenant"";
ALTER TABLE employee.""LeaveRequests"" VALIDATE CONSTRAINT ""FK_LeaveRequests_LeaveTypeId_Tenant"";
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE attendance.""AttendanceDays"" DROP CONSTRAINT IF EXISTS ""FK_AttendanceDays_EmployeeId_Tenant"";
ALTER TABLE attendance.""AttendanceDays"" DROP CONSTRAINT IF EXISTS ""FK_AttendanceDays_LeaveRequestId_Tenant"";
ALTER TABLE attendance.""AttendanceDays"" DROP CONSTRAINT IF EXISTS ""FK_AttendanceDays_ShiftId_Tenant"";
ALTER TABLE attendance.""AttendanceDevices"" DROP CONSTRAINT IF EXISTS ""FK_AttendanceDevices_LocationId_Tenant"";
ALTER TABLE attendance.""AttendancePolicies"" DROP CONSTRAINT IF EXISTS ""FK_AttendancePolicies_LocationId_Tenant"";
ALTER TABLE attendance.""AttendanceRequests"" DROP CONSTRAINT IF EXISTS ""FK_AttendanceRequests_AttendanceDayId_Tenant"";
ALTER TABLE attendance.""AttendanceRequests"" DROP CONSTRAINT IF EXISTS ""FK_AttendanceRequests_EmployeeId_Tenant"";
ALTER TABLE attendance.""RosterEntries"" DROP CONSTRAINT IF EXISTS ""FK_RosterEntries_EmployeeId_Tenant"";
ALTER TABLE attendance.""RosterEntries"" DROP CONSTRAINT IF EXISTS ""FK_RosterEntries_ShiftId_Tenant"";
ALTER TABLE attendance.""ShiftAssignments"" DROP CONSTRAINT IF EXISTS ""FK_ShiftAssignments_EmployeeId_Tenant"";
ALTER TABLE attendance.""ShiftAssignments"" DROP CONSTRAINT IF EXISTS ""FK_ShiftAssignments_ShiftId_Tenant"";
ALTER TABLE employee.""Departments"" DROP CONSTRAINT IF EXISTS ""FK_Departments_HeadEmployeeId_Tenant"";
ALTER TABLE employee.""Departments"" DROP CONSTRAINT IF EXISTS ""FK_Departments_ParentDepartmentId_Tenant"";
ALTER TABLE employee.""EmployeeDocuments"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeDocuments_EmployeeId_Tenant"";
ALTER TABLE employee.""EmployeeJobHistory"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeJobHistory_EmployeeId_Tenant"";
ALTER TABLE employee.""Employees"" DROP CONSTRAINT IF EXISTS ""FK_Employees_DepartmentId_Tenant"";
ALTER TABLE employee.""Employees"" DROP CONSTRAINT IF EXISTS ""FK_Employees_DesignationId_Tenant"";
ALTER TABLE employee.""Employees"" DROP CONSTRAINT IF EXISTS ""FK_Employees_LocationId_Tenant"";
ALTER TABLE employee.""Employees"" DROP CONSTRAINT IF EXISTS ""FK_Employees_ManagerId_Tenant"";
ALTER TABLE employee.""Holidays"" DROP CONSTRAINT IF EXISTS ""FK_Holidays_LocationId_Tenant"";
ALTER TABLE employee.""LeaveBalances"" DROP CONSTRAINT IF EXISTS ""FK_LeaveBalances_EmployeeId_Tenant"";
ALTER TABLE employee.""LeaveBalances"" DROP CONSTRAINT IF EXISTS ""FK_LeaveBalances_LeaveTypeId_Tenant"";
ALTER TABLE employee.""LeavePolicies"" DROP CONSTRAINT IF EXISTS ""FK_LeavePolicies_LocationId_Tenant"";
ALTER TABLE employee.""LeaveRequests"" DROP CONSTRAINT IF EXISTS ""FK_LeaveRequests_AttachmentDocumentId_Tenant"";
ALTER TABLE employee.""LeaveRequests"" DROP CONSTRAINT IF EXISTS ""FK_LeaveRequests_EmployeeId_Tenant"";
ALTER TABLE employee.""LeaveRequests"" DROP CONSTRAINT IF EXISTS ""FK_LeaveRequests_LeaveTypeId_Tenant"";
DROP INDEX IF EXISTS attendance.""UX_AttendanceDays_TenantId_Id"";
DROP INDEX IF EXISTS attendance.""UX_Shifts_TenantId_Id"";
DROP INDEX IF EXISTS employee.""UX_Departments_TenantId_Id"";
DROP INDEX IF EXISTS employee.""UX_Designations_TenantId_Id"";
DROP INDEX IF EXISTS employee.""UX_EmployeeDocuments_TenantId_Id"";
DROP INDEX IF EXISTS employee.""UX_Employees_TenantId_Id"";
DROP INDEX IF EXISTS employee.""UX_LeaveRequests_TenantId_Id"";
DROP INDEX IF EXISTS employee.""UX_LeaveTypes_TenantId_Id"";
DROP INDEX IF EXISTS employee.""UX_Locations_TenantId_Id"";
");
        }
    }
}
