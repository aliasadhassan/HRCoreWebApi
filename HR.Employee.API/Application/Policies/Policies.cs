namespace HR.Employee.API.Application.Policies;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Policies;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Policies & compliance: HR (settings.manage) policies likhta/publish karta hai, har employee apni audience ki published
// policies parhta aur acknowledge karta hai. employees.view = acknowledgement tracking dekh sakta hai.
// Statutory obligations (Compliance.cs): settings.view dekhe, settings.manage chalaye.

#region DTOs

public enum AckState : byte { NotRequired = 0, Pending = 1, Overdue = 2, Done = 3 }

public sealed record ComplianceSummaryDto(
    int Published, int Drafts, int ReviewsDue, int MyPending, int MyOverdue, decimal? AckRate, int PeoplePending,
    int ObligationsOverdue, int ObligationsDueSoon, int FiledThisYear,
    bool CanManage, bool CanTrack, bool CanViewStatutory, bool HasPolicies, Guid? MyEmployeeId);

public sealed record PolicyListItemDto(
    Guid Id, string Title, PolicyCategory Category, string? Summary, PolicyStatus Status, int CurrentVersion, bool HasUnpublishedChanges,
    DateTime? PublishedAt, DateOnly? EffectiveOn, Guid? DepartmentId, string? DepartmentName, Guid? OwnerEmployeeId, string? OwnerName,
    bool RequiresAcknowledgement, short? AcknowledgeWithinDays, short? ReviewEveryMonths, int? AckVersion, DateOnly? AckDueOn,
    DateOnly? NextReviewOn, bool ReviewDue, int? AudienceCount, int? AcknowledgedCount, int? OverdueCount,
    bool InMyAudience, AckState MyAckState, DateTime? MyAcknowledgedAt, DateTime UpdatedAt, uint RowVersion);

public sealed record PolicyVersionDto(
    int Number, string Title, string? ChangeSummary, DateOnly EffectiveOn, bool RequiresAcknowledgement, DateTime PublishedAt, string? PublishedByName);

public sealed record PolicyVersionContentDto(
    int Number, string Title, string? Body, string? DocumentUrl, string? ChangeSummary, DateOnly EffectiveOn, DateTime PublishedAt);

public sealed record PolicyDto(
    PolicyListItemDto Policy, PolicyVersionContentDto? Current, string? WorkingBody, string? WorkingDocumentUrl, DateOnly? LastReviewedOn,
    IReadOnlyList<PolicyVersionDto> Versions,
    bool CanEdit, bool CanPublish, bool CanArchive, bool CanRestore, bool CanDelete, bool CanMarkReviewed, bool CanAcknowledge, bool CanTrack);

public sealed record AckPersonDto(
    Guid EmployeeId, string Name, string Code, string DepartmentName, string? DesignationName, DateOnly JoiningDate,
    AckState State, DateTime? AcknowledgedAt, int? VersionNumber);

public sealed record ComplianceOptionDto(Guid Id, string Name, string? Extra);

public sealed record ComplianceLookupsDto(IReadOnlyList<ComplianceOptionDto> Departments, IReadOnlyList<ComplianceOptionDto> People);

#endregion

#region Requests

public sealed record GetComplianceSummaryQuery : IRequest<ComplianceSummaryDto>;
public sealed record GetComplianceLookupsQuery : IRequest<ComplianceLookupsDto>;
public sealed record GetPoliciesQuery(PolicyStatus? Status, PolicyCategory? Category, string? Search) : IRequest<IReadOnlyList<PolicyListItemDto>>;
public sealed record GetPolicyQuery(Guid Id) : IRequest<PolicyDto>;
public sealed record GetPolicyVersionQuery(Guid Id, int Number) : IRequest<PolicyVersionContentDto>;
public sealed record GetPolicyAcknowledgementsQuery(Guid Id, AckState? State, Guid? DepartmentId, string? Search) : IRequest<IReadOnlyList<AckPersonDto>>;

public sealed record SavePolicyRequest(
    string Title, PolicyCategory Category, string? Summary, string? Body, string? DocumentUrl, Guid? DepartmentId, Guid? OwnerEmployeeId,
    bool RequiresAcknowledgement, short? AcknowledgeWithinDays, short? ReviewEveryMonths);
public sealed record CreatePolicyCommand(SavePolicyRequest Data) : IRequest<Guid>;
public sealed record UpdatePolicyCommand(Guid Id, SavePolicyRequest Data) : IRequest;
public sealed record PublishPolicyCommand(Guid Id, string? ChangeSummary, bool Reacknowledge, DateOnly? EffectiveOn) : IRequest;
public sealed record ArchivePolicyCommand(Guid Id) : IRequest;
public sealed record RestorePolicyCommand(Guid Id) : IRequest;
public sealed record DeletePolicyCommand(Guid Id) : IRequest;
public sealed record MarkPolicyReviewedCommand(Guid Id) : IRequest;
public sealed record AcknowledgePolicyCommand(Guid Id) : IRequest;
/// <summary>Aam policies ke draft (code of conduct, leave...) jo pehle se na hon. Wapas = kitne bane.</summary>
public sealed record AddStarterPoliciesCommand : IRequest<int>;

public sealed class SavePolicyRequestValidator : AbstractValidator<SavePolicyRequest>
{
    public SavePolicyRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.Body).MaximumLength(Policy.BodyMaxLength);
        RuleFor(x => x.DocumentUrl).MaximumLength(1000);
        RuleFor(x => x.AcknowledgeWithinDays).InclusiveBetween((short)1, (short)365).When(x => x.AcknowledgeWithinDays is not null);
        RuleFor(x => x.ReviewEveryMonths).InclusiveBetween((short)1, (short)60).When(x => x.ReviewEveryMonths is not null);
    }
}

public sealed class CreatePolicyValidator : AbstractValidator<CreatePolicyCommand>
{
    public CreatePolicyValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SavePolicyRequestValidator());
}

public sealed class UpdatePolicyValidator : AbstractValidator<UpdatePolicyCommand>
{
    public UpdatePolicyValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SavePolicyRequestValidator());
}

public sealed class PublishPolicyValidator : AbstractValidator<PublishPolicyCommand>
{
    public PublishPolicyValidator() => RuleFor(x => x.ChangeSummary).MaximumLength(500);
}

#endregion

#region Access

public sealed class ComplianceAccess(IAppDbContext db, ICurrentUser currentUser)
{
    private Me? me;

    public sealed record Me(Guid? EmployeeId, Guid? DepartmentId);

    public bool CanManage => currentUser.HasPermission(Permissions.SettingsManage);
    public bool CanTrack => CanManage || currentUser.HasPermission(Permissions.EmployeesView);
    public bool CanViewStatutory => CanManage || currentUser.HasPermission(Permissions.SettingsView);
    public Guid? UserId => currentUser.UserId;
    public Guid TenantId => currentUser.RequireTenantId();

    public async Task<Me> MeAsync(CancellationToken ct)
    {
        if (me is not null)
            return me;
        var row = currentUser.UserId is { } userId
            ? await db.Employees.AsNoTracking().Where(e => e.UserId == userId && e.EmploymentStatus != EmploymentStatus.Exited)
                .Select(e => new { e.Id, e.DepartmentId }).FirstOrDefaultAsync(ct)
            : null;
        me = new Me(row?.Id, row?.DepartmentId);
        return me;
    }

    public void EnsureCanManage()
    {
        if (!CanManage)
            throw new UnauthorizedAccessException("You do not have permission to manage policies and compliance.");
    }

    public void EnsureCanTrack()
    {
        if (!CanTrack)
            throw new UnauthorizedAccessException("You do not have permission to see acknowledgement tracking.");
    }

    public void EnsureCanViewStatutory()
    {
        if (!CanViewStatutory)
            throw new UnauthorizedAccessException("You do not have permission to see statutory compliance.");
    }

    public static bool InAudience(Policy p, Me my) => my.EmployeeId is not null && (p.DepartmentId is null || p.DepartmentId == my.DepartmentId);

    /// <summary>Manager sab; tracker sab published; baqi sirf apni audience ki published.</summary>
    public IQueryable<Policy> Visible(Me my)
    {
        var q = db.Policies.AsNoTracking();
        if (CanManage)
            return q;
        if (CanTrack)
            return q.Where(p => p.Status == PolicyStatus.Published);
        var dept = my.DepartmentId;
        return q.Where(p => p.Status == PolicyStatus.Published && my.EmployeeId != null && (p.DepartmentId == null || p.DepartmentId == dept));
    }

    public async Task<Policy> SeeAsync(Guid id, CancellationToken ct)
        => await Visible(await MeAsync(ct)).FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Policy", id);
}

/// <summary>Kis ne kaunsa version acknowledge kiya + audience (active employees) se state nikalna.</summary>
internal sealed record AudienceMember(Guid Id, Guid DepartmentId);

internal sealed record AckRow(Guid PolicyId, Guid EmployeeId, int VersionNumber, DateTime AcknowledgedAt);

internal static class AckMath
{
    public static AckState StateOf(Policy p, AckRow? latest, DateOnly today)
    {
        if (!p.NeedsAcknowledgement)
            return AckState.NotRequired;
        if (latest is not null && latest.VersionNumber >= p.AckVersion)
            return AckState.Done;
        return p.AckDueOn is { } due && due < today ? AckState.Overdue : AckState.Pending;
    }

    public static bool Includes(Policy p, AudienceMember m) => p.DepartmentId is null || p.DepartmentId == m.DepartmentId;

    /// <summary>Har (policy, employee) ka sab se naya acknowledgement.</summary>
    public static async Task<Dictionary<(Guid, Guid), AckRow>> LatestAsync(IAppDbContext db, IReadOnlyCollection<Guid> policyIds, Guid? employeeId, CancellationToken ct)
    {
        var q = db.PolicyAcknowledgements.AsNoTracking().Where(a => policyIds.Contains(a.PolicyId));
        if (employeeId is { } only)
            q = q.Where(a => a.EmployeeId == only);
        var rows = await q
            .GroupBy(a => new { a.PolicyId, a.EmployeeId })
            .Select(g => new AckRow(g.Key.PolicyId, g.Key.EmployeeId, g.Max(a => a.VersionNumber), g.Max(a => a.AcknowledgedAt)))
            .ToListAsync(ct);
        return rows.ToDictionary(r => (r.PolicyId, r.EmployeeId));
    }

    public static Task<List<AudienceMember>> ActivePeopleAsync(IAppDbContext db, CancellationToken ct)
        => db.Employees.AsNoTracking().Where(e => e.EmploymentStatus != EmploymentStatus.Exited)
            .Select(e => new AudienceMember(e.Id, e.DepartmentId)).ToListAsync(ct);
}

#endregion

#region Queries

public sealed class PolicyQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetComplianceSummaryQuery, ComplianceSummaryDto>,
    IRequestHandler<GetComplianceLookupsQuery, ComplianceLookupsDto>,
    IRequestHandler<GetPoliciesQuery, IReadOnlyList<PolicyListItemDto>>,
    IRequestHandler<GetPolicyQuery, PolicyDto>,
    IRequestHandler<GetPolicyVersionQuery, PolicyVersionContentDto>,
    IRequestHandler<GetPolicyAcknowledgementsQuery, IReadOnlyList<AckPersonDto>>
{
    private readonly ComplianceAccess access = new(db, currentUser);

    public async Task<ComplianceSummaryDto> Handle(GetComplianceSummaryQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var items = await ListAsync(access.Visible(my), my, ct);
        var today = Today;

        var published = items.Where(p => p.Status == PolicyStatus.Published).ToList();
        decimal? ackRate = null;
        var peoplePending = 0;
        if (access.CanTrack)
        {
            var tracked = published.Where(p => p.AudienceCount > 0 && p.AckVersion is not null).ToList();
            var audience = tracked.Sum(p => p.AudienceCount ?? 0);
            if (audience > 0)
                ackRate = Math.Round(100m * tracked.Sum(p => p.AcknowledgedCount ?? 0) / audience, 1);
            peoplePending = await PeoplePendingAsync(tracked.Select(p => p.Id).ToList(), ct);
        }

        int overdue = 0, dueSoon = 0, filedThisYear = 0;
        if (access.CanViewStatutory)
        {
            var obligations = await db.ComplianceObligations.AsNoTracking()
                .Select(o => new { o.NextDueOn, o.IsActive, o.RemindDaysBefore }).ToListAsync(ct);
            overdue = obligations.Count(o => o.IsActive && o.NextDueOn < today);
            dueSoon = obligations.Count(o => o.IsActive && o.NextDueOn >= today && o.NextDueOn <= today.AddDays(o.RemindDaysBefore));
            var yearStart = new DateOnly(today.Year, 1, 1);
            filedThisYear = await db.ComplianceFilings.CountAsync(f => f.FiledOn >= yearStart, ct);
        }

        return new ComplianceSummaryDto(
            published.Count, items.Count(p => p.Status == PolicyStatus.Draft), published.Count(p => p.ReviewDue),
            published.Count(p => p.MyAckState is AckState.Pending or AckState.Overdue), published.Count(p => p.MyAckState == AckState.Overdue),
            ackRate, peoplePending, overdue, dueSoon, filedThisYear,
            access.CanManage, access.CanTrack, access.CanViewStatutory, items.Count > 0, my.EmployeeId);
    }

    /// <summary>Kitne alag log kam az kam ek policy acknowledge karna baqi rakhte hain.</summary>
    private async Task<int> PeoplePendingAsync(List<Guid> policyIds, CancellationToken ct)
    {
        if (policyIds.Count == 0)
            return 0;
        var policies = await db.Policies.AsNoTracking().Where(p => policyIds.Contains(p.Id)).ToListAsync(ct);
        var people = await AckMath.ActivePeopleAsync(db, ct);
        var latest = await AckMath.LatestAsync(db, policyIds, null, ct);
        var today = Today;
        return people.Count(m => policies.Any(p => AckMath.Includes(p, m)
            && AckMath.StateOf(p, latest.GetValueOrDefault((p.Id, m.Id)), today) is AckState.Pending or AckState.Overdue));
    }

    public async Task<ComplianceLookupsDto> Handle(GetComplianceLookupsQuery q, CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new ComplianceOptionDto(d.Id, d.Name, d.Code)).ToListAsync(ct);
        IReadOnlyList<ComplianceOptionDto> people = [];
        if (access.CanManage || access.CanTrack)
            people = await db.Employees.AsNoTracking()
                .Where(e => e.EmploymentStatus != EmploymentStatus.Exited)
                .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
                .Select(e => new ComplianceOptionDto(e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode + " · " + e.Department.Name))
                .ToListAsync(ct);
        return new ComplianceLookupsDto(departments, people);
    }

    public async Task<IReadOnlyList<PolicyListItemDto>> Handle(GetPoliciesQuery q, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var policies = access.Visible(my);
        if (q.Status is { } status)
            policies = policies.Where(p => p.Status == status);
        if (q.Category is { } category)
            policies = policies.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = $"%{q.Search.Trim()}%";
            policies = policies.Where(p => EF.Functions.ILike(p.Title, s) || (p.Summary != null && EF.Functions.ILike(p.Summary, s)));
        }
        return await ListAsync(policies, my, ct);
    }

    public async Task<PolicyDto> Handle(GetPolicyQuery q, CancellationToken ct)
    {
        var policy = await access.SeeAsync(q.Id, ct);
        var my = await access.MeAsync(ct);
        var item = (await ListAsync(db.Policies.AsNoTracking().Where(p => p.Id == policy.Id), my, ct))[0];

        var versions = await db.PolicyVersions.AsNoTracking().Where(v => v.PolicyId == policy.Id)
            .OrderByDescending(v => v.Number)
            .Select(v => new PolicyVersionDto(v.Number, v.Title, v.ChangeSummary, v.EffectiveOn, v.RequiresAcknowledgement, v.PublishedAt,
                v.PublishedByUserId == null ? null
                    : db.Employees.Where(e => e.UserId == v.PublishedByUserId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault()))
            .ToListAsync(ct);
        var current = policy.CurrentVersion > 0 ? await VersionAsync(policy.Id, policy.CurrentVersion, ct) : null;

        var manage = access.CanManage;
        var archived = policy.Status == PolicyStatus.Archived;
        return new PolicyDto(
            item, current, manage ? policy.Body : null, manage ? policy.DocumentUrl : null, policy.LastReviewedOn, versions,
            CanEdit: manage && !archived,
            CanPublish: manage && !archived && (policy.CurrentVersion == 0 || policy.HasUnpublishedChanges),
            CanArchive: manage && !archived,
            CanRestore: manage && archived,
            CanDelete: manage && policy.CurrentVersion == 0,
            CanMarkReviewed: manage && policy.Status == PolicyStatus.Published,
            CanAcknowledge: item.InMyAudience && item.MyAckState is AckState.Pending or AckState.Overdue,
            CanTrack: access.CanTrack && policy.Status != PolicyStatus.Draft);
    }

    public async Task<PolicyVersionContentDto> Handle(GetPolicyVersionQuery q, CancellationToken ct)
    {
        var policy = await access.SeeAsync(q.Id, ct);
        return await VersionAsync(policy.Id, q.Number, ct) ?? throw new NotFoundException("Policy version", q.Number);
    }

    private Task<PolicyVersionContentDto?> VersionAsync(Guid policyId, int number, CancellationToken ct)
        => db.PolicyVersions.AsNoTracking().Where(v => v.PolicyId == policyId && v.Number == number)
            .Select(v => new PolicyVersionContentDto(v.Number, v.Title, v.Body, v.DocumentUrl, v.ChangeSummary, v.EffectiveOn, v.PublishedAt))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AckPersonDto>> Handle(GetPolicyAcknowledgementsQuery q, CancellationToken ct)
    {
        access.EnsureCanTrack();
        var policy = await access.SeeAsync(q.Id, ct);
        var people = db.Employees.AsNoTracking().Where(e => e.EmploymentStatus != EmploymentStatus.Exited);
        if (policy.DepartmentId is { } audienceDept)
            people = people.Where(e => e.DepartmentId == audienceDept);
        if (q.DepartmentId is { } dept)
            people = people.Where(e => e.DepartmentId == dept);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = $"%{q.Search.Trim()}%";
            people = people.Where(e => EF.Functions.ILike(e.FirstName + " " + e.LastName, s) || EF.Functions.ILike(e.EmployeeCode, s));
        }

        var rows = await people
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, Department = e.Department.Name,
                Designation = e.Designation.Title, e.JoiningDate })
            .ToListAsync(ct);
        var latest = await AckMath.LatestAsync(db, [policy.Id], null, ct);
        var today = Today;

        var list = rows.Select(r =>
        {
            var ack = latest.GetValueOrDefault((policy.Id, r.Id));
            var state = AckMath.StateOf(policy, ack, today);
            return new AckPersonDto(r.Id, r.Name, r.EmployeeCode, r.Department, r.Designation, r.JoiningDate, state, ack?.AcknowledgedAt, ack?.VersionNumber);
        });
        if (q.State is { } wanted)
            list = list.Where(p => p.State == wanted);
        return list.ToList();
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Policies + naam + (tracker ke liye) audience/acknowledged ginti + meri state.</summary>
    private async Task<List<PolicyListItemDto>> ListAsync(IQueryable<Policy> policies, ComplianceAccess.Me my, CancellationToken ct)
    {
        var list = await policies.OrderBy(p => p.Status).ThenBy(p => p.Category).ThenBy(p => p.Title).Take(500).ToListAsync(ct);
        if (list.Count == 0)
            return [];

        var ids = list.Select(p => p.Id).ToList();
        var deptIds = list.Where(p => p.DepartmentId != null).Select(p => p.DepartmentId!.Value).Distinct().ToList();
        var ownerIds = list.Where(p => p.OwnerEmployeeId != null).Select(p => p.OwnerEmployeeId!.Value).Distinct().ToList();
        var departments = await db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var owners = await db.Employees.AsNoTracking().Where(e => ownerIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName, ct);

        var track = access.CanTrack;
        var people = track ? await AckMath.ActivePeopleAsync(db, ct) : [];
        var latest = await AckMath.LatestAsync(db, ids, track ? null : my.EmployeeId ?? Guid.Empty, ct);
        var today = Today;

        return list.Select(p =>
        {
            int? audience = null, acknowledged = null, overdue = null;
            if (track && p.Status != PolicyStatus.Draft)
            {
                var states = people.Where(m => AckMath.Includes(p, m))
                    .Select(m => AckMath.StateOf(p, latest.GetValueOrDefault((p.Id, m.Id)), today)).ToList();
                audience = states.Count;
                acknowledged = states.Count(s => s == AckState.Done);
                overdue = states.Count(s => s == AckState.Overdue);
            }
            var inMine = ComplianceAccess.InAudience(p, my) && p.Status == PolicyStatus.Published;
            var mine = my.EmployeeId is { } meId ? latest.GetValueOrDefault((p.Id, meId)) : null;
            return new PolicyListItemDto(
                p.Id, p.Title, p.Category, p.Summary, p.Status, p.CurrentVersion, p.HasUnpublishedChanges, p.PublishedAt, p.EffectiveOn,
                p.DepartmentId, p.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null,
                p.OwnerEmployeeId, p.OwnerEmployeeId is { } o ? owners.GetValueOrDefault(o) : null,
                p.RequiresAcknowledgement, p.AcknowledgeWithinDays, p.ReviewEveryMonths, p.AckVersion, p.AckDueOn, p.NextReviewOn,
                p.IsReviewDue(today), audience, acknowledged, overdue,
                inMine, inMine ? AckMath.StateOf(p, mine, today) : AckState.NotRequired, mine?.AcknowledgedAt,
                p.UpdatedAt ?? p.CreatedAt, p.RowVersion);
        }).ToList();
    }
}

#endregion

#region Commands

public sealed class PolicyCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreatePolicyCommand, Guid>,
    IRequestHandler<UpdatePolicyCommand>,
    IRequestHandler<PublishPolicyCommand>,
    IRequestHandler<ArchivePolicyCommand>,
    IRequestHandler<RestorePolicyCommand>,
    IRequestHandler<DeletePolicyCommand>,
    IRequestHandler<MarkPolicyReviewedCommand>,
    IRequestHandler<AcknowledgePolicyCommand>,
    IRequestHandler<AddStarterPoliciesCommand, int>
{
    private readonly ComplianceAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreatePolicyCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var policy = Policy.Create(access.TenantId, await DetailsAsync(c.Data, null, ct));
        db.Policies.Add(policy);
        await SaveAsync(ct);
        return policy.Id;
    }

    public async Task Handle(UpdatePolicyCommand c, CancellationToken ct)
    {
        var policy = await ManagedAsync(c.Id, ct);
        policy.Update(await DetailsAsync(c.Data, policy, ct), Today);
        await SaveAsync(ct);
    }

    public async Task Handle(PublishPolicyCommand c, CancellationToken ct)
    {
        var policy = await ManagedAsync(c.Id, ct, withVersions: true);
        var version = policy.Publish(c.ChangeSummary, c.Reacknowledge, c.EffectiveOn, currentUser.UserId, DateTime.UtcNow);
        db.PolicyVersions.Add(version);
        await SaveAsync(ct);
    }

    public Task Handle(ArchivePolicyCommand c, CancellationToken ct) => ChangeAsync(c.Id, p => p.Archive(DateTime.UtcNow), ct);

    public Task Handle(RestorePolicyCommand c, CancellationToken ct) => ChangeAsync(c.Id, p => p.Restore(), ct);

    public Task Handle(MarkPolicyReviewedCommand c, CancellationToken ct) => ChangeAsync(c.Id, p => p.MarkReviewed(Today), ct);

    public async Task Handle(DeletePolicyCommand c, CancellationToken ct)
    {
        var policy = await ManagedAsync(c.Id, ct);
        if (policy.CurrentVersion > 0)
            throw new ConflictException("This policy has been published. Archive it instead of deleting it.");
        db.Policies.Remove(policy);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(AcknowledgePolicyCommand c, CancellationToken ct)
    {
        var my = await access.MeAsync(ct);
        var employeeId = my.EmployeeId ?? throw new DomainException("Your login is not linked to an employee record. Ask HR to link it.");
        var policy = await db.Policies.FirstOrDefaultAsync(p => p.Id == c.Id, ct) ?? throw new NotFoundException("Policy", c.Id);
        if (!ComplianceAccess.InAudience(policy, my) || policy.Status != PolicyStatus.Published)
            throw new NotFoundException("Policy", c.Id);
        if (await db.PolicyAcknowledgements.AnyAsync(a => a.PolicyId == policy.Id && a.EmployeeId == employeeId && a.VersionNumber == policy.CurrentVersion, ct))
            throw new ConflictException("You have already acknowledged this version.");

        db.PolicyAcknowledgements.Add(PolicyAcknowledgement.Create(policy, employeeId, currentUser.UserId, DateTime.UtcNow));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_PolicyAcknowledgements_Policy_Employee_Version") == true)
        {
            throw new ConflictException("You have already acknowledged this version.");
        }
    }

    public async Task<int> Handle(AddStarterPoliciesCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var existing = (await db.Policies.Select(p => p.Title).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var d in StarterPolicies.All.Where(d => !existing.Contains(d.Title)))
        {
            db.Policies.Add(Policy.Create(access.TenantId, d));
            added++;
        }
        await SaveAsync(ct);
        return added;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task ChangeAsync(Guid id, Action<Policy> change, CancellationToken ct)
    {
        var policy = await ManagedAsync(id, ct);
        change(policy);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Policy> ManagedAsync(Guid id, CancellationToken ct, bool withVersions = false)
    {
        access.EnsureCanManage();
        IQueryable<Policy> q = withVersions ? db.Policies.Include(p => p.Versions) : db.Policies;
        return await q.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Policy", id);
    }

    private async Task<PolicyDetails> DetailsAsync(SavePolicyRequest r, Policy? existing, CancellationToken ct)
    {
        if (r.DepartmentId is { } dept && dept != Guid.Empty && dept != existing?.DepartmentId
            && !await db.Departments.AnyAsync(d => d.Id == dept, ct))
            throw new DomainException("Pick a department from the list.");
        if (r.OwnerEmployeeId is { } owner && owner != Guid.Empty && owner != existing?.OwnerEmployeeId
            && !await db.Employees.AnyAsync(e => e.Id == owner && e.EmploymentStatus != EmploymentStatus.Exited, ct))
            throw new DomainException("Pick an active employee as the policy owner.");
        var title = (r.Title ?? "").Trim().ToLower();
        var existingId = existing?.Id;
        if (await db.Policies.AnyAsync(p => p.Id != existingId && p.Title.ToLower() == title, ct))
            throw new ConflictException("A policy with this title already exists.");
        return new PolicyDetails(r.Title!, r.Category, r.Summary, r.Body, r.DocumentUrl, r.DepartmentId, r.OwnerEmployeeId,
            r.RequiresAcknowledgement, r.AcknowledgeWithinDays, r.ReviewEveryMonths);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_Policies_TenantId_Title") == true)
        {
            throw new ConflictException("A policy with this title already exists.");
        }
    }
}

#endregion
