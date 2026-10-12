namespace HR.Employee.API.Application.Relations;

using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Relations;
using MediatR;
using Microsoft.EntityFrameworkCore;

#region DTOs

public sealed record RelationsReminderDto(Guid Id, string Code, string Title, DateOnly? DueOn, int? DaysLeft, CaseMemberRole Role, byte EscalationLevel, string StageName);

public sealed record RelationsCountDto(byte Key, int Count);

public sealed record RelationsSummaryDto(
    int Open, int Unassigned, int Mine, int Overdue, int DueSoon, int Escalated, int ClosedLast90, decimal? AverageDaysToClose,
    IReadOnlyList<RelationsCountDto> ByPhase, IReadOnlyList<RelationsCountDto> ByType, IReadOnlyList<RelationsReminderDto> Reminders,
    int MyConcernsOpen, int MyConcernsTotal, bool CanManage, bool IsAdmin, bool SeesCases, bool HasStages, Guid? MyEmployeeId);

public sealed record CaseStageDto(
    Guid Id, string Name, string? Description, CasePhase Phase, short SortOrder, short? TargetDays, bool IsActive, int OpenCases, int TotalCases, uint RowVersion);

public sealed record RelationsOptionDto(Guid Id, string Name, string? Extra);

public sealed record RelationsLookupsDto(IReadOnlyList<CaseStageDto> Stages, IReadOnlyList<RelationsOptionDto> People);

public sealed record CaseListItemDto(
    Guid Id, string Code, string? Title, CaseType Type, CaseSeverity Severity, CaseConfidentiality Confidentiality, CaseSource Source,
    Guid StageId, string StageName, CasePhase Phase, Guid? OwnerEmployeeId, string? OwnerName, string? ReporterName, bool ReporterHidden,
    IReadOnlyList<string> Respondents, DateOnly ReportedOn, DateOnly? DueOn, int? DaysLeft, bool IsOverdue, byte EscalationLevel,
    CaseMemberRole? MyRole, bool Masked, CaseOutcome? Outcome, DateTime? ClosedAt, DateTime LastActivityAt);

public sealed record CaseMemberDto(Guid EmployeeId, string Name, string? Detail, CaseMemberRole Role, DateTime AddedAt);

public sealed record CasePartyDto(Guid Id, Guid? EmployeeId, string Name, string? Detail, CasePartyRole Role, string? Note, DateTime AddedAt);

public sealed record CaseEntryDto(Guid Id, CaseEntryKind Kind, string? Text, bool Shared, string? StageName, string? ByName, bool ByMe, bool ByReporter, DateTime At);

public sealed record CaseEvidenceDto(
    Guid Id, string Title, string Url, string? Note, bool IsRestricted, bool FromReporter, string? AddedByName, DateTime AddedAt);

public sealed record CaseDetailDto(
    CaseListItemDto Case, string? Description, DateOnly? IncidentOn, string? Location, CaseAction? ActionTaken, string? ResolutionSummary,
    DateTime? EscalatedAt, short ReopenCount, DateTime CreatedAt,
    IReadOnlyList<CaseMemberDto> Members, IReadOnlyList<CasePartyDto> Parties, IReadOnlyList<CaseEntryDto> Entries,
    IReadOnlyList<CaseEvidenceDto> Evidence, int HiddenEvidence, CaseView View,
    bool CanEdit, bool CanWork, bool CanManageTeam, bool CanClose, bool CanReopen, bool CanTake, bool CanBreakGlass,
    bool CanSeeAccessLog, bool CanRemoveEvidence, bool HasReporter, uint RowVersion);

public sealed record CaseAccessLogDto(Guid Id, string? Name, CaseAccessAction Action, string? Note, DateTime At);

public sealed record ConcernListItemDto(
    Guid Id, string Code, string Title, CaseType Type, CasePhase Phase, string StageName, DateOnly ReportedOn, bool ReporterHidden,
    CaseOutcome? Outcome, DateTime? ClosedAt, DateTime LastActivityAt);

public sealed record ConcernDetailDto(
    ConcernListItemDto Concern, string? Description, DateOnly? IncidentOn, string? Location, IReadOnlyList<CaseEntryDto> Entries,
    IReadOnlyList<CaseEvidenceDto> Evidence, bool CanMessage);

#endregion

#region Requests

public sealed record GetRelationsSummaryQuery : IRequest<RelationsSummaryDto>;
public sealed record GetRelationsLookupsQuery : IRequest<RelationsLookupsDto>;
public sealed record GetCaseStagesQuery : IRequest<IReadOnlyList<CaseStageDto>>;
public sealed record GetCasesQuery(string? Scope, CasePhase? Phase, CaseType? Type, CaseSeverity? Severity, bool IncludeClosed, string? Search)
    : IRequest<IReadOnlyList<CaseListItemDto>>;
public sealed record GetCaseQuery(Guid Id) : IRequest<CaseDetailDto>;
public sealed record GetCaseAccessLogQuery(Guid Id) : IRequest<IReadOnlyList<CaseAccessLogDto>>;
public sealed record GetConcernsQuery : IRequest<IReadOnlyList<ConcernListItemDto>>;
public sealed record GetConcernQuery(Guid Id) : IRequest<ConcernDetailDto>;

#endregion

public sealed class RelationsQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetRelationsSummaryQuery, RelationsSummaryDto>,
    IRequestHandler<GetRelationsLookupsQuery, RelationsLookupsDto>,
    IRequestHandler<GetCaseStagesQuery, IReadOnlyList<CaseStageDto>>,
    IRequestHandler<GetCasesQuery, IReadOnlyList<CaseListItemDto>>,
    IRequestHandler<GetCaseQuery, CaseDetailDto>,
    IRequestHandler<GetCaseAccessLogQuery, IReadOnlyList<CaseAccessLogDto>>,
    IRequestHandler<GetConcernsQuery, IReadOnlyList<ConcernListItemDto>>,
    IRequestHandler<GetConcernQuery, ConcernDetailDto>
{
    /// <summary>Ek hi shakhs baar baar khole to har 30 min mein ek "Viewed" line.</summary>
    private static readonly TimeSpan ViewThrottle = TimeSpan.FromMinutes(30);
    private const int DueSoonDays = 3;

    private readonly RelationsAccess access = new(db, currentUser);

    public async Task<RelationsSummaryDto> Handle(GetRelationsSummaryQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var today = RelationsClock.Today;
        var now = DateTime.UtcNow;

        var rows = await access.TeamCases(my)
            .Select(c => new
            {
                c.Id, c.Code, c.Title, c.Type, c.Phase, c.OwnerEmployeeId, c.DueOn, c.EscalationLevel, c.CreatedAt, c.ClosedAt, c.StageId,
                MyRole = db.CaseMembers.Where(m => m.CaseId == c.Id && m.EmployeeId == my).Select(m => (CaseMemberRole?)m.Role).FirstOrDefault()
            })
            .ToListAsync(ct);
        var open = rows.Where(r => r.Phase != CasePhase.Closed).ToList();
        var since = now.AddDays(-90);
        var closed = rows.Where(r => r.ClosedAt is { } at && at >= since).ToList();
        decimal? avgDays = closed.Count == 0 ? null : Math.Round((decimal)closed.Average(r => (r.ClosedAt!.Value - r.CreatedAt).TotalDays), 1);

        var stageNames = await db.CaseStages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var reminders = open
            .Where(r => r.MyRole is CaseMemberRole.Owner or CaseMemberRole.Investigator && r.DueOn is { } due && due <= today.AddDays(7))
            .OrderBy(r => r.DueOn)
            .Take(10)
            .Select(r => new RelationsReminderDto(r.Id, r.Code, r.Title, r.DueOn, r.DueOn!.Value.DayNumber - today.DayNumber, r.MyRole!.Value,
                r.EscalationLevel, stageNames.GetValueOrDefault(r.StageId, "")))
            .ToList();

        var concerns = my is null ? [] : await db.ErCases.AsNoTracking().Where(c => c.ReporterEmployeeId == my).Select(c => c.Phase).ToListAsync(ct);
        var hasStages = await db.CaseStages.AnyAsync(s => s.IsActive, ct);

        return new RelationsSummaryDto(
            open.Count,
            open.Count(r => r.OwnerEmployeeId == null),
            open.Count(r => r.MyRole != null),
            open.Count(r => r.DueOn is { } due && due < today),
            open.Count(r => r.DueOn is { } due && due >= today && due <= today.AddDays(DueSoonDays)),
            open.Count(r => r.EscalationLevel > 0),
            closed.Count, avgDays,
            open.GroupBy(r => r.Phase).OrderBy(g => g.Key).Select(g => new RelationsCountDto((byte)g.Key, g.Count())).ToList(),
            open.GroupBy(r => r.Type).OrderByDescending(g => g.Count()).Select(g => new RelationsCountDto((byte)g.Key, g.Count())).ToList(),
            reminders,
            concerns.Count(p => p != CasePhase.Closed), concerns.Count,
            access.CanManage, access.IsAdmin, access.CanManage || rows.Count > 0, hasStages, my);
    }

    public async Task<RelationsLookupsDto> Handle(GetRelationsLookupsQuery q, CancellationToken ct)
    {
        var stages = (await StagesAsync(ct)).Where(s => s.IsActive || access.CanManage).ToList();
        var people = await db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus != EmploymentStatus.Exited)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new RelationsOptionDto(e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode + " · " + e.Department.Name))
            .ToListAsync(ct);
        return new RelationsLookupsDto(stages, people);
    }

    public async Task<IReadOnlyList<CaseStageDto>> Handle(GetCaseStagesQuery q, CancellationToken ct)
    {
        access.EnsureCanManage();
        return await StagesAsync(ct);
    }

    private async Task<List<CaseStageDto>> StagesAsync(CancellationToken ct)
        => await db.CaseStages.AsNoTracking()
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new CaseStageDto(
                s.Id, s.Name, s.Description, s.Phase, s.SortOrder, s.TargetDays, s.IsActive,
                db.ErCases.Count(c => c.StageId == s.Id && c.Phase != CasePhase.Closed),
                db.ErCases.Count(c => c.StageId == s.Id),
                s.RowVersion))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CaseListItemDto>> Handle(GetCasesQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var today = RelationsClock.Today;
        var cases = access.TeamCases(my);

        if (q.Phase is { } phase)
            cases = cases.Where(c => c.Phase == phase);
        else if (!q.IncludeClosed)
            cases = cases.Where(c => c.Phase != CasePhase.Closed);
        if (q.Type is { } type)
            cases = cases.Where(c => c.Type == type);
        if (q.Severity is { } severity)
            cases = cases.Where(c => c.Severity == severity);
        switch (q.Scope)
        {
            case "mine":
                cases = cases.Where(c => my != null && db.CaseMembers.Any(m => m.CaseId == c.Id && m.EmployeeId == my));
                break;
            case "unassigned":
                cases = cases.Where(c => c.OwnerEmployeeId == null);
                break;
            case "overdue":
                cases = cases.Where(c => c.Phase != CasePhase.Closed && c.DueOn != null && c.DueOn < today);
                break;
            case "escalated":
                cases = cases.Where(c => c.Phase != CasePhase.Closed && c.EscalationLevel > 0);
                break;
        }

        var rows = await Project(cases, my).OrderByDescending(r => r.LastActivityAt).Take(500).ToListAsync(ct);
        var list = await ToDtosAsync(rows, today, ct);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            list = list.Where(r => r.Code.Contains(s, StringComparison.OrdinalIgnoreCase)
                || (r.Title?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.OwnerName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.ReporterName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                || r.Respondents.Any(n => n.Contains(s, StringComparison.OrdinalIgnoreCase))).ToList();
        }
        return list;
    }

    public async Task<CaseDetailDto> Handle(GetCaseQuery q, CancellationToken ct)
    {
        var c = await access.LoadAsync(q.Id, ct, tracking: false);
        var my = await access.MeAsync(ct);
        var view = access.ViewOf(c, my);
        if (view == CaseView.None)
            throw new NotFoundException("Case", q.Id);
        var today = RelationsClock.Today;
        var now = DateTime.UtcNow;

        var row = (await ToDtosAsync([await Project(db.ErCases.AsNoTracking().Where(x => x.Id == c.Id), my).FirstAsync(ct)], today, ct))[0];
        var unowned = c.OwnerEmployeeId is null && !c.IsClosed;

        if (view == CaseView.Masked)
            return new CaseDetailDto(
                row, null, null, null, null, null, null, 0, c.CreatedAt, [], [], [], [], 0, view,
                CanEdit: false, CanWork: false, CanManageTeam: false, CanClose: false, CanReopen: false,
                CanTake: unowned && my != null && access.CanManage,
                CanBreakGlass: access.IsAdmin && my != null,
                CanSeeAccessLog: false, CanRemoveEvidence: false, HasReporter: c.ReporterEmployeeId != null, c.RowVersion);

        await LogViewAsync(c.Id, my, now, ct);

        var isWorker = view is CaseView.Owner or CaseView.Investigator;
        var isOwner = view == CaseView.Owner;
        var adminMember = access.IsAdmin && view >= CaseView.Viewer;

        var reporterUserId = c.ReporterEmployeeId is { } rid
            ? await db.Employees.AsNoTracking().Where(e => e.Id == rid).Select(e => e.UserId).FirstOrDefaultAsync(ct)
            : null;
        var people = await PeopleAsync(c.Members.Select(m => (Guid?)m.EmployeeId).Concat(c.Parties.Select(p => p.EmployeeId)), ct);
        var stages = await db.CaseStages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var members = c.Members
            .OrderBy(m => m.Role).ThenBy(m => m.AddedAt)
            .Select(m => new CaseMemberDto(m.EmployeeId, people.GetValueOrDefault(m.EmployeeId)?.Name ?? "—",
                people.GetValueOrDefault(m.EmployeeId)?.Detail, m.Role, m.AddedAt))
            .ToList();
        var parties = c.Parties
            .OrderBy(p => p.Role).ThenBy(p => p.AddedAt)
            .Select(p => new CasePartyDto(p.Id, p.EmployeeId,
                p.EmployeeId is { } pid ? people.GetValueOrDefault(pid)?.Name ?? "—" : p.ExternalName ?? "—",
                p.EmployeeId is { } did ? people.GetValueOrDefault(did)?.Detail : null, p.Role, p.Note, p.AddedAt))
            .ToList();

        var entries = await EntriesAsync(c, reporterUserId, stages, forReporter: false, ct);
        var evidenceUsers = await UserNamesAsync(c.Evidence.Select(e => e.AddedByUserId), ct);
        var visibleEvidence = c.Evidence.Where(e => isWorker || !e.IsRestricted).OrderBy(e => e.AddedAt).ToList();
        var evidence = visibleEvidence
            .Select(e => new CaseEvidenceDto(e.Id, e.Title, e.Url, e.Note, e.IsRestricted, e.FromReporter,
                e.FromReporter && c.ReporterHidden ? null : e.AddedByUserId is { } u ? evidenceUsers.GetValueOrDefault(u) : null, e.AddedAt))
            .ToList();

        return new CaseDetailDto(
            row, c.Description, c.IncidentOn, c.Location, c.ActionTaken, c.ResolutionSummary, c.EscalatedAt, c.ReopenCount, c.CreatedAt,
            members, parties, entries, evidence, c.Evidence.Count - visibleEvidence.Count, view,
            CanEdit: isWorker && !c.IsClosed,
            CanWork: isWorker && !c.IsClosed,
            CanManageTeam: (isOwner || adminMember) && !c.IsClosed,
            CanClose: isOwner && !c.IsClosed,
            CanReopen: (isOwner || adminMember) && c.IsClosed,
            CanTake: unowned && my != null && access.CanManage,
            CanBreakGlass: false,
            CanSeeAccessLog: isOwner || adminMember,
            CanRemoveEvidence: isOwner && !c.IsClosed,
            HasReporter: c.ReporterEmployeeId != null,
            c.RowVersion);
    }

    public async Task<IReadOnlyList<CaseAccessLogDto>> Handle(GetCaseAccessLogQuery q, CancellationToken ct)
    {
        var c = await access.LoadAsync(q.Id, ct, tracking: false);
        var my = await access.MeAsync(ct);
        var view = access.ViewOf(c, my);
        if (view == CaseView.None)
            throw new NotFoundException("Case", q.Id);
        if (!(view == CaseView.Owner || (access.IsAdmin && view >= CaseView.Viewer)))
            throw new UnauthorizedAccessException("Only the case owner or an admin on the case can see who opened it.");

        var logs = await db.CaseAccessLogs.AsNoTracking()
            .Where(l => l.CaseId == c.Id)
            .OrderByDescending(l => l.At)
            .Take(300)
            .Select(l => new CaseAccessLogDto(l.Id,
                db.Employees.Where(e => e.Id == l.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault() ?? l.UserName,
                l.Action, l.Note, l.At))
            .ToListAsync(ct);

        db.CaseAccessLogs.Add(new CaseAccessLog(c.Id, access.UserId, my, access.UserName, CaseAccessAction.ViewedAccessLog, null, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
        return logs;
    }

    public async Task<IReadOnlyList<ConcernListItemDto>> Handle(GetConcernsQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        if (my is null)
            return [];
        return await ConcernRows(db.ErCases.AsNoTracking().Where(c => c.ReporterEmployeeId == my).OrderByDescending(c => c.LastActivityAt))
            .ToListAsync(ct);
    }

    public async Task<ConcernDetailDto> Handle(GetConcernQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var c = await access.LoadAsync(q.Id, ct, tracking: false);
        if (my is null || c.ReporterEmployeeId != my)
            throw new NotFoundException("Concern", q.Id);

        var row = await ConcernRows(db.ErCases.AsNoTracking().Where(x => x.Id == c.Id)).FirstAsync(ct);
        var stages = await db.CaseStages.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var entries = await EntriesAsync(c, access.UserId, stages, forReporter: true, ct);
        var evidence = c.Evidence.Where(e => e.FromReporter).OrderBy(e => e.AddedAt)
            .Select(e => new CaseEvidenceDto(e.Id, e.Title, e.Url, e.Note, e.IsRestricted, true, null, e.AddedAt))
            .ToList();
        return new ConcernDetailDto(row, c.Description, c.IncidentOn, c.Location, entries, evidence, !c.IsClosed);
    }

    private IQueryable<ConcernListItemDto> ConcernRows(IQueryable<ErCase> cases)
        => from c in cases
           join s in db.CaseStages on c.StageId equals s.Id
           select new ConcernListItemDto(c.Id, c.Code, c.Title, c.Type, c.Phase, s.Name, c.ReportedOn, c.ReporterHidden,
               c.Outcome, c.ClosedAt, c.LastActivityAt);

    /// <summary>
    /// Timeline. Reporter ko sirf shared lines + stage ke naam (note nahi), aur team ke naam ki jagah null (UI "Relations team").
    /// Team ko hidden reporter ki lines "Reporter" ke naam se.
    /// </summary>
    private async Task<List<CaseEntryDto>> EntriesAsync(
        ErCase c, Guid? reporterUserId, IReadOnlyDictionary<Guid, string> stages, bool forReporter, CancellationToken ct)
    {
        var raw = await db.CaseEntries.AsNoTracking()
            .Where(e => e.CaseId == c.Id && (!forReporter || e.Shared || e.Kind == CaseEntryKind.Created || e.Kind == CaseEntryKind.StageChanged))
            .OrderBy(e => e.At).ThenBy(e => e.Id)
            .ToListAsync(ct);
        var names = forReporter ? new Dictionary<Guid, string>() : await UserNamesAsync(raw.Select(e => e.ByUserId), ct);
        var userId = access.UserId;
        return raw.Select(e =>
        {
            var byReporter = reporterUserId != null && e.ByUserId == reporterUserId;
            var stageName = e.StageId is { } sid ? stages.GetValueOrDefault(sid) : null;
            var text = forReporter && e.Kind == CaseEntryKind.StageChanged ? null
                : forReporter && e.Kind == CaseEntryKind.Reopened ? stageName
                : e.Text;
            string? by = forReporter || (byReporter && c.ReporterHidden)
                ? null
                : e.ByUserId is { } u ? names.GetValueOrDefault(u) : null;
            return new CaseEntryDto(e.Id, e.Kind, text, e.Shared, stageName, by, userId != null && e.ByUserId == userId, byReporter, e.At);
        }).ToList();
    }

    private async Task LogViewAsync(Guid caseId, Guid? my, DateTime now, CancellationToken ct)
    {
        var userId = access.UserId;
        var since = now - ViewThrottle;
        var recent = await db.CaseAccessLogs.AnyAsync(l => l.CaseId == caseId && l.UserId == userId && l.Action == CaseAccessAction.Viewed && l.At >= since, ct);
        if (recent)
            return;
        db.CaseAccessLogs.Add(new CaseAccessLog(caseId, userId, my, access.UserName, CaseAccessAction.Viewed, null, now));
        await db.SaveChangesAsync(ct);
    }

    private sealed record Person(string Name, string? Detail);

    private async Task<Dictionary<Guid, Person>> PeopleAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        if (list.Count == 0)
            return [];
        return await db.Employees.AsNoTracking().IgnoreQueryFilters()
            .Where(e => list.Contains(e.Id) && e.TenantId == access.TenantId)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, Detail = e.EmployeeCode + " · " + e.Department.Name })
            .ToDictionaryAsync(e => e.Id, e => new Person(e.Name, e.Detail), ct);
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

    private IQueryable<CaseRow> Project(IQueryable<ErCase> cases, Guid? my)
        => from c in cases
           join s in db.CaseStages on c.StageId equals s.Id
           select new CaseRow
           {
               Id = c.Id,
               Code = c.Code,
               Title = c.Title,
               Type = c.Type,
               Severity = c.Severity,
               Confidentiality = c.Confidentiality,
               Source = c.Source,
               StageId = s.Id,
               StageName = s.Name,
               Phase = c.Phase,
               OwnerEmployeeId = c.OwnerEmployeeId,
               OwnerName = db.Employees.Where(e => e.Id == c.OwnerEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
               ReporterName = db.Employees.Where(e => e.Id == c.ReporterEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
               ReporterHidden = c.ReporterHidden,
               ReportedOn = c.ReportedOn,
               DueOn = c.DueOn,
               EscalationLevel = c.EscalationLevel,
               MyRole = db.CaseMembers.Where(m => m.CaseId == c.Id && m.EmployeeId == my).Select(m => (CaseMemberRole?)m.Role).FirstOrDefault(),
               Outcome = c.Outcome,
               ClosedAt = c.ClosedAt,
               LastActivityAt = c.LastActivityAt
           };

    /// <summary>Respondents ke naam alag query mein; restricted non-member line masked (title/naam nahi).</summary>
    private async Task<List<CaseListItemDto>> ToDtosAsync(List<CaseRow> rows, DateOnly today, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var respondents = await (
                from p in db.CaseParties.AsNoTracking()
                where ids.Contains(p.CaseId) && p.Role == CasePartyRole.Respondent
                select new
                {
                    p.CaseId,
                    Name = p.EmployeeId == null ? p.ExternalName
                        : db.Employees.Where(e => e.Id == p.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault()
                })
            .ToListAsync(ct);
        var byCase = respondents.GroupBy(r => r.CaseId).ToDictionary(g => g.Key, g => g.Select(x => x.Name ?? "—").ToList());

        return rows.Select(r =>
        {
            var masked = r.Confidentiality == CaseConfidentiality.Restricted && r.MyRole is null;
            int? daysLeft = r.Phase != CasePhase.Closed && r.DueOn is { } due ? due.DayNumber - today.DayNumber : null;
            return new CaseListItemDto(
                r.Id, r.Code, masked ? null : r.Title, r.Type, r.Severity, r.Confidentiality, r.Source, r.StageId, r.StageName, r.Phase,
                r.OwnerEmployeeId, r.OwnerName, masked || r.ReporterHidden ? null : r.ReporterName, r.ReporterHidden,
                masked ? [] : byCase.GetValueOrDefault(r.Id) ?? [], r.ReportedOn, r.DueOn, daysLeft, daysLeft < 0, r.EscalationLevel,
                r.MyRole, masked, r.Outcome, r.ClosedAt, r.LastActivityAt);
        }).ToList();
    }

    private sealed class CaseRow
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = default!;
        public string Title { get; init; } = default!;
        public CaseType Type { get; init; }
        public CaseSeverity Severity { get; init; }
        public CaseConfidentiality Confidentiality { get; init; }
        public CaseSource Source { get; init; }
        public Guid StageId { get; init; }
        public string StageName { get; init; } = default!;
        public CasePhase Phase { get; init; }
        public Guid? OwnerEmployeeId { get; init; }
        public string? OwnerName { get; init; }
        public string? ReporterName { get; init; }
        public bool ReporterHidden { get; init; }
        public DateOnly ReportedOn { get; init; }
        public DateOnly? DueOn { get; init; }
        public byte EscalationLevel { get; init; }
        public CaseMemberRole? MyRole { get; init; }
        public CaseOutcome? Outcome { get; init; }
        public DateTime? ClosedAt { get; init; }
        public DateTime LastActivityAt { get; init; }
    }
}
