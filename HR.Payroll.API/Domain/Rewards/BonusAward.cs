namespace HR.Payroll.API.Domain.Rewards;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Ek employee ka bonus. Batch (Eid bonus sab ko) ek BatchId share karte hain. Approve (payroll.approve) pe:
/// Payroll payout = agle khule period mein PayrollInput (Source = Bonus, BON:{id}); Direct = HR alag se de kar paid mark kare.
/// </summary>
public sealed class BonusAward : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public BonusType BonusType { get; private set; }
    public string Title { get; private set; } = default!;
    public string CurrencyCode { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public Guid PayComponentId { get; private set; }
    public Guid? BatchId { get; private set; }
    public string? Reason { get; private set; }
    public BonusStatus Status { get; private set; } = BonusStatus.Pending;
    public PayoutMethod? PayoutMethod { get; private set; }
    public Guid? PayrollInputId { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    private BonusAward() { }

    public static BonusAward Propose(
        Guid tenantId, Guid employeeId, BonusType type, string title, string currencyCode, decimal amount, Guid payComponentId,
        Guid? batchId, string? reason)
    {
        var b = new BonusAward
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            CurrencyCode = Guard.Currency(currencyCode),
            BatchId = batchId
        };
        b.Edit(type, title, amount, payComponentId, reason);
        return b;
    }

    public void Edit(BonusType type, string title, decimal amount, Guid payComponentId, string? reason)
    {
        EnsurePending();
        BonusType = Enum.IsDefined(type) ? type : throw new DomainException("Pick a bonus type.");
        Title = Guard.Required(title, "Title", 150);
        Amount = Math.Round(Guard.Positive(amount, "Amount"), 2);
        PayComponentId = Guard.NotEmpty(payComponentId, "Pay component");
        Reason = Guard.Optional(reason, "Reason", 500);
    }

    public void Approve(PayoutMethod payout, Guid byUserId, string? note)
    {
        EnsurePending();
        Status = BonusStatus.Approved;
        PayoutMethod = payout;
        Decide(byUserId, note);
    }

    public void AttachPayrollInput(Guid inputId)
    {
        if (Status != BonusStatus.Approved || PayoutMethod != Common.PayoutMethod.Payroll)
            throw new DomainException("Only an approved payroll bonus can be scheduled in payroll.");
        PayrollInputId = Guard.NotEmpty(inputId, "Payroll input");
    }

    public void MarkPaid()
    {
        if (Status != BonusStatus.Approved || PayoutMethod != Common.PayoutMethod.Direct)
            throw new DomainException("Only an approved bonus paid outside payroll can be marked as paid.");
        Status = BonusStatus.Paid;
        PaidAt = DateTime.UtcNow;
    }

    public void Reject(Guid byUserId, string reason)
    {
        EnsurePending();
        Status = BonusStatus.Rejected;
        Decide(byUserId, Guard.Required(reason, "Reason", 500));
    }

    public void Cancel(Guid byUserId)
    {
        EnsurePending();
        Status = BonusStatus.Cancelled;
        Decide(byUserId, null);
    }

    private void EnsurePending()
    {
        if (Status != BonusStatus.Pending)
            throw new DomainException("This bonus is no longer pending.");
    }

    private void Decide(Guid userId, string? note)
    {
        DecidedByUserId = Guard.NotEmpty(userId, "User");
        DecidedAt = DateTime.UtcNow;
        DecisionNote = Guard.Optional(note, "Note", 500);
    }
}
