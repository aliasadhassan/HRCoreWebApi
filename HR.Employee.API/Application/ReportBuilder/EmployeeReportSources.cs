namespace HR.Employee.API.Application.ReportBuilder;

using System.Text.RegularExpressions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Reporting;
using Microsoft.EntityFrameworkCore;
using F = HR.Shared.Library.Reporting.ReportFieldDef;
using T = HR.Shared.Library.Reporting.ReportFieldType;

using Row = System.Collections.Generic.Dictionary<string, object?>;

/// <summary>
/// Employee API ke report builder sources. Har source ka apna permission (wahi jo us module ka page maangta hai),
/// whitelisted fields aur loader. Loader tenant filter + RLS ke andar chalta hai; engine baad mein filter/group karta hai.
/// </summary>
public sealed class EmployeeReportSources(IAppDbContext db)
{
    /// <summary>Ek report mein zyada se zyada itni rows memory mein (is se zyada ho to range chhoti karo).</summary>
    public const int MaxLoad = 50_000;
    public const int MaxRangeDays = 366;

    private sealed record Source(ReportSourceDef Def, string[] AnyPermission);

    private static readonly F[] PersonFields =
    [
        new("code", "Employee code", T.Text, "Employee"),
        new("employee", "Employee", T.Text, "Employee"),
        new("department", "Department", T.Text, "Job"),
        new("designation", "Designation", T.Text, "Job"),
        new("location", "Location", T.Text, "Job"),
    ];

    private static readonly Source[] All =
    [
        new(new("employees", "Employees", "Everyone on the payroll list with job and personal details.", false,
        [
            new("code", "Employee code", T.Text, "Employee"),
            new("employee", "Full name", T.Text, "Employee"),
            new("firstName", "First name", T.Text, "Employee"),
            new("lastName", "Last name", T.Text, "Employee"),
            new("gender", "Gender", T.Text, "Employee"),
            new("dateOfBirth", "Date of birth", T.Date, "Employee"),
            new("age", "Age", T.Number, "Employee", NoTotal: true),
            new("maritalStatus", "Marital status", T.Text, "Employee"),
            new("nationality", "Nationality", T.Text, "Employee"),
            new("workEmail", "Work email", T.Text, "Employee"),
            new("workPhone", "Work phone", T.Text, "Employee"),
            new("department", "Department", T.Text, "Job"),
            new("designation", "Designation", T.Text, "Job"),
            new("level", "Grade level", T.Number, "Job", NoTotal: true),
            new("location", "Location", T.Text, "Job"),
            new("manager", "Manager", T.Text, "Job"),
            new("employmentType", "Employment type", T.Text, "Job"),
            new("status", "Status", T.Text, "Job"),
            new("joiningDate", "Joining date", T.Date, "Job"),
            new("serviceYears", "Years of service", T.Number, "Job", NoTotal: true),
            new("probationEnd", "Probation ends", T.Date, "Job"),
            new("confirmationDate", "Confirmation date", T.Date, "Job"),
            new("noticeDays", "Notice period (days)", T.Number, "Job", NoTotal: true),
            new("exitDate", "Exit date", T.Date, "Exit"),
            new("exitReason", "Exit reason", T.Text, "Exit"),
        ]), [Permissions.EmployeesView]),

        new(new("attendance", "Attendance", "One row per employee per day in the period.", true,
        [
            new("date", "Date", T.Date, "Day"),
            new("month", "Month", T.Text, "Day"),
            .. PersonFields,
            new("dayType", "Day type", T.Text, "Day"),
            new("status", "Status", T.Text, "Day"),
            new("workedHours", "Worked hours", T.Number, "Time"),
            new("lateMinutes", "Late minutes", T.Number, "Time"),
            new("earlyLeaveMinutes", "Early leave minutes", T.Number, "Time"),
            new("overtimeHours", "Overtime hours", T.Number, "Time"),
            new("late", "Was late", T.Bool, "Time"),
            new("edited", "Edited by HR", T.Bool, "Day"),
        ]), [Permissions.EmployeesView]),

        new(new("leave-requests", "Leave requests", "Leave requests that start in the period.", true,
        [
            .. PersonFields,
            new("leaveType", "Leave type", T.Text, "Leave"),
            new("paid", "Paid leave", T.Bool, "Leave"),
            new("startDate", "Start date", T.Date, "Leave"),
            new("endDate", "End date", T.Date, "Leave"),
            new("month", "Start month", T.Text, "Leave"),
            new("days", "Days", T.Number, "Leave"),
            new("halfDay", "Half day", T.Bool, "Leave"),
            new("status", "Status", T.Text, "Leave"),
            new("reason", "Reason", T.Text, "Leave"),
        ]), [Permissions.LeavesViewAll, Permissions.LeavesApprove]),

        new(new("leave-balances", "Leave balances", "Balance per employee and leave type for a year.", false,
        [
            .. PersonFields,
            new("leaveType", "Leave type", T.Text, "Leave"),
            new("year", "Year", T.Number, "Leave", NoTotal: true),
            new("entitled", "Entitled", T.Number, "Balance"),
            new("carried", "Carried forward", T.Number, "Balance"),
            new("adjusted", "Adjusted", T.Number, "Balance"),
            new("used", "Used", T.Number, "Balance"),
            new("pending", "Pending", T.Number, "Balance"),
            new("available", "Available", T.Number, "Balance"),
        ]), [Permissions.LeavesViewAll, Permissions.LeavesApprove]),

        new(new("documents", "Employee documents", "Contracts, IDs, passports and visas with their expiry.", false,
        [
            .. PersonFields,
            new("status", "Employee status", T.Text, "Employee"),
            new("documentType", "Document type", T.Text, "Document"),
            new("title", "Title", T.Text, "Document"),
            new("issueDate", "Issue date", T.Date, "Document"),
            new("expiryDate", "Expiry date", T.Date, "Document"),
            new("daysToExpiry", "Days to expiry", T.Number, "Document", NoTotal: true),
            new("expired", "Expired", T.Bool, "Document"),
        ]), [Permissions.EmployeesView]),

        new(new("assets", "Assets", "Company assets and who holds them now.", false,
        [
            new("tag", "Asset tag", T.Text, "Asset"),
            new("name", "Asset", T.Text, "Asset"),
            new("category", "Category", T.Text, "Asset"),
            new("brand", "Brand", T.Text, "Asset"),
            new("model", "Model", T.Text, "Asset"),
            new("serial", "Serial number", T.Text, "Asset"),
            new("status", "Status", T.Text, "Asset"),
            new("condition", "Condition", T.Text, "Asset"),
            new("location", "Location", T.Text, "Asset"),
            new("purchaseDate", "Purchase date", T.Date, "Purchase"),
            new("purchaseCost", "Purchase cost", T.Money, "Purchase"),
            new("vendor", "Vendor", T.Text, "Purchase"),
            new("warrantyUntil", "Warranty until", T.Date, "Purchase"),
            new("holder", "Held by", T.Text, "Holder"),
            new("holderDepartment", "Holder's department", T.Text, "Holder"),
            new("assignedOn", "Assigned on", T.Date, "Holder"),
            new("dueBack", "Due back", T.Date, "Holder"),
        ]), [Permissions.EmployeesView]),
    ];

    public static IReadOnlyList<ReportSourceDef> Definitions => All.Select(s => s.Def).ToList();

    /// <summary>Sirf woh sources jin ka permission user ke paas hai.</summary>
    public static IReadOnlyList<ReportSourceDef> Visible(Func<string, bool> has)
        => All.Where(s => s.AnyPermission.Any(has)).Select(s => s.Def).ToList();

    /// <summary>Source dhundo aur permission jaancho. Na mile ya ijazat na ho to null.</summary>
    public static ReportSourceDef? Find(string key, Func<string, bool> has)
        => All.FirstOrDefault(s => s.Def.Key == key && s.AnyPermission.Any(has))?.Def;

    public static bool Exists(string key) => All.Any(s => s.Def.Key == key);

    public async Task<List<Row>> LoadAsync(string key, DateOnly? from, DateOnly? to, DateOnly today, CancellationToken ct)
    {
        var source = All.First(s => s.Def.Key == key).Def;
        if (source.NeedsRange)
        {
            if (from is null || to is null)
                throw new ReportSpecException($"{source.Label} needs a from and a to date.");
            if (to < from)
                throw new ReportSpecException("The to date cannot be before the from date.");
            if (to.Value.DayNumber - from.Value.DayNumber + 1 > MaxRangeDays)
                throw new ReportSpecException($"Pick a period of at most {MaxRangeDays} days.");
        }

        var rows = key switch
        {
            "employees" => await EmployeesAsync(today, ct),
            "attendance" => await AttendanceAsync(from!.Value, to!.Value, ct),
            "leave-requests" => await LeaveRequestsAsync(from!.Value, to!.Value, ct),
            "leave-balances" => await LeaveBalancesAsync((short)(from?.Year ?? today.Year), ct),
            "documents" => await DocumentsAsync(today, ct),
            "assets" => await AssetsAsync(ct),
            _ => throw new ReportSpecException($"Unknown source '{key}'."),
        };
        if (rows.Count > MaxLoad)
            throw new ReportSpecException($"This report has more than {MaxLoad:N0} rows. Pick a shorter period.");
        return rows;
    }

    private sealed record Person(Guid Id, string Code, string Name, string Department, string Designation, string Location, EmploymentStatus Status);

    private async Task<Dictionary<Guid, Person>> PeopleAsync(CancellationToken ct)
        => await db.Employees.AsNoTracking()
            .Select(e => new Person(e.Id, e.EmployeeCode, e.FirstName + " " + e.LastName, e.Department.Name, e.Designation.Title,
                e.Location.Name, e.EmploymentStatus))
            .ToDictionaryAsync(p => p.Id, ct);

    private static Row PersonRow(Person? p) => new()
    {
        ["code"] = p?.Code,
        ["employee"] = p?.Name,
        ["department"] = p?.Department,
        ["designation"] = p?.Designation,
        ["location"] = p?.Location,
    };

    private async Task<List<Row>> EmployeesAsync(DateOnly today, CancellationToken ct)
    {
        var list = await db.Employees.AsNoTracking().Select(e => new
        {
            e.EmployeeCode, e.FirstName, e.LastName, e.Gender, e.DateOfBirth, e.MaritalStatus, e.NationalityCode, e.WorkEmail, e.WorkPhone,
            Department = e.Department.Name, Designation = e.Designation.Title, e.Designation.Level, Location = e.Location.Name,
            Manager = e.Manager == null ? null : e.Manager.FirstName + " " + e.Manager.LastName,
            e.EmploymentType, e.EmploymentStatus, e.JoiningDate, e.ProbationEndDate, e.ConfirmationDate, e.NoticePeriodDays,
            e.ExitDate, e.ExitReason,
        }).ToListAsync(ct);

        return list.Select(e => new Row
        {
            ["code"] = e.EmployeeCode,
            ["employee"] = $"{e.FirstName} {e.LastName}",
            ["firstName"] = e.FirstName,
            ["lastName"] = e.LastName,
            ["gender"] = Words(e.Gender),
            ["dateOfBirth"] = e.DateOfBirth,
            ["age"] = e.DateOfBirth is { } dob ? (decimal?)Years(dob, today) : null,
            ["maritalStatus"] = Words(e.MaritalStatus),
            ["nationality"] = e.NationalityCode,
            ["workEmail"] = e.WorkEmail,
            ["workPhone"] = e.WorkPhone,
            ["department"] = e.Department,
            ["designation"] = e.Designation,
            ["level"] = e.Level is { } l ? (decimal?)l : null,
            ["location"] = e.Location,
            ["manager"] = e.Manager,
            ["employmentType"] = Words(e.EmploymentType),
            ["status"] = Words(e.EmploymentStatus),
            ["joiningDate"] = e.JoiningDate,
            ["serviceYears"] = Math.Round((decimal)((e.ExitDate ?? today).DayNumber - e.JoiningDate.DayNumber) / 365.25m, 1),
            ["probationEnd"] = e.ProbationEndDate,
            ["confirmationDate"] = e.ConfirmationDate,
            ["noticeDays"] = e.NoticePeriodDays is { } n ? (decimal?)n : null,
            ["exitDate"] = e.ExitDate,
            ["exitReason"] = e.ExitReason,
        }).ToList();
    }

    private async Task<List<Row>> AttendanceAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var people = await PeopleAsync(ct);
        var days = await db.AttendanceDays.AsNoTracking().Where(d => d.WorkDate >= from && d.WorkDate <= to)
            .Select(d => new { d.EmployeeId, d.WorkDate, d.DayType, d.Status, d.WorkedMinutes, d.LateMinutes, d.EarlyLeaveMinutes, d.OvertimeMinutes, d.IsManuallyEdited })
            .Take(MaxLoad + 1).ToListAsync(ct);
        return days.Select(d =>
        {
            var row = PersonRow(people.GetValueOrDefault(d.EmployeeId));
            row["date"] = d.WorkDate;
            row["month"] = d.WorkDate.ToString("yyyy-MM");
            row["dayType"] = Words(d.DayType);
            row["status"] = Words(d.Status);
            row["workedHours"] = Math.Round(d.WorkedMinutes / 60m, 2);
            row["lateMinutes"] = (decimal)d.LateMinutes;
            row["earlyLeaveMinutes"] = (decimal)d.EarlyLeaveMinutes;
            row["overtimeHours"] = Math.Round(d.OvertimeMinutes / 60m, 2);
            row["late"] = d.LateMinutes > 0;
            row["edited"] = d.IsManuallyEdited;
            return row;
        }).ToList();
    }

    private async Task<List<Row>> LeaveRequestsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var people = await PeopleAsync(ct);
        var types = await db.LeaveTypes.AsNoTracking().IgnoreQueryFilters().Select(t => new { t.Id, t.Name, t.IsPaid }).ToDictionaryAsync(t => t.Id, ct);
        var requests = await db.LeaveRequests.AsNoTracking().Where(r => r.StartDate >= from && r.StartDate <= to)
            .Select(r => new { r.EmployeeId, r.LeaveTypeId, r.StartDate, r.EndDate, r.TotalDays, r.IsHalfDay, r.Status, r.Reason })
            .Take(MaxLoad + 1).ToListAsync(ct);
        return requests.Select(r =>
        {
            var row = PersonRow(people.GetValueOrDefault(r.EmployeeId));
            var type = types.GetValueOrDefault(r.LeaveTypeId);
            row["leaveType"] = type?.Name;
            row["paid"] = type?.IsPaid;
            row["startDate"] = r.StartDate;
            row["endDate"] = r.EndDate;
            row["month"] = r.StartDate.ToString("yyyy-MM");
            row["days"] = r.TotalDays;
            row["halfDay"] = r.IsHalfDay;
            row["status"] = Words(r.Status);
            row["reason"] = r.Reason;
            return row;
        }).ToList();
    }

    private async Task<List<Row>> LeaveBalancesAsync(short year, CancellationToken ct)
    {
        var people = await PeopleAsync(ct);
        var types = await db.LeaveTypes.AsNoTracking().IgnoreQueryFilters().Select(t => new { t.Id, t.Name }).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var balances = await db.LeaveBalances.AsNoTracking().Where(b => b.LeaveYear == year)
            .Select(b => new { b.EmployeeId, b.LeaveTypeId, b.LeaveYear, b.Entitled, b.CarriedForward, b.Adjusted, b.Used, b.Pending })
            .Take(MaxLoad + 1).ToListAsync(ct);
        return balances.Where(b => people.ContainsKey(b.EmployeeId)).Select(b =>
        {
            var row = PersonRow(people[b.EmployeeId]);
            row["leaveType"] = types.GetValueOrDefault(b.LeaveTypeId);
            row["year"] = (decimal)b.LeaveYear;
            row["entitled"] = b.Entitled;
            row["carried"] = b.CarriedForward;
            row["adjusted"] = b.Adjusted;
            row["used"] = b.Used;
            row["pending"] = b.Pending;
            row["available"] = b.Entitled + b.CarriedForward + b.Adjusted - b.Used - b.Pending;
            return row;
        }).ToList();
    }

    private async Task<List<Row>> DocumentsAsync(DateOnly today, CancellationToken ct)
    {
        var people = await PeopleAsync(ct);
        var docs = await db.EmployeeDocuments.AsNoTracking()
            .Select(d => new { d.EmployeeId, d.DocumentType, d.Title, d.IssueDate, d.ExpiryDate })
            .Take(MaxLoad + 1).ToListAsync(ct);
        return docs.Where(d => people.ContainsKey(d.EmployeeId)).Select(d =>
        {
            var p = people[d.EmployeeId];
            var row = PersonRow(p);
            row["status"] = Words(p.Status);
            row["documentType"] = Words(d.DocumentType);
            row["title"] = d.Title;
            row["issueDate"] = d.IssueDate;
            row["expiryDate"] = d.ExpiryDate;
            row["daysToExpiry"] = d.ExpiryDate is { } x ? (decimal?)(x.DayNumber - today.DayNumber) : null;
            row["expired"] = d.ExpiryDate is { } y ? y < today : (bool?)null;
            return row;
        }).ToList();
    }

    private async Task<List<Row>> AssetsAsync(CancellationToken ct)
    {
        var people = await PeopleAsync(ct);
        var categories = await db.AssetCategories.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var locations = await db.Locations.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var assets = await db.Assets.AsNoTracking()
            .Select(a => new
            {
                a.AssetTag, a.Name, a.AssetCategoryId, a.Brand, a.Model, a.SerialNumber, a.Status, a.Condition, a.LocationId,
                a.PurchaseDate, a.PurchaseCost, a.Vendor, a.WarrantyUntil,
                Holder = a.Assignments.Where(x => x.ReturnedOn == null).Select(x => new { x.EmployeeId, x.AssignedOn, x.DueBack }).FirstOrDefault(),
            })
            .Take(MaxLoad + 1).ToListAsync(ct);
        return assets.Select(a =>
        {
            var holder = a.Holder is null ? null : people.GetValueOrDefault(a.Holder.EmployeeId);
            return new Row
            {
                ["tag"] = a.AssetTag,
                ["name"] = a.Name,
                ["category"] = categories.GetValueOrDefault(a.AssetCategoryId),
                ["brand"] = a.Brand,
                ["model"] = a.Model,
                ["serial"] = a.SerialNumber,
                ["status"] = Words(a.Status),
                ["condition"] = Words(a.Condition),
                ["location"] = a.LocationId is { } l ? locations.GetValueOrDefault(l) : null,
                ["purchaseDate"] = a.PurchaseDate,
                ["purchaseCost"] = a.PurchaseCost,
                ["vendor"] = a.Vendor,
                ["warrantyUntil"] = a.WarrantyUntil,
                ["holder"] = holder?.Name,
                ["holderDepartment"] = holder?.Department,
                ["assignedOn"] = a.Holder?.AssignedOn,
                ["dueBack"] = a.Holder?.DueBack,
            };
        }).ToList();
    }

    private static decimal Years(DateOnly from, DateOnly to)
    {
        var y = to.Year - from.Year;
        if (to < from.AddYears(y)) y--;
        return y;
    }

    /// <summary>"OnNotice" → "On notice", "FullTime, Contract" → "Full time, Contract".</summary>
    public static string? Words<TEnum>(TEnum? value) where TEnum : struct, Enum
        => value is null ? null : Regex.Replace(value.Value.ToString(), "(?<=[a-z])(?=[A-Z])", m => " ").Replace(" ,", ",") is var s
            ? string.Join(", ", s.Split(", ").Select(p => p.Length > 1 ? p[0] + p[1..].ToLowerInvariant() : p))
            : null;

    public static string? Words<TEnum>(TEnum value) where TEnum : struct, Enum => Words((TEnum?)value);
}
