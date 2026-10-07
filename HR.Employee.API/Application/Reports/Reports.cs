namespace HR.Employee.API.Application.Reports;

using FluentValidation;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Leaves;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Reports (people + time): sirf parhna, koi schema nahi. Gateway: /reports/people, /reports/time.
// Pay wali reports Payroll API ke /api/payroll/reports se aati hain.
// Range zyada se zyada ek saal; department optional.

public sealed record CountRowDto(string Key, int Count);

public sealed record PeopleSummaryDto(
    int HeadcountStart, int HeadcountEnd, int Joined, int Left, decimal AttritionRate, decimal AverageTenureYears, decimal? AverageAgeYears);

public sealed record PeopleDepartmentRowDto(Guid? DepartmentId, string Name, int Headcount, int Joined, int Left, decimal AverageTenureYears);

public sealed record HeadcountPointDto(DateOnly Date, int Headcount);

public enum MovementKind : byte { Joined = 1, Left = 2 }

public sealed record MovementDto(
    Guid EmployeeId, string EmployeeCode, string Name, string Department, string Designation, DateOnly Date, MovementKind Kind, string? Reason);

public sealed record PeopleReportDto(
    DateOnly From, DateOnly To, PeopleSummaryDto Summary, IReadOnlyList<PeopleDepartmentRowDto> Departments,
    IReadOnlyList<CountRowDto> ByType, IReadOnlyList<CountRowDto> ByStatus, IReadOnlyList<CountRowDto> ByGender,
    IReadOnlyList<CountRowDto> ByLocation, IReadOnlyList<CountRowDto> TenureBands,
    IReadOnlyList<HeadcountPointDto> Trend, IReadOnlyList<MovementDto> Movements);

public sealed record TimeSummaryDto(
    int Scheduled, int Attended, int Absent, int LateDays, int LateMinutes, int OvertimeMinutes, decimal LeaveDays, decimal AttendanceRate);

public sealed record TimeEmployeeRowDto(
    Guid EmployeeId, string EmployeeCode, string Name, string Department,
    int Scheduled, int Present, int HalfDays, int Absent, int Incomplete, int LateDays, int LateMinutes, int OvertimeMinutes,
    decimal LeaveDays, decimal AttendanceRate);

public sealed record TimeDepartmentRowDto(string Name, int Employees, int Scheduled, int Attended, int LateDays, decimal LeaveDays, decimal AttendanceRate);

public sealed record LeaveTypeRowDto(string Name, string? Color, bool IsPaid, int Requests, int Employees, decimal Days);

public sealed record TimeReportDto(
    DateOnly From, DateOnly To, TimeSummaryDto Summary, IReadOnlyList<TimeEmployeeRowDto> Employees,
    IReadOnlyList<TimeDepartmentRowDto> Departments, IReadOnlyList<LeaveTypeRowDto> LeaveTypes);

public sealed record GetPeopleReportQuery(DateOnly From, DateOnly To, Guid? DepartmentId) : IRequest<PeopleReportDto>;

public sealed record GetTimeReportQuery(DateOnly From, DateOnly To, Guid? DepartmentId) : IRequest<TimeReportDto>;

public static class ReportRange
{
    public const int MaxDays = 366;

    public static void Rules<T>(AbstractValidator<T> v, Func<T, DateOnly> from, Func<T, DateOnly> to)
    {
        v.RuleFor(x => from(x)).LessThanOrEqualTo(x => to(x)).WithName("From").WithMessage("'From' must be on or before 'To'.");
        v.RuleFor(x => to(x).DayNumber - from(x).DayNumber).LessThan(MaxDays).WithName("To").WithMessage("Pick a range of one year or less.");
    }
}

public sealed class GetPeopleReportValidator : AbstractValidator<GetPeopleReportQuery>
{
    public GetPeopleReportValidator() => ReportRange.Rules(this, x => x.From, x => x.To);
}

public sealed class GetTimeReportValidator : AbstractValidator<GetTimeReportQuery>
{
    public GetTimeReportValidator() => ReportRange.Rules(this, x => x.From, x => x.To);
}

public sealed class ReportHandlers(IAppDbContext db) :
    IRequestHandler<GetPeopleReportQuery, PeopleReportDto>,
    IRequestHandler<GetTimeReportQuery, TimeReportDto>
{
    private const int TrendMonths = 12;
    private const int MaxMovements = 500;

    public async Task<PeopleReportDto> Handle(GetPeopleReportQuery q, CancellationToken ct)
    {
        var employees = db.Employees.AsNoTracking();
        if (q.DepartmentId is { } dept)
            employees = employees.Where(e => e.DepartmentId == dept);

        var people = await employees
            .Select(e => new Person(e.Id, e.EmployeeCode, e.FirstName + " " + e.LastName, e.DepartmentId, e.Department.Name,
                e.Designation.Title, e.Location.Name, e.EmploymentType, e.EmploymentStatus, e.Gender, e.DateOfBirth,
                e.JoiningDate, e.ExitDate, e.ExitReason))
            .ToListAsync(ct);

        var dayBefore = q.From.AddDays(-1);
        var atStart = people.Where(p => p.IsCurrent(dayBefore)).ToList();
        var atEnd = people.Where(p => p.IsCurrent(q.To)).ToList();
        var joined = people.Where(p => p.JoiningDate >= q.From && p.JoiningDate <= q.To).ToList();
        var left = people.Where(p => p.ExitDate is { } x && x >= q.From && x <= q.To).ToList();

        // Attrition = range mein exits / average headcount
        var average = (atStart.Count + atEnd.Count) / 2m;
        var attrition = average > 0 ? Math.Round(left.Count * 100m / average, 1) : 0m;

        var ages = atEnd.Where(p => p.DateOfBirth is not null).Select(p => YearsBetween(p.DateOfBirth!.Value, q.To)).ToList();
        var summary = new PeopleSummaryDto(atStart.Count, atEnd.Count, joined.Count, left.Count, attrition,
            Tenure(atEnd, q.To), ages.Count > 0 ? Math.Round(ages.Average(), 1) : null);

        var departments = people
            .GroupBy(p => (p.DepartmentId, p.Department))
            .Select(g => new PeopleDepartmentRowDto(g.Key.DepartmentId, g.Key.Department,
                g.Count(p => p.IsCurrent(q.To)),
                g.Count(p => p.JoiningDate >= q.From && p.JoiningDate <= q.To),
                g.Count(p => p.ExitDate is { } x && x >= q.From && x <= q.To),
                Tenure(g.Where(p => p.IsCurrent(q.To)).ToList(), q.To)))
            .Where(d => d.Headcount > 0 || d.Joined > 0 || d.Left > 0)
            .OrderByDescending(d => d.Headcount).ThenBy(d => d.Name)
            .ToList();

        var tenureBands = new (string Key, decimal Min, decimal Max)[]
            {
                ("lt1", 0, 1), ("1to3", 1, 3), ("3to5", 3, 5), ("5to10", 5, 10), ("10plus", 10, decimal.MaxValue)
            }
            .Select(b => new CountRowDto(b.Key, atEnd.Count(p => YearsBetween(p.JoiningDate, q.To) is var y && y >= b.Min && y < b.Max)))
            .ToList();

        // 12 month-end points, aakhri point "To" khud
        var trend = Enumerable.Range(0, TrendMonths)
            .Select(i => MonthEnd(q.To, i - (TrendMonths - 1)))
            .Select(d => d > q.To ? q.To : d)
            .Distinct()
            .Select(d => new HeadcountPointDto(d, people.Count(p => p.IsCurrent(d))))
            .ToList();

        var movements = joined.Select(p => new MovementDto(p.Id, p.Code, p.Name, p.Department, p.Designation, p.JoiningDate, MovementKind.Joined, null))
            .Concat(left.Select(p => new MovementDto(p.Id, p.Code, p.Name, p.Department, p.Designation, p.ExitDate!.Value, MovementKind.Left, p.ExitReason)))
            .OrderByDescending(m => m.Date).ThenBy(m => m.Name)
            .Take(MaxMovements)
            .ToList();

        return new PeopleReportDto(q.From, q.To, summary, departments,
            Counts(atEnd, p => p.Type.ToString()),
            Counts(atEnd, p => p.Status.ToString()),
            Counts(atEnd, p => p.Gender?.ToString() ?? "Unknown"),
            Counts(atEnd, p => p.Location),
            tenureBands, trend, movements);
    }

    public async Task<TimeReportDto> Handle(GetTimeReportQuery q, CancellationToken ct)
    {
        var employees = db.Employees.AsNoTracking();
        if (q.DepartmentId is { } dept)
            employees = employees.Where(e => e.DepartmentId == dept);

        // Range mein kabhi bhi company mein tha
        var people = await employees
            .Where(e => e.JoiningDate <= q.To && (e.ExitDate == null || e.ExitDate >= q.From))
            .Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName, Department = e.Department.Name })
            .ToListAsync(ct);
        var ids = people.Select(p => p.Id).ToList();

        var days = await db.AttendanceDays.AsNoTracking()
            .Where(d => d.WorkDate >= q.From && d.WorkDate <= q.To && d.DayType == DayType.Workday && ids.Contains(d.EmployeeId))
            .GroupBy(d => d.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                Scheduled = g.Count(d => d.Status != AttendanceStatus.OnLeave),
                Present = g.Count(d => d.Status == AttendanceStatus.Present),
                HalfDays = g.Count(d => d.Status == AttendanceStatus.HalfDay),
                Absent = g.Count(d => d.Status == AttendanceStatus.Absent),
                Incomplete = g.Count(d => d.Status == AttendanceStatus.Incomplete),
                LateDays = g.Count(d => d.LateMinutes > 0),
                LateMinutes = g.Sum(d => (int)d.LateMinutes),
                OvertimeMinutes = g.Sum(d => (int)d.OvertimeMinutes)
            })
            .ToDictionaryAsync(x => x.EmployeeId, ct);

        // Leave start date ke hisaab se range mein ginti (approved hi)
        var leaves = await db.LeaveRequests.AsNoTracking()
            .Where(r => r.Status == LeaveRequestStatus.Approved && r.StartDate >= q.From && r.StartDate <= q.To && ids.Contains(r.EmployeeId))
            .Join(db.LeaveTypes, r => r.LeaveTypeId, t => t.Id, (r, t) => new { r.EmployeeId, r.TotalDays, t.Name, t.Color, t.IsPaid })
            .ToListAsync(ct);
        var leaveByEmployee = leaves.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(l => l.TotalDays));

        var rows = people
            .Select(p =>
            {
                days.TryGetValue(p.Id, out var d);
                var scheduled = d?.Scheduled ?? 0;
                var attended = (d?.Present ?? 0) + (d?.HalfDays ?? 0) + (d?.Incomplete ?? 0);
                return new TimeEmployeeRowDto(p.Id, p.EmployeeCode, p.Name, p.Department,
                    scheduled, d?.Present ?? 0, d?.HalfDays ?? 0, d?.Absent ?? 0, d?.Incomplete ?? 0,
                    d?.LateDays ?? 0, d?.LateMinutes ?? 0, d?.OvertimeMinutes ?? 0,
                    leaveByEmployee.GetValueOrDefault(p.Id), Rate(attended, scheduled));
            })
            .Where(r => r.Scheduled > 0 || r.LeaveDays > 0)
            .OrderBy(r => r.Department).ThenBy(r => r.Name)
            .ToList();

        static int Attended(TimeEmployeeRowDto r) => r.Present + r.HalfDays + r.Incomplete;

        var departments = rows
            .GroupBy(r => r.Department)
            .Select(g => new TimeDepartmentRowDto(g.Key, g.Count(), g.Sum(r => r.Scheduled), g.Sum(Attended), g.Sum(r => r.LateDays),
                g.Sum(r => r.LeaveDays), Rate(g.Sum(Attended), g.Sum(r => r.Scheduled))))
            .OrderBy(d => d.AttendanceRate)
            .ToList();

        var leaveTypes = leaves
            .GroupBy(l => (l.Name, l.Color, l.IsPaid))
            .Select(g => new LeaveTypeRowDto(g.Key.Name, g.Key.Color, g.Key.IsPaid, g.Count(), g.Select(l => l.EmployeeId).Distinct().Count(), g.Sum(l => l.TotalDays)))
            .OrderByDescending(t => t.Days)
            .ToList();

        var totalScheduled = rows.Sum(r => r.Scheduled);
        var totalAttended = rows.Sum(Attended);
        var summary = new TimeSummaryDto(totalScheduled, totalAttended, rows.Sum(r => r.Absent), rows.Sum(r => r.LateDays),
            rows.Sum(r => r.LateMinutes), rows.Sum(r => r.OvertimeMinutes), rows.Sum(r => r.LeaveDays), Rate(totalAttended, totalScheduled));

        return new TimeReportDto(q.From, q.To, summary, rows, departments, leaveTypes);
    }

    private static decimal Rate(int part, int whole) => whole > 0 ? Math.Round(part * 100m / whole, 1) : 0m;

    private static decimal YearsBetween(DateOnly from, DateOnly to) => (to.DayNumber - from.DayNumber) / 365.25m;

    private static decimal Tenure(List<Person> people, DateOnly asOf)
        => people.Count > 0 ? Math.Round(people.Average(p => YearsBetween(p.JoiningDate, asOf)), 1) : 0m;

    private static DateOnly MonthEnd(DateOnly date, int monthOffset)
    {
        var first = new DateOnly(date.Year, date.Month, 1).AddMonths(monthOffset);
        return first.AddMonths(1).AddDays(-1);
    }

    private static List<CountRowDto> Counts(List<Person> people, Func<Person, string> key)
        => people.GroupBy(key).Select(g => new CountRowDto(g.Key, g.Count())).OrderByDescending(c => c.Count).ThenBy(c => c.Key).ToList();

    private sealed record Person(
        Guid Id, string Code, string Name, Guid DepartmentId, string Department, string Designation, string Location,
        EmploymentType Type, EmploymentStatus Status, Gender? Gender, DateOnly? DateOfBirth,
        DateOnly JoiningDate, DateOnly? ExitDate, string? ExitReason)
    {
        /// <summary>Us din company mein tha? (Dashboard wala hi rule)</summary>
        public bool IsCurrent(DateOnly date) => JoiningDate <= date && (ExitDate is null || ExitDate >= date)
                                                && (Status != EmploymentStatus.Exited || ExitDate is not null);
    }
}
