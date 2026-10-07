namespace HR.Employee.API.Application.Lifecycle;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Lifecycle;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Onboarding & exit page: cases (checklist per employee), "my tasks", aur checklist templates.
// HR: employees.view dekhna, employees.edit chalana. Templates: settings.manage ya employees.edit.
// Jis ko task assign hai (ya employee/manager-owned task) woh "My tasks" se khud complete kar sakta hai.

#region DTOs

public sealed record LifecycleSummaryDto(
    int OnboardingOpen, int ExitsOpen, int OverdueTasks, int DueThisWeek, int JoinersWithoutChecklist, int ClosedThisMonth);

public sealed record LifecycleCaseListItemDto(
    Guid Id, LifecycleKind Kind, CaseStatus Status, Guid EmployeeId, string EmployeeCode, string EmployeeName,
    string? DepartmentName, string? DesignationTitle, DateOnly AnchorDate, ExitType? ExitType,
    int TotalTasks, int ClosedTasks, int OpenRequired, int OverdueTasks, DateOnly? NextDue, DateTime CreatedAt, DateTime? ClosedAt);

public sealed record LifecycleTaskDto(
    Guid Id, string Title, string? Description, TaskOwner Owner, Guid? AssigneeEmployeeId, string? AssigneeName,
    DateOnly? DueDate, bool IsRequired, short SortOrder, LifecycleTaskStatus Status, string? Note, DateTime? CompletedAt);

public sealed record LifecycleCaseDto(
    Guid Id, LifecycleKind Kind, CaseStatus Status, Guid EmployeeId, string EmployeeCode, string EmployeeName,
    string? DepartmentName, string? DesignationTitle, string? ManagerName, string WorkEmail, EmploymentStatus EmploymentStatus,
    DateOnly JoiningDate, DateOnly AnchorDate, Guid? TemplateId, string? TemplateName,
    ExitType? ExitType, DateOnly? NoticeDate, string? Reason, bool? EligibleForRehire, string? InterviewNotes,
    string? Notes, DateTime CreatedAt, DateTime? ClosedAt, IReadOnlyList<LifecycleTaskDto> Tasks);

public sealed record MyLifecycleTaskDto(
    Guid TaskId, Guid CaseId, LifecycleKind Kind, Guid EmployeeId, string EmployeeName, bool IsSelf,
    string Title, string? Description, TaskOwner Owner, DateOnly? DueDate, bool IsRequired);

public sealed record LifecycleCandidateDto(
    Guid Id, string EmployeeCode, string Name, string? DepartmentName, DateOnly JoiningDate, EmploymentStatus Status);

public sealed record TemplateTaskDto(Guid Id, string Title, string? Description, TaskOwner Owner, short DueOffsetDays, bool IsRequired);

public sealed record ChecklistTemplateDto(
    Guid Id, LifecycleKind Kind, string Name, string? Description, bool IsDefault, bool IsActive, int InUse,
    IReadOnlyList<TemplateTaskDto> Tasks);

#endregion

#region Requests

public sealed record GetLifecycleSummaryQuery : IRequest<LifecycleSummaryDto>;

public sealed record GetLifecycleCasesQuery(LifecycleKind? Kind, CaseStatus? Status, string? Search)
    : IRequest<IReadOnlyList<LifecycleCaseListItemDto>>;

public sealed record GetLifecycleCaseQuery(Guid Id) : IRequest<LifecycleCaseDto>;

public sealed record GetMyLifecycleTasksQuery : IRequest<IReadOnlyList<MyLifecycleTaskDto>>;

public sealed record GetLifecycleCandidatesQuery(LifecycleKind Kind, string? Search) : IRequest<IReadOnlyList<LifecycleCandidateDto>>;

public sealed record StartOnboardingCommand(Guid EmployeeId, Guid? TemplateId, string? Notes) : IRequest<Guid>;

public sealed record StartExitCommand(
    Guid EmployeeId, Guid? TemplateId, ExitType ExitType, DateOnly NoticeDate, DateOnly LastWorkingDay, string Reason, string? Notes)
    : IRequest<Guid>;

public sealed record UpdateExitDetailsRequest(
    ExitType ExitType, DateOnly NoticeDate, DateOnly LastWorkingDay, string Reason, bool? EligibleForRehire, string? InterviewNotes);

public sealed record UpdateExitDetailsCommand(Guid CaseId, UpdateExitDetailsRequest Data) : IRequest;

public sealed record UpdateCaseNotesCommand(Guid CaseId, string? Notes) : IRequest;

public sealed record SaveLifecycleTaskRequest(
    string Title, string? Description, TaskOwner Owner, Guid? AssigneeEmployeeId, DateOnly? DueDate, bool IsRequired);

public sealed record AddLifecycleTaskCommand(Guid CaseId, SaveLifecycleTaskRequest Data) : IRequest<Guid>;
public sealed record UpdateLifecycleTaskCommand(Guid CaseId, Guid TaskId, SaveLifecycleTaskRequest Data) : IRequest;
public sealed record RemoveLifecycleTaskCommand(Guid CaseId, Guid TaskId) : IRequest;

public enum TaskAction : byte { Complete = 1, Skip = 2, Reopen = 3 }

public sealed record ChangeLifecycleTaskCommand(Guid CaseId, Guid TaskId, TaskAction Action, string? Note) : IRequest;

public sealed record CompleteLifecycleCaseCommand(Guid CaseId) : IRequest;
public sealed record CancelLifecycleCaseCommand(Guid CaseId) : IRequest;

public sealed record SaveTemplateTaskRequest(string Title, string? Description, TaskOwner Owner, short DueOffsetDays, bool IsRequired);

public sealed record SaveChecklistTemplateRequest(
    LifecycleKind Kind, string Name, string? Description, bool IsDefault, bool IsActive, IReadOnlyList<SaveTemplateTaskRequest> Tasks);

public sealed record GetChecklistTemplatesQuery(LifecycleKind? Kind, bool IncludeInactive = true) : IRequest<IReadOnlyList<ChecklistTemplateDto>>;
public sealed record CreateChecklistTemplateCommand(SaveChecklistTemplateRequest Data) : IRequest<Guid>;
public sealed record UpdateChecklistTemplateCommand(Guid Id, SaveChecklistTemplateRequest Data) : IRequest;
public sealed record DeleteChecklistTemplateCommand(Guid Id) : IRequest;

/// <summary>Koi template na ho to ek standard onboarding aur ek exit template bana do (default).</summary>
public sealed record CreateStarterTemplatesCommand : IRequest<int>;

#endregion

#region Validators

public sealed class StartExitValidator : AbstractValidator<StartExitCommand>
{
    public StartExitValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.ExitType).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.LastWorkingDay).GreaterThanOrEqualTo(x => x.NoticeDate)
            .WithMessage("Last working day cannot be before the notice date.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class StartOnboardingValidator : AbstractValidator<StartOnboardingCommand>
{
    public StartOnboardingValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateExitDetailsValidator : AbstractValidator<UpdateExitDetailsCommand>
{
    public UpdateExitDetailsValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.ExitType).IsInEnum();
        RuleFor(x => x.Data.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Data.InterviewNotes).MaximumLength(4000);
        RuleFor(x => x.Data.LastWorkingDay).GreaterThanOrEqualTo(x => x.Data.NoticeDate)
            .WithMessage("Last working day cannot be before the notice date.");
    }
}

public sealed class SaveLifecycleTaskRequestValidator : AbstractValidator<SaveLifecycleTaskRequest>
{
    public SaveLifecycleTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Owner).IsInEnum();
    }
}

public sealed class AddLifecycleTaskValidator : AbstractValidator<AddLifecycleTaskCommand>
{
    public AddLifecycleTaskValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveLifecycleTaskRequestValidator());
}

public sealed class UpdateLifecycleTaskValidator : AbstractValidator<UpdateLifecycleTaskCommand>
{
    public UpdateLifecycleTaskValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveLifecycleTaskRequestValidator());
}

public sealed class ChangeLifecycleTaskValidator : AbstractValidator<ChangeLifecycleTaskCommand>
{
    public ChangeLifecycleTaskValidator()
    {
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed class SaveChecklistTemplateRequestValidator : AbstractValidator<SaveChecklistTemplateRequest>
{
    public SaveChecklistTemplateRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Tasks).NotNull();
        RuleFor(x => x.Tasks.Count).LessThanOrEqualTo(ChecklistTemplate.MaxTasks).When(x => x.Tasks is not null)
            .WithMessage($"A checklist can have at most {ChecklistTemplate.MaxTasks} tasks.");
        RuleForEach(x => x.Tasks).ChildRules(t =>
        {
            t.RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            t.RuleFor(x => x.Description).MaximumLength(1000);
            t.RuleFor(x => x.Owner).IsInEnum();
            t.RuleFor(x => x.DueOffsetDays).InclusiveBetween((short)-ChecklistTemplateTask.MaxOffset, ChecklistTemplateTask.MaxOffset);
        });
    }
}

public sealed class CreateChecklistTemplateValidator : AbstractValidator<CreateChecklistTemplateCommand>
{
    public CreateChecklistTemplateValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveChecklistTemplateRequestValidator());
}

public sealed class UpdateChecklistTemplateValidator : AbstractValidator<UpdateChecklistTemplateCommand>
{
    public UpdateChecklistTemplateValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveChecklistTemplateRequestValidator());
}

#endregion

#region Access

public sealed class LifecycleAccess(IAppDbContext db, ICurrentUser currentUser)
{
    public bool CanView => currentUser.HasPermission(Permissions.EmployeesView);
    public bool CanManage => currentUser.HasPermission(Permissions.EmployeesEdit);
    public bool CanManageTemplates => CanManage || currentUser.HasPermission(Permissions.SettingsManage);

    public void EnsureCanView()
    {
        if (!CanView)
            throw new UnauthorizedAccessException("You do not have permission to see onboarding and exits.");
    }

    public void EnsureCanManage()
    {
        if (!CanManage)
            throw new UnauthorizedAccessException("You do not have permission to manage onboarding and exits.");
    }

    public void EnsureCanManageTemplates()
    {
        if (!CanManageTemplates)
            throw new UnauthorizedAccessException("You do not have permission to change checklist templates.");
    }

    /// <summary>Token ka user → Employee (Id, apne reports ke liye bhi). Link na ho to null.</summary>
    public async Task<Guid?> MyEmployeeIdAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return null;
        var id = await db.Employees.AsNoTracking().Where(e => e.UserId == userId).Select(e => e.Id).FirstOrDefaultAsync(ct);
        return id == Guid.Empty ? null : id;
    }
}

#endregion

#region Queries

public sealed class LifecycleQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetLifecycleSummaryQuery, LifecycleSummaryDto>,
    IRequestHandler<GetLifecycleCasesQuery, IReadOnlyList<LifecycleCaseListItemDto>>,
    IRequestHandler<GetLifecycleCaseQuery, LifecycleCaseDto>,
    IRequestHandler<GetMyLifecycleTasksQuery, IReadOnlyList<MyLifecycleTaskDto>>,
    IRequestHandler<GetLifecycleCandidatesQuery, IReadOnlyList<LifecycleCandidateDto>>,
    IRequestHandler<GetChecklistTemplatesQuery, IReadOnlyList<ChecklistTemplateDto>>
{
    private readonly LifecycleAccess access = new(db, currentUser);

    /// <summary>Is window mein join karne wale (pichhle 30 / agle 60 din) jinka onboarding case nahi — "start karo" reminder.</summary>
    private const int JoinerLookbackDays = 30;
    private const int JoinerLookaheadDays = 60;

    public async Task<LifecycleSummaryDto> Handle(GetLifecycleSummaryQuery q, CancellationToken ct)
    {
        access.EnsureCanView();
        var today = Today;
        var weekEnd = today.AddDays(7);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var open = db.LifecycleCases.AsNoTracking().Where(c => c.Status == CaseStatus.InProgress);
        var openTasks = open.SelectMany(c => c.Tasks).Where(t => t.Status == LifecycleTaskStatus.Open);

        var onboarding = await open.CountAsync(c => c.Kind == LifecycleKind.Onboarding, ct);
        var exits = await open.CountAsync(c => c.Kind == LifecycleKind.Exit, ct);
        var overdue = await openTasks.CountAsync(t => t.DueDate < today, ct);
        var dueWeek = await openTasks.CountAsync(t => t.DueDate >= today && t.DueDate <= weekEnd, ct);
        var closed = await db.LifecycleCases.AsNoTracking()
            .CountAsync(c => c.Status == CaseStatus.Completed && c.ClosedAt >= monthStart, ct);
        var joiners = await JoinersWithoutChecklist(today).CountAsync(ct);

        return new LifecycleSummaryDto(onboarding, exits, overdue, dueWeek, joiners, closed);
    }

    public async Task<IReadOnlyList<LifecycleCaseListItemDto>> Handle(GetLifecycleCasesQuery q, CancellationToken ct)
    {
        access.EnsureCanView();
        var today = Today;

        var cases = db.LifecycleCases.AsNoTracking();
        if (q.Kind is { } kind) cases = cases.Where(c => c.Kind == kind);
        if (q.Status is { } status) cases = cases.Where(c => c.Status == status);

        var rows = from c in cases
                   join e in db.Employees.AsNoTracking() on c.EmployeeId equals e.Id
                   select new { c, e };

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            rows = rows.Where(x => EF.Functions.ILike(x.e.FirstName + " " + x.e.LastName, term) || EF.Functions.ILike(x.e.EmployeeCode, term));
        }

        return await rows
            .OrderBy(x => x.c.Status).ThenBy(x => x.c.AnchorDate).ThenBy(x => x.e.FirstName)
            .Take(500)
            .Select(x => new LifecycleCaseListItemDto(
                x.c.Id, x.c.Kind, x.c.Status, x.e.Id, x.e.EmployeeCode,
                x.e.MiddleName == null ? x.e.FirstName + " " + x.e.LastName : x.e.FirstName + " " + x.e.MiddleName + " " + x.e.LastName,
                x.e.Department.Name, x.e.Designation.Title, x.c.AnchorDate, x.c.ExitType,
                x.c.Tasks.Count(),
                x.c.Tasks.Count(t => t.Status != LifecycleTaskStatus.Open),
                // Band case ke khule tasks ab kisi kaam ke nahi — overdue / next due sirf chalte case ke
                x.c.Status != CaseStatus.InProgress ? 0 : x.c.Tasks.Count(t => t.IsRequired && t.Status == LifecycleTaskStatus.Open),
                x.c.Status != CaseStatus.InProgress ? 0 : x.c.Tasks.Count(t => t.Status == LifecycleTaskStatus.Open && t.DueDate < today),
                x.c.Status != CaseStatus.InProgress ? null : x.c.Tasks.Where(t => t.Status == LifecycleTaskStatus.Open && t.DueDate != null).Min(t => t.DueDate),
                x.c.CreatedAt, x.c.ClosedAt))
            .ToListAsync(ct);
    }

    public async Task<LifecycleCaseDto> Handle(GetLifecycleCaseQuery q, CancellationToken ct)
    {
        access.EnsureCanView();

        var c = await db.LifecycleCases.AsNoTracking().Include(x => x.Tasks).FirstOrDefaultAsync(x => x.Id == q.Id, ct)
                ?? throw new NotFoundException("Checklist", q.Id);

        var e = await db.Employees.AsNoTracking().Where(x => x.Id == c.EmployeeId)
                    .Select(x => new
                    {
                        x.EmployeeCode, x.FirstName, x.MiddleName, x.LastName, x.WorkEmail, x.EmploymentStatus, x.JoiningDate,
                        Department = x.Department.Name, Designation = x.Designation.Title,
                        Manager = x.Manager == null ? null : x.Manager.FirstName + " " + x.Manager.LastName
                    })
                    .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Employee", c.EmployeeId);

        var assigneeIds = c.Tasks.Where(t => t.AssigneeEmployeeId != null).Select(t => t.AssigneeEmployeeId!.Value).Distinct().ToList();
        var assignees = await db.Employees.AsNoTracking().IgnoreQueryFilters()
            .Where(x => assigneeIds.Contains(x.Id) && x.TenantId == c.TenantId)
            .Select(x => new { x.Id, Name = x.FirstName + " " + x.LastName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        string? templateName = null;
        if (c.ChecklistTemplateId is { } templateId)
            templateName = await db.ChecklistTemplates.AsNoTracking().IgnoreQueryFilters()
                .Where(t => t.Id == templateId && t.TenantId == c.TenantId).Select(t => t.Name).FirstOrDefaultAsync(ct);

        var tasks = c.Tasks
            .OrderBy(t => t.SortOrder)
            .Select(t => new LifecycleTaskDto(t.Id, t.Title, t.Description, t.Owner, t.AssigneeEmployeeId,
                t.AssigneeEmployeeId is { } a && assignees.TryGetValue(a, out var n) ? n : null,
                t.DueDate, t.IsRequired, t.SortOrder, t.Status, t.Note, t.CompletedAt))
            .ToList();

        var name = string.IsNullOrWhiteSpace(e.MiddleName) ? $"{e.FirstName} {e.LastName}" : $"{e.FirstName} {e.MiddleName} {e.LastName}";
        return new LifecycleCaseDto(c.Id, c.Kind, c.Status, c.EmployeeId, e.EmployeeCode, name, e.Department, e.Designation, e.Manager,
            e.WorkEmail, e.EmploymentStatus, e.JoiningDate, c.AnchorDate, c.ChecklistTemplateId, templateName,
            c.ExitType, c.NoticeDate, c.Reason, c.EligibleForRehire, c.InterviewNotes, c.Notes, c.CreatedAt, c.ClosedAt, tasks);
    }

    /// <summary>Mere khule tasks: mujhe assigned, mere apne case ke Employee tasks, mere reports ke Manager tasks.</summary>
    public async Task<IReadOnlyList<MyLifecycleTaskDto>> Handle(GetMyLifecycleTasksQuery q, CancellationToken ct)
    {
        if (await access.MyEmployeeIdAsync(ct) is not { } me)
            return [];

        var rows = from c in db.LifecycleCases.AsNoTracking()
                   where c.Status == CaseStatus.InProgress
                   join e in db.Employees.AsNoTracking() on c.EmployeeId equals e.Id
                   from t in c.Tasks
                   where t.Status == LifecycleTaskStatus.Open
                         && (t.AssigneeEmployeeId == me
                             || (t.AssigneeEmployeeId == null && t.Owner == TaskOwner.Employee && c.EmployeeId == me)
                             || (t.AssigneeEmployeeId == null && t.Owner == TaskOwner.Manager && e.ManagerId == me))
                   orderby t.DueDate ?? DateOnly.MaxValue, t.SortOrder
                   select new MyLifecycleTaskDto(t.Id, c.Id, c.Kind, e.Id, e.FirstName + " " + e.LastName, e.Id == me,
                       t.Title, t.Description, t.Owner, t.DueDate, t.IsRequired);

        return await rows.Take(200).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LifecycleCandidateDto>> Handle(GetLifecycleCandidatesQuery q, CancellationToken ct)
    {
        access.EnsureCanManage();

        var openKind = db.LifecycleCases.Where(c => c.Kind == q.Kind && c.Status == CaseStatus.InProgress).Select(c => c.EmployeeId);
        var employees = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus != EmploymentStatus.Exited && !openKind.Contains(e.Id));

        if (q.Kind == LifecycleKind.Exit)
            employees = employees.Where(e => e.EmploymentStatus != EmploymentStatus.OnNotice);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            employees = employees.Where(e => EF.Functions.ILike(e.FirstName + " " + e.LastName, term) || EF.Functions.ILike(e.EmployeeCode, term));
        }

        // Onboarding: naye joiners pehle
        var ordered = q.Kind == LifecycleKind.Onboarding
            ? employees.OrderByDescending(e => e.JoiningDate).ThenBy(e => e.FirstName)
            : employees.OrderBy(e => e.FirstName).ThenBy(e => e.LastName);

        return await ordered.Take(50)
            .Select(e => new LifecycleCandidateDto(e.Id, e.EmployeeCode, e.FirstName + " " + e.LastName, e.Department.Name, e.JoiningDate, e.EmploymentStatus))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChecklistTemplateDto>> Handle(GetChecklistTemplatesQuery q, CancellationToken ct)
    {
        access.EnsureCanView();

        var templates = await db.ChecklistTemplates.AsNoTracking().Include(t => t.Tasks)
            .Where(t => q.Kind == null || t.Kind == q.Kind)
            .Where(t => q.IncludeInactive || t.IsActive)
            .OrderBy(t => t.Kind).ThenByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .ToListAsync(ct);

        var ids = templates.Select(t => t.Id).ToList();
        var inUse = await db.LifecycleCases.AsNoTracking()
            .Where(c => c.ChecklistTemplateId != null && ids.Contains(c.ChecklistTemplateId.Value) && c.Status == CaseStatus.InProgress)
            .GroupBy(c => c.ChecklistTemplateId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return templates.Select(t => new ChecklistTemplateDto(t.Id, t.Kind, t.Name, t.Description, t.IsDefault, t.IsActive,
                inUse.GetValueOrDefault(t.Id),
                t.Tasks.OrderBy(x => x.SortOrder)
                    .Select(x => new TemplateTaskDto(x.Id, x.Title, x.Description, x.Owner, x.DueOffsetDays, x.IsRequired)).ToList()))
            .ToList();
    }

    private IQueryable<Employee> JoinersWithoutChecklist(DateOnly today)
    {
        var from = today.AddDays(-JoinerLookbackDays);
        var to = today.AddDays(JoinerLookaheadDays);
        var withCase = db.LifecycleCases.Where(c => c.Kind == LifecycleKind.Onboarding && c.Status != CaseStatus.Cancelled).Select(c => c.EmployeeId);
        return db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus != EmploymentStatus.Exited && e.JoiningDate >= from && e.JoiningDate <= to && !withCase.Contains(e.Id));
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}

#endregion

#region Commands

public sealed class LifecycleCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<StartOnboardingCommand, Guid>,
    IRequestHandler<StartExitCommand, Guid>,
    IRequestHandler<UpdateExitDetailsCommand>,
    IRequestHandler<UpdateCaseNotesCommand>,
    IRequestHandler<AddLifecycleTaskCommand, Guid>,
    IRequestHandler<UpdateLifecycleTaskCommand>,
    IRequestHandler<RemoveLifecycleTaskCommand>,
    IRequestHandler<ChangeLifecycleTaskCommand>,
    IRequestHandler<CompleteLifecycleCaseCommand>,
    IRequestHandler<CancelLifecycleCaseCommand>
{
    private readonly LifecycleAccess access = new(db, currentUser);

    public async Task<Guid> Handle(StartOnboardingCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var employee = await ActiveEmployeeAsync(c.EmployeeId, ct);
        await EnsureNoOpenCaseAsync(employee.Id, LifecycleKind.Onboarding, ct);

        var template = await TemplateAsync(c.TemplateId, LifecycleKind.Onboarding, ct);
        var @case = LifecycleCase.StartOnboarding(currentUser.RequireTenantId(), employee.Id, employee.JoiningDate, template);
        @case.SetNotes(c.Notes);

        db.LifecycleCases.Add(@case);
        await SaveAsync(ct);
        return @case.Id;
    }

    public async Task<Guid> Handle(StartExitCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var employee = await ActiveEmployeeAsync(c.EmployeeId, ct);
        await EnsureNoOpenCaseAsync(employee.Id, LifecycleKind.Exit, ct);

        var template = await TemplateAsync(c.TemplateId, LifecycleKind.Exit, ct);
        var @case = LifecycleCase.StartExit(currentUser.RequireTenantId(), employee.Id, c.ExitType, c.NoticeDate, c.LastWorkingDay, c.Reason, template);
        @case.SetNotes(c.Notes);

        if (employee.EmploymentStatus != EmploymentStatus.OnNotice)
            employee.ServeNotice(c.NoticeDate, c.LastWorkingDay);

        db.LifecycleCases.Add(@case);
        await SaveAsync(ct);
        return @case.Id;
    }

    public async Task Handle(UpdateExitDetailsCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var @case = await CaseAsync(c.CaseId, ct);
        var d = c.Data;
        var employee = await EmployeeAsync(@case.EmployeeId, ct);
        if (d.LastWorkingDay < employee.JoiningDate)
            throw new ConflictException("Last working day cannot be before the joining date.");

        @case.SetExitDetails(d.ExitType, d.NoticeDate, d.LastWorkingDay, d.Reason, d.EligibleForRehire, d.InterviewNotes);
        await SaveAsync(ct);
    }

    public async Task Handle(UpdateCaseNotesCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var @case = await CaseAsync(c.CaseId, ct);
        @case.SetNotes(c.Notes);
        await SaveAsync(ct);
    }

    public async Task<Guid> Handle(AddLifecycleTaskCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var @case = await CaseAsync(c.CaseId, ct);
        var d = c.Data;
        await EnsureAssigneeAsync(d.AssigneeEmployeeId, ct);

        var task = @case.AddTask(d.Title, d.Description, d.Owner, d.AssigneeEmployeeId, d.DueDate, d.IsRequired);
        await SaveAsync(ct);
        return task.Id;
    }

    public async Task Handle(UpdateLifecycleTaskCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var @case = await CaseAsync(c.CaseId, ct);
        var d = c.Data;
        await EnsureAssigneeAsync(d.AssigneeEmployeeId, ct);

        @case.UpdateTask(c.TaskId, d.Title, d.Description, d.Owner, d.AssigneeEmployeeId, d.DueDate, d.IsRequired);
        await SaveAsync(ct);
    }

    public async Task Handle(RemoveLifecycleTaskCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var @case = await CaseAsync(c.CaseId, ct);
        @case.RemoveTask(c.TaskId);
        await SaveAsync(ct);
    }

    /// <summary>HR koi bhi task; baaki log sirf apne (assigned / employee-owned / apne report ka manager-owned).</summary>
    public async Task Handle(ChangeLifecycleTaskCommand c, CancellationToken ct)
    {
        var @case = await CaseAsync(c.CaseId, ct);
        var task = @case.Tasks.FirstOrDefault(t => t.Id == c.TaskId) ?? throw new NotFoundException("Task", c.TaskId);

        if (!access.CanManage)
        {
            var me = await access.MyEmployeeIdAsync(ct)
                     ?? throw new UnauthorizedAccessException("Your login is not linked to an employee record.");
            var managerId = await db.Employees.Where(e => e.Id == @case.EmployeeId).Select(e => e.ManagerId).FirstOrDefaultAsync(ct);
            var mine = task.AssigneeEmployeeId == me
                       || (task.AssigneeEmployeeId == null && task.Owner == TaskOwner.Employee && @case.EmployeeId == me)
                       || (task.AssigneeEmployeeId == null && task.Owner == TaskOwner.Manager && managerId == me);
            if (!mine)
                throw new UnauthorizedAccessException("This task is not assigned to you.");
            if (c.Action == TaskAction.Skip)
                throw new UnauthorizedAccessException("Only HR can skip a task.");
        }

        var now = DateTime.UtcNow;
        switch (c.Action)
        {
            case TaskAction.Complete: @case.CompleteTask(c.TaskId, currentUser.UserId, c.Note, now); break;
            case TaskAction.Skip: @case.SkipTask(c.TaskId, currentUser.UserId, c.Note, now); break;
            case TaskAction.Reopen: @case.ReopenTask(c.TaskId); break;
        }
        await SaveAsync(ct);
    }

    /// <summary>Exit case complete = employee ka asal exit (status Exited, login band, department head seat khali).</summary>
    public async Task Handle(CompleteLifecycleCaseCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var @case = await CaseAsync(c.CaseId, ct);

        if (@case.Kind == LifecycleKind.Exit)
        {
            if (@case.AnchorDate > DateOnly.FromDateTime(DateTime.UtcNow))
                throw new ConflictException("An exit can be completed on or after the last working day. Change the last working day if the employee is leaving earlier.");

            var employee = await EmployeeAsync(@case.EmployeeId, ct);
            @case.Complete(currentUser.UserId, DateTime.UtcNow);
            if (!employee.HasExited)
            {
                employee.Exit(@case.AnchorDate, @case.Reason!);
                var headed = await db.Departments.Where(d => d.HeadEmployeeId == employee.Id).ToListAsync(ct);
                foreach (var department in headed)
                    department.AssignHead(null);
            }
        }
        else
        {
            @case.Complete(currentUser.UserId, DateTime.UtcNow);
        }

        await SaveAsync(ct);   // Exit: EmployeeExitedDomainEvent → integration event (Identity user disable)
    }

    public async Task Handle(CancelLifecycleCaseCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var @case = await CaseAsync(c.CaseId, ct);
        @case.Cancel(currentUser.UserId, DateTime.UtcNow);

        if (@case.Kind == LifecycleKind.Exit)
        {
            var employee = await EmployeeAsync(@case.EmployeeId, ct);
            if (!employee.HasExited)
                employee.WithdrawNotice(DateOnly.FromDateTime(DateTime.UtcNow));
        }

        await SaveAsync(ct);
    }

    private async Task<LifecycleCase> CaseAsync(Guid id, CancellationToken ct)
        => await db.LifecycleCases.Include(x => x.Tasks).FirstOrDefaultAsync(x => x.Id == id, ct)
           ?? throw new NotFoundException("Checklist", id);

    private async Task<Employee> EmployeeAsync(Guid id, CancellationToken ct)
        => await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Employee", id);

    private async Task<Employee> ActiveEmployeeAsync(Guid id, CancellationToken ct)
    {
        var employee = await EmployeeAsync(id, ct);
        if (employee.HasExited)
            throw new ConflictException("This employee has already exited.");
        return employee;
    }

    private async Task EnsureNoOpenCaseAsync(Guid employeeId, LifecycleKind kind, CancellationToken ct)
    {
        if (await db.LifecycleCases.AnyAsync(x => x.EmployeeId == employeeId && x.Kind == kind && x.Status == CaseStatus.InProgress, ct))
            throw new ConflictException(kind == LifecycleKind.Onboarding
                ? "This employee already has an onboarding checklist in progress."
                : "This employee already has an exit in progress.");
    }

    /// <summary>TemplateId diya to woh (active, same kind); warna is kind ka default (na ho to khali checklist).</summary>
    private async Task<ChecklistTemplate?> TemplateAsync(Guid? templateId, LifecycleKind kind, CancellationToken ct)
    {
        if (templateId is { } id)
        {
            var template = await db.ChecklistTemplates.Include(t => t.Tasks).FirstOrDefaultAsync(t => t.Id == id, ct)
                           ?? throw new NotFoundException("Checklist template", id);
            if (template.Kind != kind)
                throw new ConflictException("This template is for a different kind of checklist.");
            if (!template.IsActive)
                throw new ConflictException("This template is inactive.");
            return template;
        }

        return await db.ChecklistTemplates.Include(t => t.Tasks)
            .FirstOrDefaultAsync(t => t.Kind == kind && t.IsDefault && t.IsActive, ct);
    }

    private async Task EnsureAssigneeAsync(Guid? employeeId, CancellationToken ct)
    {
        if (employeeId is { } id && id != Guid.Empty &&
            !await db.Employees.AnyAsync(e => e.Id == id && e.EmploymentStatus != EmploymentStatus.Exited, ct))
            throw new NotFoundException("Assignee", id);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_LifecycleCases_EmployeeId_Kind_Open") == true)
        {
            throw new ConflictException("This employee already has a checklist of this kind in progress.");
        }
    }
}

#endregion

#region Templates

public sealed class ChecklistTemplateHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateChecklistTemplateCommand, Guid>,
    IRequestHandler<UpdateChecklistTemplateCommand>,
    IRequestHandler<DeleteChecklistTemplateCommand>,
    IRequestHandler<CreateStarterTemplatesCommand, int>
{
    private readonly LifecycleAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateChecklistTemplateCommand c, CancellationToken ct)
    {
        access.EnsureCanManageTemplates();
        var d = c.Data;
        await EnsureNameIsFreeAsync(d.Kind, d.Name, null, ct);

        var template = ChecklistTemplate.Create(currentUser.RequireTenantId(), d.Kind, d.Name, d.Description);
        template.ReplaceTasks(ToData(d.Tasks));
        if (!d.IsActive) template.Deactivate();
        await ApplyDefaultAsync(template, d.IsDefault, ct);

        db.ChecklistTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return template.Id;
    }

    public async Task Handle(UpdateChecklistTemplateCommand c, CancellationToken ct)
    {
        access.EnsureCanManageTemplates();
        var d = c.Data;
        var template = await db.ChecklistTemplates.Include(t => t.Tasks).FirstOrDefaultAsync(t => t.Id == c.Id, ct)
                       ?? throw new NotFoundException("Checklist template", c.Id);
        if (template.Kind != d.Kind)
            throw new ConflictException("A template cannot change between onboarding and exit.");
        await EnsureNameIsFreeAsync(d.Kind, d.Name, c.Id, ct);

        template.Update(d.Name, d.Description);
        template.ReplaceTasks(ToData(d.Tasks));
        if (d.IsActive) template.Activate(); else template.Deactivate();
        await ApplyDefaultAsync(template, d.IsDefault && d.IsActive, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Soft delete. Chalte cases ke tasks pehle se copy hain, un par asar nahi.</summary>
    public async Task Handle(DeleteChecklistTemplateCommand c, CancellationToken ct)
    {
        access.EnsureCanManageTemplates();
        var template = await db.ChecklistTemplates.FirstOrDefaultAsync(t => t.Id == c.Id, ct)
                       ?? throw new NotFoundException("Checklist template", c.Id);
        template.Deactivate();
        db.ChecklistTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> Handle(CreateStarterTemplatesCommand c, CancellationToken ct)
    {
        access.EnsureCanManageTemplates();
        var tenantId = currentUser.RequireTenantId();
        var existing = await db.ChecklistTemplates.Select(t => t.Kind).Distinct().ToListAsync(ct);

        var created = 0;
        foreach (var (kind, name, description, tasks) in StarterTemplates.All)
        {
            if (existing.Contains(kind))
                continue;
            var template = ChecklistTemplate.Create(tenantId, kind, name, description);
            template.ReplaceTasks(tasks);
            template.SetDefault(true);
            db.ChecklistTemplates.Add(template);
            created++;
        }

        if (created > 0)
            await db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>Naya default: is kind ka purana default hata do (unique index ek hi allow karta hai).</summary>
    private async Task ApplyDefaultAsync(ChecklistTemplate template, bool isDefault, CancellationToken ct)
    {
        if (isDefault)
        {
            var others = await db.ChecklistTemplates
                .Where(t => t.Kind == template.Kind && t.IsDefault && t.Id != template.Id).ToListAsync(ct);
            foreach (var other in others)
                other.SetDefault(false);
        }
        template.SetDefault(isDefault);
    }

    private async Task EnsureNameIsFreeAsync(LifecycleKind kind, string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await db.ChecklistTemplates.AnyAsync(t => t.Kind == kind && t.Name == trimmed && t.Id != exceptId, ct))
            throw new ConflictException($"A template named '{trimmed}' already exists.");
    }

    private static IEnumerable<TemplateTaskData> ToData(IEnumerable<SaveTemplateTaskRequest> tasks)
        => tasks.Select(t => new TemplateTaskData(t.Title, t.Description, t.Owner, t.DueOffsetDays, t.IsRequired));
}

/// <summary>Starter checklists (company baad mein badal sakti hai).</summary>
internal static class StarterTemplates
{
    public static readonly (LifecycleKind Kind, string Name, string Description, TemplateTaskData[] Tasks)[] All =
    [
        (LifecycleKind.Onboarding, "Standard onboarding", "Default checklist for new joiners.",
        [
            new("Send offer letter and collect signed copy", null, TaskOwner.Hr, -14, true),
            new("Collect ID, education and bank documents", "CNIC / passport, degrees, bank account details.", TaskOwner.Hr, -7, true),
            new("Prepare laptop and accessories", null, TaskOwner.It, -3, true),
            new("Create email and system accounts", null, TaskOwner.It, -1, true),
            new("Arrange desk and access card", null, TaskOwner.Admin, -1, false),
            new("Add to payroll with salary structure", null, TaskOwner.Finance, 0, true),
            new("Welcome meeting and team introduction", null, TaskOwner.Manager, 0, false),
            new("Read and accept company policies", null, TaskOwner.Employee, 3, true),
            new("Complete personal and emergency contact details", null, TaskOwner.Employee, 3, true),
            new("Set 30-day goals", null, TaskOwner.Manager, 7, false),
            new("30-day check-in", null, TaskOwner.Manager, 30, false),
        ]),
        (LifecycleKind.Exit, "Standard exit", "Default checklist for leavers.",
        [
            new("Acknowledge resignation or issue termination letter", null, TaskOwner.Hr, -25, true),
            new("Plan knowledge handover", null, TaskOwner.Manager, -14, true),
            new("Complete handover of work and documents", null, TaskOwner.Employee, -3, true),
            new("Exit interview", null, TaskOwner.Hr, -2, false),
            new("Return laptop, access card and company assets", null, TaskOwner.It, 0, true),
            new("Disable email and system access", null, TaskOwner.It, 0, true),
            new("Clear loans, advances and expense claims", null, TaskOwner.Finance, 0, true),
            new("Process final settlement", null, TaskOwner.Finance, 7, true),
            new("Issue experience and relieving letter", null, TaskOwner.Hr, 7, true),
        ]),
    ];
}

#endregion
