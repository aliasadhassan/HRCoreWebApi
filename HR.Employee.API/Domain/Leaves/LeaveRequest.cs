namespace HR.Employee.API.Domain.Leaves;

using HR.Employee.API.Domain.Common;

public sealed record ApprovalStep(byte Level, ApproverType ApproverType, Guid? AssignedApproverId);

/// <summary>
/// Aggregate root. Submit ke waqt tenant settings ka SNAPSHOT (approval rows) ban jata hai —
/// baad mein settings badlein to chalti requests pe asar nahi.
/// Balance update application handler karta hai (LeaveBalance alag aggregate hai).
/// </summary>
public sealed class LeaveRequest : AuditableEntity
{
    private readonly List<LeaveRequestApproval> _approvals = new();

    public Guid EmployeeId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsHalfDay { get; private set; }
    public HalfDayPeriod? HalfDayPeriod { get; private set; }
    public decimal TotalDays { get; private set; }
    public string? Reason { get; private set; }
    public Guid? AttachmentDocumentId { get; private set; }
    public LeaveRequestStatus Status { get; private set; }
    public byte CurrentApprovalLevel { get; private set; }

    public IReadOnlyCollection<LeaveRequestApproval> Approvals => _approvals.AsReadOnly();

    private LeaveRequest() { }

    public static LeaveRequest Submit(
        Guid tenantId, Guid employeeId, Guid leaveTypeId,
        DateOnly startDate, DateOnly endDate, HalfDayPeriod? halfDayPeriod, decimal totalDays,
        string? reason, Guid? attachmentDocumentId, IReadOnlyList<ApprovalStep> approvalChain)
    {
        if (endDate < startDate)
            throw new DomainException("End date cannot be before the start date.");
        if (halfDayPeriod is not null && startDate != endDate)
            throw new DomainException("A half-day leave must start and end on the same day.");
        if (totalDays <= 0)
            throw new DomainException("The selected dates contain no working days.");
        if (approvalChain.Count == 0)
            throw new DomainException("An approval chain is required.");

        var request = new LeaveRequest
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            LeaveTypeId = Guard.NotEmpty(leaveTypeId, "Leave type"),
            StartDate = startDate,
            EndDate = endDate,
            IsHalfDay = halfDayPeriod is not null,
            HalfDayPeriod = halfDayPeriod,
            TotalDays = totalDays,
            Reason = Guard.Optional(reason, "Reason", 1000),
            AttachmentDocumentId = attachmentDocumentId,
            Status = LeaveRequestStatus.Pending
        };

        foreach (var step in approvalChain.OrderBy(s => s.Level))
            request._approvals.Add(new LeaveRequestApproval(step.Level, step.ApproverType, step.AssignedApproverId));

        request.CurrentApprovalLevel = request._approvals[0].Level;
        request.Raise(new LeaveRequestSubmittedDomainEvent(request));
        return request;
    }

    /// <summary>Current level approve. Aakhri level tha to request Approved.</summary>
    public void Approve(Guid approverEmployeeId, string? comment)
    {
        var step = CurrentStep(approverEmployeeId);
        step.Decide(ApprovalDecision.Approved, approverEmployeeId, comment);

        var next = _approvals.Where(a => a.Level > step.Level).OrderBy(a => a.Level).FirstOrDefault();
        if (next is null)
        {
            Status = LeaveRequestStatus.Approved;
            Raise(new LeaveRequestApprovedDomainEvent(this));
        }
        else
        {
            CurrentApprovalLevel = next.Level;
        }
    }

    public void Reject(Guid approverEmployeeId, string comment)
    {
        var step = CurrentStep(approverEmployeeId);
        step.Decide(ApprovalDecision.Rejected, approverEmployeeId, Guard.Required(comment, "Rejection reason", 500));
        SkipRemainingSteps();

        Status = LeaveRequestStatus.Rejected;
        Raise(new LeaveRequestRejectedDomainEvent(this));
    }

    public void Cancel(Guid byEmployeeId, bool allowCancelAfterApproval, DateOnly today)
    {
        if (byEmployeeId != EmployeeId)
            throw new DomainException("Only the employee can cancel their own leave request.");

        var wasApproved = Status == LeaveRequestStatus.Approved;

        if (wasApproved)
        {
            if (!allowCancelAfterApproval)
                throw new DomainException("Approved leave cannot be cancelled. Contact HR.");
            if (StartDate <= today)
                throw new DomainException("Leave that has already started cannot be cancelled.");
        }
        else if (Status != LeaveRequestStatus.Pending)
        {
            throw new DomainException("Only pending or approved requests can be cancelled.");
        }

        SkipRemainingSteps();
        Status = LeaveRequestStatus.Cancelled;
        Raise(new LeaveRequestCancelledDomainEvent(this, wasApproved));
    }

    private LeaveRequestApproval CurrentStep(Guid approverEmployeeId)
    {
        if (Status != LeaveRequestStatus.Pending)
            throw new DomainException("This leave request is no longer pending.");
        if (approverEmployeeId == EmployeeId)
            throw new DomainException("You cannot approve or reject your own leave request.");

        return _approvals.Single(a => a.Level == CurrentApprovalLevel);
    }

    private void SkipRemainingSteps()
    {
        foreach (var approval in _approvals.Where(a => a.Decision == ApprovalDecision.Pending))
            approval.Skip();
    }
}

public sealed class LeaveRequestApproval : Entity
{
    public Guid LeaveRequestId { get; private set; }
    public byte Level { get; private set; }
    public ApproverType ApproverType { get; private set; }
    public Guid? AssignedApproverId { get; private set; }    // manager/head: submit pe resolve; HR: null (role-based)
    public ApprovalDecision Decision { get; private set; }
    public Guid? DecidedByEmployeeId { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? Comment { get; private set; }

    private LeaveRequestApproval() { }

    internal LeaveRequestApproval(byte level, ApproverType approverType, Guid? assignedApproverId)
    {
        Level = level;
        ApproverType = approverType;
        AssignedApproverId = assignedApproverId;
        Decision = ApprovalDecision.Pending;
    }

    internal void Decide(ApprovalDecision decision, Guid decidedBy, string? comment)
    {
        Decision = decision;
        DecidedByEmployeeId = decidedBy;
        DecidedAt = DateTime.UtcNow;
        Comment = Guard.Optional(comment, "Comment", 500);
    }

    internal void Skip() => Decision = ApprovalDecision.Skipped;
}
