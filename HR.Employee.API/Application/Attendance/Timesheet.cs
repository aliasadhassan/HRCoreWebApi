namespace HR.Employee.API.Application.Attendance;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Application.Common.Models;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Attendance page → Timesheet tab + "clock in/out" card. Raw punches → AttendanceDay totals (AttendanceEngine).

public sealed record PunchDto(
    Guid Id, DateTime PunchedAt, PunchDirection Direction, PunchSource Source, string? DeviceName,
    decimal? Latitude, decimal? Longitude, bool IsIgnored, string? Note);

public sealed record AttendanceDayDto(
    Guid Id, Guid EmployeeId, string EmployeeCode, string EmployeeName, string DepartmentName,
    DateOnly WorkDate, DayType DayType, string? ShiftCode, DateTime? ScheduledStart, DateTime? ScheduledEnd,
    DateTime? FirstIn, DateTime? LastOut, short WorkedMinutes, short LateMinutes, short EarlyLeaveMinutes,
    short OvertimeMinutes, AttendanceStatus Status, bool IsManuallyEdited, string? Remarks);

public sealed record AttendanceDayDetailDto(AttendanceDayDto Day, IReadOnlyList<PunchDto> Punches);

public sealed record MyTodayDto(
    DateOnly WorkDate, DayType DayType, string? ShiftName, DateTime? ScheduledStart, DateTime? ScheduledEnd,
    AttendanceDayDto? Day, IReadOnlyList<PunchDto> Punches, PunchDirection NextAction,
    bool RequiresLocation, IReadOnlyList<PunchSource> AllowedSources);

public sealed record AttendanceSummaryDto(DateOnly Date, int Total, IReadOnlyDictionary<AttendanceStatus, int> ByStatus, int Late);

public sealed record GetMyTodayQuery : IRequest<MyTodayDto>;

/// <summary>Web/Mobile clock-in/out. Direction khud: pehla punch In, baad wale Out.</summary>
public sealed record ClockCommand(PunchSource Source, decimal? Latitude, decimal? Longitude, string? Note, string? IpAddress) : IRequest<MyTodayDto>;

public sealed class ClockValidator : AbstractValidator<ClockCommand>
{
    public ClockValidator()
    {
        RuleFor(x => x.Source).Must(s => s is PunchSource.Web or PunchSource.Mobile).WithMessage("Clock in from the web or mobile app.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m);
        RuleFor(x => x.Note).MaximumLength(200);
    }
}

public sealed record GetTimesheetQuery : IRequest<PagedResult<AttendanceDayDto>>
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public Guid? EmployeeId { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? LocationId { get; init; }
    public AttendanceStatus? Status { get; init; }
    public bool LateOnly { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class GetTimesheetValidator : AbstractValidator<GetTimesheetQuery>
{
    public GetTimesheetValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 62).WithMessage("Timesheet range cannot be longer than 62 days.");
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed record GetAttendanceDayQuery(Guid Id) : IRequest<AttendanceDayDetailDto>;
public sealed record GetAttendanceSummaryQuery(DateOnly Date, Guid? LocationId) : IRequest<AttendanceSummaryDto>;

/// <summary>HR ne haath se punch dala (machine kharab, card bhool gaya).</summary>
public sealed record AddManualPunchCommand(Guid EmployeeId, DateTime PunchedAt, string Note) : IRequest<Guid>;

public sealed class AddManualPunchValidator : AbstractValidator<AddManualPunchCommand>
{
    public AddManualPunchValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.PunchedAt).LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5)).WithMessage("A punch cannot be in the future.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(200);
    }
}

public sealed record IgnorePunchCommand(Guid DayId, Guid PunchId, string Reason) : IRequest;

public sealed class IgnorePunchValidator : AbstractValidator<IgnorePunchCommand>
{
    public IgnorePunchValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
}

/// <summary>HR override: status/remarks haath se. Iske baad calculator is din ko nahi chhedta.</summary>
public sealed record OverrideAttendanceDayCommand(Guid DayId, AttendanceStatus Status, string Remarks) : IRequest;

public sealed class OverrideAttendanceDayValidator : AbstractValidator<OverrideAttendanceDayCommand>
{
    public OverrideAttendanceDayValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Remarks).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Biometric export / agent se punches (employee code + time). Duplicate punches khud skip.</summary>
public sealed record ImportPunchesCommand(Guid DeviceId, IReadOnlyList<ImportedPunch> Punches) : IRequest<ImportPunchesResult>;
public sealed record ImportedPunch(string EmployeeCode, DateTime PunchedAt);
public sealed record ImportPunchesResult(int Imported, int Skipped, IReadOnlyList<string> UnknownEmployeeCodes);

public sealed class ImportPunchesValidator : AbstractValidator<ImportPunchesCommand>
{
    public ImportPunchesValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.Punches).NotEmpty().Must(p => p.Count <= 5000).WithMessage("Import at most 5000 punches at once.");
        RuleForEach(x => x.Punches).ChildRules(p => p.RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(30));
    }
}

/// <summary>Kisi date ke sab employees ke din bana/refresh (absent/off/holiday/leave). Rozana job ya HR button.</summary>
public sealed record ProcessAttendanceCommand(DateOnly Date, Guid? LocationId) : IRequest<int>;

public sealed class TimesheetHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetMyTodayQuery, MyTodayDto>,
    IRequestHandler<ClockCommand, MyTodayDto>,
    IRequestHandler<GetTimesheetQuery, PagedResult<AttendanceDayDto>>,
    IRequestHandler<GetAttendanceDayQuery, AttendanceDayDetailDto>,
    IRequestHandler<GetAttendanceSummaryQuery, AttendanceSummaryDto>,
    IRequestHandler<AddManualPunchCommand, Guid>,
    IRequestHandler<IgnorePunchCommand>,
    IRequestHandler<OverrideAttendanceDayCommand>,
    IRequestHandler<ImportPunchesCommand, ImportPunchesResult>,
    IRequestHandler<ProcessAttendanceCommand, int>
{
    private readonly AttendanceAccess _access = new(db, currentUser);
    private readonly AttendanceEngine _engine = new(db);

    public async Task<MyTodayDto> Handle(GetMyTodayQuery q, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var workDate = await _engine.WorkDateForAsync(me.Id, DateTime.UtcNow, ct);
        return await TodayAsync(me.Id, workDate, ct);
    }

    public async Task<MyTodayDto> Handle(ClockCommand c, CancellationToken ct)
    {
        var me = await _access.CurrentEmployeeAsync(ct);
        var policy = await _engine.PolicyForAsync(me.Id, ct);

        if (!policy.Allows(c.Source))
            throw new DomainException($"Clocking in from {c.Source.ToString().ToLowerInvariant()} is not allowed by your attendance policy.");
        if (!policy.IsInsideGeofence(c.Latitude, c.Longitude))
            throw new DomainException(c.Latitude is null
                ? "Your location is required to clock in."
                : "You are outside the allowed office area.");

        var now = DateTime.UtcNow;
        var workDate = await _engine.WorkDateForAsync(me.Id, now, ct);
        var day = await _engine.GetOrOpenDayAsync(currentUser.RequireTenantId(), me.Id, workDate, ct);

        var direction = day.Punches.Any(p => !p.IsIgnored) ? PunchDirection.Out : PunchDirection.In;
        day.AddPunch(now, direction, c.Source, null, c.Latitude, c.Longitude, c.IpAddress, c.Note);
        await _engine.RecalculateAsync(day, ct);
        await db.SaveChangesAsync(ct);

        return await TodayAsync(me.Id, workDate, ct);
    }

    public async Task<PagedResult<AttendanceDayDto>> Handle(GetTimesheetQuery q, CancellationToken ct)
    {
        var days = db.AttendanceDays.AsNoTracking().Where(d => d.WorkDate >= q.From && d.WorkDate <= q.To);

        if (q.EmployeeId is { } employeeId)
        {
            await _access.EnsureCanViewAsync(employeeId, ct);
            days = days.Where(d => d.EmployeeId == employeeId);
        }
        else if (!_access.CanViewAll)
        {
            var me = await _access.CurrentEmployeeAsync(ct);
            var team = db.Employees.Where(e => e.Id == me.Id || e.ManagerId == me.Id).Select(e => e.Id);
            days = days.Where(d => team.Contains(d.EmployeeId));
        }

        if (q.DepartmentId is { } departmentId)
            days = days.Where(d => db.Employees.Any(e => e.Id == d.EmployeeId && e.DepartmentId == departmentId));
        if (q.LocationId is { } locationId)
            days = days.Where(d => db.Employees.Any(e => e.Id == d.EmployeeId && e.LocationId == locationId));
        if (q.Status is { } status)
            days = days.Where(d => d.Status == status);
        if (q.LateOnly)
            days = days.Where(d => d.LateMinutes > 0);

        return await Project(days.OrderByDescending(d => d.WorkDate)).ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<AttendanceDayDetailDto> Handle(GetAttendanceDayQuery q, CancellationToken ct)
    {
        var employeeId = await db.AttendanceDays.Where(d => d.Id == q.Id).Select(d => (Guid?)d.EmployeeId).FirstOrDefaultAsync(ct)
                         ?? throw new NotFoundException("Attendance day", q.Id);
        await _access.EnsureCanViewAsync(employeeId, ct);

        var day = await Project(db.AttendanceDays.AsNoTracking().Where(d => d.Id == q.Id)).SingleAsync(ct);
        return new AttendanceDayDetailDto(day, await PunchesAsync(q.Id, ct));
    }

    public async Task<AttendanceSummaryDto> Handle(GetAttendanceSummaryQuery q, CancellationToken ct)
    {
        if (!_access.CanViewAll)
            throw new UnauthorizedAccessException("You do not have permission to see the attendance summary.");

        var days = db.AttendanceDays.AsNoTracking().Where(d => d.WorkDate == q.Date);
        if (q.LocationId is { } locationId)
            days = days.Where(d => db.Employees.Any(e => e.Id == d.EmployeeId && e.LocationId == locationId));

        var byStatus = await days.GroupBy(d => d.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var late = await days.CountAsync(d => d.LateMinutes > 0, ct);
        return new AttendanceSummaryDto(q.Date, byStatus.Sum(x => x.Count), byStatus.ToDictionary(x => x.Key, x => x.Count), late);
    }

    public async Task<Guid> Handle(AddManualPunchCommand c, CancellationToken ct)
    {
        _access.EnsureCanManage();
        if (!await db.Employees.AnyAsync(e => e.Id == c.EmployeeId, ct))
            throw new NotFoundException("Employee", c.EmployeeId);

        var punchedAt = DateTime.SpecifyKind(c.PunchedAt.ToUniversalTime(), DateTimeKind.Utc);
        var workDate = await _engine.WorkDateForAsync(c.EmployeeId, punchedAt, ct);
        var day = await _engine.GetOrOpenDayAsync(currentUser.RequireTenantId(), c.EmployeeId, workDate, ct);
        day.AddPunch(punchedAt, PunchDirection.Unknown, PunchSource.Manual, null, null, null, null, c.Note);
        await _engine.RecalculateAsync(day, ct);
        await db.SaveChangesAsync(ct);
        return day.Id;
    }

    public async Task Handle(IgnorePunchCommand c, CancellationToken ct)
    {
        _access.EnsureCanManage();
        var day = await db.AttendanceDays.Include(d => d.Punches).FirstOrDefaultAsync(d => d.Id == c.DayId, ct)
                  ?? throw new NotFoundException("Attendance day", c.DayId);
        day.IgnorePunch(c.PunchId, c.Reason);
        await _engine.RecalculateAsync(day, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(OverrideAttendanceDayCommand c, CancellationToken ct)
    {
        _access.EnsureCanManage();
        var day = await db.AttendanceDays.FirstOrDefaultAsync(d => d.Id == c.DayId, ct)
                  ?? throw new NotFoundException("Attendance day", c.DayId);
        day.ApplyTotals(day.WorkedMinutes, day.LateMinutes, day.EarlyLeaveMinutes, day.OvertimeMinutes, c.Status, manual: true, c.Remarks);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ImportPunchesResult> Handle(ImportPunchesCommand c, CancellationToken ct)
    {
        _access.EnsureCanManage();
        var tenantId = currentUser.RequireTenantId();
        var device = await db.AttendanceDevices.FirstOrDefaultAsync(d => d.Id == c.DeviceId && d.IsActive, ct)
                     ?? throw new NotFoundException("Active device", c.DeviceId);

        var codes = c.Punches.Select(p => p.EmployeeCode.Trim().ToUpperInvariant()).Distinct().ToList();
        var employees = await db.Employees.Where(e => codes.Contains(e.EmployeeCode.ToUpper()))
            .Select(e => new { e.Id, Code = e.EmployeeCode.ToUpper() })
            .ToDictionaryAsync(e => e.Code, e => e.Id, ct);

        int imported = 0, skipped = 0;
        var touched = new HashSet<AttendanceDay>();
        foreach (var p in c.Punches.OrderBy(p => p.PunchedAt))
        {
            if (!employees.TryGetValue(p.EmployeeCode.Trim().ToUpperInvariant(), out var employeeId))
            {
                skipped++;
                continue;
            }

            var punchedAt = DateTime.SpecifyKind(p.PunchedAt.ToUniversalTime(), DateTimeKind.Utc);
            var workDate = await _engine.WorkDateForAsync(employeeId, punchedAt, ct);
            var day = touched.FirstOrDefault(d => d.EmployeeId == employeeId && d.WorkDate == workDate)
                      ?? await _engine.GetOrOpenDayAsync(tenantId, employeeId, workDate, ct);

            var before = day.Punches.Count;
            day.AddPunch(punchedAt, PunchDirection.Unknown, PunchSource.Biometric, device.Id, null, null, null, null);
            if (day.Punches.Count > before) imported++; else skipped++;
            touched.Add(day);
        }

        foreach (var day in touched)
            await _engine.RecalculateAsync(day, ct);

        device.MarkSynced(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return new ImportPunchesResult(imported, skipped, codes.Where(code => !employees.ContainsKey(code)).ToList());
    }

    public async Task<int> Handle(ProcessAttendanceCommand c, CancellationToken ct)
    {
        _access.EnsureCanManage();
        var count = await _engine.ProcessDateAsync(currentUser.RequireTenantId(), c.Date, c.LocationId, ct);
        await db.SaveChangesAsync(ct);
        return count;
    }

    private async Task<MyTodayDto> TodayAsync(Guid employeeId, DateOnly workDate, CancellationToken ct)
    {
        var schedule = await _engine.ResolveAsync(employeeId, workDate, ct);
        var policy = await _engine.PolicyForAsync(employeeId, ct);
        var day = await Project(db.AttendanceDays.AsNoTracking().Where(d => d.EmployeeId == employeeId && d.WorkDate == workDate))
            .FirstOrDefaultAsync(ct);
        var punches = day is null ? [] : await PunchesAsync(day.Id, ct);

        var allowed = new[] { PunchSource.Web, PunchSource.Mobile }.Where(policy.Allows).ToList();
        return new MyTodayDto(
            workDate, schedule.DayType, schedule.Shift?.Name, schedule.StartUtc, schedule.EndUtc, day, punches,
            punches.Any(p => !p.IsIgnored) ? PunchDirection.Out : PunchDirection.In,
            policy.RequireGeofence, allowed);
    }

    private Task<List<PunchDto>> PunchesAsync(Guid dayId, CancellationToken ct)
        => db.AttendanceDays.AsNoTracking()
            .Where(d => d.Id == dayId)
            .SelectMany(d => d.Punches)
            .OrderBy(p => p.PunchedAt)
            .Select(p => new PunchDto(
                p.Id, p.PunchedAt, p.Direction, p.Source,
                db.AttendanceDevices.Where(x => x.Id == p.DeviceId).Select(x => x.Name).FirstOrDefault(),
                p.Latitude, p.Longitude, p.IsIgnored, p.Note))
            .ToListAsync(ct);

    private IQueryable<AttendanceDayDto> Project(IQueryable<AttendanceDay> days)
        => days.Join(db.Employees, d => d.EmployeeId, e => e.Id, (d, e) => new AttendanceDayDto(
            d.Id, d.EmployeeId, e.EmployeeCode, e.FirstName + " " + e.LastName, e.Department.Name,
            d.WorkDate, d.DayType,
            db.Shifts.Where(s => s.Id == d.ShiftId).Select(s => s.Code).FirstOrDefault(),
            d.ScheduledStart, d.ScheduledEnd, d.FirstIn, d.LastOut, d.WorkedMinutes, d.LateMinutes,
            d.EarlyLeaveMinutes, d.OvertimeMinutes, d.Status, d.IsManuallyEdited, d.Remarks));
}
