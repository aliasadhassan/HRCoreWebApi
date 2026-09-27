namespace HR.Payroll.API.Domain.Setup;

using HR.Payroll.API.Domain.Common;

public sealed class PayPeriod : AuditableEntity
{
    public Guid PayGroupId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public DateOnly PayDate { get; private set; }
    public short FiscalYear { get; private set; }
    public byte PeriodNumber { get; private set; }
    public PayPeriodStatus Status { get; private set; } = PayPeriodStatus.Open;

    public int CalendarDays => PeriodEnd.DayNumber - PeriodStart.DayNumber + 1;

    private PayPeriod() { }

    internal static PayPeriod Create(Guid tenantId, Guid payGroupId, DateOnly start, DateOnly end, DateOnly payDate, byte periodNumber)
    {
        if (end < start)
            throw new DomainException("Period end cannot be before period start.");

        return new PayPeriod
        {
            TenantId = tenantId,
            PayGroupId = payGroupId,
            PeriodStart = start,
            PeriodEnd = end,
            PayDate = payDate,
            FiscalYear = (short)start.Year,
            PeriodNumber = periodNumber
        };
    }

    public bool Contains(DateOnly date) => date >= PeriodStart && date <= PeriodEnd;

    /// <summary>Regular run Approved/Paid hone pe — is period ke inputs ab nahi badal sakte.</summary>
    public void Lock() => Status = PayPeriodStatus.Locked;

    public void EnsureOpen()
    {
        if (Status == PayPeriodStatus.Locked)
            throw new DomainException("This pay period is locked; its payroll has already been approved.");
    }
}
