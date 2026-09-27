namespace HR.Payroll.API.Domain.Inputs;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Employee API ka LeaveRequestApproved (unpaid type) → har din ki ek row. Cancelled → rows delete.
/// Unique (LeaveRequestId, LeaveDate) event redelivery pe double deduction rokta hai.
/// Hard delete wali table hai (AuditableEntity nahi) — cancel ka matlab "ye din kabhi unpaid tha hi nahi".
/// </summary>
public sealed class UnpaidLeaveDay : Entity
{
    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid LeaveRequestId { get; private set; }
    public DateOnly LeaveDate { get; private set; }
    public decimal DayFraction { get; private set; }
    public DateTime ReceivedAt { get; private set; }

    private UnpaidLeaveDay() { }

    public static UnpaidLeaveDay Create(Guid tenantId, Guid employeeId, Guid leaveRequestId, DateOnly leaveDate, decimal dayFraction)
    {
        if (dayFraction is not (0.5m or 1m))
            throw new DomainException("Day fraction must be 0.5 or 1.");

        return new UnpaidLeaveDay
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            LeaveRequestId = Guard.NotEmpty(leaveRequestId, "Leave request"),
            LeaveDate = leaveDate,
            DayFraction = dayFraction,
            ReceivedAt = DateTime.UtcNow
        };
    }
}
