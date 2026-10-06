namespace HR.Employee.API.Application.Dashboard;

using HR.Employee.API.Application.Attendance;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Leaves;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Dashboard (people side): aaj ki haazri, workforce ledger, 14 din ka trend, mere pending kaam, aane wale din.
// Payroll wala hissa (banknote, department cost, payday) Payroll API ke /api/payroll/dashboard se aata hai.
// Scope: employees.view = poori company, warna sirf apna department.

public enum DashboardScope : byte { Company = 1, Department = 2, None = 3 }

public enum AwayKind : byte { Leave = 1, Remote = 2 }

public enum DashboardTaskKind : byte { Leave = 1, AttendanceRequest = 2, Probation = 3 }

public enum UpcomingKind : byte { Holiday = 1, Leave = 2, Anniversary = 3, Joiner = 4, Probation = 5 }

public sealed record WorkforceStatsDto(int Headcount, int HeadcountChange, int JoinedThisMonth, decimal AttritionRate, decimal AverageTenureYears);

public sealed record AwayPersonDto(Guid EmployeeId, string Name, AwayKind Kind, string? LeaveTypeName, DateOnly? Until);

public sealed record TodayDto(DateOnly Date, int Total, int Present, int Remote, int OnLeave, int NotIn, int Late, IReadOnlyList<AwayPersonDto> Away);

public sealed record AttendanceTrendPointDto(DateOnly Date, decimal Rate);

public sealed record DashboardTaskDto(
    Guid Id, DashboardTaskKind Kind, Guid EmployeeId, string EmployeeName, string? Detail,
    DateOnly? StartDate, DateOnly? EndDate, decimal? Days, AttendanceRequestType? RequestType);

public sealed record UpcomingEventDto(DateOnly Date, UpcomingKind Kind, string Name, int? Years);

public sealed record PeopleDashboardDto(
    DashboardScope Scope, WorkforceStatsDto Stats, TodayDto Today, IReadOnlyList<AttendanceTrendPointDto> AttendanceTrend,
    IReadOnlyList<DashboardTaskDto> Tasks, IReadOnlyList<UpcomingEventDto> Upcoming);

public sealed record GetPeopleDashboardQuery : IRequest<PeopleDashboardDto>;

public sealed class DashboardHandlers(IAppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetPeopleDashboardQuery, PeopleDashboardDto>
{
    private const int TrendDays = 14;
    private const int UpcomingDays = 30;
    private const int MaxAway = 8;
    private const int MaxUpcoming = 8;

    private readonly AttendanceAccess _access = new(db, currentUser);

    private bool IsHrApprover => currentUser.HasPermission(Permissions.LeavesApprove);

    public async Task<PeopleDashboardDto> Handle(GetPeopleDashboardQuery q, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var me = await _access.TryCurrentEmployeeAsync(ct);

        var scope = _access.CanViewAll ? DashboardScope.Company : me is null ? DashboardScope.None : DashboardScope.Department;
        var employees = db.Employees.AsNoTracking();
        employees = scope switch
        {
            DashboardScope.Company => employees,
            DashboardScope.Department => employees.Where(e => e.DepartmentId == me!.DepartmentId),
            _ => employees.Where(_ => false)
        };

        // Halki list: naam, dates, status — tenant chhota ho ya bada, ek hi query
        var people = await employees
            .Select(e => new Person(e.Id, e.FirstName + " " + e.LastName, e.LocationId, e.EmploymentStatus,
                e.JoiningDate, e.ExitDate, e.ProbationEndDate))
            .ToListAsync(ct);

        var current = people.Where(p => p.IsCurrent(today)).ToList();
        var ids = current.Select(p => p.Id).ToList();

        var stats = BuildStats(people, current, today);
        var todayDto = await BuildTodayAsync(current, ids, today, ct);
        var trend = await BuildTrendAsync(ids, today, ct);
        var tasks = await BuildTasksAsync(me, current, today, ct);
        var upcoming = await BuildUpcomingAsync(people, current, ids, today, ct);

        return new PeopleDashboardDto(scope, stats, todayDto, trend, tasks, upcoming);
    }

    private static WorkforceStatsDto BuildStats(List<Person> all, List<Person> current, DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var yearAgo = today.AddYears(-1);

        var headcountAtMonthStart = all.Count(p => p.IsCurrent(monthStart.AddDays(-1)));
        var joinedThisMonth = all.Count(p => p.JoiningDate >= monthStart && p.JoiningDate <= today);

        // Attrition (12 mahine) = exits / average headcount
        var exits = all.Count(p => p.ExitDate is { } x && x > yearAgo && x <= today);
        var headcountYearAgo = all.Count(p => p.IsCurrent(yearAgo));
        var average = (headcountYearAgo + current.Count) / 2m;
        var attrition = average > 0 ? Math.Round(exits * 100m / average, 1) : 0m;

        var tenure = current.Count > 0
            ? Math.Round((decimal)current.Average(p => today.DayNumber - p.JoiningDate.DayNumber) / 365.25m, 1)
            : 0m;

        return new WorkforceStatsDto(current.Count, current.Count - headcountAtMonthStart, joinedThisMonth, attrition, tenure);
    }

    private async Task<TodayDto> BuildTodayAsync(List<Person> current, List<Guid> ids, DateOnly today, CancellationToken ct)
    {
        var names = current.ToDictionary(p => p.Id, p => p.Name);

        var leaves = await db.LeaveRequests.AsNoTracking()
            .Where(r => r.Status == LeaveRequestStatus.Approved && r.StartDate <= today && r.EndDate >= today && ids.Contains(r.EmployeeId))
            .Join(db.LeaveTypes, r => r.LeaveTypeId, t => t.Id, (r, t) => new { r.EmployeeId, r.EndDate, r.IsHalfDay, TypeName = t.Name })
            .ToListAsync(ct);
        // Half-day leave wala aadha din kaam pe bhi hota hai, isliye "on leave" mein sirf poore din wale
        var onLeave = leaves.Where(l => !l.IsHalfDay).GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.First());

        var remote = (await db.AttendanceRequests.AsNoTracking()
                .Where(r => r.Type == AttendanceRequestType.WorkFromHome && r.Status == AttendanceRequestStatus.Approved
                            && r.WorkDate == today && ids.Contains(r.EmployeeId))
                .Select(r => r.EmployeeId)
                .ToListAsync(ct))
            .Where(id => !onLeave.ContainsKey(id))
            .ToHashSet();

        var days = await db.AttendanceDays.AsNoTracking()
            .Where(d => d.WorkDate == today && ids.Contains(d.EmployeeId))
            .Select(d => new { d.EmployeeId, d.Status, d.FirstIn, d.LateMinutes })
            .ToListAsync(ct);

        var present = days.Count(d => d.FirstIn != null && !onLeave.ContainsKey(d.EmployeeId) && !remote.Contains(d.EmployeeId));
        var late = days.Count(d => d.LateMinutes > 0);
        var notIn = Math.Max(0, current.Count - present - remote.Count - onLeave.Count);

        var away = onLeave.Values
            .Select(l => new AwayPersonDto(l.EmployeeId, names[l.EmployeeId], AwayKind.Leave, l.TypeName, l.EndDate > today ? l.EndDate : null))
            .Concat(remote.Select(id => new AwayPersonDto(id, names[id], AwayKind.Remote, null, null)))
            .OrderBy(a => a.Kind).ThenBy(a => a.Name)
            .Take(MaxAway)
            .ToList();

        return new TodayDto(today, current.Count, present, remote.Count, onLeave.Count, notIn, late, away);
    }

    /// <summary>Pichhle 14 working din (jin din attendance khuli): haazir / (scheduled − leave).</summary>
    private async Task<IReadOnlyList<AttendanceTrendPointDto>> BuildTrendAsync(List<Guid> ids, DateOnly today, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];

        var from = today.AddDays(-TrendDays * 2);
        var rows = await db.AttendanceDays.AsNoTracking()
            .Where(d => d.WorkDate > from && d.WorkDate <= today && d.DayType == DayType.Workday
                        && d.Status != AttendanceStatus.OnLeave && ids.Contains(d.EmployeeId))
            .GroupBy(d => d.WorkDate)
            .Select(g => new
            {
                Date = g.Key,
                Scheduled = g.Count(),
                Attended = g.Count(d => d.Status == AttendanceStatus.Present || d.Status == AttendanceStatus.HalfDay
                                        || d.Status == AttendanceStatus.Incomplete)
            })
            .ToListAsync(ct);

        return rows
            .Where(r => r.Scheduled > 0)
            .OrderByDescending(r => r.Date)
            .Take(TrendDays)
            .OrderBy(r => r.Date)
            .Select(r => new AttendanceTrendPointDto(r.Date, Math.Round(r.Attended * 100m / r.Scheduled, 1)))
            .ToList();
    }

    /// <summary>Wahi inbox jo Leaves/Attendance ke Approvals tab mein hai, plus HR ke liye probation khatam.</summary>
    private async Task<IReadOnlyList<DashboardTaskDto>> BuildTasksAsync(CurrentEmployee? me, List<Person> current, DateOnly today, CancellationToken ct)
    {
        var meId = me?.Id;
        var hr = IsHrApprover;

        var leaveTasks = await db.LeaveRequests.AsNoTracking()
            .Where(r => r.Status == LeaveRequestStatus.Pending && r.EmployeeId != meId
                        && r.Approvals.Any(a => a.Level == r.CurrentApprovalLevel
                            && ((meId != null && a.AssignedApproverId == meId) || (hr && a.ApproverType == ApproverType.HR))))
            .OrderBy(r => r.StartDate)
            .Take(20)
            .Join(db.Employees, r => r.EmployeeId, e => e.Id, (r, e) => new { r, Name = e.FirstName + " " + e.LastName })
            .Join(db.LeaveTypes, x => x.r.LeaveTypeId, t => t.Id, (x, t) => new DashboardTaskDto(
                x.r.Id, DashboardTaskKind.Leave, x.r.EmployeeId, x.Name, t.Name,
                x.r.StartDate, x.r.EndDate, x.r.TotalDays, null))
            .ToListAsync(ct);

        var attendanceTasks = await db.AttendanceRequests.AsNoTracking()
            .Where(r => r.Status == AttendanceRequestStatus.Pending && r.EmployeeId != meId
                        && ((meId != null && r.AssignedApproverId == meId) || (hr && r.ApproverType == ApproverType.HR)))
            .OrderBy(r => r.WorkDate)
            .Take(20)
            .Join(db.Employees, r => r.EmployeeId, e => e.Id, (r, e) => new DashboardTaskDto(
                r.Id, DashboardTaskKind.AttendanceRequest, r.EmployeeId, e.FirstName + " " + e.LastName, null,
                r.WorkDate, r.WorkDate, null, r.Type))
            .ToListAsync(ct);

        var probationTasks = currentUser.HasPermission(Permissions.EmployeesEdit)
            ? current
                .Where(p => p.Status == EmploymentStatus.Probation && p.ProbationEndDate is { } end && end <= today.AddDays(UpcomingDays))
                .OrderBy(p => p.ProbationEndDate)
                .Select(p => new DashboardTaskDto(p.Id, DashboardTaskKind.Probation, p.Id, p.Name, null,
                    null, p.ProbationEndDate, p.ProbationEndDate!.Value.DayNumber - today.DayNumber, null))
                .ToList()
            : [];

        return [.. leaveTasks, .. attendanceTasks, .. probationTasks];
    }

    private async Task<IReadOnlyList<UpcomingEventDto>> BuildUpcomingAsync(List<Person> all, List<Person> current, List<Guid> ids, DateOnly today, CancellationToken ct)
    {
        var until = today.AddDays(UpcomingDays);
        var events = new List<UpcomingEventDto>();

        var locations = current.Select(p => p.LocationId).Distinct().ToList();
        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.Date >= today && h.Date <= until && (h.LocationId == null || locations.Contains(h.LocationId.Value)))
            .Select(h => new { h.Date, h.Name })
            .ToListAsync(ct);
        events.AddRange(holidays.DistinctBy(h => (h.Date, h.Name)).Select(h => new UpcomingEventDto(h.Date, UpcomingKind.Holiday, h.Name, null)));

        var leaves = await db.LeaveRequests.AsNoTracking()
            .Where(r => r.Status == LeaveRequestStatus.Approved && r.StartDate > today && r.StartDate <= until && ids.Contains(r.EmployeeId))
            .Select(r => new { r.EmployeeId, r.StartDate })
            .ToListAsync(ct);
        var names = current.ToDictionary(p => p.Id, p => p.Name);
        events.AddRange(leaves.Select(l => new UpcomingEventDto(l.StartDate, UpcomingKind.Leave, names[l.EmployeeId], null)));

        // Naye joiners abhi "current" mein nahi (joining date aage hai)
        events.AddRange(all
            .Where(p => p.JoiningDate > today && p.JoiningDate <= until && p.Status != EmploymentStatus.Exited)
            .Select(p => new UpcomingEventDto(p.JoiningDate, UpcomingKind.Joiner, p.Name, null)));

        foreach (var p in current)
        {
            if (NextAnniversary(p.JoiningDate, today) is var (date, years) && years > 0 && date <= until)
                events.Add(new UpcomingEventDto(date, UpcomingKind.Anniversary, p.Name, years));

            if (p.Status == EmploymentStatus.Probation && p.ProbationEndDate is { } end && end >= today && end <= until)
                events.Add(new UpcomingEventDto(end, UpcomingKind.Probation, p.Name, null));
        }

        return events.OrderBy(e => e.Date).ThenBy(e => e.Kind).Take(MaxUpcoming).ToList();
    }

    private static (DateOnly Date, int Years) NextAnniversary(DateOnly joined, DateOnly today)
    {
        var years = today.Year - joined.Year;
        var date = SafeDate(today.Year, joined.Month, joined.Day);
        if (date < today)
        {
            years++;
            date = SafeDate(today.Year + 1, joined.Month, joined.Day);
        }
        return (date, years);
    }

    // 29 Feb wale ka anniversary non-leap saal mein 28 Feb
    private static DateOnly SafeDate(int year, int month, int day) => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    private sealed record Person(
        Guid Id, string Name, Guid LocationId, EmploymentStatus Status, DateOnly JoiningDate, DateOnly? ExitDate, DateOnly? ProbationEndDate)
    {
        /// <summary>Us din company mein tha? (join ho chuka, exit nahi hua)</summary>
        public bool IsCurrent(DateOnly date) => JoiningDate <= date && (ExitDate is null || ExitDate >= date)
                                                && (Status != EmploymentStatus.Exited || ExitDate is not null);
    }
}
