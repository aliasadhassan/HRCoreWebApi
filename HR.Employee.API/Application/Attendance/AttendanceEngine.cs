namespace HR.Employee.API.Application.Attendance;

using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Leaves;
using Microsoft.EntityFrameworkCore;

/// <summary>Roster ka ek din: kis shift pe, kab se kab tak (UTC), din ki qisam, aur ye kahan se aaya.</summary>
public sealed record ScheduledDay(
    Guid EmployeeId, DateOnly Date, DayType DayType, Shift? Shift,
    DateTime? StartUtc, DateTime? EndUtc, ScheduleSource Source);

public enum ScheduleSource : byte { Override = 1, Assignment = 2, WorkWeek = 3 }

/// <summary>
/// Attendance ka "engine": roster resolve, policy chunna, din banana aur recalculate.
/// Handlers isay `new AttendanceEngine(db)` se use karte hain (state nahi, sirf DbContext).
///
/// Roster priority: RosterEntry (us din ka override) → ShiftAssignment (date range) → location ka work week (bina shift).
/// Holiday (tenant ya employee ki location) har cheez pe bhari.
/// </summary>
public sealed class AttendanceEngine(IAppDbContext db)
{
    private sealed record EmployeeInfo(Guid Id, Guid LocationId, string TimeZone, byte WorkWeekDays);

    public async Task<IReadOnlyList<ScheduledDay>> ResolveAsync(
        IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .Select(e => new EmployeeInfo(e.Id, e.LocationId, e.Location.TimeZone, e.Location.WorkWeekDays))
            .ToListAsync(ct);

        var overrides = await db.RosterEntries.AsNoTracking()
            .Where(r => employeeIds.Contains(r.EmployeeId) && r.WorkDate >= from && r.WorkDate <= to)
            .ToListAsync(ct);

        var assignments = await db.ShiftAssignments.AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.EffectiveFrom <= to && (a.EffectiveTo == null || a.EffectiveTo >= from))
            .ToListAsync(ct);

        var locationIds = employees.Select(e => e.LocationId).Distinct().ToList();
        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.Date >= from && h.Date <= to && !h.IsOptional && (h.LocationId == null || locationIds.Contains(h.LocationId.Value)))
            .Select(h => new { h.Date, h.LocationId })
            .ToListAsync(ct);

        var shiftIds = overrides.Where(o => o.ShiftId != null).Select(o => o.ShiftId!.Value)
            .Concat(assignments.Select(a => a.ShiftId)).Distinct().ToList();
        var shifts = await db.Shifts.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        var result = new List<ScheduledDay>();
        foreach (var employee in employees)
        {
            var tz = FindTimeZone(employee.TimeZone);
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var isHoliday = holidays.Any(h => h.Date == date && (h.LocationId == null || h.LocationId == employee.LocationId));
                var day = date;
                var entry = overrides.FirstOrDefault(o => o.EmployeeId == employee.Id && o.WorkDate == day);
                var assignment = assignments.FirstOrDefault(a => a.EmployeeId == employee.Id && a.Covers(day));

                Shift? shift;
                ScheduleSource source;
                bool isOff;
                if (entry is not null)
                {
                    shift = entry.ShiftId is { } id ? shifts.GetValueOrDefault(id) : null;
                    source = ScheduleSource.Override;
                    isOff = entry.IsOff;
                }
                else if (assignment is not null)
                {
                    shift = shifts.GetValueOrDefault(assignment.ShiftId);
                    source = ScheduleSource.Assignment;
                    isOff = assignment.WeeklyOffDays is { } off ? (off & DayBit(day)) != 0 : (employee.WorkWeekDays & DayBit(day)) == 0;
                }
                else
                {
                    shift = null;
                    source = ScheduleSource.WorkWeek;
                    isOff = (employee.WorkWeekDays & DayBit(day)) == 0;
                }

                var dayType = isHoliday ? DayType.Holiday : isOff ? DayType.WeeklyOff : DayType.Workday;
                var (start, end) = shift is null ? (null, null) : ToUtc(day, shift, tz);
                result.Add(new ScheduledDay(employee.Id, day, dayType, shift, start, end, source));
            }
        }

        return result;
    }

    public async Task<ScheduledDay> ResolveAsync(Guid employeeId, DateOnly date, CancellationToken ct)
        => (await ResolveAsync([employeeId], date, date, ct)).Single();

    /// <summary>Employee ki location ki active policy, warna tenant default, warna built-in defaults.</summary>
    public async Task<AttendancePolicy> PolicyForAsync(Guid employeeId, CancellationToken ct)
    {
        var locationId = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.LocationId).SingleAsync(ct);
        var policies = await db.AttendancePolicies.AsNoTracking()
            .Where(p => p.IsActive && (p.LocationId == locationId || p.LocationId == null))
            .ToListAsync(ct);

        return policies.FirstOrDefault(p => p.LocationId == locationId)
               ?? policies.FirstOrDefault()
               ?? AttendancePolicy.Create(Guid.NewGuid(), "Default", null);   // sirf memory mein, save nahi hota
    }

    /// <summary>
    /// Punch kis "work date" ka hai: raat ki shift (22:00–06:00) ka 03:00 wala punch pichle din ka hai.
    /// </summary>
    public async Task<DateOnly> WorkDateForAsync(Guid employeeId, DateTime punchedAtUtc, CancellationToken ct)
    {
        var timeZone = await db.Employees.Where(e => e.Id == employeeId).Select(e => e.Location.TimeZone).SingleAsync(ct);
        var local = TimeZoneInfo.ConvertTimeFromUtc(punchedAtUtc, FindTimeZone(timeZone));
        var today = DateOnly.FromDateTime(local);

        var yesterday = await ResolveAsync(employeeId, today.AddDays(-1), ct);
        if (yesterday.Shift is { CrossesMidnight: true } && yesterday.EndUtc is { } end && punchedAtUtc <= end.AddHours(4))
            return yesterday.Date;

        return today;
    }

    /// <summary>Din dhoondo ya banao (schedule snapshot ke saath). Naya din context mein Add hota hai, save caller karta hai.</summary>
    public async Task<AttendanceDay> GetOrOpenDayAsync(Guid tenantId, Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var day = await db.AttendanceDays.Include(d => d.Punches)
            .FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.WorkDate == date, ct);
        if (day is not null)
            return day;

        var schedule = await ResolveAsync(employeeId, date, ct);
        day = AttendanceDay.Open(tenantId, employeeId, date, schedule.DayType, schedule.Shift?.Id, schedule.StartUtc, schedule.EndUtc);
        db.AttendanceDays.Add(day);
        return day;
    }

    /// <summary>Leave link + totals dobara (punch, correction, roster change ke baad).</summary>
    public async Task RecalculateAsync(AttendanceDay day, CancellationToken ct)
    {
        var leave = await db.LeaveRequests.AsNoTracking()
            .Where(l => l.EmployeeId == day.EmployeeId && l.Status == LeaveRequestStatus.Approved
                        && l.StartDate <= day.WorkDate && l.EndDate >= day.WorkDate)
            .Select(l => new { l.Id, l.IsHalfDay })
            .FirstOrDefaultAsync(ct);
        day.LinkLeave(leave?.Id, day.Status);

        var shift = day.ShiftId is { } shiftId ? await db.Shifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shiftId, ct) : null;
        var policy = await PolicyForAsync(day.EmployeeId, ct);
        day.Apply(AttendanceCalculator.Calculate(day, shift, policy, leave?.IsHalfDay ?? false));
    }

    /// <summary>Kisi date ke saare active employees ke din bana/refresh karo (absent, off, holiday, leave sab).</summary>
    public async Task<int> ProcessDateAsync(Guid tenantId, DateOnly date, Guid? locationId, CancellationToken ct)
    {
        var employeeIds = await db.Employees
            .Where(e => e.EmploymentStatus != EmploymentStatus.Exited && e.JoiningDate <= date
                        && (e.ExitDate == null || e.ExitDate >= date)
                        && (locationId == null || e.LocationId == locationId))
            .Select(e => e.Id)
            .ToListAsync(ct);

        var schedules = (await ResolveAsync(employeeIds, date, date, ct)).ToDictionary(s => s.EmployeeId);
        var existing = await db.AttendanceDays.Include(d => d.Punches)
            .Where(d => d.WorkDate == date && employeeIds.Contains(d.EmployeeId))
            .ToDictionaryAsync(d => d.EmployeeId, ct);

        foreach (var employeeId in employeeIds)
        {
            var schedule = schedules[employeeId];
            if (!existing.TryGetValue(employeeId, out var day))
            {
                day = AttendanceDay.Open(tenantId, employeeId, date, schedule.DayType, schedule.Shift?.Id, schedule.StartUtc, schedule.EndUtc);
                db.AttendanceDays.Add(day);
            }
            else if (!day.IsManuallyEdited)
            {
                day.Reschedule(schedule.DayType, schedule.Shift?.Id, schedule.StartUtc, schedule.EndUtc);
            }

            await RecalculateAsync(day, ct);
        }

        return employeeIds.Count;
    }

    /// <summary>Location ke local time mein shift → UTC. Raat ki shift ka end agle din.</summary>
    public static (DateTime? Start, DateTime? End) ToUtc(DateOnly date, Shift shift, TimeZoneInfo tz)
    {
        var start = date.ToDateTime(shift.StartTime);
        var end = (shift.CrossesMidnight ? date.AddDays(1) : date).ToDateTime(shift.EndTime);
        return (TimeZoneInfo.ConvertTimeToUtc(start, tz), TimeZoneInfo.ConvertTimeToUtc(end, tz));
    }

    public static TimeZoneInfo FindTimeZone(string id)
        => TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz) ? tz : TimeZoneInfo.Utc;

    /// <summary>Location.WorkWeekDays wala bitmask: Mon=1 .. Sun=64.</summary>
    public static int DayBit(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 64 : 1 << ((int)date.DayOfWeek - 1);
}
