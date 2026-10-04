namespace HR.Employee.API.Domain.Attendance;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Leaves;

/// <summary>
/// Ek hi table mein saari attendance requests: correction (missed/galat punch), WFH, on duty, overtime.
/// Attendance page ke "Requests" aur "Overtime" tabs isi se bante hain — ek inbox, ek approval flow.
/// Approval single level hai; approver policy se submit ke waqt SNAPSHOT hota hai.
/// </summary>
public sealed class AttendanceRequest : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public AttendanceRequestType Type { get; private set; }
    public Guid? AttendanceDayId { get; private set; }

    public DateTime? RequestedIn { get; private set; }         // Correction / WFH / OnDuty
    public DateTime? RequestedOut { get; private set; }
    public short? OvertimeMinutes { get; private set; }        // Overtime: claimed
    public short? ApprovedMinutes { get; private set; }        // Overtime: manager kam bhi kar sakta hai
    public decimal? OvertimeRate { get; private set; }         // policy se snapshot (1.5x / 2x)
    public string? Reason { get; private set; }

    public AttendanceRequestStatus Status { get; private set; }
    public ApproverType ApproverType { get; private set; }
    public Guid? AssignedApproverId { get; private set; }      // manager/head: submit pe resolve; HR: null (role-based)
    public Guid? DecidedByEmployeeId { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionComment { get; private set; }

    private AttendanceRequest() { }

    public static AttendanceRequest SubmitCorrection(
        Guid tenantId, Guid employeeId, DateOnly workDate, AttendanceRequestType type,
        DateTime? requestedIn, DateTime? requestedOut, string reason,
        Guid? attendanceDayId, ApproverType approverType, Guid? assignedApproverId)
    {
        if (type == AttendanceRequestType.Overtime)
            throw new DomainException("Use the overtime request for overtime.");
        if (requestedIn is null && requestedOut is null)
            throw new DomainException("Enter the in time, the out time, or both.");
        if (requestedIn is not null && requestedOut is not null && requestedOut <= requestedIn)
            throw new DomainException("Out time must be after the in time.");

        var request = New(tenantId, employeeId, workDate, type, reason, attendanceDayId, approverType, assignedApproverId);
        request.RequestedIn = requestedIn;
        request.RequestedOut = requestedOut;
        request.Raise(new AttendanceRequestSubmittedDomainEvent(request));
        return request;
    }

    public static AttendanceRequest SubmitOvertime(
        Guid tenantId, Guid employeeId, DateOnly workDate, short minutes, decimal rate, string? reason,
        Guid? attendanceDayId, ApproverType approverType, Guid? assignedApproverId)
    {
        if (minutes <= 0)
            throw new DomainException("Overtime must be greater than zero.");

        var request = New(tenantId, employeeId, workDate, AttendanceRequestType.Overtime, reason, attendanceDayId, approverType, assignedApproverId);
        request.OvertimeMinutes = minutes;
        request.OvertimeRate = rate;
        request.Raise(new AttendanceRequestSubmittedDomainEvent(request));
        return request;
    }

    /// <summary>Policy mein approval band ho to overtime system khud approve karta hai.</summary>
    public static AttendanceRequest AutoApprovedOvertime(
        Guid tenantId, Guid employeeId, DateOnly workDate, short minutes, decimal rate, Guid attendanceDayId)
    {
        var request = SubmitOvertime(tenantId, employeeId, workDate, minutes, rate, null, attendanceDayId, ApproverType.HR, null);
        request.ClearDomainEvents();
        request.Status = AttendanceRequestStatus.Approved;
        request.ApprovedMinutes = minutes;
        request.DecidedAt = DateTime.UtcNow;
        request.Raise(new AttendanceRequestApprovedDomainEvent(request));
        return request;
    }

    private static AttendanceRequest New(
        Guid tenantId, Guid employeeId, DateOnly workDate, AttendanceRequestType type, string? reason,
        Guid? attendanceDayId, ApproverType approverType, Guid? assignedApproverId)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            WorkDate = workDate,
            Type = type,
            AttendanceDayId = attendanceDayId,
            Reason = type == AttendanceRequestType.Overtime
                ? Guard.Optional(reason, "Reason", 1000)
                : Guard.Required(reason, "Reason", 1000),
            Status = AttendanceRequestStatus.Pending,
            ApproverType = approverType,
            AssignedApproverId = assignedApproverId
        };

    /// <summary>approvedMinutes sirf overtime ke liye (null = jitna maanga utna).</summary>
    public void Approve(Guid approverEmployeeId, string? comment, short? approvedMinutes = null)
    {
        EnsurePendingFor(approverEmployeeId);

        if (Type == AttendanceRequestType.Overtime)
        {
            var minutes = approvedMinutes ?? OvertimeMinutes!.Value;
            if (minutes <= 0 || minutes > OvertimeMinutes)
                throw new DomainException("Approved overtime must be between 1 and the requested minutes.");
            ApprovedMinutes = minutes;
        }

        Decide(AttendanceRequestStatus.Approved, approverEmployeeId, Guard.Optional(comment, "Comment", 500));
        Raise(new AttendanceRequestApprovedDomainEvent(this));
    }

    public void Reject(Guid approverEmployeeId, string comment)
    {
        EnsurePendingFor(approverEmployeeId);
        Decide(AttendanceRequestStatus.Rejected, approverEmployeeId, Guard.Required(comment, "Rejection reason", 500));
        Raise(new AttendanceRequestRejectedDomainEvent(this));
    }

    public void Cancel(Guid byEmployeeId)
    {
        if (byEmployeeId != EmployeeId)
            throw new DomainException("Only the employee can cancel their own request.");
        if (Status != AttendanceRequestStatus.Pending)
            throw new DomainException("Only pending requests can be cancelled.");
        Status = AttendanceRequestStatus.Cancelled;
    }

    private void EnsurePendingFor(Guid approverEmployeeId)
    {
        if (Status != AttendanceRequestStatus.Pending)
            throw new DomainException("This request is no longer pending.");
        if (approverEmployeeId == EmployeeId)
            throw new DomainException("You cannot approve or reject your own request.");
    }

    private void Decide(AttendanceRequestStatus status, Guid decidedBy, string? comment)
    {
        Status = status;
        DecidedByEmployeeId = decidedBy;
        DecidedAt = DateTime.UtcNow;
        DecisionComment = comment;
    }
}
