namespace HR.Employee.API.Application.Common.Interfaces;

using HR.Employee.API.Domain.Assets;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Leaves;
using HR.Employee.API.Domain.Lifecycle;
using HR.Employee.API.Domain.Organization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Repository + UnitOfWork ki jagah: DbContext khud dono hai.
/// Handlers is interface se kaam karte hain (tenant filter + soft delete automatic).
/// </summary>
public interface IAppDbContext
{
    DbSet<Location> Locations { get; }
    DbSet<Department> Departments { get; }
    DbSet<Designation> Designations { get; }

    DbSet<Employee> Employees { get; }
    DbSet<EmployeeDocument> EmployeeDocuments { get; }
    DbSet<JobHistoryEntry> EmployeeJobHistory { get; }

    DbSet<Holiday> Holidays { get; }
    DbSet<LeaveType> LeaveTypes { get; }
    DbSet<LeavePolicy> LeavePolicies { get; }
    DbSet<LeaveApprovalSettings> LeaveApprovalSettings { get; }
    DbSet<LeaveBalance> LeaveBalances { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }

    DbSet<Shift> Shifts { get; }
    DbSet<ShiftAssignment> ShiftAssignments { get; }
    DbSet<RosterEntry> RosterEntries { get; }
    DbSet<AttendancePolicy> AttendancePolicies { get; }
    DbSet<AttendanceDevice> AttendanceDevices { get; }
    DbSet<AttendanceDay> AttendanceDays { get; }
    DbSet<AttendanceRequest> AttendanceRequests { get; }

    DbSet<ChecklistTemplate> ChecklistTemplates { get; }
    DbSet<LifecycleCase> LifecycleCases { get; }

    DbSet<AssetCategory> AssetCategories { get; }
    DbSet<Asset> Assets { get; }
    DbSet<AssetAssignment> AssetAssignments { get; }
    DbSet<AssetEvent> AssetEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
