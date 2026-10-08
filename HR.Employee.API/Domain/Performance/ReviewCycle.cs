namespace HR.Employee.API.Domain.Performance;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Review ka daur (e.g. "2026 H2"). Draft mein setup, Launch par employees ke reviews bante hain, Close ke baad sab lock.
/// Rating scale fixed 1-5 hai.
/// </summary>
public sealed class ReviewCycle : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public bool IncludeSelfReview { get; private set; }
    public DateOnly? SelfReviewDue { get; private set; }
    public DateOnly ManagerReviewDue { get; private set; }
    public CycleStatus Status { get; private set; }
    public DateTime? LaunchedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    private ReviewCycle() { }

    public static ReviewCycle Create(Guid tenantId, string name, string? description, DateOnly periodStart, DateOnly periodEnd,
        bool includeSelfReview, DateOnly? selfReviewDue, DateOnly managerReviewDue)
    {
        var cycle = new ReviewCycle { TenantId = Guard.NotEmpty(tenantId, "Tenant"), Status = CycleStatus.Draft };
        cycle.Update(name, description, periodStart, periodEnd, includeSelfReview, selfReviewDue, managerReviewDue);
        return cycle;
    }

    /// <summary>Draft mein sab badal sakte hain; Active mein sirf naam, description aur due dates.</summary>
    public void Update(string name, string? description, DateOnly periodStart, DateOnly periodEnd,
        bool includeSelfReview, DateOnly? selfReviewDue, DateOnly managerReviewDue)
    {
        if (Status == CycleStatus.Closed)
            throw new DomainException("This review cycle is closed.");
        if (periodEnd < periodStart)
            throw new DomainException("Period end cannot be before the period start.");
        if (Status == CycleStatus.Active && (periodStart != PeriodStart || periodEnd != PeriodEnd || includeSelfReview != IncludeSelfReview))
            throw new DomainException("The period and self review setting cannot change after launch.");
        if (includeSelfReview && selfReviewDue is null)
            throw new DomainException("Set the self review due date.");
        if (includeSelfReview && selfReviewDue > managerReviewDue)
            throw new DomainException("Self reviews must be due on or before manager reviews.");

        Name = Guard.Required(name, "Name", 100);
        Description = Guard.Optional(description, "Description", 500);
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        IncludeSelfReview = includeSelfReview;
        SelfReviewDue = includeSelfReview ? selfReviewDue : null;
        ManagerReviewDue = managerReviewDue;
    }

    public void Launch(DateTime now)
    {
        if (Status != CycleStatus.Draft)
            throw new DomainException("Only a draft cycle can be launched.");
        Status = CycleStatus.Active;
        LaunchedAt = now;
    }

    public void Close(DateTime now)
    {
        if (Status != CycleStatus.Active)
            throw new DomainException("Only an active cycle can be closed.");
        Status = CycleStatus.Closed;
        ClosedAt = now;
    }

    public void EnsureActive()
    {
        if (Status == CycleStatus.Draft)
            throw new DomainException("This review cycle has not been launched yet.");
        if (Status == CycleStatus.Closed)
            throw new DomainException("This review cycle is closed.");
    }

    public ReviewStatus FirstStage => IncludeSelfReview ? ReviewStatus.SelfReview : ReviewStatus.ManagerReview;
}
