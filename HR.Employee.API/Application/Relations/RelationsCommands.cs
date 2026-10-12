namespace HR.Employee.API.Application.Relations;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Relations;
using MediatR;
using Microsoft.EntityFrameworkCore;

#region Requests

public sealed record CasePartyInput(Guid? EmployeeId, string? Name, CasePartyRole Role, string? Note);

public sealed record CreateCaseRequest(
    string Title, string? Description, CaseType Type, CaseSeverity Severity, CaseConfidentiality Confidentiality, CaseSource Source,
    DateOnly? ReportedOn, DateOnly? IncidentOn, string? Location, Guid? ReporterEmployeeId, Guid? OwnerEmployeeId, DateOnly? DueOn,
    IReadOnlyList<CasePartyInput>? Parties);

public sealed record UpdateCaseRequest(
    string Title, string? Description, CaseType Type, CaseSeverity Severity, CaseConfidentiality Confidentiality, DateOnly? IncidentOn, string? Location);

public sealed record RaiseConcernRequest(
    CaseType Type, string Title, string Description, DateOnly? IncidentOn, string? Location, IReadOnlyList<Guid>? RespondentIds,
    bool HideIdentity, string? EvidenceTitle, string? EvidenceUrl);

public sealed record CreateCaseCommand(CreateCaseRequest Data) : IRequest<Guid>;
public sealed record UpdateCaseCommand(Guid Id, UpdateCaseRequest Data) : IRequest;
public sealed record MoveCaseCommand(Guid Id, Guid StageId, string? Note) : IRequest;
public sealed record CloseCaseCommand(Guid Id, Guid? StageId, CaseOutcome Outcome, CaseAction Action, string Summary) : IRequest;
public sealed record ReopenCaseCommand(Guid Id, Guid? StageId, string Reason) : IRequest;
public sealed record SetCaseDeadlineCommand(Guid Id, DateOnly? DueOn, string? Reason) : IRequest;
public sealed record EscalateCaseCommand(Guid Id, string Reason, Guid? ToEmployeeId) : IRequest;
public sealed record TakeCaseCommand(Guid Id) : IRequest;
public sealed record ChangeCaseOwnerCommand(Guid Id, Guid EmployeeId) : IRequest;
public sealed record AddCaseMemberCommand(Guid Id, Guid EmployeeId, CaseMemberRole Role) : IRequest;
public sealed record RemoveCaseMemberCommand(Guid Id, Guid EmployeeId) : IRequest;
public sealed record AddCasePartyCommand(Guid Id, CasePartyInput Data) : IRequest<Guid>;
public sealed record RemoveCasePartyCommand(Guid Id, Guid PartyId) : IRequest;
public sealed record AddCaseNoteCommand(Guid Id, string Text, bool Shared) : IRequest;
public sealed record AddCaseEvidenceCommand(Guid Id, string Title, string Url, string? Note, bool Restricted) : IRequest<Guid>;
public sealed record RemoveCaseEvidenceCommand(Guid Id, Guid EvidenceId) : IRequest;
public sealed record BreakGlassCommand(Guid Id, string Reason) : IRequest;

public sealed record RaiseConcernCommand(RaiseConcernRequest Data) : IRequest<Guid>;
public sealed record ConcernMessageCommand(Guid Id, string Text) : IRequest;
public sealed record ConcernEvidenceCommand(Guid Id, string Title, string Url, string? Note) : IRequest<Guid>;

public sealed class CreateCaseValidator : AbstractValidator<CreateCaseCommand>
{
    public CreateCaseValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Data.Description).MaximumLength(4000);
        RuleFor(x => x.Data.Location).MaximumLength(200);
        RuleFor(x => x.Data.Type).IsInEnum();
        RuleFor(x => x.Data.Severity).IsInEnum();
        RuleFor(x => x.Data.Confidentiality).IsInEnum();
        RuleFor(x => x.Data.Source).IsInEnum();
        RuleFor(x => x.Data.Parties).Must(p => p is null || p.Count <= ErCase.MaxParties);
    }
}

public sealed class RaiseConcernValidator : AbstractValidator<RaiseConcernCommand>
{
    public RaiseConcernValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Type).IsInEnum();
        RuleFor(x => x.Data.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Data.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Data.Location).MaximumLength(200);
        RuleFor(x => x.Data.RespondentIds).Must(r => r is null || r.Count <= 10).WithMessage("Name at most 10 people.");
        RuleFor(x => x.Data.EvidenceUrl).MaximumLength(1000);
    }
}

public sealed class AddCaseNoteValidator : AbstractValidator<AddCaseNoteCommand>
{
    public AddCaseNoteValidator() => RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
}

public sealed class ConcernMessageValidator : AbstractValidator<ConcernMessageCommand>
{
    public ConcernMessageValidator() => RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
}

#endregion

public sealed class RelationsCaseCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateCaseCommand, Guid>,
    IRequestHandler<UpdateCaseCommand>,
    IRequestHandler<MoveCaseCommand>,
    IRequestHandler<CloseCaseCommand>,
    IRequestHandler<ReopenCaseCommand>,
    IRequestHandler<SetCaseDeadlineCommand>,
    IRequestHandler<EscalateCaseCommand>,
    IRequestHandler<TakeCaseCommand>,
    IRequestHandler<ChangeCaseOwnerCommand>,
    IRequestHandler<AddCaseMemberCommand>,
    IRequestHandler<RemoveCaseMemberCommand>,
    IRequestHandler<AddCasePartyCommand, Guid>,
    IRequestHandler<RemoveCasePartyCommand>,
    IRequestHandler<AddCaseNoteCommand>,
    IRequestHandler<AddCaseEvidenceCommand, Guid>,
    IRequestHandler<RemoveCaseEvidenceCommand>,
    IRequestHandler<BreakGlassCommand>,
    IRequestHandler<RaiseConcernCommand, Guid>,
    IRequestHandler<ConcernMessageCommand>,
    IRequestHandler<ConcernEvidenceCommand, Guid>
{
    private readonly RelationsAccess access = new(db, currentUser);

    private Guid? By => currentUser.UserId;

    public async Task<Guid> Handle(CreateCaseCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var me = await access.RequireMeAsync(ct);
        var d = c.Data;
        var today = RelationsClock.Today;
        var now = DateTime.UtcNow;

        if (d.ReporterEmployeeId is { } reporterId)
            await PersonAsync(reporterId, ct);
        var ownerId = d.OwnerEmployeeId is { } o && o != Guid.Empty ? o : me;
        var ownerName = (await PersonAsync(ownerId, ct, active: true)).Name;

        var stage = await FirstStageAsync(ct);
        var entity = ErCase.Create(
            access.TenantId, await NextCodeAsync(ct),
            new CaseDetails(d.Title, d.Description, d.Type, d.Severity, d.Confidentiality, d.IncidentOn, d.Location),
            d.Source, stage, d.ReportedOn ?? today, d.ReporterEmployeeId, false, By, today, now);

        foreach (var p in d.Parties ?? [])
            await AddPartyAsync(entity, p, now, ct);
        if (d.ReporterEmployeeId is { } rid && !entity.Parties.Any(p => p.EmployeeId == rid))
            entity.AddParty(rid, null, (await PersonAsync(rid, ct)).Name, CasePartyRole.Complainant, null, By, now);
        entity.ChangeOwner(ownerId, ownerName, By, now);
        if (d.DueOn is not null)
            entity.SetDeadline(d.DueOn, null, By, today, now);

        db.ErCases.Add(entity);
        await SaveNewAsync(ct);
        return entity.Id;
    }

    public async Task Handle(UpdateCaseCommand c, CancellationToken ct)
    {
        var (entity, _, view) = await access.WorkAsync(c.Id, ct);
        var d = c.Data;
        if (d.Confidentiality != entity.Confidentiality && view != CaseView.Owner)
            throw new UnauthorizedAccessException("Only the case owner can change who can see the case.");
        entity.Update(new CaseDetails(d.Title, d.Description, d.Type, d.Severity, d.Confidentiality, d.IncidentOn, d.Location),
            By, RelationsClock.Today, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(MoveCaseCommand c, CancellationToken ct)
    {
        var (entity, _, _) = await access.WorkAsync(c.Id, ct);
        var stage = await StageAsync(c.StageId, ct);
        entity.MoveTo(stage, Optional(c.Note, 1000), By, RelationsClock.Today, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CloseCaseCommand c, CancellationToken ct)
    {
        var (entity, _, _) = await access.WorkAsync(c.Id, ct, ownerOnly: true);
        var stage = c.StageId is { } sid
            ? await StageAsync(sid, ct)
            : await db.CaseStages.Where(s => s.Phase == CasePhase.Closed && s.IsActive).OrderBy(s => s.SortOrder).FirstOrDefaultAsync(ct)
              ?? throw new DomainException("Add a closed stage first (Stages tab).");
        entity.Close(stage, c.Outcome, c.Action, c.Summary, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ReopenCaseCommand c, CancellationToken ct)
    {
        var entity = await access.LoadAsync(c.Id, ct);
        var my = await access.MeAsync(ct);
        var view = access.ViewOf(entity, my);
        if (view == CaseView.None)
            throw new NotFoundException("Case", c.Id);
        if (!(view == CaseView.Owner || (access.IsAdmin && view >= CaseView.Viewer)))
            throw new UnauthorizedAccessException("Only the case owner can reopen the case.");
        var stage = c.StageId is { } sid
            ? await StageAsync(sid, ct)
            : await db.CaseStages.Where(s => s.Phase == CasePhase.Review && s.IsActive).OrderBy(s => s.SortOrder).FirstOrDefaultAsync(ct)
              ?? await FirstStageAsync(ct);
        entity.Reopen(stage, c.Reason, By, RelationsClock.Today, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SetCaseDeadlineCommand c, CancellationToken ct)
    {
        var (entity, _, _) = await access.WorkAsync(c.Id, ct);
        entity.SetDeadline(c.DueOn, c.Reason, By, RelationsClock.Today, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(EscalateCaseCommand c, CancellationToken ct)
    {
        var (entity, _, _) = await access.WorkAsync(c.Id, ct);
        var now = DateTime.UtcNow;
        string? toName = null;
        if (c.ToEmployeeId is { } to && to != Guid.Empty)
        {
            toName = (await PersonAsync(to, ct, active: true)).Name;
            if (entity.RoleOf(to) is null or CaseMemberRole.Viewer)
                entity.AddMember(to, toName, CaseMemberRole.Investigator, By, now);
        }
        entity.Escalate(c.Reason, toName, By, now);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Bina owner wala case relations team ka koi bhi le sakta hai (restricted bhi; access log mein).</summary>
    public async Task Handle(TakeCaseCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var entity = await access.LoadAsync(c.Id, ct);
        var me = await access.RequireMeAsync(ct);
        if (access.ViewOf(entity, me) == CaseView.None)
            throw new NotFoundException("Case", c.Id);
        if (entity.OwnerEmployeeId is not null)
            throw new DomainException("Someone already owns this case.");
        var now = DateTime.UtcNow;
        entity.ChangeOwner(me, (await PersonAsync(me, ct)).Name, By, now);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ChangeCaseOwnerCommand c, CancellationToken ct)
    {
        var entity = await TeamManagerCaseAsync(c.Id, ct);
        entity.ChangeOwner(c.EmployeeId, (await PersonAsync(c.EmployeeId, ct, active: true)).Name, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(AddCaseMemberCommand c, CancellationToken ct)
    {
        var entity = await TeamManagerCaseAsync(c.Id, ct);
        entity.AddMember(c.EmployeeId, (await PersonAsync(c.EmployeeId, ct, active: true)).Name, c.Role, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RemoveCaseMemberCommand c, CancellationToken ct)
    {
        var entity = await TeamManagerCaseAsync(c.Id, ct);
        var name = await db.Employees.AsNoTracking().IgnoreQueryFilters().Where(e => e.Id == c.EmployeeId && e.TenantId == access.TenantId)
            .Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync(ct) ?? "—";
        entity.RemoveMember(c.EmployeeId, name, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> Handle(AddCasePartyCommand c, CancellationToken ct)
    {
        var (entity, _, _) = await access.WorkAsync(c.Id, ct);
        var party = await AddPartyAsync(entity, c.Data, DateTime.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return party.Id;
    }

    public async Task Handle(RemoveCasePartyCommand c, CancellationToken ct)
    {
        var (entity, _, _) = await access.WorkAsync(c.Id, ct);
        var party = entity.Parties.FirstOrDefault(p => p.Id == c.PartyId) ?? throw new NotFoundException("Person", c.PartyId);
        var name = party.ExternalName ?? (party.EmployeeId is { } eid
            ? await db.Employees.AsNoTracking().IgnoreQueryFilters().Where(e => e.Id == eid && e.TenantId == access.TenantId)
                .Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync(ct)
            : null) ?? "—";
        entity.RemoveParty(c.PartyId, name, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(AddCaseNoteCommand c, CancellationToken ct)
    {
        var (entity, _, _) = await access.WorkAsync(c.Id, ct);
        entity.AddNote(c.Text, c.Shared, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> Handle(AddCaseEvidenceCommand c, CancellationToken ct)
    {
        var (entity, _, _) = await access.WorkAsync(c.Id, ct);
        var item = entity.AddEvidence(c.Title, c.Url, c.Note, c.Restricted, false, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }

    public async Task Handle(RemoveCaseEvidenceCommand c, CancellationToken ct)
    {
        var (entity, _, _) = await access.WorkAsync(c.Id, ct, ownerOnly: true);
        entity.RemoveEvidence(c.EvidenceId, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Admin restricted case khole: viewer ban jata hai, wajah timeline + access log mein.</summary>
    public async Task Handle(BreakGlassCommand c, CancellationToken ct)
    {
        if (!access.IsAdmin)
            throw new UnauthorizedAccessException("Only an admin with employee relations access can open a restricted case this way.");
        var entity = await access.LoadAsync(c.Id, ct);
        var me = await access.RequireMeAsync(ct);
        if (access.ViewOf(entity, me) != CaseView.Masked)
            throw new DomainException("You can already open this case, or you are involved in it.");
        var now = DateTime.UtcNow;
        entity.BreakGlass(me, (await PersonAsync(me, ct)).Name, c.Reason, By, now);
        db.CaseAccessLogs.Add(new CaseAccessLog(entity.Id, By, me, access.UserName, CaseAccessAction.BreakGlass, c.Reason.Trim(), now));
        await db.SaveChangesAsync(ct);
    }

    // ---- Reporter (self-service) ----

    public async Task<Guid> Handle(RaiseConcernCommand c, CancellationToken ct)
    {
        var me = await access.RequireMeAsync(ct);
        var d = c.Data;
        var today = RelationsClock.Today;
        var now = DateTime.UtcNow;
        var (severity, confidentiality) = CaseDetails.DefaultsFor(d.Type);

        var stage = await FirstStageAsync(ct);
        var entity = ErCase.Create(
            access.TenantId, await NextCodeAsync(ct),
            new CaseDetails(d.Title, d.Description, d.Type, severity, confidentiality, d.IncidentOn, d.Location),
            CaseSource.Employee, stage, today, me, d.HideIdentity, By, today, now);

        if (!d.HideIdentity)
            entity.AddParty(me, null, (await PersonAsync(me, ct)).Name, CasePartyRole.Complainant, null, By, now);
        foreach (var rid in (d.RespondentIds ?? []).Distinct())
        {
            if (rid == me)
                throw new DomainException("You cannot name yourself as the person the concern is about.");
            entity.AddParty(rid, null, (await PersonAsync(rid, ct)).Name, CasePartyRole.Respondent, null, By, now);
        }
        if (!string.IsNullOrWhiteSpace(d.EvidenceUrl))
            entity.AddEvidence(string.IsNullOrWhiteSpace(d.EvidenceTitle) ? "Evidence" : d.EvidenceTitle, d.EvidenceUrl, null, true, true, By, now);

        db.ErCases.Add(entity);
        await SaveNewAsync(ct);
        return entity.Id;
    }

    public async Task Handle(ConcernMessageCommand c, CancellationToken ct)
    {
        var entity = await ReporterCaseAsync(c.Id, ct);
        entity.AddReporterMessage(c.Text, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> Handle(ConcernEvidenceCommand c, CancellationToken ct)
    {
        var entity = await ReporterCaseAsync(c.Id, ct);
        var item = entity.AddEvidence(c.Title, c.Url, c.Note, true, true, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }

    // ---- Helpers ----

    private async Task<ErCase> ReporterCaseAsync(Guid id, CancellationToken ct)
    {
        var me = await access.RequireMeAsync(ct);
        var entity = await access.LoadAsync(id, ct);
        if (entity.ReporterEmployeeId != me)
            throw new NotFoundException("Concern", id);
        return entity;
    }

    /// <summary>Members badalna: owner, ya admin jo case par ho (e.g. break-glass ke baad naya owner lagana).</summary>
    private async Task<ErCase> TeamManagerCaseAsync(Guid id, CancellationToken ct)
    {
        var entity = await access.LoadAsync(id, ct);
        var my = await access.MeAsync(ct);
        var view = access.ViewOf(entity, my);
        if (view == CaseView.None)
            throw new NotFoundException("Case", id);
        if (!(view == CaseView.Owner || (access.IsAdmin && view >= CaseView.Viewer)))
            throw new UnauthorizedAccessException("Only the case owner can change who works on the case.");
        return entity;
    }

    private async Task<CaseParty> AddPartyAsync(ErCase entity, CasePartyInput p, DateTime now, CancellationToken ct)
    {
        if (p.EmployeeId is { } eid && eid != Guid.Empty)
            return entity.AddParty(eid, null, (await PersonAsync(eid, ct)).Name, p.Role, p.Note, By, now);
        return entity.AddParty(null, p.Name, p.Name?.Trim() ?? "", p.Role, p.Note, By, now);
    }

    private sealed record PersonInfo(string Name);

    /// <summary>active = case par kaam karne wala (chhor chuka shakhs nahi). Parties ke liye chhor chuka bhi chalega.</summary>
    private async Task<PersonInfo> PersonAsync(Guid id, CancellationToken ct, bool active = false)
    {
        var row = await db.Employees.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new { Name = e.FirstName + " " + e.LastName, e.EmploymentStatus }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Employee", id);
        if (active && row.EmploymentStatus == EmploymentStatus.Exited)
            throw new DomainException($"{row.Name} has left the company and cannot work on cases.");
        return new PersonInfo(row.Name);
    }

    private async Task<CaseStage> StageAsync(Guid id, CancellationToken ct)
        => await db.CaseStages.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Stage", id);

    /// <summary>Pehla khula stage. Company ke stages na hon to default ban jate hain.</summary>
    private async Task<CaseStage> FirstStageAsync(CancellationToken ct)
    {
        await RelationsStageDefaults.EnsureAsync(db, access.TenantId, ct);
        return await db.CaseStages.Where(s => s.IsActive && s.Phase != CasePhase.Closed)
                   .OrderBy(s => s.Phase).ThenBy(s => s.SortOrder).FirstOrDefaultAsync(ct)
               ?? throw new DomainException("All case stages are switched off. Turn one on in the Stages tab.");
    }

    /// <summary>ER-0001... (deleted bhi gino taake code dobara na mile).</summary>
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var tenantId = access.TenantId;
        var codes = await db.ErCases.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Code.StartsWith(ErCase.CodePrefix))
            .Select(c => c.Code).ToListAsync(ct);
        var max = codes.Select(x => int.TryParse(x[ErCase.CodePrefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{ErCase.CodePrefix}{max + 1:D4}";
    }

    private async Task SaveNewAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_ErCases_TenantId_Code") == true)
        {
            throw new ConflictException("Someone opened a case at the same moment. Try again.");
        }
    }

    private static string? Optional(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim() is { } v && v.Length > max ? throw new DomainException($"Note cannot exceed {max} characters.") : value.Trim();
}

internal static class RelationsStageDefaults
{
    /// <summary>Company ka koi stage na ho to default 5 bana do (pehle case ya stages tab se).</summary>
    public static async Task<int> EnsureAsync(IAppDbContext db, Guid tenantId, CancellationToken ct, bool addMissing = false)
    {
        var existing = await db.CaseStages.Select(s => s.Name).ToListAsync(ct);
        if (existing.Count > 0 && !addMissing)
            return 0;
        var added = 0;
        foreach (var d in CaseStage.Defaults.Where(d => !existing.Contains(d.Name, StringComparer.OrdinalIgnoreCase)))
        {
            db.CaseStages.Add(CaseStage.Create(tenantId, d));
            added++;
        }
        if (added > 0)
            await db.SaveChangesAsync(ct);
        return added;
    }
}
