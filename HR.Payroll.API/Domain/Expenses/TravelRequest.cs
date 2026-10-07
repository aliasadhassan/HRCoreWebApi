namespace HR.Payroll.API.Domain.Expenses;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Safar ki pehle se manzoori, saath mein optional advance. Wapsi pe employee isi travel se claim jorta hai;
/// claim approve hone pe advance claim mein se kat jata hai (Settled).
/// </summary>
public sealed class TravelRequest : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public string Purpose { get; private set; } = default!;
    public string Destination { get; private set; } = default!;
    public DateOnly DepartDate { get; private set; }
    public DateOnly ReturnDate { get; private set; }
    public TravelMode TravelMode { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public decimal EstimatedCost { get; private set; }
    public decimal AdvanceRequested { get; private set; }
    public TravelRequestStatus Status { get; private set; } = TravelRequestStatus.Pending;

    public decimal AdvanceApproved { get; private set; }
    public AdvanceStatus AdvanceStatus { get; private set; } = AdvanceStatus.None;
    public PayoutMethod? AdvancePayoutMethod { get; private set; }
    public Guid? AdvancePayrollInputId { get; private set; }
    public DateTime? AdvancePaidAt { get; private set; }

    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionComment { get; private set; }

    private TravelRequest() { }

    public static TravelRequest Submit(
        Guid tenantId, Guid employeeId, string purpose, string destination, DateOnly departDate, DateOnly returnDate,
        TravelMode mode, string currencyCode, decimal estimatedCost, decimal advanceRequested)
    {
        if (returnDate < departDate)
            throw new DomainException("Return date cannot be before the departure date.");
        Guard.Positive(estimatedCost, "Estimated cost");
        Guard.NotNegative(advanceRequested, "Advance");
        if (advanceRequested > estimatedCost)
            throw new DomainException("Advance cannot be more than the estimated cost.");

        return new TravelRequest
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            Purpose = Guard.Required(purpose, "Purpose", 500),
            Destination = Guard.Required(destination, "Destination", 200),
            DepartDate = departDate,
            ReturnDate = returnDate,
            TravelMode = mode,
            CurrencyCode = Guard.Currency(currencyCode),
            EstimatedCost = estimatedCost,
            AdvanceRequested = advanceRequested
        };
    }

    public void Approve(Guid decidedByUserId, decimal advanceApproved, string? comment)
    {
        EnsurePending();
        Guard.NotNegative(advanceApproved, "Advance");
        if (advanceApproved > EstimatedCost)
            throw new DomainException("Advance cannot be more than the estimated cost.");

        Status = TravelRequestStatus.Approved;
        AdvanceApproved = advanceApproved;
        AdvanceStatus = advanceApproved > 0 ? AdvanceStatus.Approved : AdvanceStatus.None;
        Decide(decidedByUserId, comment);
    }

    public void Reject(Guid decidedByUserId, string comment)
    {
        EnsurePending();
        Status = TravelRequestStatus.Rejected;
        Decide(decidedByUserId, Guard.Required(comment, "Rejection reason", 500));
    }

    public void Cancel(Guid byEmployeeId)
    {
        if (byEmployeeId != EmployeeId)
            throw new DomainException("Only the employee can cancel their own request.");
        EnsurePending();
        Status = TravelRequestStatus.Cancelled;
    }

    /// <summary>Advance diya: payroll ke zariye (input id) ya seedha (cash / bank).</summary>
    public void MarkAdvancePaid(PayoutMethod method, Guid? payrollInputId)
    {
        if (AdvanceStatus != AdvanceStatus.Approved)
            throw new DomainException("There is no approved advance waiting to be paid.");
        if (method == PayoutMethod.Payroll && payrollInputId is null)
            throw new DomainException("Payroll payout needs a payroll input.");

        AdvanceStatus = AdvanceStatus.Paid;
        AdvancePayoutMethod = method;
        AdvancePayrollInputId = payrollInputId;
        AdvancePaidAt = DateTime.UtcNow;
    }

    /// <summary>Claim approve hua: advance us mein adjust ho gaya.</summary>
    public void Settle()
    {
        if (AdvanceStatus != AdvanceStatus.Paid)
            throw new DomainException("Only a paid advance can be settled.");
        AdvanceStatus = AdvanceStatus.Settled;
    }

    private void Decide(Guid userId, string? comment)
    {
        DecidedByUserId = Guard.NotEmpty(userId, "Approver");
        DecidedAt = DateTime.UtcNow;
        DecisionComment = Guard.Optional(comment, "Comment", 500);
    }

    private void EnsurePending()
    {
        if (Status != TravelRequestStatus.Pending)
            throw new DomainException("This request is no longer pending.");
    }
}
