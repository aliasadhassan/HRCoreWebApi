namespace HR.Payroll.API.Domain.Rewards;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Increment / promotion ki tajweez. Approve hote hi handler nayi EmployeeSalary banata hai (purani band) aur yahan Applied.
/// Current salary ka snapshot rakha jata hai taake % aur history baad mein bhi sahi dikhe.
/// </summary>
public sealed class SalaryRevision : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public SalaryChangeReason Reason { get; private set; }
    public Guid CurrentSalaryId { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public SalaryBasis SalaryBasis { get; private set; }
    public decimal CurrentAmount { get; private set; }
    public decimal ProposedAmount { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public string? NewTitle { get; private set; }
    public string? Justification { get; private set; }
    public SalaryRevisionStatus Status { get; private set; } = SalaryRevisionStatus.Pending;
    public Guid? AppliedSalaryId { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    public decimal ChangePercent => CurrentAmount == 0 ? 0 : Math.Round((ProposedAmount - CurrentAmount) / CurrentAmount * 100, 2);

    private SalaryRevision() { }

    public static SalaryRevision Propose(
        Guid tenantId, Guid employeeId, SalaryChangeReason reason, Guid currentSalaryId, string currencyCode, SalaryBasis basis,
        decimal currentAmount, DateOnly currentFrom, decimal proposedAmount, DateOnly effectiveFrom, string? newTitle, string? justification)
    {
        var r = new SalaryRevision
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            CurrentSalaryId = Guard.NotEmpty(currentSalaryId, "Current salary"),
            CurrencyCode = Guard.Currency(currencyCode),
            SalaryBasis = basis,
            CurrentAmount = Guard.Positive(currentAmount, "Current salary")
        };
        r.Edit(reason, proposedAmount, effectiveFrom, currentFrom, newTitle, justification);
        return r;
    }

    public void Edit(SalaryChangeReason reason, decimal proposedAmount, DateOnly effectiveFrom, DateOnly currentFrom, string? newTitle, string? justification)
    {
        EnsurePending();
        if (reason is not (SalaryChangeReason.Increment or SalaryChangeReason.Promotion or SalaryChangeReason.Correction or SalaryChangeReason.Other))
            throw new DomainException("Pick increment, promotion, correction or other.");
        if (effectiveFrom <= currentFrom)
            throw new DomainException($"The new salary must start after the current one ({currentFrom:d MMM yyyy}).");
        Reason = reason;
        ProposedAmount = Guard.Positive(proposedAmount, "New salary");
        if (ProposedAmount == CurrentAmount)
            throw new DomainException("The new salary is the same as the current one.");
        if (ProposedAmount > CurrentAmount * 5)
            throw new DomainException("The new salary is more than five times the current one. Check the amount.");
        EffectiveFrom = effectiveFrom;
        NewTitle = Guard.Optional(newTitle, "New title", 150);
        Justification = Guard.Optional(justification, "Justification", 1000);
    }

    public void MarkApplied(Guid newSalaryId, Guid byUserId, string? note)
    {
        EnsurePending();
        Status = SalaryRevisionStatus.Applied;
        AppliedSalaryId = Guard.NotEmpty(newSalaryId, "New salary");
        Decide(byUserId, note);
    }

    public void Reject(Guid byUserId, string reason)
    {
        EnsurePending();
        Status = SalaryRevisionStatus.Rejected;
        Decide(byUserId, Guard.Required(reason, "Reason", 500));
    }

    public void Cancel(Guid byUserId)
    {
        EnsurePending();
        Status = SalaryRevisionStatus.Cancelled;
        Decide(byUserId, null);
    }

    private void EnsurePending()
    {
        if (Status != SalaryRevisionStatus.Pending)
            throw new DomainException("This salary change is no longer pending.");
    }

    private void Decide(Guid userId, string? note)
    {
        DecidedByUserId = Guard.NotEmpty(userId, "User");
        DecidedAt = DateTime.UtcNow;
        DecisionNote = Guard.Optional(note, "Note", 500);
    }
}
