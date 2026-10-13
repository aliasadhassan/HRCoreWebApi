namespace HR.Employee.API.Application.Safety;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Safety;
using MediatR;
using Microsoft.EntityFrameworkCore;

#region Requests

public sealed record IncidentRequest(
    string Title, string? Description, IncidentType Type, SafetySeverity Severity, DateTime OccurredAt,
    Guid? LocationId, string? Area, Guid? DepartmentId,
    Guid? AffectedEmployeeId, string? AffectedName, IncidentTreatment? Treatment, string? InjuryNature, string? BodyPart,
    short LostDays, string? Witnesses, string? ImmediateAction, bool IsReportable,
    Guid? ReporterEmployeeId = null, Guid? InvestigatorEmployeeId = null);

/// <summary>Employee ki apni report (self-service). Treatment / lost days safety team baad mein bharti hai.</summary>
public sealed record ReportIncidentRequest(
    string Title, string? Description, IncidentType Type, SafetySeverity Severity, DateTime OccurredAt,
    Guid? LocationId, string? Area, Guid? AffectedEmployeeId, string? AffectedName, string? InjuryNature, string? BodyPart,
    string? Witnesses, string? ImmediateAction, bool Anonymous);

public sealed record ActionRequest(
    string Title, string? Description, Guid AssigneeEmployeeId, SafetySeverity Priority, DateOnly DueOn,
    Guid? IncidentId = null, Guid? InspectionId = null);

public sealed record ChecklistRequest(string Name, string? Description, bool IsActive, IReadOnlyList<string> Items);

public sealed record InspectionRequest(
    string Title, Guid ChecklistId, Guid InspectorEmployeeId, Guid? LocationId, string? Area, DateOnly DueOn, short? RepeatDays);

public sealed record FailedLineAction(Guid AssigneeEmployeeId, SafetySeverity Priority, DateOnly DueOn, string? Title);

public sealed record InspectionAnswer(Guid LineId, InspectionResult Result, string? Note, FailedLineAction? Action);

public sealed record CompleteInspectionRequest(IReadOnlyList<InspectionAnswer> Answers, string? Notes);

public sealed record CompleteInspectionResult(int ActionsCreated, Guid? NextInspectionId, string? NextInspectionCode, DateOnly? NextDueOn);

public sealed record CreateIncidentCommand(IncidentRequest Data) : IRequest<Guid>;
public sealed record ReportIncidentCommand(ReportIncidentRequest Data) : IRequest<Guid>;
public sealed record UpdateIncidentCommand(Guid Id, IncidentRequest Data) : IRequest;
public sealed record MoveIncidentCommand(Guid Id, IncidentStatus Status, string? Note) : IRequest;
public sealed record AssignInvestigatorCommand(Guid Id, Guid? EmployeeId) : IRequest;
public sealed record SetRootCauseCommand(Guid Id, RootCauseCategory? Category, string? RootCause) : IRequest;
public sealed record NotifyAuthorityCommand(Guid Id, DateOnly NotifiedOn, string? Reference) : IRequest;
public sealed record AddIncidentNoteCommand(Guid Id, string Text) : IRequest;
public sealed record CloseIncidentCommand(Guid Id, string Note) : IRequest;
public sealed record ReopenIncidentCommand(Guid Id, string Reason) : IRequest;

public sealed record CreateSafetyActionCommand(ActionRequest Data) : IRequest<Guid>;
public sealed record UpdateSafetyActionCommand(Guid Id, ActionRequest Data) : IRequest;
public sealed record StartSafetyActionCommand(Guid Id) : IRequest;
public sealed record CompleteSafetyActionCommand(Guid Id, string Note) : IRequest;
public sealed record VerifySafetyActionCommand(Guid Id, bool Accepted, string? Note) : IRequest;
public sealed record CancelSafetyActionCommand(Guid Id, string? Reason) : IRequest;

public sealed record CreateChecklistCommand(ChecklistRequest Data) : IRequest<Guid>;
public sealed record UpdateChecklistCommand(Guid Id, ChecklistRequest Data) : IRequest;
public sealed record DeleteChecklistCommand(Guid Id) : IRequest;
public sealed record AddSampleChecklistsCommand : IRequest<int>;

public sealed record ScheduleInspectionCommand(InspectionRequest Data) : IRequest<Guid>;
public sealed record RescheduleInspectionCommand(Guid Id, InspectionRequest Data) : IRequest;
public sealed record CancelInspectionCommand(Guid Id, string? Reason) : IRequest;
public sealed record CompleteInspectionCommand(Guid Id, CompleteInspectionRequest Data) : IRequest<CompleteInspectionResult>;

public sealed class IncidentRequestValidator : AbstractValidator<IncidentRequest>
{
    public IncidentRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Severity).IsInEnum();
        RuleFor(x => x.Treatment).IsInEnum().When(x => x.Treatment is not null);
        RuleFor(x => x.Area).MaximumLength(150);
        RuleFor(x => x.AffectedName).MaximumLength(120);
        RuleFor(x => x.InjuryNature).MaximumLength(120);
        RuleFor(x => x.BodyPart).MaximumLength(120);
        RuleFor(x => x.LostDays).InclusiveBetween((short)0, SafetyIncident.MaxLostDays);
        RuleFor(x => x.Witnesses).MaximumLength(500);
        RuleFor(x => x.ImmediateAction).MaximumLength(2000);
    }
}

public sealed class CreateIncidentValidator : AbstractValidator<CreateIncidentCommand>
{
    public CreateIncidentValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new IncidentRequestValidator());
}

public sealed class UpdateIncidentValidator : AbstractValidator<UpdateIncidentCommand>
{
    public UpdateIncidentValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new IncidentRequestValidator());
}

public sealed class ReportIncidentValidator : AbstractValidator<ReportIncidentCommand>
{
    public ReportIncidentValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Data.Description).MaximumLength(4000);
        RuleFor(x => x.Data.Type).IsInEnum();
        RuleFor(x => x.Data.Severity).IsInEnum();
        RuleFor(x => x.Data.Area).MaximumLength(150);
        RuleFor(x => x.Data.AffectedName).MaximumLength(120);
        RuleFor(x => x.Data.InjuryNature).MaximumLength(120);
        RuleFor(x => x.Data.BodyPart).MaximumLength(120);
        RuleFor(x => x.Data.Witnesses).MaximumLength(500);
        RuleFor(x => x.Data.ImmediateAction).MaximumLength(2000);
    }
}

public sealed class AddIncidentNoteValidator : AbstractValidator<AddIncidentNoteCommand>
{
    public AddIncidentNoteValidator() => RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
}

public sealed class CloseIncidentValidator : AbstractValidator<CloseIncidentCommand>
{
    public CloseIncidentValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
}

public sealed class ActionRequestValidator : AbstractValidator<ActionRequest>
{
    public ActionRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.AssigneeEmployeeId).NotEmpty();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class CreateSafetyActionValidator : AbstractValidator<CreateSafetyActionCommand>
{
    public CreateSafetyActionValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new ActionRequestValidator());
}

public sealed class UpdateSafetyActionValidator : AbstractValidator<UpdateSafetyActionCommand>
{
    public UpdateSafetyActionValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new ActionRequestValidator());
}

public sealed class CompleteSafetyActionValidator : AbstractValidator<CompleteSafetyActionCommand>
{
    public CompleteSafetyActionValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
}

public sealed class ChecklistRequestValidator : AbstractValidator<ChecklistRequest>
{
    public ChecklistRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Items).NotNull().Must(i => i.Count is > 0 and <= SafetyChecklist.MaxItems)
            .WithMessage($"Add 1 to {SafetyChecklist.MaxItems} checklist items.");
        RuleForEach(x => x.Items).NotEmpty().MaximumLength(300);
    }
}

public sealed class CreateChecklistValidator : AbstractValidator<CreateChecklistCommand>
{
    public CreateChecklistValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new ChecklistRequestValidator());
}

public sealed class UpdateChecklistValidator : AbstractValidator<UpdateChecklistCommand>
{
    public UpdateChecklistValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new ChecklistRequestValidator());
}

public sealed class InspectionRequestValidator : AbstractValidator<InspectionRequest>
{
    public InspectionRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ChecklistId).NotEmpty();
        RuleFor(x => x.InspectorEmployeeId).NotEmpty();
        RuleFor(x => x.Area).MaximumLength(150);
        RuleFor(x => x.RepeatDays).InclusiveBetween((short)7, (short)365).When(x => x.RepeatDays is not null);
    }
}

public sealed class ScheduleInspectionValidator : AbstractValidator<ScheduleInspectionCommand>
{
    public ScheduleInspectionValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new InspectionRequestValidator());
}

public sealed class RescheduleInspectionValidator : AbstractValidator<RescheduleInspectionCommand>
{
    public RescheduleInspectionValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new InspectionRequestValidator());
}

public sealed class CompleteInspectionValidator : AbstractValidator<CompleteInspectionCommand>
{
    public CompleteInspectionValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Answers).NotNull().Must(a => a.Count <= SafetyChecklist.MaxItems);
        RuleFor(x => x.Data.Notes).MaximumLength(2000);
        RuleForEach(x => x.Data.Answers).ChildRules(a =>
        {
            a.RuleFor(x => x.Result).IsInEnum();
            a.RuleFor(x => x.Note).MaximumLength(500);
            a.RuleFor(x => x.Action!.Title).MaximumLength(200).When(x => x.Action is not null);
            a.RuleFor(x => x.Action!.Priority).IsInEnum().When(x => x.Action is not null);
        });
    }
}

#endregion

/// <summary>Incidents: safety team (safety.manage) sab; investigator apne incident par kaam; har employee report.</summary>
public sealed class SafetyIncidentCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateIncidentCommand, Guid>,
    IRequestHandler<ReportIncidentCommand, Guid>,
    IRequestHandler<UpdateIncidentCommand>,
    IRequestHandler<MoveIncidentCommand>,
    IRequestHandler<AssignInvestigatorCommand>,
    IRequestHandler<SetRootCauseCommand>,
    IRequestHandler<NotifyAuthorityCommand>,
    IRequestHandler<AddIncidentNoteCommand>,
    IRequestHandler<CloseIncidentCommand>,
    IRequestHandler<ReopenIncidentCommand>
{
    private readonly SafetyAccess access = new(db, currentUser);
    private readonly SafetyRefs refs = new(db);

    private Guid? By => currentUser.UserId;

    public async Task<Guid> Handle(CreateIncidentCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var d = c.Data;
        var now = DateTime.UtcNow;
        await refs.CheckIncidentAsync(d.LocationId, d.DepartmentId, d.AffectedEmployeeId, ct);
        if (d.ReporterEmployeeId is { } rid && rid != Guid.Empty)
            await refs.PersonAsync(rid, ct);

        var entity = SafetyIncident.Create(access.TenantId, await refs.NextIncidentCodeAsync(access.TenantId, ct), Details(d),
            d.ReporterEmployeeId == Guid.Empty ? null : d.ReporterEmployeeId, false, By, now);
        if (d.InvestigatorEmployeeId is { } inv && inv != Guid.Empty)
            entity.AssignInvestigator(inv, await refs.PersonAsync(inv, ct, active: true), By, now);

        db.SafetyIncidents.Add(entity);
        await SafetyRefs.SaveNewAsync(db, "UX_HsIncidents_TenantId_Code", ct);
        return entity.Id;
    }

    public async Task<Guid> Handle(ReportIncidentCommand c, CancellationToken ct)
    {
        var me = await access.RequireMeAsync(ct);
        var d = c.Data;
        await refs.CheckIncidentAsync(d.LocationId, null, d.AffectedEmployeeId, ct);
        // Anonymous report par department nahi lagate (pehchan ka ishara ho sakta hai)
        Guid? department = d.Anonymous ? null
            : await db.Employees.AsNoTracking().Where(e => e.Id == me).Select(e => e.DepartmentId).FirstOrDefaultAsync(ct);
        var details = new IncidentDetails(d.Title, d.Description, d.Type, d.Severity, d.OccurredAt, d.LocationId, d.Area, department,
            d.AffectedEmployeeId, d.AffectedName, null, d.InjuryNature, d.BodyPart, 0, d.Witnesses, d.ImmediateAction, false);
        var entity = SafetyIncident.Create(access.TenantId, await refs.NextIncidentCodeAsync(access.TenantId, ct), details,
            me, d.Anonymous, d.Anonymous ? null : By, DateTime.UtcNow);
        db.SafetyIncidents.Add(entity);
        await SafetyRefs.SaveNewAsync(db, "UX_HsIncidents_TenantId_Code", ct);
        return entity.Id;
    }

    public async Task Handle(UpdateIncidentCommand c, CancellationToken ct)
    {
        var entity = await access.WorkIncidentAsync(c.Id, ct, manageOnly: true);
        var d = c.Data;
        await refs.CheckIncidentAsync(d.LocationId, d.DepartmentId, d.AffectedEmployeeId, ct);
        entity.Update(Details(d), By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(MoveIncidentCommand c, CancellationToken ct)
    {
        var entity = await access.WorkIncidentAsync(c.Id, ct);
        entity.MoveTo(c.Status, c.Note, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(AssignInvestigatorCommand c, CancellationToken ct)
    {
        var entity = await access.WorkIncidentAsync(c.Id, ct, manageOnly: true);
        var id = c.EmployeeId == Guid.Empty ? null : c.EmployeeId;
        var name = id is { } eid ? await refs.PersonAsync(eid, ct, active: true) : null;
        entity.AssignInvestigator(id, name, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SetRootCauseCommand c, CancellationToken ct)
    {
        var entity = await access.WorkIncidentAsync(c.Id, ct);
        entity.SetRootCause(c.Category, c.RootCause, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(NotifyAuthorityCommand c, CancellationToken ct)
    {
        var entity = await access.WorkIncidentAsync(c.Id, ct);
        entity.NotifyAuthority(c.NotifiedOn, c.Reference, By, SafetyClock.Today, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(AddIncidentNoteCommand c, CancellationToken ct)
    {
        var entity = await access.WorkIncidentAsync(c.Id, ct);
        entity.AddNote(c.Text, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CloseIncidentCommand c, CancellationToken ct)
    {
        var entity = await access.WorkIncidentAsync(c.Id, ct, manageOnly: true);
        var open = await db.SafetyActions.CountAsync(
            a => a.IncidentId == entity.Id && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress), ct);
        entity.Close(c.Note, open, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ReopenIncidentCommand c, CancellationToken ct)
    {
        var entity = await access.WorkIncidentAsync(c.Id, ct, manageOnly: true);
        entity.Reopen(c.Reason, By, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private static IncidentDetails Details(IncidentRequest d) => new(
        d.Title, d.Description, d.Type, d.Severity, d.OccurredAt, d.LocationId, d.Area, d.DepartmentId,
        d.AffectedEmployeeId, d.AffectedName, d.Treatment, d.InjuryNature, d.BodyPart, d.LostDays, d.Witnesses, d.ImmediateAction,
        d.IsReportable);
}

/// <summary>
/// Corrective actions. Banana / badalna / cancel: safety team ya us incident ka investigator (inspection ho to uska inspector).
/// Shuru / mukammal: assignee ya safety team. Verify: sirf safety team.
/// </summary>
public sealed class SafetyActionCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateSafetyActionCommand, Guid>,
    IRequestHandler<UpdateSafetyActionCommand>,
    IRequestHandler<StartSafetyActionCommand>,
    IRequestHandler<CompleteSafetyActionCommand>,
    IRequestHandler<VerifySafetyActionCommand>,
    IRequestHandler<CancelSafetyActionCommand>
{
    private readonly SafetyAccess access = new(db, currentUser);
    private readonly SafetyRefs refs = new(db);

    private Guid? By => currentUser.UserId;

    public async Task<Guid> Handle(CreateSafetyActionCommand c, CancellationToken ct)
    {
        var d = c.Data;
        var now = DateTime.UtcNow;
        SafetyIncident? incident = null;
        if (d.IncidentId is { } iid && iid != Guid.Empty)
        {
            incident = await access.WorkIncidentAsync(iid, ct);
            if (incident.IsClosed)
                throw new DomainException("This incident is closed. Reopen it to add actions.");
        }
        else if (d.InspectionId is { } sid && sid != Guid.Empty)
            await InspectionForActionAsync(sid, ct);
        else
            access.EnsureCanManage();

        await refs.PersonAsync(d.AssigneeEmployeeId, ct, active: true);
        var action = SafetyAction.Create(access.TenantId,
            new ActionDetails(d.Title, d.Description, d.AssigneeEmployeeId, d.Priority, d.DueOn),
            incident?.Id, incident is null ? d.InspectionId : null, SafetyClock.Today);
        db.SafetyActions.Add(action);
        if (incident is not null)
        {
            incident.LogAction(IncidentEntryKind.ActionAdded, action.Title, By, now);
            if (incident.Status == IncidentStatus.Investigating)
                incident.MoveTo(IncidentStatus.ActionPending, null, By, now);
        }
        await db.SaveChangesAsync(ct);
        return action.Id;
    }

    public async Task Handle(UpdateSafetyActionCommand c, CancellationToken ct)
    {
        var action = await OwnActionAsync(c.Id, ct);
        var d = c.Data;
        if (d.AssigneeEmployeeId != action.AssigneeEmployeeId)
            await refs.PersonAsync(d.AssigneeEmployeeId, ct, active: true);
        action.Update(new ActionDetails(d.Title, d.Description, d.AssigneeEmployeeId, d.Priority, d.DueOn), SafetyClock.Today);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(StartSafetyActionCommand c, CancellationToken ct)
    {
        var action = await AssignedActionAsync(c.Id, ct);
        action.Start();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CompleteSafetyActionCommand c, CancellationToken ct)
    {
        var action = await AssignedActionAsync(c.Id, ct);
        var now = DateTime.UtcNow;
        action.Complete(c.Note, now);
        if (action.IncidentId is { } iid && await db.SafetyIncidents.FirstOrDefaultAsync(i => i.Id == iid, ct) is { IsClosed: false } incident)
            incident.LogAction(IncidentEntryKind.ActionCompleted, action.Title, By, now);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(VerifySafetyActionCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var action = await LoadAsync(c.Id, ct);
        var now = DateTime.UtcNow;
        action.Verify(c.Accepted, By, now);
        if (!c.Accepted && action.IncidentId is { } iid
            && await db.SafetyIncidents.FirstOrDefaultAsync(i => i.Id == iid, ct) is { IsClosed: false } incident)
        {
            var why = string.IsNullOrWhiteSpace(c.Note) ? null : c.Note.Trim();
            if (why is { Length: > 1000 })
                throw new DomainException("Note cannot exceed 1000 characters.");
            incident.AddNote(why is null ? $"Action sent back: {action.Title}" : $"Action sent back: {action.Title} · {why}", By, now);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelSafetyActionCommand c, CancellationToken ct)
    {
        var action = await OwnActionAsync(c.Id, ct);
        action.Cancel(c.Reason);
        await db.SaveChangesAsync(ct);
    }

    private async Task<SafetyAction> LoadAsync(Guid id, CancellationToken ct)
        => await db.SafetyActions.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Action", id);

    /// <summary>Banane wala paksh: safety team, incident ka investigator, ya inspection ka inspector.</summary>
    private async Task<SafetyAction> OwnActionAsync(Guid id, CancellationToken ct)
    {
        var action = await LoadAsync(id, ct);
        if (access.CanManage)
            return action;
        var my = await access.MeAsync(ct);
        var owns = my is not null && (
            action.IncidentId is { } iid && await db.SafetyIncidents.AnyAsync(i => i.Id == iid && i.InvestigatorEmployeeId == my, ct)
            || action.InspectionId is { } sid && await db.SafetyInspections.AnyAsync(i => i.Id == sid && i.InspectorEmployeeId == my, ct));
        if (owns)
            return action;
        if (my is not null && action.AssigneeEmployeeId == my)
            throw new UnauthorizedAccessException("Only the safety team can change this action.");
        throw new NotFoundException("Action", id);
    }

    private async Task<SafetyAction> AssignedActionAsync(Guid id, CancellationToken ct)
    {
        var action = await LoadAsync(id, ct);
        if (access.CanManage || await access.MeAsync(ct) is { } my && action.AssigneeEmployeeId == my)
            return action;
        throw new NotFoundException("Action", id);
    }

    private async Task InspectionForActionAsync(Guid id, CancellationToken ct)
    {
        var row = await db.SafetyInspections.AsNoTracking().Where(i => i.Id == id)
            .Select(i => new { i.InspectorEmployeeId }).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Inspection", id);
        if (access.CanManage)
            return;
        if (await access.MeAsync(ct) is not { } my || row.InspectorEmployeeId != my)
            throw new NotFoundException("Inspection", id);
    }
}

/// <summary>Checklists aur inspections. Checklist / schedule: safety team. Inspection mukammal: inspector ya safety team.</summary>
public sealed class SafetyInspectionCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateChecklistCommand, Guid>,
    IRequestHandler<UpdateChecklistCommand>,
    IRequestHandler<DeleteChecklistCommand>,
    IRequestHandler<AddSampleChecklistsCommand, int>,
    IRequestHandler<ScheduleInspectionCommand, Guid>,
    IRequestHandler<RescheduleInspectionCommand>,
    IRequestHandler<CancelInspectionCommand>,
    IRequestHandler<CompleteInspectionCommand, CompleteInspectionResult>
{
    private readonly SafetyAccess access = new(db, currentUser);
    private readonly SafetyRefs refs = new(db);

    public async Task<Guid> Handle(CreateChecklistCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var d = c.Data;
        await EnsureNameFreeAsync(d.Name, null, ct);
        var entity = SafetyChecklist.Create(access.TenantId, d.Name, d.Description, d.Items);
        if (!d.IsActive)
            entity.Update(d.Name, d.Description, false, d.Items);
        db.SafetyChecklists.Add(entity);
        await SafetyRefs.SaveNewAsync(db, "UX_HsChecklists_TenantId_Name", ct, "A checklist with this name already exists.");
        return entity.Id;
    }

    public async Task Handle(UpdateChecklistCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var entity = await db.SafetyChecklists.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == c.Id, ct)
                     ?? throw new NotFoundException("Checklist", c.Id);
        var d = c.Data;
        await EnsureNameFreeAsync(d.Name, entity.Id, ct);
        entity.Update(d.Name, d.Description, d.IsActive, d.Items);
        await SafetyRefs.SaveNewAsync(db, "UX_HsChecklists_TenantId_Name", ct, "A checklist with this name already exists.");
    }

    public async Task Handle(DeleteChecklistCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var entity = await db.SafetyChecklists.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Checklist", c.Id);
        if (await db.SafetyInspections.AnyAsync(i => i.ChecklistId == entity.Id && i.Status == InspectionStatus.Scheduled, ct))
            throw new DomainException("Inspections are scheduled with this checklist. Cancel them or switch the checklist off instead.");
        db.SafetyChecklists.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> Handle(AddSampleChecklistsCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var names = await db.SafetyChecklists.Select(x => x.Name).ToListAsync(ct);
        var added = 0;
        foreach (var s in SafetyChecklist.Samples.Where(s => !names.Contains(s.Name, StringComparer.OrdinalIgnoreCase)))
        {
            db.SafetyChecklists.Add(SafetyChecklist.Create(access.TenantId, s.Name, s.Description, s.Items));
            added++;
        }
        if (added > 0)
            await SafetyRefs.SaveNewAsync(db, "UX_HsChecklists_TenantId_Name", ct, "A checklist with this name already exists.");
        return added;
    }

    public async Task<Guid> Handle(ScheduleInspectionCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var d = c.Data;
        var checklist = await ChecklistAsync(d.ChecklistId, ct);
        await refs.PersonAsync(d.InspectorEmployeeId, ct, active: true);
        await refs.CheckIncidentAsync(d.LocationId, null, null, ct);
        var entity = SafetyInspection.Schedule(access.TenantId, await refs.NextInspectionCodeAsync(access.TenantId, ct), d.Title, checklist,
            Plan(d), SafetyClock.Today);
        db.SafetyInspections.Add(entity);
        await SafetyRefs.SaveNewAsync(db, "UX_HsInspections_TenantId_Code", ct);
        return entity.Id;
    }

    public async Task Handle(RescheduleInspectionCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var entity = await db.SafetyInspections.FirstOrDefaultAsync(i => i.Id == c.Id, ct) ?? throw new NotFoundException("Inspection", c.Id);
        var d = c.Data;
        if (d.InspectorEmployeeId != entity.InspectorEmployeeId)
            await refs.PersonAsync(d.InspectorEmployeeId, ct, active: true);
        await refs.CheckIncidentAsync(d.LocationId, null, null, ct);
        entity.Reschedule(d.Title, Plan(d), SafetyClock.Today);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelInspectionCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var entity = await db.SafetyInspections.FirstOrDefaultAsync(i => i.Id == c.Id, ct) ?? throw new NotFoundException("Inspection", c.Id);
        entity.Cancel(c.Reason);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Har line ka natija. Fail line par chaho to action ban jata hai. RepeatDays ho to agli inspection
    /// (pichli due date + RepeatDays, aaj se pehle nahi) khud schedule hoti hai.
    /// </summary>
    public async Task<CompleteInspectionResult> Handle(CompleteInspectionCommand c, CancellationToken ct)
    {
        var entity = await db.SafetyInspections.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == c.Id, ct)
                     ?? throw new NotFoundException("Inspection", c.Id);
        if (!access.CanManage && (await access.MeAsync(ct) is not { } my || entity.InspectorEmployeeId != my))
            throw new NotFoundException("Inspection", c.Id);

        var answers = c.Data.Answers ?? [];
        if (answers.Select(a => a.LineId).Distinct().Count() != answers.Count)
            throw new DomainException("Each item can be answered only once.");
        var today = SafetyClock.Today;
        var now = DateTime.UtcNow;
        entity.Complete(answers.ToDictionary(a => a.LineId, a => (a.Result, a.Note)), c.Data.Notes, now);

        var created = 0;
        foreach (var a in answers.Where(a => a.Action is not null))
        {
            if (a.Result != InspectionResult.Fail)
                throw new DomainException("Actions can only be raised for failed items.");
            var line = entity.Lines.First(l => l.Id == a.LineId);
            var spec = a.Action!;
            await refs.PersonAsync(spec.AssigneeEmployeeId, ct, active: true);
            var title = string.IsNullOrWhiteSpace(spec.Title) ? Trim(line.Text, 200) : spec.Title;
            db.SafetyActions.Add(SafetyAction.Create(access.TenantId,
                new ActionDetails(title, $"{entity.Code}: {line.Text}" + (line.Note is null ? "" : $"\n{line.Note}"),
                    spec.AssigneeEmployeeId, spec.Priority, spec.DueOn),
                null, entity.Id, today));
            created++;
        }

        SafetyInspection? next = null;
        if (entity.RepeatDays is { } every && entity.ChecklistId is { } cid
            && await db.SafetyChecklists.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == cid, ct) is { IsActive: true } checklist
            && await db.Employees.AnyAsync(e => e.Id == entity.InspectorEmployeeId && e.EmploymentStatus != EmploymentStatus.Exited, ct))
        {
            var due = entity.DueOn.AddDays(every);
            if (due < today)
                due = today.AddDays(every);
            next = SafetyInspection.Schedule(access.TenantId, await refs.NextInspectionCodeAsync(access.TenantId, ct), entity.Title, checklist,
                new InspectionPlan(entity.InspectorEmployeeId, entity.LocationId, entity.Area, due, entity.RepeatDays), today);
            db.SafetyInspections.Add(next);
        }

        await SafetyRefs.SaveNewAsync(db, "UX_HsInspections_TenantId_Code", ct);
        return new CompleteInspectionResult(created, next?.Id, next?.Code, next?.DueOn);
    }

    private static InspectionPlan Plan(InspectionRequest d) => new(d.InspectorEmployeeId, d.LocationId, d.Area, d.DueOn, d.RepeatDays);

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    private async Task<SafetyChecklist> ChecklistAsync(Guid id, CancellationToken ct)
        => await db.SafetyChecklists.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Checklist", id);

    private async Task EnsureNameFreeAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var n = name.Trim().ToLower();
        if (await db.SafetyChecklists.AnyAsync(x => x.Name.ToLower() == n && x.Id != exceptId, ct))
            throw new ConflictException("A checklist with this name already exists.");
    }
}

/// <summary>Safety handlers ke saanjhe helpers: codes, references, save.</summary>
internal sealed class SafetyRefs(IAppDbContext db)
{
    public async Task<string> PersonAsync(Guid id, CancellationToken ct, bool active = false)
    {
        var row = await db.Employees.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new { Name = e.FirstName + " " + e.LastName, e.EmploymentStatus }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Employee", id);
        if (active && row.EmploymentStatus == EmploymentStatus.Exited)
            throw new DomainException($"{row.Name} has left the company.");
        return row.Name;
    }

    public async Task CheckIncidentAsync(Guid? locationId, Guid? departmentId, Guid? affectedEmployeeId, CancellationToken ct)
    {
        if (locationId is { } l && l != Guid.Empty && !await db.Locations.AnyAsync(x => x.Id == l, ct))
            throw new NotFoundException("Location", l);
        if (departmentId is { } d && d != Guid.Empty && !await db.Departments.AnyAsync(x => x.Id == d, ct))
            throw new NotFoundException("Department", d);
        if (affectedEmployeeId is { } a && a != Guid.Empty)
            await PersonAsync(a, ct);
    }

    /// <summary>HS-0001... (deleted bhi gino taake code dobara na mile).</summary>
    public async Task<string> NextIncidentCodeAsync(Guid tenantId, CancellationToken ct)
    {
        var codes = await db.SafetyIncidents.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Code.StartsWith(SafetyIncident.CodePrefix)).Select(c => c.Code).ToListAsync(ct);
        return Next(SafetyIncident.CodePrefix, codes);
    }

    /// <summary>HI-0001... Isi save mein naye (abhi DB mein nahi) codes bhi gine jate hain.</summary>
    public async Task<string> NextInspectionCodeAsync(Guid tenantId, CancellationToken ct)
    {
        var codes = await db.SafetyInspections.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Code.StartsWith(SafetyInspection.CodePrefix)).Select(c => c.Code).ToListAsync(ct);
        codes.AddRange(db.SafetyInspections.Local.Select(i => i.Code));
        return Next(SafetyInspection.CodePrefix, codes);
    }

    private static string Next(string prefix, IEnumerable<string> codes)
    {
        var max = codes.Select(x => int.TryParse(x[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D4}";
    }

    public static async Task SaveNewAsync(IAppDbContext db, string index, CancellationToken ct, string? message = null)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(index) == true)
        {
            throw new ConflictException(message ?? "Someone saved at the same moment. Try again.");
        }
    }
}
