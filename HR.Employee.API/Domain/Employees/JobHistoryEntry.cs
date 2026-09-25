namespace HR.Employee.API.Domain.Employees;

using HR.Employee.API.Domain.Common;

/// <summary>Append-only: har job change ka snapshot. Employee ke andar se hi banta hai.</summary>
public sealed class JobHistoryEntry : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public JobChangeType ChangeType { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid DesignationId { get; private set; }
    public Guid? ManagerId { get; private set; }
    public EmploymentStatus EmploymentStatus { get; private set; }
    public string? Remarks { get; private set; }

    private JobHistoryEntry() { }

    internal static JobHistoryEntry Snapshot(Employee employee, JobChangeType type, DateOnly effectiveDate, string? remarks)
        => new()
        {
            TenantId = employee.TenantId,
            EffectiveDate = effectiveDate,
            ChangeType = type,
            LocationId = employee.LocationId,
            DepartmentId = employee.DepartmentId,
            DesignationId = employee.DesignationId,
            ManagerId = employee.ManagerId,
            EmploymentStatus = employee.EmploymentStatus,
            Remarks = Guard.Optional(remarks, "Remarks", 500)
        };
}
