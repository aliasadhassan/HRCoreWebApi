namespace HR.Employee.API.Application.Attendance;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Employees;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Attendance page → Roster tab: calendar (resolved shifts), default shift assignments, per-day overrides.

public sealed record RosterDayDto(
    DateOnly Date, DayType DayType, Guid? ShiftId, string? ShiftCode, string? ShiftColor,
    TimeOnly? StartTime, TimeOnly? EndTime, ScheduleSource Source);

public sealed record RosterRowDto(Guid EmployeeId, string EmployeeCode, string EmployeeName, string DepartmentName, IReadOnlyList<RosterDayDto> Days);

/// <summary>Max 42 din (6 hafte) ek dafa — month view ke liye kaafi.</summary>
public sealed record GetRosterQuery(DateOnly From, DateOnly To, Guid? DepartmentId, Guid? LocationId, Guid? EmployeeId)
    : IRequest<IReadOnlyList<RosterRowDto>>;

public sealed class GetRosterValidator : AbstractValidator<GetRosterQuery>
{
    public GetRosterValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber < 42).WithMessage("Roster range cannot be longer than 42 days.");
    }
}

public sealed record ShiftAssignmentDto(
    Guid Id, Guid EmployeeId, string EmployeeName, Guid ShiftId, string ShiftName, string ShiftCode,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, byte? WeeklyOffDays);

public sealed record GetShiftAssignmentsQuery(Guid? EmployeeId, Guid? ShiftId, bool CurrentOnly = true) : IRequest<IReadOnlyList<ShiftAssignmentDto>>;

/// <summary>Bulk: kai employees ko ek shift. Har employee ki chalti assignment ek din pehle band ho jati hai.</summary>
public sealed record AssignShiftCommand(
    IReadOnlyList<Guid> EmployeeIds, Guid ShiftId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, byte? WeeklyOffDays) : IRequest<int>;

public sealed class AssignShiftValidator : AbstractValidator<AssignShiftCommand>
{
    public AssignShiftValidator()
    {
        RuleFor(x => x.EmployeeIds).NotEmpty().Must(ids => ids.Count <= 500).WithMessage("Assign at most 500 employees at once.");
        RuleFor(x => x.ShiftId).NotEmpty();
        RuleFor(x => x.EffectiveTo).GreaterThanOrEqualTo(x => x.EffectiveFrom).When(x => x.EffectiveTo is not null);
        RuleFor(x => x.WeeklyOffDays).LessThanOrEqualTo((byte)127);
    }
}

public sealed record DeleteShiftAssignmentCommand(Guid Id) : IRequest;

/// <summary>Ek din ka override. ShiftId null = off. Already ho to update (upsert).</summary>
public sealed record SetRosterEntryCommand(Guid EmployeeId, DateOnly WorkDate, Guid? ShiftId, string? Note) : IRequest<Guid>;

public sealed class SetRosterEntryValidator : AbstractValidator<SetRosterEntryCommand>
{
    public SetRosterEntryValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(200);
    }
}

/// <summary>Override hatao → din wapis assignment/work week pe.</summary>
public sealed record ClearRosterEntryCommand(Guid EmployeeId, DateOnly WorkDate) : IRequest;

public sealed class RosterHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetRosterQuery, IReadOnlyList<RosterRowDto>>,
    IRequestHandler<GetShiftAssignmentsQuery, IReadOnlyList<ShiftAssignmentDto>>,
    IRequestHandler<AssignShiftCommand, int>,
    IRequestHandler<DeleteShiftAssignmentCommand>,
    IRequestHandler<SetRosterEntryCommand, Guid>,
    IRequestHandler<ClearRosterEntryCommand>
{
    private readonly AttendanceAccess _access = new(db, currentUser);
    private readonly AttendanceEngine _engine = new(db);

    public async Task<IReadOnlyList<RosterRowDto>> Handle(GetRosterQuery q, CancellationToken ct)
    {
        var employees = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus != EmploymentStatus.Exited && e.JoiningDate <= q.To);

        if (!_access.CanViewAll)
        {
            var me = await _access.CurrentEmployeeAsync(ct);
            employees = employees.Where(e => e.Id == me.Id || e.ManagerId == me.Id);
        }
        if (q.EmployeeId is { } employeeId) employees = employees.Where(e => e.Id == employeeId);
        if (q.DepartmentId is { } departmentId) employees = employees.Where(e => e.DepartmentId == departmentId);
        if (q.LocationId is { } locationId) employees = employees.Where(e => e.LocationId == locationId);

        var people = await employees
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Take(300)
            .Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department.Name })
            .ToListAsync(ct);

        var days = (await _engine.ResolveAsync(people.Select(p => p.Id).ToList(), q.From, q.To, ct))
            .ToLookup(d => d.EmployeeId);

        return people.Select(p => new RosterRowDto(p.Id, p.EmployeeCode, p.Name, p.Department,
            days[p.Id].Select(d => new RosterDayDto(
                d.Date, d.DayType, d.Shift?.Id, d.Shift?.Code, d.Shift?.Color,
                d.Shift?.StartTime, d.Shift?.EndTime, d.Source)).ToList())).ToList();
    }

    public async Task<IReadOnlyList<ShiftAssignmentDto>> Handle(GetShiftAssignmentsQuery q, CancellationToken ct)
    {
        if (q.EmployeeId is { } id)
            await _access.EnsureCanViewAsync(id, ct);
        else if (!_access.CanViewAll)
            throw new UnauthorizedAccessException("Filter by an employee to see shift assignments.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = db.ShiftAssignments.AsNoTracking();
        if (q.EmployeeId is { } employeeId) query = query.Where(a => a.EmployeeId == employeeId);
        if (q.ShiftId is { } shiftId) query = query.Where(a => a.ShiftId == shiftId);
        if (q.CurrentOnly) query = query.Where(a => a.EffectiveTo == null || a.EffectiveTo >= today);

        return await query
            .OrderByDescending(a => a.EffectiveFrom)
            .Take(500)
            .Join(db.Employees, a => a.EmployeeId, e => e.Id, (a, e) => new { a, e })
            .Join(db.Shifts, x => x.a.ShiftId, s => s.Id, (x, s) => new ShiftAssignmentDto(
                x.a.Id, x.a.EmployeeId, x.e.FirstName + " " + x.e.LastName, s.Id, s.Name, s.Code,
                x.a.EffectiveFrom, x.a.EffectiveTo, x.a.WeeklyOffDays))
            .ToListAsync(ct);
    }

    public async Task<int> Handle(AssignShiftCommand c, CancellationToken ct)
    {
        _access.EnsureCanManage();
        var tenantId = currentUser.RequireTenantId();

        if (!await db.Shifts.AnyAsync(s => s.Id == c.ShiftId && s.IsActive, ct))
            throw new NotFoundException("Active shift", c.ShiftId);

        var employeeIds = c.EmployeeIds.Distinct().ToList();
        var found = await db.Employees.CountAsync(e => employeeIds.Contains(e.Id), ct);
        if (found != employeeIds.Count)
            throw new NotFoundException("Employee", "one or more of the selected employees");

        // Overlap hatao: jo assignment naye se pehle shuru hui → ek din pehle band; jo baad mein/andar shuru → hata do
        var overlapping = await db.ShiftAssignments
            .Where(a => employeeIds.Contains(a.EmployeeId)
                        && (a.EffectiveTo == null || a.EffectiveTo >= c.EffectiveFrom)
                        && (c.EffectiveTo == null || a.EffectiveFrom <= c.EffectiveTo))
            .ToListAsync(ct);

        foreach (var old in overlapping)
        {
            if (old.EffectiveFrom < c.EffectiveFrom)
                old.EndOn(c.EffectiveFrom.AddDays(-1));
            else if (c.EffectiveTo is { } newEnd && (old.EffectiveTo is null || old.EffectiveTo > newEnd))
                old.Update(old.ShiftId, newEnd.AddDays(1), old.EffectiveTo, old.WeeklyOffDays);   // purani baad mein chalti rahe
            else
                db.ShiftAssignments.Remove(old);
        }

        foreach (var employeeId in employeeIds)
            db.ShiftAssignments.Add(ShiftAssignment.Create(tenantId, employeeId, c.ShiftId, c.EffectiveFrom, c.EffectiveTo, c.WeeklyOffDays));

        await db.SaveChangesAsync(ct);
        return employeeIds.Count;
    }

    public async Task Handle(DeleteShiftAssignmentCommand c, CancellationToken ct)
    {
        _access.EnsureCanManage();
        var assignment = await db.ShiftAssignments.FirstOrDefaultAsync(a => a.Id == c.Id, ct)
                         ?? throw new NotFoundException("Shift assignment", c.Id);
        db.ShiftAssignments.Remove(assignment);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> Handle(SetRosterEntryCommand c, CancellationToken ct)
    {
        _access.EnsureCanManage();
        if (!await db.Employees.AnyAsync(e => e.Id == c.EmployeeId, ct))
            throw new NotFoundException("Employee", c.EmployeeId);
        if (c.ShiftId is { } shiftId && !await db.Shifts.AnyAsync(s => s.Id == shiftId && s.IsActive, ct))
            throw new NotFoundException("Active shift", shiftId);

        var entry = await db.RosterEntries.FirstOrDefaultAsync(r => r.EmployeeId == c.EmployeeId && r.WorkDate == c.WorkDate, ct);
        if (entry is null)
        {
            entry = RosterEntry.Create(currentUser.RequireTenantId(), c.EmployeeId, c.WorkDate, c.ShiftId, c.Note);
            db.RosterEntries.Add(entry);
        }
        else
        {
            entry.Change(c.ShiftId, c.Note);
        }

        await db.SaveChangesAsync(ct);
        await RefreshDayAsync(c.EmployeeId, c.WorkDate, ct);
        return entry.Id;
    }

    public async Task Handle(ClearRosterEntryCommand c, CancellationToken ct)
    {
        _access.EnsureCanManage();
        var entry = await db.RosterEntries.FirstOrDefaultAsync(r => r.EmployeeId == c.EmployeeId && r.WorkDate == c.WorkDate, ct);
        if (entry is null)
            return;

        db.RosterEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        await RefreshDayAsync(c.EmployeeId, c.WorkDate, ct);
    }

    /// <summary>Us din ki attendance pehle se bani ho to naye roster ke hisaab se dobara calculate.</summary>
    private async Task RefreshDayAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var day = await db.AttendanceDays.Include(d => d.Punches)
            .FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.WorkDate == date, ct);
        if (day is null || day.IsManuallyEdited)
            return;

        var schedule = await _engine.ResolveAsync(employeeId, date, ct);
        day.Reschedule(schedule.DayType, schedule.Shift?.Id, schedule.StartUtc, schedule.EndUtc);
        await _engine.RecalculateAsync(day, ct);
        await db.SaveChangesAsync(ct);
    }
}
