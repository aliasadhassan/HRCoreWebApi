namespace HR.Employee.API.Domain.Leaves;

using HR.Employee.API.Domain.Common;

/// <summary>Per employee, per leave type, per leave year. RowVersion do parallel requests ko pakadta hai.</summary>
public sealed class LeaveBalance : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public short LeaveYear { get; private set; }
    public decimal Entitled { get; private set; }
    public decimal CarriedForward { get; private set; }
    public decimal Adjusted { get; private set; }      // HR manual +/-
    public decimal Used { get; private set; }
    public decimal Pending { get; private set; }       // submitted, abhi approve nahi

    public decimal Available => Entitled + CarriedForward + Adjusted - Used - Pending;

    private LeaveBalance() { }

    public static LeaveBalance Create(Guid tenantId, Guid employeeId, Guid leaveTypeId, short leaveYear, decimal entitled, decimal carriedForward)
    {
        if (entitled < 0 || carriedForward < 0)
            throw new DomainException("Entitlement and carry forward cannot be negative.");

        return new LeaveBalance
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            LeaveTypeId = Guard.NotEmpty(leaveTypeId, "Leave type"),
            LeaveYear = leaveYear,
            Entitled = entitled,
            CarriedForward = carriedForward
        };
    }

    /// <summary>Request submit: din "pending" mein chale jate hain.</summary>
    public void Reserve(decimal days, bool allowNegative)
    {
        EnsurePositive(days);
        if (!allowNegative && days > Available)
            throw new DomainException($"Insufficient leave balance. Available: {Available:0.##}, requested: {days:0.##}.");
        Pending += days;
    }

    /// <summary>Reject / pending cancel: reserved din wapas.</summary>
    public void ReleasePending(decimal days)
    {
        EnsurePositive(days);
        Pending = Math.Max(0, Pending - days);
    }

    /// <summary>Final approval: pending se used.</summary>
    public void ConfirmUsage(decimal days)
    {
        EnsurePositive(days);
        Pending = Math.Max(0, Pending - days);
        Used += days;
    }

    /// <summary>Approved leave cancel: used din wapas.</summary>
    public void RestoreUsed(decimal days)
    {
        EnsurePositive(days);
        Used = Math.Max(0, Used - days);
    }

    public void Adjust(decimal delta) => Adjusted += delta;

    public void SetEntitlement(decimal entitled)
    {
        if (entitled < 0)
            throw new DomainException("Entitlement cannot be negative.");
        Entitled = entitled;
    }

    private static void EnsurePositive(decimal days)
    {
        if (days <= 0)
            throw new DomainException("Days must be greater than zero.");
    }
}
