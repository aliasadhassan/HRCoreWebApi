namespace HR.Payroll.API.Domain.Expenses;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Employee ka kharcha wapsi claim: kai lines (har line ek bill). Ek level approval (payroll.approve).
/// Approve pe: linked travel ka advance kat jata hai, baqi raqam payroll (PayrollInput) ya seedhi di jati hai.
/// </summary>
public sealed class ExpenseClaim : AuditableEntity
{
    private readonly List<ExpenseClaimLine> _lines = new();

    public Guid EmployeeId { get; private set; }
    public string Title { get; private set; } = default!;
    public Guid? TravelRequestId { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public decimal TotalAmount { get; private set; }
    public ExpenseClaimStatus Status { get; private set; } = ExpenseClaimStatus.Submitted;

    public decimal? ApprovedAmount { get; private set; }
    public decimal AdvanceAdjusted { get; private set; }               // travel advance jo is claim mein kata
    public PayoutMethod? PayoutMethod { get; private set; }
    public Guid? PayrollInputId { get; private set; }                  // reimbursement earning
    public Guid? RecoveryPayrollInputId { get; private set; }          // advance > claim: wapsi deduction
    public DateTime? PaidAt { get; private set; }

    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionComment { get; private set; }

    public IReadOnlyCollection<ExpenseClaimLine> Lines => _lines.AsReadOnly();

    /// <summary>Approve ke baad employee ko asal mein kitna milna hai (advance ke baad).</summary>
    public decimal NetPayable => Math.Max(0, (ApprovedAmount ?? 0) - AdvanceAdjusted);

    private ExpenseClaim() { }

    public static ExpenseClaim Submit(
        Guid tenantId, Guid employeeId, string title, string currencyCode, Guid? travelRequestId, IEnumerable<ExpenseLineData> lines)
    {
        var claim = new ExpenseClaim
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            Title = Guard.Required(title, "Title", 200),
            CurrencyCode = Guard.Currency(currencyCode),
            TravelRequestId = travelRequestId
        };
        foreach (var line in lines)
            claim._lines.Add(new ExpenseClaimLine(line));
        if (claim._lines.Count == 0)
            throw new DomainException("Add at least one expense.");
        if (claim._lines.Count > 50)
            throw new DomainException("A claim can have at most 50 expenses.");

        claim.TotalAmount = claim._lines.Sum(l => l.Amount);
        return claim;
    }

    /// <summary>
    /// approvedAmount: HR kam kar sakta hai (kuch bill nahi maane). advance: linked travel ka paid advance.
    /// Payout payroll ho to handler PayrollInput bana kar <see cref="AttachPayrollInputs"/> se jorta hai.
    /// </summary>
    public void Approve(Guid decidedByUserId, decimal approvedAmount, decimal advance, PayoutMethod payout, string? comment)
    {
        EnsureSubmitted();
        Guard.NotNegative(approvedAmount, "Approved amount");
        if (approvedAmount > TotalAmount)
            throw new DomainException("Approved amount cannot be more than the claimed amount.");
        if (approvedAmount == 0)
            throw new DomainException("To approve nothing, reject the claim instead.");

        Status = ExpenseClaimStatus.Approved;
        ApprovedAmount = approvedAmount;
        AdvanceAdjusted = Math.Min(Guard.NotNegative(advance, "Advance"), approvedAmount);
        PayoutMethod = payout;
        Decide(decidedByUserId, comment);

        // Advance ne poora claim cover kar liya: dene ko kuch nahi bacha
        if (NetPayable == 0)
        {
            Status = ExpenseClaimStatus.Paid;
            PaidAt = DateTime.UtcNow;
        }
    }

    public void AttachPayrollInputs(Guid? reimbursementInputId, Guid? recoveryInputId)
    {
        if (Status is not (ExpenseClaimStatus.Approved or ExpenseClaimStatus.Paid))
            throw new DomainException("Only an approved claim can be scheduled in payroll.");
        PayrollInputId = reimbursementInputId;
        RecoveryPayrollInputId = recoveryInputId;
    }

    /// <summary>Direct payout: HR ne paisa de diya. Payroll payout run "paid" hone pe khud paid dikhta hai.</summary>
    public void MarkPaid()
    {
        if (Status != ExpenseClaimStatus.Approved)
            throw new DomainException("Only an approved claim can be marked as paid.");
        Status = ExpenseClaimStatus.Paid;
        PaidAt = DateTime.UtcNow;
    }

    public void Reject(Guid decidedByUserId, string comment)
    {
        EnsureSubmitted();
        Status = ExpenseClaimStatus.Rejected;
        Decide(decidedByUserId, Guard.Required(comment, "Rejection reason", 500));
    }

    public void Cancel(Guid byEmployeeId)
    {
        if (byEmployeeId != EmployeeId)
            throw new DomainException("Only the employee can cancel their own claim.");
        EnsureSubmitted();
        Status = ExpenseClaimStatus.Cancelled;
    }

    private void Decide(Guid userId, string? comment)
    {
        DecidedByUserId = Guard.NotEmpty(userId, "Approver");
        DecidedAt = DateTime.UtcNow;
        DecisionComment = Guard.Optional(comment, "Comment", 500);
    }

    private void EnsureSubmitted()
    {
        if (Status != ExpenseClaimStatus.Submitted)
            throw new DomainException("This claim is no longer waiting for a decision.");
    }
}

public sealed record ExpenseLineData(
    Guid CategoryId, DateOnly ExpenseDate, string Description, string? Merchant, decimal Amount, string? ReceiptNumber, bool HasReceipt);

/// <summary>Claim ki ek line. Apna TenantId (composite FK + RLS); AppDbContext Add pe set karta hai.</summary>
public sealed class ExpenseClaimLine : TenantChildEntity
{
    public Guid ExpenseClaimId { get; private set; }
    public Guid CategoryId { get; private set; }
    public DateOnly ExpenseDate { get; private set; }
    public string Description { get; private set; } = default!;
    public string? Merchant { get; private set; }
    public decimal Amount { get; private set; }
    public string? ReceiptNumber { get; private set; }
    public bool HasReceipt { get; private set; }

    private ExpenseClaimLine() { }

    internal ExpenseClaimLine(ExpenseLineData d)
    {
        CategoryId = Guard.NotEmpty(d.CategoryId, "Category");
        ExpenseDate = d.ExpenseDate;
        Description = Guard.Required(d.Description, "Description", 300);
        Merchant = Guard.Optional(d.Merchant, "Merchant", 150);
        Amount = Guard.Positive(d.Amount, "Amount");
        ReceiptNumber = Guard.Optional(d.ReceiptNumber, "Receipt number", 100);
        HasReceipt = d.HasReceipt || ReceiptNumber is not null;
    }
}
