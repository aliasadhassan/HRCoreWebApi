namespace HR.Payroll.API.Domain.Inputs;

using HR.Payroll.API.Domain.Common;

public sealed class EmployeeLoan : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public LoanType LoanType { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public decimal PrincipalAmount { get; private set; }
    public decimal InstallmentAmount { get; private set; }
    public decimal OutstandingAmount { get; private set; }
    public DateOnly StartDate { get; private set; }
    public Guid DeductionComponentId { get; private set; }
    public LoanStatus Status { get; private set; } = LoanStatus.Active;
    public string? Remarks { get; private set; }

    private EmployeeLoan() { }

    public static EmployeeLoan Create(
        Guid tenantId, Guid employeeId, LoanType type, string currencyCode, decimal principal,
        decimal installment, DateOnly startDate, Guid deductionComponentId, string? remarks)
    {
        Guard.Positive(principal, "Principal");
        Guard.Positive(installment, "Installment");
        if (installment > principal)
            throw new DomainException("Installment cannot be greater than the loan amount.");

        return new EmployeeLoan
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            LoanType = type,
            CurrencyCode = Guard.Currency(currencyCode),
            PrincipalAmount = principal,
            InstallmentAmount = installment,
            OutstandingAmount = principal,
            StartDate = startDate,
            DeductionComponentId = Guard.NotEmpty(deductionComponentId, "Deduction component"),
            Remarks = Guard.Optional(remarks, "Remarks", 500)
        };
    }

    /// <summary>Is period mein kitna katna chahiye (aakhri installment chhoti ho sakti hai).</summary>
    public decimal DueFor(DateOnly periodEnd)
        => Status == LoanStatus.Active && StartDate <= periodEnd ? Math.Min(InstallmentAmount, OutstandingAmount) : 0m;

    public void ApplyRepayment(decimal amount)
    {
        Guard.Positive(amount, "Repayment");
        if (amount > OutstandingAmount)
            throw new DomainException("Repayment exceeds the outstanding amount.");

        OutstandingAmount -= amount;
        if (OutstandingAmount == 0)
            Status = LoanStatus.Closed;
    }

    /// <summary>Payslip recalculate hui (run approve se pehle) → pichhli katauti wapas.</summary>
    public void ReverseRepayment(decimal amount)
    {
        Guard.Positive(amount, "Repayment");
        if (OutstandingAmount + amount > PrincipalAmount)
            throw new DomainException("Reversal exceeds the loan amount.");

        OutstandingAmount += amount;
        if (Status == LoanStatus.Closed)
            Status = LoanStatus.Active;
    }

    public void ChangeInstallment(decimal installment)
    {
        EnsureOpen();
        InstallmentAmount = Guard.Positive(installment, "Installment");
    }

    public void Pause() { EnsureOpen(); Status = LoanStatus.Paused; }

    public void Resume()
    {
        if (Status != LoanStatus.Paused)
            throw new DomainException("Only a paused loan can be resumed.");
        Status = LoanStatus.Active;
    }

    public void Cancel()
    {
        if (OutstandingAmount != PrincipalAmount)
            throw new DomainException("A loan with repayments cannot be cancelled; close it instead.");
        Status = LoanStatus.Cancelled;
    }

    private void EnsureOpen()
    {
        if (Status is LoanStatus.Closed or LoanStatus.Cancelled)
            throw new DomainException("This loan is no longer open.");
    }
}
