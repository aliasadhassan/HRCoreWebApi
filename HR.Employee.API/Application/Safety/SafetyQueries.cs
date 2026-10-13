namespace HR.Employee.API.Application.Safety;

using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Safety;
using MediatR;
using Microsoft.EntityFrameworkCore;

#region DTOs

public sealed record SafetyMonthDto(string Month, int Incidents, int NearMisses);

public sealed record SafetyCountDto(byte Key, int Count);

public sealed record SafetySummaryDto(
    int? DaysSinceLostTime, DateTime? LastLostTimeAt,
    int OpenIncidents, int ReportedLast30, int LostTimeYtd, int LostDaysYtd, int RecordableYtd, int NearMissYtd, int ReportablePending,
    int OpenActions, int OverdueActions, int ActionsDueSoon, int ActionsToVerify,
    int InspectionsDue, int OverdueInspections, decimal? AverageScore90,
    IReadOnlyList<SafetyMonthDto> ByMonth, IReadOnlyList<SafetyCountDto> ByType,
    int MyOpenActions, int MyOverdueActions, int MyInspectionsDue, int MyReportsOpen, int MyInvestigations,
    bool CanManage, bool HasChecklists, Guid? MyEmployeeId);

public sealed record SafetyOptionDto(Guid Id, string Name, string? Extra);

public sealed record SafetyChecklistOptionDto(Guid Id, string Name, int ItemCount, bool IsActive);

public sealed record SafetyLookupsDto(
    IReadOnlyList<SafetyOptionDto> People, IReadOnlyList<SafetyOptionDto> Departments, IReadOnlyList<SafetyOptionDto> Locations,
    IReadOnlyList<SafetyChecklistOptionDto> Checklists);

public sealed record IncidentListItemDto(
    Guid Id, string Code, string Title, IncidentType Type, SafetySeverity Severity, IncidentStatus Status, DateTime OccurredAt,
    string? LocationName, string? Area, string? DepartmentName, string? ReporterName, bool Anonymous, string? AffectedName,
    IncidentTreatment? Treatment, short LostDays, bool IsReportable, DateOnly? AuthorityNotifiedOn,
    Guid? InvestigatorEmployeeId, string? InvestigatorName, int OpenActions, int OverdueActions, DateTime? ClosedAt,
    DateTime LastActivityAt, bool IsMine);

public sealed record IncidentEntryDto(Guid Id, IncidentEntryKind Kind, string? Text, string? ByName, bool ByMe, DateTime At);

public sealed record SafetyActionDto(
    Guid Id, string Title, string? Description, Guid? IncidentId, string? IncidentCode, Guid? InspectionId, string? InspectionCode,
    Guid AssigneeEmployeeId, string AssigneeName, SafetySeverity Priority, DateOnly DueOn, int DaysLeft, bool IsOverdue,
    ActionStatus Status, DateTime? CompletedAt, string? CompletionNote, DateTime? VerifiedAt, DateTime CreatedAt,
    bool IsMine, bool CanUpdate, bool CanProgress, bool CanVerify, uint RowVersion);

public sealed record IncidentDetailDto(
    IncidentListItemDto Incident, string? Description, Guid? LocationId, Guid? DepartmentId, Guid? AffectedEmployeeId,
    string? InjuryNature, string? BodyPart, string? Witnesses, string? ImmediateAction,
    RootCauseCategory? RootCauseCategory, string? RootCause, string? AuthorityReference, string? ClosureNote, DateTime ReportedAt,
    IReadOnlyList<IncidentEntryDto> Entries, IReadOnlyList<SafetyActionDto> Actions,
    bool CanEdit, bool CanWork, bool CanClose, bool CanReopen, uint RowVersion);

public sealed record MyReportDto(
    Guid Id, string Code, string Title, IncidentType Type, IncidentStatus Status, DateTime OccurredAt, DateTime ReportedAt,
    bool Anonymous, DateTime? ClosedAt, DateTime LastActivityAt);

public sealed record MyReportUpdateDto(Guid Id, IncidentEntryKind Kind, string? Text, DateTime At);

public sealed record MyReportDetailDto(
    MyReportDto Report, string? Description, string? LocationName, string? Area, string? ClosureNote, IReadOnlyList<MyReportUpdateDto> Updates);

public sealed record SafetyChecklistDto(
    Guid Id, string Name, string? Description, bool IsActive, IReadOnlyList<string> Items, int Inspections, uint RowVersion);

public sealed record InspectionListItemDto(
    Guid Id, string Code, string Title, Guid? ChecklistId, string? ChecklistName, string? LocationName, string? Area,
    Guid InspectorEmployeeId, string InspectorName, DateOnly DueOn, int DaysLeft, bool IsOverdue, short? RepeatDays,
    InspectionStatus Status, DateTime? CompletedAt, decimal? ScorePercent, int ItemCount, int Failed, int OpenActions, bool IsMine);

public sealed record InspectionLineDto(Guid Id, short SortOrder, string Text, InspectionResult? Result, string? Note);

public sealed record InspectionDetailDto(
    InspectionListItemDto Inspection, Guid? LocationId, string? Notes, IReadOnlyList<InspectionLineDto> Lines,
    IReadOnlyList<SafetyActionDto> Actions, bool CanComplete, bool CanEdit, uint RowVersion);

#endregion

#region Requests

public sealed record GetSafetySummaryQuery : IRequest<SafetySummaryDto>;
public sealed record GetSafetyLookupsQuery : IRequest<SafetyLookupsDto>;
public sealed record GetIncidentsQuery(
    string? Scope, IncidentStatus? Status, IncidentType? Type, SafetySeverity? Severity, DateOnly? From, DateOnly? To, string? Search)
    : IRequest<IReadOnlyList<IncidentListItemDto>>;
public sealed record GetIncidentQuery(Guid Id) : IRequest<IncidentDetailDto>;
public sealed record GetMyReportsQuery : IRequest<IReadOnlyList<MyReportDto>>;
public sealed record GetMyReportQuery(Guid Id) : IRequest<MyReportDetailDto>;
public sealed record GetSafetyActionsQuery(string? Scope, ActionStatus? Status, string? Search) : IRequest<IReadOnlyList<SafetyActionDto>>;
public sealed record GetChecklistsQuery : IRequest<IReadOnlyList<SafetyChecklistDto>>;
public sealed record GetInspectionsQuery(string? Scope, InspectionStatus? Status) : IRequest<IReadOnlyList<InspectionListItemDto>>;
public sealed record GetInspectionQuery(Guid Id) : IRequest<InspectionDetailDto>;

#endregion

public sealed class SafetyQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetSafetySummaryQuery, SafetySummaryDto>,
    IRequestHandler<GetSafetyLookupsQuery, SafetyLookupsDto>,
    IRequestHandler<GetIncidentsQuery, IReadOnlyList<IncidentListItemDto>>,
    IRequestHandler<GetIncidentQuery, IncidentDetailDto>,
    IRequestHandler<GetMyReportsQuery, IReadOnlyList<MyReportDto>>,
    IRequestHandler<GetMyReportQuery, MyReportDetailDto>,
    IRequestHandler<GetSafetyActionsQuery, IReadOnlyList<SafetyActionDto>>,
    IRequestHandler<GetChecklistsQuery, IReadOnlyList<SafetyChecklistDto>>,
    IRequestHandler<GetInspectionsQuery, IReadOnlyList<InspectionListItemDto>>,
    IRequestHandler<GetInspectionQuery, InspectionDetailDto>
{
    private readonly SafetyAccess access = new(db, currentUser);

    // ───── Summary ─────

    public async Task<SafetySummaryDto> Handle(GetSafetySummaryQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var canManage = access.CanManage;
        var today = SafetyClock.Today;
        var now = DateTime.UtcNow;
        var yearStart = new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthsFrom = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

        // Lost-time ka din sab ko dikhta hai (notice board wala number)
        var lastLti = await db.SafetyIncidents.AsNoTracking()
            .Where(i => i.LostDays > 0 || i.Treatment == IncidentTreatment.Fatal)
            .OrderByDescending(i => i.OccurredAt).Select(i => (DateTime?)i.OccurredAt).FirstOrDefaultAsync(ct);
        int? daysSince = lastLti is { } at ? Math.Max(0, today.DayNumber - DateOnly.FromDateTime(at).DayNumber) : null;

        int myOpen = 0, myOverdue = 0, myInspections = 0, myReports = 0, myInvestigations = 0;
        if (my is { } me)
        {
            var mine = await db.SafetyActions.AsNoTracking()
                .Where(a => a.AssigneeEmployeeId == me && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress))
                .Select(a => a.DueOn).ToListAsync(ct);
            myOpen = mine.Count;
            myOverdue = mine.Count(d => d < today);
            myInspections = await db.SafetyInspections.CountAsync(i => i.InspectorEmployeeId == me && i.Status == InspectionStatus.Scheduled
                                                                       && i.DueOn <= today.AddDays(7), ct);
            myReports = await db.SafetyIncidents.CountAsync(i => i.ReporterEmployeeId == me && i.Status != IncidentStatus.Closed, ct);
            myInvestigations = await db.SafetyIncidents.CountAsync(i => i.InvestigatorEmployeeId == me && i.Status != IncidentStatus.Closed, ct);
        }

        var hasChecklists = await db.SafetyChecklists.AnyAsync(ct);
        if (!canManage)
            return new SafetySummaryDto(daysSince, lastLti, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, [], [],
                myOpen, myOverdue, myInspections, myReports, myInvestigations, false, hasChecklists, my);

        var recent = await db.SafetyIncidents.AsNoTracking()
            .Where(i => i.OccurredAt >= (monthsFrom < yearStart ? monthsFrom : yearStart) || i.Status != IncidentStatus.Closed)
            .Select(i => new { i.Type, i.Status, i.OccurredAt, i.CreatedAt, i.LostDays, i.Treatment, i.IsReportable, i.AuthorityNotifiedOn })
            .ToListAsync(ct);
        var ytd = recent.Where(i => i.OccurredAt >= yearStart).ToList();
        var nearTypes = new[] { IncidentType.NearMiss, IncidentType.Hazard };

        var byMonth = Enumerable.Range(0, 12).Select(n =>
        {
            var from = monthsFrom.AddMonths(n);
            var to = from.AddMonths(1);
            var inMonth = recent.Where(i => i.OccurredAt >= from && i.OccurredAt < to).ToList();
            return new SafetyMonthDto(from.ToString("yyyy-MM"), inMonth.Count(i => !nearTypes.Contains(i.Type)), inMonth.Count(i => nearTypes.Contains(i.Type)));
        }).ToList();

        var actions = await db.SafetyActions.AsNoTracking()
            .Where(a => a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress || a.Status == ActionStatus.Done)
            .Select(a => new { a.Status, a.DueOn }).ToListAsync(ct);
        var openActions = actions.Where(a => a.Status != ActionStatus.Done).ToList();

        var inspections = await db.SafetyInspections.AsNoTracking()
            .Where(i => i.Status == InspectionStatus.Scheduled && i.DueOn <= today.AddDays(7))
            .Select(i => i.DueOn).ToListAsync(ct);
        var since90 = now.AddDays(-90);
        var avgScore = await db.SafetyInspections.AsNoTracking()
            .Where(i => i.Status == InspectionStatus.Completed && i.CompletedAt >= since90 && i.ScorePercent != null)
            .AverageAsync(i => i.ScorePercent, ct);

        return new SafetySummaryDto(
            daysSince, lastLti,
            recent.Count(i => i.Status != IncidentStatus.Closed),
            recent.Count(i => i.CreatedAt >= now.AddDays(-30)),
            ytd.Count(i => i.LostDays > 0 || i.Treatment == IncidentTreatment.Fatal),
            ytd.Sum(i => i.LostDays),
            ytd.Count(i => i.Treatment >= IncidentTreatment.MedicalTreatment || i.LostDays > 0),
            ytd.Count(i => nearTypes.Contains(i.Type)),
            recent.Count(i => i.IsReportable && i.AuthorityNotifiedOn == null),
            openActions.Count, openActions.Count(a => a.DueOn < today), openActions.Count(a => a.DueOn >= today && a.DueOn <= today.AddDays(7)),
            actions.Count(a => a.Status == ActionStatus.Done),
            inspections.Count, inspections.Count(d => d < today), avgScore is { } s ? Math.Round(s, 1) : null,
            byMonth,
            ytd.GroupBy(i => i.Type).OrderBy(g => g.Key).Select(g => new SafetyCountDto((byte)g.Key, g.Count())).ToList(),
            myOpen, myOverdue, myInspections, myReports, myInvestigations, true, hasChecklists, my);
    }

    public async Task<SafetyLookupsDto> Handle(GetSafetyLookupsQuery q, CancellationToken ct)
    {
        var people = await db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus != EmploymentStatus.Exited)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new SafetyOptionDto(e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode + " · " + e.Department.Name))
            .ToListAsync(ct);
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name)
            .Select(d => new SafetyOptionDto(d.Id, d.Name, null)).ToListAsync(ct);
        var locations = await db.Locations.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.Name)
            .Select(l => new SafetyOptionDto(l.Id, l.Name, l.City)).ToListAsync(ct);
        var checklists = await db.SafetyChecklists.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new SafetyChecklistOptionDto(c.Id, c.Name, c.Items.Count, c.IsActive)).ToListAsync(ct);
        return new SafetyLookupsDto(people, departments, locations, checklists);
    }

    // ───── Incidents (safety team + investigators) ─────

    public async Task<IReadOnlyList<IncidentListItemDto>> Handle(GetIncidentsQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var scope = (q.Scope ?? "open").ToLowerInvariant();
        IQueryable<SafetyIncident> list = db.SafetyIncidents.AsNoTracking();
        if (!access.CanManage || scope == "mine")
        {
            if (my is null)
                return [];
            list = list.Where(i => i.InvestigatorEmployeeId == my);
        }
        if (scope == "open")
            list = list.Where(i => i.Status != IncidentStatus.Closed);
        if (scope == "reportable")
            list = list.Where(i => i.IsReportable);
        if (q.Status is { } st)
            list = list.Where(i => i.Status == st);
        if (q.Type is { } type)
            list = list.Where(i => i.Type == type);
        if (q.Severity is { } sev)
            list = list.Where(i => i.Severity == sev);
        if (q.From is { } from)
        {
            var f = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            list = list.Where(i => i.OccurredAt >= f);
        }
        if (q.To is { } to)
        {
            var t = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            list = list.Where(i => i.OccurredAt < t);
        }

        var rows = await IncidentRowsAsync(list.OrderByDescending(i => i.OccurredAt).Take(500), my, ct);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            rows = rows.Where(r => r.Code.Contains(s, StringComparison.OrdinalIgnoreCase)
                || r.Title.Contains(s, StringComparison.OrdinalIgnoreCase)
                || (r.Area?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.LocationName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.AffectedName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.ReporterName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }
        return rows;
    }

    public async Task<IncidentDetailDto> Handle(GetIncidentQuery q, CancellationToken ct)
    {
        var i = await db.SafetyIncidents.AsNoTracking().Include(x => x.Entries).AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Incident", q.Id);
        if (!await access.CanSeeIncidentAsync(i, ct))
            throw new NotFoundException("Incident", q.Id);
        var my = await access.MeAsync(ct);
        var row = (await IncidentRowsAsync(db.SafetyIncidents.AsNoTracking().Where(x => x.Id == i.Id), my, ct))[0];

        var reporterUser = i.Anonymous && i.ReporterEmployeeId is { } rid
            ? await db.Employees.AsNoTracking().Where(e => e.Id == rid).Select(e => e.UserId).FirstOrDefaultAsync(ct)
            : null;
        var names = await UserNamesAsync(i.Entries.Select(e => e.ByUserId), ct);
        var entries = i.Entries.OrderBy(e => e.At).ThenBy(e => e.Id)
            .Select(e => new IncidentEntryDto(e.Id, e.Kind, e.Text,
                reporterUser != null && e.ByUserId == reporterUser ? null : e.ByUserId is { } u ? names.GetValueOrDefault(u) : null,
                access.UserId != null && e.ByUserId == access.UserId, e.At))
            .ToList();
        var actions = await ActionRowsAsync(db.SafetyActions.AsNoTracking().Where(a => a.IncidentId == i.Id), my, ct);
        var canManage = access.CanManage;
        return new IncidentDetailDto(
            row, i.Description, i.LocationId, i.DepartmentId, i.AffectedEmployeeId, i.InjuryNature, i.BodyPart, i.Witnesses, i.ImmediateAction,
            i.RootCauseCategory, i.RootCause, i.AuthorityReference, i.ClosureNote, i.CreatedAt, entries, actions,
            CanEdit: canManage && !i.IsClosed, CanWork: !i.IsClosed, CanClose: canManage && !i.IsClosed, CanReopen: canManage && i.IsClosed,
            i.RowVersion);
    }

    // ───── My reports (jo maine report kiye) ─────

    public async Task<IReadOnlyList<MyReportDto>> Handle(GetMyReportsQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        if (my is null)
            return [];
        return await db.SafetyIncidents.AsNoTracking().Where(i => i.ReporterEmployeeId == my)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new MyReportDto(i.Id, i.Code, i.Title, i.Type, i.Status, i.OccurredAt, i.CreatedAt, i.Anonymous, i.ClosedAt, i.LastActivityAt))
            .ToListAsync(ct);
    }

    /// <summary>Reporter ko status ki tabdeeli aur band karne ka note; team ke notes aur naam nahi.</summary>
    public async Task<MyReportDetailDto> Handle(GetMyReportQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var i = await db.SafetyIncidents.AsNoTracking().Include(x => x.Entries)
                    .FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (i is null || my is null || i.ReporterEmployeeId != my)
            throw new NotFoundException("Report", q.Id);
        var location = i.LocationId is { } lid ? await db.Locations.AsNoTracking().Where(l => l.Id == lid).Select(l => l.Name).FirstOrDefaultAsync(ct) : null;
        var shown = new[] { IncidentEntryKind.Created, IncidentEntryKind.StatusChanged, IncidentEntryKind.InvestigatorChanged, IncidentEntryKind.Closed, IncidentEntryKind.Reopened };
        var updates = i.Entries.Where(e => shown.Contains(e.Kind)).OrderBy(e => e.At).ThenBy(e => e.Id)
            .Select(e => new MyReportUpdateDto(e.Id, e.Kind,
                e.Kind == IncidentEntryKind.StatusChanged ? e.Text?.Split(" · ")[0] : e.Kind == IncidentEntryKind.Closed ? e.Text : null, e.At))
            .ToList();
        return new MyReportDetailDto(
            new MyReportDto(i.Id, i.Code, i.Title, i.Type, i.Status, i.OccurredAt, i.CreatedAt, i.Anonymous, i.ClosedAt, i.LastActivityAt),
            i.Description, location, i.Area, i.ClosureNote, updates);
    }

    // ───── Actions ─────

    public async Task<IReadOnlyList<SafetyActionDto>> Handle(GetSafetyActionsQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var scope = (q.Scope ?? "open").ToLowerInvariant();
        var today = SafetyClock.Today;
        IQueryable<SafetyAction> list = db.SafetyActions.AsNoTracking();
        if (!access.CanManage || scope == "mine")
        {
            if (my is null)
                return [];
            list = list.Where(a => a.AssigneeEmployeeId == my);
        }
        list = scope switch
        {
            "open" or "mine" => q.Status is null ? list.Where(a => a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress) : list,
            "overdue" => list.Where(a => (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress) && a.DueOn < today),
            "verify" => list.Where(a => a.Status == ActionStatus.Done),
            _ => list
        };
        if (q.Status is { } st)
            list = list.Where(a => a.Status == st);
        var rows = await ActionRowsAsync(list.OrderBy(a => a.DueOn).Take(500), my, ct);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            rows = rows.Where(r => r.Title.Contains(s, StringComparison.OrdinalIgnoreCase)
                || r.AssigneeName.Contains(s, StringComparison.OrdinalIgnoreCase)
                || (r.IncidentCode?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.InspectionCode?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }
        return rows;
    }

    // ───── Checklists + inspections ─────

    public async Task<IReadOnlyList<SafetyChecklistDto>> Handle(GetChecklistsQuery q, CancellationToken ct)
    {
        access.EnsureCanManage();
        var lists = await db.SafetyChecklists.AsNoTracking().Include(c => c.Items).OrderBy(c => c.Name).ToListAsync(ct);
        var counts = await db.SafetyInspections.AsNoTracking().Where(i => i.ChecklistId != null)
            .GroupBy(i => i.ChecklistId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return lists.Select(c => new SafetyChecklistDto(c.Id, c.Name, c.Description, c.IsActive,
            c.Items.OrderBy(i => i.SortOrder).Select(i => i.Text).ToList(), counts.GetValueOrDefault(c.Id), c.RowVersion)).ToList();
    }

    public async Task<IReadOnlyList<InspectionListItemDto>> Handle(GetInspectionsQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var scope = (q.Scope ?? "upcoming").ToLowerInvariant();
        var today = SafetyClock.Today;
        IQueryable<SafetyInspection> list = db.SafetyInspections.AsNoTracking();
        if (!access.CanManage || scope == "mine")
        {
            if (my is null)
                return [];
            list = list.Where(i => i.InspectorEmployeeId == my);
        }
        list = scope switch
        {
            "upcoming" or "mine" => q.Status is null ? list.Where(i => i.Status == InspectionStatus.Scheduled) : list,
            "overdue" => list.Where(i => i.Status == InspectionStatus.Scheduled && i.DueOn < today),
            "completed" => list.Where(i => i.Status == InspectionStatus.Completed),
            _ => list
        };
        if (q.Status is { } st)
            list = list.Where(i => i.Status == st);
        var ordered = scope == "completed" ? list.OrderByDescending(i => i.CompletedAt) : list.OrderBy(i => i.DueOn);
        return await InspectionRowsAsync(ordered.Take(500), my, ct);
    }

    public async Task<InspectionDetailDto> Handle(GetInspectionQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var i = await db.SafetyInspections.AsNoTracking().Include(x => x.Lines)
                    .FirstOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Inspection", q.Id);
        var mine = my is not null && i.InspectorEmployeeId == my;
        if (!access.CanManage && !mine)
            throw new NotFoundException("Inspection", q.Id);
        var row = (await InspectionRowsAsync(db.SafetyInspections.AsNoTracking().Where(x => x.Id == i.Id), my, ct))[0];
        var lines = i.Lines.OrderBy(l => l.SortOrder).Select(l => new InspectionLineDto(l.Id, l.SortOrder, l.Text, l.Result, l.Note)).ToList();
        var actions = await ActionRowsAsync(db.SafetyActions.AsNoTracking().Where(a => a.InspectionId == i.Id), my, ct);
        var scheduled = i.Status == InspectionStatus.Scheduled;
        return new InspectionDetailDto(row, i.LocationId, i.Notes, lines, actions,
            CanComplete: scheduled && (mine || access.CanManage), CanEdit: scheduled && access.CanManage, i.RowVersion);
    }

    // ───── Projections ─────

    private async Task<List<IncidentListItemDto>> IncidentRowsAsync(IQueryable<SafetyIncident> q, Guid? my, CancellationToken ct)
    {
        var today = SafetyClock.Today;
        var raw = await q.Select(i => new
        {
            i.Id, i.Code, i.Title, i.Type, i.Severity, i.Status, i.OccurredAt, i.Area, i.Anonymous, i.Treatment, i.LostDays, i.IsReportable,
            i.AuthorityNotifiedOn, i.InvestigatorEmployeeId, i.ClosedAt, i.LastActivityAt,
            LocationName = db.Locations.Where(l => l.Id == i.LocationId).Select(l => l.Name).FirstOrDefault(),
            DepartmentName = db.Departments.Where(d => d.Id == i.DepartmentId).Select(d => d.Name).FirstOrDefault(),
            ReporterName = i.Anonymous ? null : db.Employees.Where(e => e.Id == i.ReporterEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
            AffectedName = i.AffectedName ?? db.Employees.Where(e => e.Id == i.AffectedEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
            InvestigatorName = db.Employees.Where(e => e.Id == i.InvestigatorEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
            Actions = db.SafetyActions.Where(a => a.IncidentId == i.Id && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress))
                .Select(a => a.DueOn).ToList()
        }).ToListAsync(ct);
        return raw.Select(r => new IncidentListItemDto(
            r.Id, r.Code, r.Title, r.Type, r.Severity, r.Status, r.OccurredAt, r.LocationName, r.Area, r.DepartmentName, r.ReporterName,
            r.Anonymous, r.AffectedName, r.Treatment, r.LostDays, r.IsReportable, r.AuthorityNotifiedOn, r.InvestigatorEmployeeId,
            r.InvestigatorName, r.Actions.Count, r.Actions.Count(d => d < today), r.ClosedAt, r.LastActivityAt,
            my != null && r.InvestigatorEmployeeId == my)).ToList();
    }

    private async Task<List<SafetyActionDto>> ActionRowsAsync(IQueryable<SafetyAction> q, Guid? my, CancellationToken ct)
    {
        var today = SafetyClock.Today;
        var canManage = access.CanManage;
        var raw = await q.Select(a => new
        {
            Action = a,
            IncidentCode = db.SafetyIncidents.Where(i => i.Id == a.IncidentId).Select(i => i.Code).FirstOrDefault(),
            Investigator = db.SafetyIncidents.Where(i => i.Id == a.IncidentId).Select(i => i.InvestigatorEmployeeId).FirstOrDefault(),
            InspectionCode = db.SafetyInspections.Where(i => i.Id == a.InspectionId).Select(i => i.Code).FirstOrDefault(),
            AssigneeName = db.Employees.Where(e => e.Id == a.AssigneeEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault()
        }).ToListAsync(ct);
        return raw.OrderBy(r => r.Action.DueOn).Select(r =>
        {
            var a = r.Action;
            var mine = my != null && a.AssigneeEmployeeId == my;
            var investigates = my != null && r.Investigator == my;
            return new SafetyActionDto(
                a.Id, a.Title, a.Description, a.IncidentId, r.IncidentCode, a.InspectionId, r.InspectionCode, a.AssigneeEmployeeId,
                r.AssigneeName ?? "—", a.Priority, a.DueOn, a.DueOn.DayNumber - today.DayNumber, a.IsOverdue(today), a.Status, a.CompletedAt,
                a.CompletionNote, a.VerifiedAt, a.CreatedAt, mine,
                CanUpdate: a.IsOpen && (canManage || investigates),
                CanProgress: a.IsOpen && (mine || canManage),
                CanVerify: a.Status == ActionStatus.Done && canManage,
                a.RowVersion);
        }).ToList();
    }

    private async Task<List<InspectionListItemDto>> InspectionRowsAsync(IQueryable<SafetyInspection> q, Guid? my, CancellationToken ct)
    {
        var today = SafetyClock.Today;
        var raw = await q.Select(i => new
        {
            i.Id, i.Code, i.Title, i.ChecklistId, i.Area, i.InspectorEmployeeId, i.DueOn, i.RepeatDays, i.Status, i.CompletedAt, i.ScorePercent,
            ChecklistName = db.SafetyChecklists.IgnoreQueryFilters().Where(c => c.Id == i.ChecklistId).Select(c => c.Name).FirstOrDefault(),
            LocationName = db.Locations.Where(l => l.Id == i.LocationId).Select(l => l.Name).FirstOrDefault(),
            InspectorName = db.Employees.Where(e => e.Id == i.InspectorEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
            ItemCount = i.Lines.Count,
            Failed = i.Lines.Count(l => l.Result == InspectionResult.Fail),
            OpenActions = db.SafetyActions.Count(a => a.InspectionId == i.Id && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress))
        }).ToListAsync(ct);
        return raw.Select(r => new InspectionListItemDto(
            r.Id, r.Code, r.Title, r.ChecklistId, r.ChecklistName, r.LocationName, r.Area, r.InspectorEmployeeId, r.InspectorName ?? "—",
            r.DueOn, r.DueOn.DayNumber - today.DayNumber, r.Status == InspectionStatus.Scheduled && r.DueOn < today, r.RepeatDays, r.Status,
            r.CompletedAt, r.ScorePercent, r.ItemCount, r.Failed, r.OpenActions, my != null && r.InspectorEmployeeId == my)).ToList();
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> userIds, CancellationToken ct)
    {
        var list = userIds.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        if (list.Count == 0)
            return [];
        var rows = await db.Employees.AsNoTracking().IgnoreQueryFilters()
            .Where(e => e.UserId != null && list.Contains(e.UserId.Value) && e.TenantId == access.TenantId)
            .Select(e => new { UserId = e.UserId!.Value, Name = e.FirstName + " " + e.LastName })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.First().Name);
    }
}
