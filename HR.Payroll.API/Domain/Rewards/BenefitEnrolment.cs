namespace HR.Payroll.API.Domain.Rewards;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Employee ka ek plan mein hona. Kharcha enrolment pe snapshot hota hai (plan ka rate baad mein badle to purani coverage na badle).
/// Employee "Requested" bhejta hai → HR approve (Active) / reject. HR seedha bhi enrol kar sakta hai.
/// </summary>
public sealed class BenefitEnrolment : AuditableEntity
{
    public Guid BenefitPlanId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public EnrolmentStatus Status { get; private set; }
    public short Dependents { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public decimal EmployerMonthlyCost { get; private set; }
    public decimal EmployeeMonthlyCost { get; private set; }
    public string? EmployeeNote { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    public bool IsLive => Status is EnrolmentStatus.Requested or EnrolmentStatus.Active;

    private BenefitEnrolment() { }

    public static BenefitEnrolment Request(Guid tenantId, BenefitPlan plan, Guid employeeId, short dependents, string? note)
    {
        if (!plan.IsActive || !plan.OpenForRequests)
            throw new DomainException($"{plan.Name} is not open for requests.");
        var e = New(tenantId, plan, employeeId, dependents);
        e.Status = EnrolmentStatus.Requested;
        e.EmployeeNote = Guard.Optional(note, "Note", 500);
        return e;
    }

    /// <summary>HR ka seedha enrolment.</summary>
    public static BenefitEnrolment Enrol(Guid tenantId, BenefitPlan plan, Guid employeeId, short dependents, DateOnly startDate, Guid byUserId, string? note)
    {
        if (!plan.IsActive)
            throw new DomainException($"{plan.Name} is no longer offered.");
        var e = New(tenantId, plan, employeeId, dependents);
        e.Status = EnrolmentStatus.Active;
        e.StartDate = startDate;
        e.Decide(byUserId, note);
        return e;
    }

    private static BenefitEnrolment New(Guid tenantId, BenefitPlan plan, Guid employeeId, short dependents)
    {
        var e = new BenefitEnrolment
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            BenefitPlanId = plan.Id,
            EmployeeId = Guard.NotEmpty(employeeId, "Employee")
        };
        e.Price(plan, dependents);
        return e;
    }

    public void Approve(BenefitPlan plan, DateOnly startDate, short dependents, Guid byUserId, string? note)
    {
        if (Status != EnrolmentStatus.Requested)
            throw new DomainException("This request is no longer waiting for a decision.");
        Price(plan, dependents);
        Status = EnrolmentStatus.Active;
        StartDate = startDate;
        Decide(byUserId, note);
    }

    public void Reject(Guid byUserId, string reason)
    {
        if (Status != EnrolmentStatus.Requested)
            throw new DomainException("This request is no longer waiting for a decision.");
        Status = EnrolmentStatus.Rejected;
        Decide(byUserId, Guard.Required(reason, "Reason", 500));
    }

    public void Cancel(Guid byEmployeeId)
    {
        if (byEmployeeId != EmployeeId)
            throw new DomainException("Only the employee can cancel their own request.");
        if (Status != EnrolmentStatus.Requested)
            throw new DomainException("Only a pending request can be cancelled.");
        Status = EnrolmentStatus.Cancelled;
    }

    /// <summary>HR: dependents badle (shaadi, bacha) — naya rate abhi se.</summary>
    public void ChangeDependents(BenefitPlan plan, short dependents)
    {
        if (Status != EnrolmentStatus.Active)
            throw new DomainException("Only active coverage can be changed.");
        Price(plan, dependents);
    }

    public void End(DateOnly endDate, Guid byUserId, string? note)
    {
        if (Status != EnrolmentStatus.Active)
            throw new DomainException("Only active coverage can be ended.");
        if (StartDate is { } start && endDate < start)
            throw new DomainException("Coverage cannot end before it started.");
        Status = EnrolmentStatus.Ended;
        EndDate = endDate;
        Decide(byUserId, note);
    }

    /// <summary>Payroll run: is period ke aakhri din coverage chal rahi thi?</summary>
    public bool CoversDate(DateOnly date)
        => StartDate is { } start && start <= date && (EndDate is null || EndDate >= date)
           && Status is EnrolmentStatus.Active or EnrolmentStatus.Ended;

    private void Price(BenefitPlan plan, short dependents)
    {
        if (dependents < 0 || dependents > plan.MaxDependents)
            throw new DomainException(plan.MaxDependents == 0
                ? $"{plan.Name} covers the employee only."
                : $"{plan.Name} covers up to {plan.MaxDependents} dependents.");
        Dependents = dependents;
        EmployerMonthlyCost = plan.EmployerCostFor(dependents);
        EmployeeMonthlyCost = plan.EmployeeMonthlyCost;
    }

    private void Decide(Guid userId, string? note)
    {
        DecidedByUserId = Guard.NotEmpty(userId, "User");
        DecidedAt = DateTime.UtcNow;
        DecisionNote = Guard.Optional(note, "Note", 500);
    }
}
