namespace HR.Payroll.API.Domain.Expenses;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Har tenant ki ek row: expense claims + travel ke qaide. Claim submit aur approve pe inhi pe check hota hai.
/// Payroll se reimbursement ke liye earning component, aur advance zyada ho to wapsi ke liye deduction component.
/// </summary>
public sealed class ExpensePolicy : AuditableEntity
{
    public bool ClaimsEnabled { get; private set; } = true;
    public bool TravelEnabled { get; private set; } = true;
    public decimal? ReceiptRequiredAbove { get; private set; } = 0;   // null = kabhi zaroori nahi; 0 = har line pe
    public short SubmitWithinDays { get; private set; } = 90;         // kharche ke kitne din baad tak claim
    public bool AdvancesEnabled { get; private set; } = true;
    public decimal MaxAdvancePercent { get; private set; } = 75;      // travel ke estimated cost ka %
    public Guid? ReimbursementComponentId { get; private set; }      // earning (non-taxable)
    public Guid? AdvanceRecoveryComponentId { get; private set; }    // deduction

    private ExpensePolicy() { }

    public static ExpensePolicy CreateDefault(Guid tenantId) => new() { TenantId = Guard.NotEmpty(tenantId, "Tenant") };

    public void Update(
        bool claimsEnabled, bool travelEnabled, decimal? receiptRequiredAbove, short submitWithinDays,
        bool advancesEnabled, decimal maxAdvancePercent, Guid? reimbursementComponentId, Guid? advanceRecoveryComponentId)
    {
        if (receiptRequiredAbove is < 0)
            throw new DomainException("Receipt limit cannot be negative.");
        if (submitWithinDays is < 1 or > 365)
            throw new DomainException("Claims must be allowed between 1 and 365 days after the expense.");
        if (maxAdvancePercent is <= 0 or > 100)
            throw new DomainException("Advance limit must be between 1 and 100 percent of the estimated cost.");

        ClaimsEnabled = claimsEnabled;
        TravelEnabled = travelEnabled;
        ReceiptRequiredAbove = receiptRequiredAbove;
        SubmitWithinDays = submitWithinDays;
        AdvancesEnabled = advancesEnabled;
        MaxAdvancePercent = maxAdvancePercent;
        ReimbursementComponentId = reimbursementComponentId;
        AdvanceRecoveryComponentId = advanceRecoveryComponentId;
    }

    public bool NeedsReceipt(decimal amount, bool categoryRequires)
        => categoryRequires || (ReceiptRequiredAbove is { } limit && amount > limit);

    public decimal MaxAdvanceFor(decimal estimatedCost) => Math.Round(estimatedCost * MaxAdvancePercent / 100m, 2);
}
