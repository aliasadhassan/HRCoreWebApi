namespace HR.Payroll.API.Domain.Inputs;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Har tenant ki ek row: loan / salary advance ke qaide. Employee request karta hai to inhi pe check hota hai;
/// approve karte waqt deduction component yahin se default aata hai.
/// </summary>
public sealed class LoanPolicy : AuditableEntity
{
    public bool LoansEnabled { get; private set; } = true;
    public decimal? MaxLoanAmount { get; private set; }               // null = koi had nahi
    public byte? MaxLoanSalaryMultiple { get; private set; } = 3;     // loan ≤ N × monthly gross
    public short MaxLoanInstallments { get; private set; } = 24;
    public short MinServiceMonths { get; private set; } = 6;          // sirf loan; advance pe nahi

    public bool AdvancesEnabled { get; private set; } = true;
    public decimal MaxAdvancePercent { get; private set; } = 50;      // monthly gross ka %
    public byte MaxAdvanceInstallments { get; private set; } = 1;

    public bool AllowMultipleActive { get; private set; }            // ek waqt mein ek se zyada khula loan
    public Guid? LoanDeductionComponentId { get; private set; }
    public Guid? AdvanceDeductionComponentId { get; private set; }

    private LoanPolicy() { }

    public static LoanPolicy CreateDefault(Guid tenantId) => new() { TenantId = Guard.NotEmpty(tenantId, "Tenant") };

    public void Update(
        bool loansEnabled, decimal? maxLoanAmount, byte? maxLoanSalaryMultiple, short maxLoanInstallments, short minServiceMonths,
        bool advancesEnabled, decimal maxAdvancePercent, byte maxAdvanceInstallments, bool allowMultipleActive,
        Guid? loanDeductionComponentId, Guid? advanceDeductionComponentId)
    {
        if (maxLoanAmount is <= 0)
            throw new DomainException("Maximum loan amount must be greater than zero.");
        if (maxLoanSalaryMultiple is 0 or > 24)
            throw new DomainException("Loan limit must be between 1 and 24 months of salary.");
        if (maxLoanInstallments is < 1 or > 120)
            throw new DomainException("Loan installments must be between 1 and 120.");
        if (minServiceMonths is < 0 or > 120)
            throw new DomainException("Minimum service must be between 0 and 120 months.");
        if (maxAdvancePercent is <= 0 or > 100)
            throw new DomainException("Advance limit must be between 1 and 100 percent of salary.");
        if (maxAdvanceInstallments is < 1 or > 12)
            throw new DomainException("Advance installments must be between 1 and 12.");

        LoansEnabled = loansEnabled;
        MaxLoanAmount = maxLoanAmount;
        MaxLoanSalaryMultiple = maxLoanSalaryMultiple;
        MaxLoanInstallments = maxLoanInstallments;
        MinServiceMonths = minServiceMonths;
        AdvancesEnabled = advancesEnabled;
        MaxAdvancePercent = maxAdvancePercent;
        MaxAdvanceInstallments = maxAdvanceInstallments;
        AllowMultipleActive = allowMultipleActive;
        LoanDeductionComponentId = loanDeductionComponentId;
        AdvanceDeductionComponentId = advanceDeductionComponentId;
    }

    public bool IsEnabled(LoanType type) => type == LoanType.Loan ? LoansEnabled : AdvancesEnabled;

    public short MaxInstallments(LoanType type) => type == LoanType.Loan ? MaxLoanInstallments : MaxAdvanceInstallments;

    public Guid? DeductionComponentFor(LoanType type) => type == LoanType.Loan ? LoanDeductionComponentId : AdvanceDeductionComponentId;

    /// <summary>Monthly gross ke hisaab se zyada se zyada kitna (null = had nahi).</summary>
    public decimal? LimitFor(LoanType type, decimal monthlyGross)
    {
        if (type == LoanType.SalaryAdvance)
            return Math.Round(monthlyGross * MaxAdvancePercent / 100m, 2);

        decimal? bySalary = MaxLoanSalaryMultiple is { } n ? monthlyGross * n : null;
        return (bySalary, MaxLoanAmount) switch
        {
            ({ } a, { } b) => Math.Min(a, b),
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => null
        };
    }
}
