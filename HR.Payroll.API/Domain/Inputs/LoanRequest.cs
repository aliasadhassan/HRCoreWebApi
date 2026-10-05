namespace HR.Payroll.API.Domain.Inputs;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Employee ki loan / salary advance ki darkhwast. Approve pe asal EmployeeLoan banta hai
/// (HR raqam ya installment kam kar sakta hai) aur payroll run mein katauti shuru hoti hai.
/// Approval ek level: payroll.approve wala.
/// </summary>
public sealed class LoanRequest : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public LoanType LoanType { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public decimal RequestedAmount { get; private set; }
    public short RequestedInstallments { get; private set; }
    public DateOnly PreferredStartDate { get; private set; }          // pehli katauti kis period se
    public string Reason { get; private set; } = default!;
    public LoanRequestStatus Status { get; private set; } = LoanRequestStatus.Pending;

    public decimal? ApprovedAmount { get; private set; }
    public decimal? ApprovedInstallmentAmount { get; private set; }
    public Guid? EmployeeLoanId { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionComment { get; private set; }

    private LoanRequest() { }

    public static LoanRequest Submit(
        Guid tenantId, Guid employeeId, LoanType type, string currencyCode, decimal amount,
        short installments, DateOnly preferredStartDate, string reason)
    {
        Guard.Positive(amount, "Amount");
        if (installments is < 1 or > 120)
            throw new DomainException("Installments must be between 1 and 120.");

        return new LoanRequest
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            LoanType = type,
            CurrencyCode = Guard.Currency(currencyCode),
            RequestedAmount = amount,
            RequestedInstallments = installments,
            PreferredStartDate = preferredStartDate,
            Reason = Guard.Required(reason, "Reason", 1000)
        };
    }

    /// <summary>Handler pehle EmployeeLoan banata hai, phir uska Id yahan jorta hai.</summary>
    public void MarkApproved(Guid decidedByUserId, EmployeeLoan loan, string? comment)
    {
        EnsurePending();
        if (loan.EmployeeId != EmployeeId)
            throw new DomainException("The loan belongs to a different employee.");

        Status = LoanRequestStatus.Approved;
        ApprovedAmount = loan.PrincipalAmount;
        ApprovedInstallmentAmount = loan.InstallmentAmount;
        EmployeeLoanId = loan.Id;
        Decide(decidedByUserId, comment);
    }

    public void Reject(Guid decidedByUserId, string comment)
    {
        EnsurePending();
        Status = LoanRequestStatus.Rejected;
        Decide(decidedByUserId, Guard.Required(comment, "Rejection reason", 500));
    }

    public void Cancel(Guid byEmployeeId)
    {
        if (byEmployeeId != EmployeeId)
            throw new DomainException("Only the employee can cancel their own request.");
        EnsurePending();
        Status = LoanRequestStatus.Cancelled;
    }

    private void Decide(Guid userId, string? comment)
    {
        DecidedByUserId = Guard.NotEmpty(userId, "Approver");
        DecidedAt = DateTime.UtcNow;
        DecisionComment = Guard.Optional(comment, "Comment", 500);
    }

    private void EnsurePending()
    {
        if (Status != LoanRequestStatus.Pending)
            throw new DomainException("This request is no longer pending.");
    }
}
