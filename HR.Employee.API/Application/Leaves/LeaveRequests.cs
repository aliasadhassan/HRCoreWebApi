namespace HR.Employee.API.Application.Leaves;

using FluentValidation;
using HR.Employee.API.Application.Attendance;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Leaves;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Leaves page: "My leave" (balances + apne requests), Approvals inbox, All requests (HR).
// Balance ka hisaab yahin: submit = Reserve, final approve = ConfirmUsage, reject/cancel = wapas.

public enum LeaveRequestScope : byte { Mine = 1, Approvals = 2, All = 3 }

public sealed record LeaveBalanceDto(
    Guid LeaveTypeId, string LeaveTypeName, string Code, string? Color, bool IsPaid, bool AllowHalfDay, short Year,
    decimal Entitled, decimal CarriedForward, decimal Adjusted, decimal Used, decimal Pending, decimal Available);

public sealed record LeaveApprovalStepDto(
    byte Level, ApproverType ApproverType, string? ApproverName, ApprovalDecision Decision,
    string? DecidedByName, DateTime? DecidedAt, string? Comment);

public sealed record LeaveRequestDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? DepartmentName,
    Guid LeaveTypeId, string LeaveTypeName, string? Color, DateOnly StartDate, DateOnly EndDate,
    bool IsHalfDay, HalfDayPeriod? HalfDayPeriod, decimal TotalDays, string? Reason, LeaveRequestStatus Status,
    byte CurrentApprovalLevel, DateTime CreatedAt, bool CanApprove, bool CanCancel, IReadOnlyList<LeaveApprovalStepDto> Approvals);

public sealed record MyLeaveDto(Guid EmployeeId, short Year, IReadOnlyList<LeaveBalanceDto> Balances, IReadOnlyList<LeaveRequestDto> Requests);

public sealed record LeavePreviewDto(decimal Days, IReadOnlyList<string> Holidays);

public sealed record GetMyLeaveQuery(short? Year) : IRequest<MyLeaveDto>;

public sealed record PreviewLeaveQuery(DateOnly Start, DateOnly End, bool HalfDay) : IRequest<LeavePreviewDto>;

public sealed class PreviewLeaveValidator : AbstractValidator<PreviewLeaveQuery>
{
    public PreviewLeaveValidator()
    {
        RuleFor(x => x.End).GreaterThanOrEqualTo(x => x.Start).WithMessage("End date cannot be before the start date.");
        RuleFor(x => x).Must(x => x.End.DayNumber - x.Start.DayNumber <= 366).WithMessage("A single leave request cannot span more than a year.");
    }
}

public sealed record GetLeaveRequestsQuery : IRequest<IReadOnlyList<LeaveRequestDto>>
{
    public LeaveRequestScope Scope { get; init; } = LeaveRequestScope.Mine;
    public LeaveRequestStatus? Status { get; init; }
    /// <summary>Approvals inbox pe year nahi lagta (har pending dikhe).</summary>
    public short? Year { get; init; }
}

public sealed class GetLeaveRequestsValidator : AbstractValidator<GetLeaveRequestsQuery>
{
    public GetLeaveRequestsValidator()
    {
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Year).InclusiveBetween((short)2000, (short)2100).When(x => x.Year is not null);
    }
}

public sealed record SubmitLeaveRequestCommand(
    Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, HalfDayPeriod? HalfDayPeriod, string? Reason) : IRequest<Guid>;

public sealed class SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequestCommand>
{
    public SubmitLeaveRequestValidator()
    {
        RuleFor(x => x.LeaveTypeId).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date cannot be before the start date.");
        RuleFor(x => x.HalfDayPeriod).IsInEnum();
        RuleFor(x => x.EndDate).Equal(x => x.StartDate).When(x => x.HalfDayPeriod is not null)
            .WithMessage("A half-day leave must start and end on the same day.");
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

public sealed record ApproveLeaveRequestCommand(Guid Id, string? Comment) : IRequest;
public sealed record RejectLeaveRequestCommand(Guid Id, string? Comment) : IRequest;
public sealed record CancelLeaveRequestCommand(Guid Id) : IRequest;

public sealed class RejectLeaveRequestValidator : AbstractValidator<RejectLeaveRequestCommand>
{
    public RejectLeaveRequestValidator()
        => RuleFor(x => x.Comment).NotEmpty().WithMessage("Please give a reason for rejecting.").MaximumLength(500);
}

public sealed class LeaveRequestHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetMyLeaveQuery, MyLeaveDto>,
    IRequestHandler<PreviewLeaveQuery, LeavePreviewDto>,
    IRequestHandler<GetLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>,
    IRequestHandler<SubmitLeaveRequestCommand, Guid>,
    IRequestHandler<ApproveLeaveRequestCommand>,
    IRequestHandler<RejectLeaveRequestCommand>,
    IRequestHandler<CancelLeaveRequestCommand>
{
    private readonly AttendanceAccess _access = new(db, currentUser);
    private readonly LeaveEntitlements _entitlements = new(db);

    /// <summary>ApproverType.HR wale step ko leaves.approve wala koi bhi approve kar sakta hai.</summary>
    private bool IsHrApprover => currentUser.HasPermission(Permissions.LeavesApprove);

    private bool CanViewAll => currentUser.HasPermission(Permissions.LeavesViewAll) || currentUser.HasPermission(Permissions.EmployeesView);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<MyLeaveDto> Handle(GetMyLeaveQuery q, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var year = q.Year ?? (short)Today.Year;

        var employee = await db.Employees.FirstAsync(e => e.Id == me.Id, ct);
        var balances = await _entitlements.EnsureBalancesAsync(employee, year, Today, ct);
        await db.SaveChangesAsync(ct);

        var types = await db.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        var balanceDtos = balances
            .Where(b => types.ContainsKey(b.LeaveTypeId))
            .Select(b => (Balance: b, Type: types[b.LeaveTypeId]))
            .OrderBy(x => x.Type.SortOrder).ThenBy(x => x.Type.Name)
            .Select(x => new LeaveBalanceDto(
                x.Type.Id, x.Type.Name, x.Type.Code, x.Type.Color, x.Type.IsPaid, x.Type.AllowHalfDay, x.Balance.LeaveYear,
                x.Balance.Entitled, x.Balance.CarriedForward, x.Balance.Adjusted, x.Balance.Used, x.Balance.Pending, x.Balance.Available))
            .ToList();

        var requests = await db.LeaveRequests.AsNoTracking().Include(r => r.Approvals)
            .Where(r => r.EmployeeId == me.Id && r.StartDate.Year <= year && r.EndDate.Year >= year)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync(ct);

        return new MyLeaveDto(me.Id, year, balanceDtos, await ToDtosAsync(requests, me.Id, ct));
    }

    public async Task<LeavePreviewDto> Handle(PreviewLeaveQuery q, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == me.LocationId, ct);
        var end = q.HalfDay ? q.Start : q.End;
        var holidays = await HolidaysAsync(me.LocationId, q.Start, end, ct);

        var days = LeaveDayCalculator.CountWorkingDays(q.Start, end, q.HalfDay, location, holidays.Select(h => h.Date).ToHashSet());
        return new LeavePreviewDto(days, holidays.Select(h => $"{h.Name} · {h.Date:dd MMM}").ToList());
    }

    public async Task<IReadOnlyList<LeaveRequestDto>> Handle(GetLeaveRequestsQuery q, CancellationToken ct)
    {
        var me = await _access.TryCurrentEmployeeAsync(ct);
        var meId = me?.Id;
        var requests = db.LeaveRequests.AsNoTracking().Include(r => r.Approvals).AsQueryable();

        switch (q.Scope)
        {
            case LeaveRequestScope.Mine:
                if (meId is null)
                    throw new UnauthorizedAccessException("Your login is not linked to an employee record.");
                requests = requests.Where(r => r.EmployeeId == meId);
                break;
            case LeaveRequestScope.Approvals:
                var hr = IsHrApprover;
                requests = requests.Where(r => r.Status == LeaveRequestStatus.Pending && r.EmployeeId != meId
                    && r.Approvals.Any(a => a.Level == r.CurrentApprovalLevel
                        && ((meId != null && a.AssignedApproverId == meId) || (hr && a.ApproverType == ApproverType.HR))));
                break;
            default:
                if (!CanViewAll)
                    throw new UnauthorizedAccessException("You do not have permission to see all leave requests.");
                break;
        }

        if (q.Status is { } status) requests = requests.Where(r => r.Status == status);
        if (q.Year is { } year && q.Scope != LeaveRequestScope.Approvals)
            requests = requests.Where(r => r.StartDate.Year <= year && r.EndDate.Year >= year);

        var list = await requests
            .OrderBy(r => r.Status == LeaveRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.StartDate)
            .Take(500)
            .ToListAsync(ct);
        return await ToDtosAsync(list, meId, ct);
    }

    public async Task<Guid> Handle(SubmitLeaveRequestCommand c, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var employee = await db.Employees.FirstAsync(e => e.Id == me.Id, ct);

        if (c.StartDate.Year != c.EndDate.Year)
            throw new DomainException("Leave cannot cross into the next year. Please split it into two requests.");

        var type = await db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == c.LeaveTypeId, ct)
                   ?? throw new NotFoundException("Leave type", c.LeaveTypeId);
        if (!type.IsActive)
            throw new DomainException($"{type.Name} is no longer available.");
        if (c.HalfDayPeriod is not null && !type.AllowHalfDay)
            throw new DomainException($"{type.Name} cannot be taken as a half day.");

        var policy = await _entitlements.PolicyForAsync(me.LocationId, ct)
                     ?? throw new DomainException("No leave policy applies to your location yet. Please contact HR.");
        var rule = policy.RuleFor(type.Id)
                   ?? throw new DomainException($"Your leave policy does not include {type.Name}.");
        if (!rule.IsEligible(employee, c.StartDate))
            throw new DomainException(employee.JoiningDate.AddDays(rule.MinServiceDays) > c.StartDate
                ? $"{type.Name} is available after {rule.MinServiceDays} days of service."
                : $"You are not eligible for {type.Name}.");

        var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == me.LocationId, ct);
        var holidays = await HolidaysAsync(me.LocationId, c.StartDate, c.EndDate, ct);
        var days = LeaveDayCalculator.CountWorkingDays(c.StartDate, c.EndDate, c.HalfDayPeriod is not null, location,
                                                       holidays.Select(h => h.Date).ToHashSet());

        if (rule.MaxConsecutiveDays is { } max && days > max)
            throw new DomainException($"{type.Name} can be taken for at most {max} days at a time.");

        var overlaps = await db.LeaveRequests.AnyAsync(r => r.EmployeeId == me.Id
            && (r.Status == LeaveRequestStatus.Pending || r.Status == LeaveRequestStatus.Approved)
            && r.StartDate <= c.EndDate && r.EndDate >= c.StartDate, ct);
        if (overlaps)
            throw new ConflictException("You already have a leave request on some of these dates.");

        var balances = await _entitlements.EnsureBalancesAsync(employee, (short)c.StartDate.Year, Today, ct);
        var balance = balances.FirstOrDefault(b => b.LeaveTypeId == type.Id)
                      ?? throw new DomainException($"You have no {type.Name} balance for {c.StartDate.Year}.");
        if (days > 0)
            balance.Reserve(days, type.AllowNegativeBalance);

        var chain = await ApprovalChainAsync(me, ct);
        var request = LeaveRequest.Submit(currentUser.RequireTenantId(), me.Id, type.Id, c.StartDate, c.EndDate,
                                          c.HalfDayPeriod, days, c.Reason, null, chain);
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request.Id;
    }

    public async Task Handle(ApproveLeaveRequestCommand c, CancellationToken ct)
    {
        var (request, approverId) = await LoadForDecisionAsync(c.Id, ct);
        request.Approve(approverId, c.Comment);

        if (request.Status == LeaveRequestStatus.Approved)
            (await BalanceForAsync(request, ct))?.ConfirmUsage(request.TotalDays);

        await db.SaveChangesAsync(ct);
        if (request.Status == LeaveRequestStatus.Approved)
            await RefreshAttendanceAsync(request, ct);
    }

    public async Task Handle(RejectLeaveRequestCommand c, CancellationToken ct)
    {
        var (request, approverId) = await LoadForDecisionAsync(c.Id, ct);
        request.Reject(approverId, c.Comment!);
        (await BalanceForAsync(request, ct))?.ReleasePending(request.TotalDays);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelLeaveRequestCommand c, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var request = await db.LeaveRequests.Include(r => r.Approvals).FirstOrDefaultAsync(r => r.Id == c.Id, ct)
                      ?? throw new NotFoundException("Leave request", c.Id);
        var settings = await SettingsAsync(ct);

        var wasApproved = request.Status == LeaveRequestStatus.Approved;
        request.Cancel(me.Id, settings.AllowCancelAfterApproval, Today);

        var balance = await BalanceForAsync(request, ct);
        if (wasApproved) balance?.RestoreUsed(request.TotalDays);
        else balance?.ReleasePending(request.TotalDays);

        await db.SaveChangesAsync(ct);
        if (wasApproved)
            await RefreshAttendanceAsync(request, ct);
    }

    // ── helpers ──

    private async Task<(LeaveRequest Request, Guid ApproverId)> LoadForDecisionAsync(Guid id, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var request = await db.LeaveRequests.Include(r => r.Approvals).FirstOrDefaultAsync(r => r.Id == id, ct)
                      ?? throw new NotFoundException("Leave request", id);

        if (!CanDecide(request, me.Id))
            throw new UnauthorizedAccessException("You are not the approver for this leave request.");
        return (request, me.Id);
    }

    private bool CanDecide(LeaveRequest r, Guid? meId)
    {
        if (r.Status != LeaveRequestStatus.Pending || r.EmployeeId == meId)
            return false;
        var step = r.Approvals.FirstOrDefault(a => a.Level == r.CurrentApprovalLevel);
        return step is not null && ((meId is not null && step.AssignedApproverId == meId) || (step.ApproverType == ApproverType.HR && IsHrApprover));
    }

    private Task<LeaveBalance?> BalanceForAsync(LeaveRequest r, CancellationToken ct)
        => r.TotalDays <= 0
            ? Task.FromResult<LeaveBalance?>(null)
            : db.LeaveBalances.FirstOrDefaultAsync(b => b.EmployeeId == r.EmployeeId && b.LeaveTypeId == r.LeaveTypeId
                                                        && b.LeaveYear == r.StartDate.Year, ct);

    private async Task<LeaveApprovalSettings> SettingsAsync(CancellationToken ct)
        => await db.LeaveApprovalSettings.AsNoTracking().FirstOrDefaultAsync(ct)
           ?? LeaveApprovalSettings.CreateDefault(currentUser.RequireTenantId());

    /// <summary>Settings ka chain → asal log. Manager/head na mile (ya khud ho) to woh step HR ke paas.</summary>
    private async Task<IReadOnlyList<ApprovalStep>> ApprovalChainAsync(CurrentEmployee me, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var head = await db.Departments.Where(d => d.Id == me.DepartmentId).Select(d => d.HeadEmployeeId).FirstOrDefaultAsync(ct);

        var steps = new List<ApprovalStep>();
        foreach (var (level, approver) in settings.Chain())
        {
            Guid? person = approver switch
            {
                ApproverType.LineManager => me.ManagerId,
                ApproverType.DepartmentHead => head,
                _ => null
            };
            var step = person is { } id && id != me.Id ? new ApprovalStep(level, approver, id) : new ApprovalStep(level, ApproverType.HR, null);

            // Dono level ek hi bande/HR pe aa jayen to ek hi kaafi
            if (steps.Any(s => s.ApproverType == step.ApproverType && s.AssignedApproverId == step.AssignedApproverId))
                continue;
            steps.Add(step with { Level = (byte)(steps.Count + 1) });
        }
        return steps;
    }

    private async Task<List<Holiday>> HolidaysAsync(Guid locationId, DateOnly from, DateOnly to, CancellationToken ct)
        => await db.Holidays.AsNoTracking()
            .Where(h => !h.IsOptional && h.Date >= from && h.Date <= to && (h.LocationId == null || h.LocationId == locationId))
            .OrderBy(h => h.Date)
            .ToListAsync(ct);

    /// <summary>Jo din pehle se ban chuke hain unhe leave ke hisaab se dobara (absent → on leave, ya wapas).</summary>
    private async Task RefreshAttendanceAsync(LeaveRequest request, CancellationToken ct)
    {
        var days = await db.AttendanceDays
            .Where(d => d.EmployeeId == request.EmployeeId && d.WorkDate >= request.StartDate && d.WorkDate <= request.EndDate)
            .ToListAsync(ct);
        if (days.Count == 0)
            return;

        var engine = new AttendanceEngine(db);
        foreach (var day in days)
            await engine.RecalculateAsync(day, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<LeaveRequestDto>> ToDtosAsync(IReadOnlyList<LeaveRequest> requests, Guid? meId, CancellationToken ct)
    {
        if (requests.Count == 0)
            return [];

        var people = requests.Select(r => r.EmployeeId)
            .Concat(requests.SelectMany(r => r.Approvals).SelectMany(a => new[] { a.AssignedApproverId, a.DecidedByEmployeeId })
                .Where(id => id is not null).Select(id => id!.Value))
            .Distinct().ToList();

        var employees = await db.Employees.AsNoTracking()
            .Where(e => people.Contains(e.Id))
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, e.DepartmentId })
            .ToDictionaryAsync(e => e.Id, ct);
        var departments = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var types = await db.LeaveTypes.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.TenantId == currentUser.TenantId)
            .ToDictionaryAsync(t => t.Id, ct);
        var settings = await SettingsAsync(ct);
        var today = Today;

        string? NameOf(Guid? id) => id is { } v && employees.TryGetValue(v, out var e) ? e.Name : null;

        return requests.Select(r =>
        {
            employees.TryGetValue(r.EmployeeId, out var emp);
            types.TryGetValue(r.LeaveTypeId, out var type);
            var canCancel = r.EmployeeId == meId && (r.Status == LeaveRequestStatus.Pending
                || (r.Status == LeaveRequestStatus.Approved && settings.AllowCancelAfterApproval && r.StartDate > today));

            return new LeaveRequestDto(
                r.Id, r.EmployeeId, emp?.Name ?? "—", emp?.EmployeeCode ?? "",
                emp is not null ? departments.GetValueOrDefault(emp.DepartmentId) : null,
                r.LeaveTypeId, type?.Name ?? "—", type?.Color, r.StartDate, r.EndDate, r.IsHalfDay, r.HalfDayPeriod,
                r.TotalDays, r.Reason, r.Status, r.CurrentApprovalLevel, r.CreatedAt,
                CanDecide(r, meId), canCancel,
                r.Approvals.OrderBy(a => a.Level).Select(a => new LeaveApprovalStepDto(
                    a.Level, a.ApproverType, NameOf(a.AssignedApproverId), a.Decision,
                    NameOf(a.DecidedByEmployeeId), a.DecidedAt, a.Comment)).ToList());
        }).ToList();
    }
}
