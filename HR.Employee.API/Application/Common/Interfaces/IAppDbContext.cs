namespace HR.Employee.API.Application.Common.Interfaces;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Leaves;
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
