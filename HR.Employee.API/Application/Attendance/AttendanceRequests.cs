namespace HR.Employee.API.Application.Attendance;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Application.Common.Models;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Leaves;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Attendance page → Requests + Overtime tabs. Ek table, ek inbox; approver policy se submit pe snapshot.

public enum AttendanceRequestScope : byte { Mine = 1, Approvals = 2, All = 3 }

public sealed record AttendanceRequestDto(
    Guid Id, Guid EmployeeId, string EmployeeName, DateOnly WorkDate, AttendanceRequestType Type,
    DateTime? RequestedIn, DateTime? RequestedOut, short? OvertimeMinutes, short? ApprovedMinutes, decimal? OvertimeRate,
    string? Reason, AttendanceRequestStatus Status, ApproverType ApproverType, string? ApproverName,
    string? DecidedByName, DateTime? DecidedAt, string? DecisionComment, DateTime CreatedAt);

public sealed record GetAttendanceRequestsQuery : IRequest<PagedResult<AttendanceRequestDto>>
{
    public AttendanceRequestScope Scope { get; init; } = AttendanceRequestScope.Mine;
    public AttendanceRequestType? Type { get; init; }
    /// <summary>true = sirf overtime (Overtime tab), false = overtime ke ilawa (Requests tab), null = sab.</summary>
    public bool? Overtime { get; init; }
    public AttendanceRequestStatus? Status { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class GetAttendanceRequestsValidator : AbstractValidator<GetAttendanceRequestsQuery>
{
    public GetAttendanceRequestsValidator()
    {
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

/// <summary>Correction / WFH / On duty. Times UTC.</summary>
public sealed record SubmitAttendanceRequestCommand(
    DateOnly WorkDate, AttendanceRequestType Type, DateTime? RequestedIn, DateTime? RequestedOut, string Reason) : IRequest<Guid>;

public sealed class SubmitAttendanceRequestValidator : AbstractValidator<SubmitAttendanceRequestCommand>
{
    public SubmitAttendanceRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum().NotEqual(AttendanceRequestType.Overtime).WithMessage("Use the overtime request for overtime.");
        RuleFor(x => x).Must(x => x.RequestedIn is not null || x.RequestedOut is not null).WithMessage("Enter the in time, the out time, or both.");
        RuleFor(x => x.RequestedOut).GreaterThan(x => x.RequestedIn).When(x => x.RequestedIn is not null && x.RequestedOut is not null);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

/// <summary>Overtime claim. Minutes na diye to us din ka calculated overtime.</summary>
public sealed record SubmitOvertimeRequestCommand(DateOnly WorkDate, short? Minutes, string? Reason) : IRequest<Guid>;

public sealed class SubmitOvertimeRequestValidator : AbstractValidator<SubmitOvertimeRequestCommand>
{
    public SubmitOvertimeRequestValidator()
    {
        RuleFor(x => x.Minutes).InclusiveBetween((short)1, (short)1440).When(x => x.Minutes is not null);
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

public sealed record ApproveAttendanceRequestCommand(Guid Id, string? Comment, short? ApprovedMinutes) : IRequest;
public sealed record RejectAttendanceRequestCommand(Guid Id, string Comment) : IRequest;
public sealed record CancelAttendanceRequestCommand(Guid Id) : IRequest;

public sealed class RejectAttendanceRequestValidator : AbstractValidator<RejectAttendanceRequestCommand>
{
    public RejectAttendanceRequestValidator() => RuleFor(x => x.Comment).NotEmpty().MaximumLength(500);
}

public sealed class AttendanceRequestHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetAttendanceRequestsQuery, PagedResult<AttendanceRequestDto>>,
    IRequestHandler<SubmitAttendanceRequestCommand, Guid>,
    IRequestHandler<SubmitOvertimeRequestCommand, Guid>,
    IRequestHandler<ApproveAttendanceRequestCommand>,
    IRequestHandler<RejectAttendanceRequestCommand>,
    IRequestHandler<CancelAttendanceRequestCommand>
{
    private readonly AttendanceAccess _access = new(db, currentUser);
    private readonly AttendanceEngine _engine = new(db);

    /// <summary>HR approver (ApproverType.HR) = leaves.approve wala (abhi alag attendance permission nahi).</summary>
    private bool IsHrApprover => currentUser.HasPermission(Permissions.LeavesApprove);

    public async Task<PagedResult<AttendanceRequestDto>> Handle(GetAttendanceRequestsQuery q, CancellationToken ct)
    {
        var requests = db.AttendanceRequests.AsNoTracking();

        switch (q.Scope)
        {
            case AttendanceRequestScope.Mine:
                var me = await _access.CurrentEmployeeAsync(ct);
                requests = requests.Where(r => r.EmployeeId == me.Id);
                break;
            case AttendanceRequestScope.Approvals:
                var approver = await _access.TryCurrentEmployeeAsync(ct);
                var approverId = approver?.Id;
                var hr = IsHrApprover;
                requests = requests.Where(r => r.Status == AttendanceRequestStatus.Pending && r.EmployeeId != approverId
                                               && (r.AssignedApproverId == approverId || (hr && r.ApproverType == ApproverType.HR)));
                break;
            default:
                if (!_access.CanViewAll)
                    throw new UnauthorizedAccessException("You do not have permission to see all attendance requests.");
                break;
        }

        if (q.Type is { } type) requests = requests.Where(r => r.Type == type);
        if (q.Overtime is true) requests = requests.Where(r => r.Type == AttendanceRequestType.Overtime);
        if (q.Overtime is false) requests = requests.Where(r => r.Type != AttendanceRequestType.Overtime);
        if (q.Status is { } status) requests = requests.Where(r => r.Status == status);
        if (q.From is { } from) requests = requests.Where(r => r.WorkDate >= from);
        if (q.To is { } to) requests = requests.Where(r => r.WorkDate <= to);

        return await requests
            .OrderByDescending(r => r.CreatedAt)
            .Join(db.Employees, r => r.EmployeeId, e => e.Id, (r, e) => new AttendanceRequestDto(
                r.Id, r.EmployeeId, e.FirstName + " " + e.LastName, r.WorkDate, r.Type,
                r.RequestedIn, r.RequestedOut, r.OvertimeMinutes, r.ApprovedMinutes, r.OvertimeRate,
                r.Reason, r.Status, r.ApproverType,
                db.Employees.Where(a => a.Id == r.AssignedApproverId).Select(a => a.FirstName + " " + a.LastName).FirstOrDefault(),
                db.Employees.Where(a => a.Id == r.DecidedByEmployeeId).Select(a => a.FirstName + " " + a.LastName).FirstOrDefault(),
                r.DecidedAt, r.DecisionComment, r.CreatedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<Guid> Handle(SubmitAttendanceRequestCommand c, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var policy = await _engine.PolicyForAsync(me.Id, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (c.WorkDate > today)
            throw new DomainException("You cannot request a correction for a future date.");
        if (c.WorkDate < today.AddDays(-policy.CorrectionWindowDays))
            throw new DomainException($"Corrections are only allowed for the last {policy.CorrectionWindowDays} days.");

        if (policy.MaxCorrectionsPerMonth is { } max)
        {
            var monthStart = new DateOnly(c.WorkDate.Year, c.WorkDate.Month, 1);
            var used = await db.AttendanceRequests.CountAsync(r =>
                r.EmployeeId == me.Id && r.Type == AttendanceRequestType.Correction
                && r.WorkDate >= monthStart && r.WorkDate < monthStart.AddMonths(1)
                && (r.Status == AttendanceRequestStatus.Pending || r.Status == AttendanceRequestStatus.Approved), ct);
            if (c.Type == AttendanceRequestType.Correction && used >= max)
                throw new DomainException($"You have used all {max} corrections for this month.");
        }

        await EnsureNoOpenRequestAsync(me.Id, c.WorkDate, c.Type, ct);
        var dayId = await DayIdAsync(me.Id, c.WorkDate, ct);
        var (approverType, approverId) = await ApproverAsync(me, policy, ct);

        var request = AttendanceRequest.SubmitCorrection(
            currentUser.RequireTenantId(), me.Id, c.WorkDate, c.Type,
            Utc(c.RequestedIn), Utc(c.RequestedOut), c.Reason, dayId, approverType, approverId);
        db.AttendanceRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request.Id;
    }

    public async Task<Guid> Handle(SubmitOvertimeRequestCommand c, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var policy = await _engine.PolicyForAsync(me.Id, ct);
        if (!policy.OvertimeEnabled)
            throw new DomainException("Overtime is not enabled in your attendance policy.");

        var day = await db.AttendanceDays.AsNoTracking().FirstOrDefaultAsync(d => d.EmployeeId == me.Id && d.WorkDate == c.WorkDate, ct)
                  ?? throw new DomainException("There is no attendance for this date yet.");
        var minutes = c.Minutes ?? day.OvertimeMinutes;
        if (minutes <= 0)
            throw new DomainException("No overtime was recorded on this date.");

        await EnsureNoOpenRequestAsync(me.Id, c.WorkDate, AttendanceRequestType.Overtime, ct);
        var rate = policy.OvertimeRateFor(day.DayType);
        var tenantId = currentUser.RequireTenantId();

        AttendanceRequest request;
        if (!policy.OvertimeRequiresApproval)
        {
            request = AttendanceRequest.AutoApprovedOvertime(tenantId, me.Id, c.WorkDate, minutes, rate, day.Id);
        }
        else
        {
            var (approverType, approverId) = await ApproverAsync(me, policy, ct);
            request = AttendanceRequest.SubmitOvertime(tenantId, me.Id, c.WorkDate, minutes, rate, c.Reason, day.Id, approverType, approverId);
        }

        db.AttendanceRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request.Id;
    }

    public async Task Handle(ApproveAttendanceRequestCommand c, CancellationToken ct)
    {
        var (request, approverId) = await LoadForDecisionAsync(c.Id, ct);
        request.Approve(approverId, c.Comment, c.ApprovedMinutes);
        await db.SaveChangesAsync(ct);   // AttendanceRequestApprovedDomainEvent → correction punches (handler neeche)
    }

    public async Task Handle(RejectAttendanceRequestCommand c, CancellationToken ct)
    {
        var (request, approverId) = await LoadForDecisionAsync(c.Id, ct);
        request.Reject(approverId, c.Comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelAttendanceRequestCommand c, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var request = await db.AttendanceRequests.FirstOrDefaultAsync(r => r.Id == c.Id, ct)
                      ?? throw new NotFoundException("Attendance request", c.Id);
        request.Cancel(me.Id);
        await db.SaveChangesAsync(ct);
    }

    private async Task<(AttendanceRequest Request, Guid ApproverId)> LoadForDecisionAsync(Guid id, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var request = await db.AttendanceRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                      ?? throw new NotFoundException("Attendance request", id);

        var allowed = request.AssignedApproverId == me.Id || (request.ApproverType == ApproverType.HR && IsHrApprover);
        if (!allowed)
            throw new UnauthorizedAccessException("You are not the approver for this request.");
        return (request, me.Id);
    }

    /// <summary>Policy ka approver → asal banda. Manager/head na mile to HR pe chala jata hai.</summary>
    private async Task<(ApproverType, Guid?)> ApproverAsync(CurrentEmployee me, AttendancePolicy policy, CancellationToken ct)
    {
        Guid? approverId = policy.RequestApprover switch
        {
            ApproverType.LineManager => me.ManagerId,
            ApproverType.DepartmentHead => await db.Departments.Where(d => d.Id == me.DepartmentId).Select(d => d.HeadEmployeeId).FirstOrDefaultAsync(ct),
            _ => null
        };

        return approverId is { } id && id != me.Id ? (policy.RequestApprover, id) : (ApproverType.HR, null);
    }

    private async Task EnsureNoOpenRequestAsync(Guid employeeId, DateOnly date, AttendanceRequestType type, CancellationToken ct)
    {
        if (await db.AttendanceRequests.AnyAsync(r => r.EmployeeId == employeeId && r.WorkDate == date && r.Type == type
                && (r.Status == AttendanceRequestStatus.Pending || r.Status == AttendanceRequestStatus.Approved), ct))
            throw new ConflictException("You already have a pending or approved request of this type for this date.");
    }

    private Task<Guid?> DayIdAsync(Guid employeeId, DateOnly date, CancellationToken ct)
        => db.AttendanceDays.Where(d => d.EmployeeId == employeeId && d.WorkDate == date).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);

    private static DateTime? Utc(DateTime? value)
        => value is { } v ? DateTime.SpecifyKind(v.ToUniversalTime(), DateTimeKind.Utc) : null;
}

/// <summary>
/// Correction/WFH/On duty approve → maange gaye in/out punches us din mein (Source = Request) aur recalculate.
/// SaveChanges se pehle chalta hai, isliye sab ek transaction mein.
/// </summary>
public sealed class AttendanceRequestApprovedHandler(IAppDbContext db)
    : INotificationHandler<AttendanceRequestApprovedDomainEvent>
{
    public async Task Handle(AttendanceRequestApprovedDomainEvent notification, CancellationToken ct)
    {
        var request = notification.Request;
        if (request.Type == AttendanceRequestType.Overtime)
            return;   // payroll baad mein approved overtime uthayega

        var engine = new AttendanceEngine(db);
        var day = await engine.GetOrOpenDayAsync(request.TenantId, request.EmployeeId, request.WorkDate, ct);
        var note = $"Approved {request.Type} request";
        if (request.RequestedIn is { } inAt)
            day.AddPunch(inAt, PunchDirection.In, PunchSource.Request, null, null, null, null, note);
        if (request.RequestedOut is { } outAt)
            day.AddPunch(outAt, PunchDirection.Out, PunchSource.Request, null, null, null, null, note);

        await engine.RecalculateAsync(day, ct);
    }
}
