namespace HR.Employee.API.Application.Helpdesk;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Helpdesk;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Requests & helpdesk: employee request/sawal uthata hai (category ke hisaab se), manager approval (agar chahiye),
// phir HR helpdesk (agent = employees.edit) ya category ka assignee usay hal karta hai.
// employees.view = queue dekh sakta hai (confidential nahi). settings.manage = categories.

#region DTOs

public enum TicketScope : byte { Mine = 1, Approvals = 2, Queue = 3 }

public sealed record HelpdeskSummaryDto(
    int MyActive, int MyResolved, int AwaitingMyApproval, int QueueOpen, int Unassigned, int AssignedToMe, int Overdue,
    int ResolvedLast30, decimal? AverageResolutionHours, decimal? Satisfaction,
    bool IsAgent, bool CanViewQueue, bool CanConfigure, bool HasCategories, Guid? MyEmployeeId);

public sealed record HelpdeskCategoryDto(
    Guid Id, string Name, string? Description, string? Icon, bool NeedsManagerApproval, bool IsConfidential, short? ResolutionHours,
    Guid? DefaultAssigneeEmployeeId, string? DefaultAssigneeName, short SortOrder, bool IsActive, int OpenTickets, int TotalTickets, uint RowVersion);

public sealed record HelpdeskOptionDto(Guid Id, string Name, string? Extra);

public sealed record HelpdeskLookupsDto(IReadOnlyList<HelpdeskCategoryDto> Categories, IReadOnlyList<HelpdeskOptionDto> People);

public sealed record TicketListItemDto(
    Guid Id, string Code, string Subject, Guid CategoryId, string CategoryName, string? CategoryIcon,
    Guid EmployeeId, string EmployeeName, string EmployeeCode, string DepartmentName,
    TicketPriority Priority, TicketStatus Status, bool IsConfidential,
    Guid? AssigneeEmployeeId, string? AssigneeName, Guid? ApproverEmployeeId, string? ApproverName,
    DateTime CreatedAt, DateTime LastActivityAt, DateTime? DueAt, bool IsOverdue, DateTime? ResolvedAt, byte? SatisfactionRating);

public sealed record TicketActivityDto(
    Guid Id, TicketActivityKind Kind, TicketStatus? FromStatus, TicketStatus ToStatus, string? Note, string? ByName, bool ByMe, DateTime At);

public sealed record TicketDto(
    TicketListItemDto Ticket, string? Description, string? Link, string? DecisionNote, DateTime? DecidedAt, DateTime? OpenedAt,
    DateTime? FirstResponseAt, string? Resolution, DateTime? ClosedAt, IReadOnlyList<TicketActivityDto> Activities,
    bool IsRequester, bool CanEdit, bool CanComment, bool CanInternalNote, bool CanApprove, bool CanWork, bool CanAssign,
    bool CanConfirm, bool CanReopen, bool CanCancel, bool CanRate, uint RowVersion);

#endregion

#region Requests

public sealed record GetHelpdeskSummaryQuery : IRequest<HelpdeskSummaryDto>;
public sealed record GetHelpdeskLookupsQuery : IRequest<HelpdeskLookupsDto>;
public sealed record GetHelpdeskCategoriesQuery : IRequest<IReadOnlyList<HelpdeskCategoryDto>>;
public sealed record GetTicketsQuery(
    TicketScope Scope, TicketStatus? Status, bool ActiveOnly, Guid? CategoryId, string? Assignee, string? Search) : IRequest<IReadOnlyList<TicketListItemDto>>;
public sealed record GetTicketQuery(Guid Id) : IRequest<TicketDto>;

public sealed record SaveTicketRequest(Guid CategoryId, string Subject, string? Description, string? Link, TicketPriority Priority, Guid? EmployeeId);
public sealed record CreateTicketCommand(SaveTicketRequest Data) : IRequest<Guid>;
public sealed record UpdateTicketCommand(Guid Id, SaveTicketRequest Data) : IRequest;
public sealed record ApproveTicketCommand(Guid Id, string? Note) : IRequest;
public sealed record RejectTicketCommand(Guid Id, string? Note) : IRequest;
public sealed record AssignTicketCommand(Guid Id, Guid? AssigneeEmployeeId) : IRequest;
public sealed record SetTicketStatusCommand(Guid Id, TicketStatus Status, string? Note) : IRequest;
public sealed record CommentTicketCommand(Guid Id, string Body, bool Internal) : IRequest;
public sealed record ConfirmTicketCommand(Guid Id, byte? Rating) : IRequest;
public sealed record RateTicketCommand(Guid Id, byte Rating) : IRequest;
public sealed record ReopenTicketCommand(Guid Id, string? Reason) : IRequest;
public sealed record CancelTicketCommand(Guid Id, string? Reason) : IRequest;

public sealed record SaveCategoryRequest(
    string Name, string? Description, string? Icon, bool NeedsManagerApproval, bool IsConfidential, short? ResolutionHours,
    Guid? DefaultAssigneeEmployeeId, short SortOrder, bool IsActive);
public sealed record CreateCategoryCommand(SaveCategoryRequest Data) : IRequest<Guid>;
public sealed record UpdateCategoryCommand(Guid Id, SaveCategoryRequest Data) : IRequest;
public sealed record DeleteCategoryCommand(Guid Id) : IRequest;
/// <summary>Aam categories (salary certificate, payroll question...) jo pehle se na hon. Wapas = kitni bani.</summary>
public sealed record AddDefaultCategoriesCommand : IRequest<int>;

public sealed class SaveTicketRequestValidator : AbstractValidator<SaveTicketRequest>
{
    public SaveTicketRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Link).MaximumLength(1000);
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class CreateTicketValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveTicketRequestValidator());
}

public sealed class UpdateTicketValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveTicketRequestValidator());
}

public sealed class SetTicketStatusValidator : AbstractValidator<SetTicketStatusCommand>
{
    public SetTicketStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed class CommentTicketValidator : AbstractValidator<CommentTicketCommand>
{
    public CommentTicketValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
}

public sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.Icon).MaximumLength(40);
        RuleFor(x => x.ResolutionHours).InclusiveBetween((short)1, (short)2000).When(x => x.ResolutionHours is not null);
        RuleFor(x => x.SortOrder).InclusiveBetween((short)0, (short)999);
    }
}

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveCategoryRequestValidator());
}

public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveCategoryRequestValidator());
}

#endregion

#region Access

public sealed class HelpdeskAccess(IAppDbContext db, ICurrentUser currentUser)
{
    private Guid? me;
    private bool loaded;

    /// <summary>Agent: sab tickets, assign, status, internal notes.</summary>
    public bool IsAgent => currentUser.HasPermission(Permissions.EmployeesEdit);
    /// <summary>Queue dekh sakta hai (confidential sirf agent).</summary>
    public bool CanViewQueue => IsAgent || currentUser.HasPermission(Permissions.EmployeesView);
    public bool CanConfigure => currentUser.HasPermission(Permissions.SettingsManage);
    public Guid? UserId => currentUser.UserId;

    public async Task<Guid?> MeAsync(CancellationToken ct)
    {
        if (loaded)
            return me;
        loaded = true;
        if (currentUser.UserId is { } userId)
        {
            var id = await db.Employees.AsNoTracking().Where(e => e.UserId == userId).Select(e => e.Id).FirstOrDefaultAsync(ct);
            me = id == Guid.Empty ? null : id;
        }
        return me;
    }

    public void EnsureCanConfigure()
    {
        if (!CanConfigure)
            throw new UnauthorizedAccessException("You do not have permission to manage helpdesk categories.");
    }

    public bool IsRequester(HelpdeskTicket t, Guid? my) => my is not null && t.EmployeeId == my;
    public bool IsAssignee(HelpdeskTicket t, Guid? my) => my is not null && t.AssigneeEmployeeId == my;
    public bool IsApprover(HelpdeskTicket t, Guid? my) => my is not null && t.ApproverEmployeeId == my && !t.IsConfidential;

    public bool CanWork(HelpdeskTicket t, Guid? my) => IsAgent || IsAssignee(t, my);

    public bool CanSee(HelpdeskTicket t, Guid? my)
        => IsRequester(t, my) || CanWork(t, my) || IsApprover(t, my) || (CanViewQueue && !t.IsConfidential);

    public async Task<HelpdeskTicket> SeeAsync(HelpdeskTicket t, CancellationToken ct)
    {
        if (!CanSee(t, await MeAsync(ct)))
            throw new NotFoundException("Request", t.Id);
        return t;
    }

    /// <summary>Tickets jo ye user dekh sakta hai (scope ke hisaab se).</summary>
    public IQueryable<HelpdeskTicket> Scoped(TicketScope scope, Guid? my)
    {
        var q = db.HelpdeskTickets.AsNoTracking();
        return scope switch
        {
            TicketScope.Mine => q.Where(t => my != null && t.EmployeeId == my),
            TicketScope.Approvals => q.Where(t => my != null && t.ApproverEmployeeId == my && !t.IsConfidential),
            _ when IsAgent => q,
            _ when CanViewQueue => q.Where(t => !t.IsConfidential || (my != null && t.AssigneeEmployeeId == my)),
            _ => q.Where(t => my != null && t.AssigneeEmployeeId == my)
        };
    }
}

#endregion

#region Queries

public sealed class HelpdeskQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetHelpdeskSummaryQuery, HelpdeskSummaryDto>,
    IRequestHandler<GetHelpdeskLookupsQuery, HelpdeskLookupsDto>,
    IRequestHandler<GetHelpdeskCategoriesQuery, IReadOnlyList<HelpdeskCategoryDto>>,
    IRequestHandler<GetTicketsQuery, IReadOnlyList<TicketListItemDto>>,
    IRequestHandler<GetTicketQuery, TicketDto>
{
    private readonly HelpdeskAccess access = new(db, currentUser);

    private static readonly TicketStatus[] Active = [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.WaitingOnEmployee];

    public async Task<HelpdeskSummaryDto> Handle(GetHelpdeskSummaryQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var now = DateTime.UtcNow;

        var mine = await access.Scoped(TicketScope.Mine, me)
            .Where(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Rejected && t.Status != TicketStatus.Cancelled)
            .Select(t => t.Status).ToListAsync(ct);
        var approvals = await access.Scoped(TicketScope.Approvals, me).CountAsync(t => t.Status == TicketStatus.PendingApproval, ct);

        int queueOpen = 0, unassigned = 0, assignedToMe = 0, overdue = 0, resolved30 = 0;
        decimal? avgHours = null, satisfaction = null;
        var seesQueue = access.CanViewQueue || (me != null && await db.HelpdeskTickets.AnyAsync(t => t.AssigneeEmployeeId == me, ct));
        if (seesQueue)
        {
            var queue = await access.Scoped(TicketScope.Queue, me)
                .Where(t => Active.Contains(t.Status))
                .Select(t => new { t.AssigneeEmployeeId, t.DueAt }).ToListAsync(ct);
            queueOpen = queue.Count;
            unassigned = queue.Count(t => t.AssigneeEmployeeId == null);
            assignedToMe = queue.Count(t => me != null && t.AssigneeEmployeeId == me);
            overdue = queue.Count(t => t.DueAt != null && t.DueAt < now);

            var since = now.AddDays(-30);
            var done = await access.Scoped(TicketScope.Queue, me)
                .Where(t => t.ResolvedAt != null && t.ResolvedAt >= since && t.OpenedAt != null)
                .Select(t => new { t.OpenedAt, t.ResolvedAt, t.SatisfactionRating }).ToListAsync(ct);
            resolved30 = done.Count;
            if (done.Count > 0)
                avgHours = Math.Round((decimal)done.Average(t => (t.ResolvedAt!.Value - t.OpenedAt!.Value).TotalHours), 1);
            var rated = done.Where(t => t.SatisfactionRating != null).ToList();
            if (rated.Count > 0)
                satisfaction = Math.Round((decimal)rated.Average(t => (double)t.SatisfactionRating!.Value), 1);
        }

        var hasCategories = await db.HelpdeskCategories.AnyAsync(c => c.IsActive, ct);
        return new HelpdeskSummaryDto(
            mine.Count(s => s is TicketStatus.PendingApproval or TicketStatus.Open or TicketStatus.InProgress or TicketStatus.WaitingOnEmployee),
            mine.Count(s => s == TicketStatus.Resolved), approvals, queueOpen, unassigned, assignedToMe, overdue, resolved30, avgHours, satisfaction,
            access.IsAgent, seesQueue, access.CanConfigure, hasCategories, me);
    }

    public async Task<HelpdeskLookupsDto> Handle(GetHelpdeskLookupsQuery q, CancellationToken ct)
    {
        var categories = (await CategoriesAsync(ct)).Where(c => c.IsActive).ToList();
        IReadOnlyList<HelpdeskOptionDto> people = [];
        if (access.IsAgent || access.CanConfigure)
            people = await db.Employees.AsNoTracking()
                .Where(e => e.EmploymentStatus != EmploymentStatus.Exited)
                .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
                .Select(e => new HelpdeskOptionDto(e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode + " · " + e.Department.Name))
                .ToListAsync(ct);
        return new HelpdeskLookupsDto(categories, people);
    }

    public async Task<IReadOnlyList<HelpdeskCategoryDto>> Handle(GetHelpdeskCategoriesQuery q, CancellationToken ct)
    {
        if (!access.CanConfigure && !access.IsAgent)
            throw new UnauthorizedAccessException("You do not have permission to see helpdesk settings.");
        return await CategoriesAsync(ct);
    }

    private async Task<List<HelpdeskCategoryDto>> CategoriesAsync(CancellationToken ct)
        => await db.HelpdeskCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new HelpdeskCategoryDto(
                c.Id, c.Name, c.Description, c.Icon, c.NeedsManagerApproval, c.IsConfidential, c.ResolutionHours, c.DefaultAssigneeEmployeeId,
                db.Employees.Where(e => e.Id == c.DefaultAssigneeEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
                c.SortOrder, c.IsActive,
                db.HelpdeskTickets.Count(t => t.CategoryId == c.Id && Active.Contains(t.Status)),
                db.HelpdeskTickets.Count(t => t.CategoryId == c.Id),
                c.RowVersion))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TicketListItemDto>> Handle(GetTicketsQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var tickets = access.Scoped(q.Scope, me);

        if (q.Status is { } status)
            tickets = tickets.Where(t => t.Status == status);
        else if (q.ActiveOnly)
            tickets = tickets.Where(t => t.Status == TicketStatus.PendingApproval || Active.Contains(t.Status) || t.Status == TicketStatus.Resolved);
        if (q.CategoryId is { } categoryId)
            tickets = tickets.Where(t => t.CategoryId == categoryId);
        switch (q.Assignee)
        {
            case "me":
                tickets = tickets.Where(t => me != null && t.AssigneeEmployeeId == me);
                break;
            case "none":
                tickets = tickets.Where(t => t.AssigneeEmployeeId == null);
                break;
            case { } other when Guid.TryParse(other, out var assigneeId):
                tickets = tickets.Where(t => t.AssigneeEmployeeId == assigneeId);
                break;
        }

        var rows = Project(tickets, DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = $"%{q.Search.Trim()}%";
            rows = rows.Where(r => EF.Functions.ILike(r.Code, s) || EF.Functions.ILike(r.Subject, s)
                || EF.Functions.ILike(r.EmployeeName, s) || EF.Functions.ILike(r.EmployeeCode, s));
        }

        var list = await rows.OrderByDescending(r => r.LastActivityAt).Take(500).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<TicketDto> Handle(GetTicketQuery q, CancellationToken ct)
    {
        var t = await db.HelpdeskTickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Request", q.Id);
        await access.SeeAsync(t, ct);
        var me = await access.MeAsync(ct);
        var now = DateTime.UtcNow;

        var row = await Project(db.HelpdeskTickets.AsNoTracking().Where(x => x.Id == t.Id), now).FirstAsync(ct);
        var canWork = access.CanWork(t, me);
        var isRequester = access.IsRequester(t, me);
        var userId = access.UserId;

        var activities = await db.TicketActivities.AsNoTracking()
            .Where(a => a.TicketId == t.Id && (canWork || a.Kind != TicketActivityKind.InternalNote))
            .OrderBy(a => a.At)
            .Select(a => new TicketActivityDto(
                a.Id, a.Kind, a.FromStatus, a.ToStatus, a.Note,
                a.ByUserId == null ? null : db.Employees.Where(e => e.UserId == a.ByUserId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
                userId != null && a.ByUserId == userId, a.At))
            .ToListAsync(ct);

        var final = t.IsFinal;
        return new TicketDto(
            ToDto(row), t.Description, t.Link, t.DecisionNote, t.DecidedAt, t.OpenedAt, t.FirstResponseAt, t.Resolution, t.ClosedAt, activities,
            isRequester,
            CanEdit: !final && (canWork || (isRequester && (t.Status == TicketStatus.PendingApproval || (t.Status == TicketStatus.Open && t.FirstResponseAt == null)))),
            CanComment: !final && (isRequester || canWork),
            CanInternalNote: !final && canWork,
            CanApprove: t.Status == TicketStatus.PendingApproval && (access.IsApprover(t, me) || access.IsAgent),
            CanWork: !final && canWork && t.Status != TicketStatus.PendingApproval,
            CanAssign: !final && access.IsAgent && t.Status != TicketStatus.PendingApproval,
            CanConfirm: isRequester && t.Status == TicketStatus.Resolved,
            CanReopen: (isRequester || canWork) && (t.Status == TicketStatus.Resolved
                || (t.Status == TicketStatus.Closed && t.ClosedAt is { } closed && closed.AddDays(30) >= now)),
            CanCancel: (isRequester || access.IsAgent) && t.Status is TicketStatus.PendingApproval or TicketStatus.Open or TicketStatus.WaitingOnEmployee,
            CanRate: isRequester && t.Status is TicketStatus.Resolved or TicketStatus.Closed,
            t.RowVersion);
    }

    private IQueryable<TicketRow> Project(IQueryable<HelpdeskTicket> tickets, DateTime now)
        => from t in tickets
           join e in db.Employees on t.EmployeeId equals e.Id
           join c in db.HelpdeskCategories on t.CategoryId equals c.Id
           select new TicketRow
           {
               Id = t.Id,
               Code = t.Code,
               Subject = t.Subject,
               CategoryId = c.Id,
               CategoryName = c.Name,
               CategoryIcon = c.Icon,
               EmployeeId = e.Id,
               EmployeeName = e.FirstName + " " + e.LastName,
               EmployeeCode = e.EmployeeCode,
               DepartmentName = e.Department.Name,
               Priority = t.Priority,
               Status = t.Status,
               IsConfidential = t.IsConfidential,
               AssigneeEmployeeId = t.AssigneeEmployeeId,
               AssigneeName = db.Employees.Where(a => a.Id == t.AssigneeEmployeeId).Select(a => a.FirstName + " " + a.LastName).FirstOrDefault(),
               ApproverEmployeeId = t.ApproverEmployeeId,
               ApproverName = db.Employees.Where(a => a.Id == t.ApproverEmployeeId).Select(a => a.FirstName + " " + a.LastName).FirstOrDefault(),
               CreatedAt = t.CreatedAt,
               LastActivityAt = t.LastActivityAt,
               DueAt = t.DueAt,
               IsOverdue = (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingOnEmployee)
                   && t.DueAt != null && t.DueAt < now,
               ResolvedAt = t.ResolvedAt,
               SatisfactionRating = t.SatisfactionRating
           };

    private static TicketListItemDto ToDto(TicketRow r) => new(
        r.Id, r.Code, r.Subject, r.CategoryId, r.CategoryName, r.CategoryIcon, r.EmployeeId, r.EmployeeName, r.EmployeeCode, r.DepartmentName,
        r.Priority, r.Status, r.IsConfidential, r.AssigneeEmployeeId, r.AssigneeName, r.ApproverEmployeeId, r.ApproverName,
        r.CreatedAt, r.LastActivityAt, r.DueAt, r.IsOverdue, r.ResolvedAt, r.SatisfactionRating);

    private sealed class TicketRow
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = default!;
        public string Subject { get; init; } = default!;
        public Guid CategoryId { get; init; }
        public string CategoryName { get; init; } = default!;
        public string? CategoryIcon { get; init; }
        public Guid EmployeeId { get; init; }
        public string EmployeeName { get; init; } = default!;
        public string EmployeeCode { get; init; } = default!;
        public string DepartmentName { get; init; } = default!;
        public TicketPriority Priority { get; init; }
        public TicketStatus Status { get; init; }
        public bool IsConfidential { get; init; }
        public Guid? AssigneeEmployeeId { get; init; }
        public string? AssigneeName { get; init; }
        public Guid? ApproverEmployeeId { get; init; }
        public string? ApproverName { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime LastActivityAt { get; init; }
        public DateTime? DueAt { get; init; }
        public bool IsOverdue { get; init; }
        public DateTime? ResolvedAt { get; init; }
        public byte? SatisfactionRating { get; init; }
    }
}

#endregion

#region Ticket commands

public sealed class TicketCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateTicketCommand, Guid>,
    IRequestHandler<UpdateTicketCommand>,
    IRequestHandler<ApproveTicketCommand>,
    IRequestHandler<RejectTicketCommand>,
    IRequestHandler<AssignTicketCommand>,
    IRequestHandler<SetTicketStatusCommand>,
    IRequestHandler<CommentTicketCommand>,
    IRequestHandler<ConfirmTicketCommand>,
    IRequestHandler<RateTicketCommand>,
    IRequestHandler<ReopenTicketCommand>,
    IRequestHandler<CancelTicketCommand>
{
    private readonly HelpdeskAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateTicketCommand c, CancellationToken ct)
    {
        var d = c.Data;
        var me = await access.MeAsync(ct);
        Guid employeeId;
        if (d.EmployeeId is { } forId && forId != Guid.Empty && forId != me)
        {
            if (!access.IsAgent)
                throw new UnauthorizedAccessException("Only the helpdesk team can raise a request for someone else.");
            employeeId = forId;
        }
        else
            employeeId = me ?? throw new DomainException("Your login is not linked to an employee record. Ask HR to link it.");

        var employee = await db.Employees.AsNoTracking().Where(e => e.Id == employeeId)
            .Select(e => new { e.ManagerId, e.EmploymentStatus }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Employee", employeeId);
        if (employee.EmploymentStatus == EmploymentStatus.Exited)
            throw new DomainException("This employee has left the company.");

        var category = await db.HelpdeskCategories.FirstOrDefaultAsync(x => x.Id == d.CategoryId, ct)
            ?? throw new NotFoundException("Category", d.CategoryId);

        Guid? approver = null;
        if (category.NeedsManagerApproval && employee.ManagerId is { } managerId)
        {
            var managerActive = await db.Employees.AnyAsync(e => e.Id == managerId && e.EmploymentStatus != EmploymentStatus.Exited, ct);
            approver = managerActive ? managerId : null;
        }

        var ticket = HelpdeskTicket.Create(
            currentUser.RequireTenantId(), await NextCodeAsync(ct), employeeId, category, d.Subject, d.Description, d.Link,
            d.Priority, approver, currentUser.UserId, DateTime.UtcNow);
        db.HelpdeskTickets.Add(ticket);
        await SaveAsync(ct);
        return ticket.Id;
    }

    public async Task Handle(UpdateTicketCommand c, CancellationToken ct)
    {
        var t = await TicketAsync(c.Id, ct);
        var me = await access.MeAsync(ct);
        var canWork = access.CanWork(t, me);
        var requesterMayEdit = access.IsRequester(t, me)
            && (t.Status == TicketStatus.PendingApproval || (t.Status == TicketStatus.Open && t.FirstResponseAt == null));
        if (!canWork && !requesterMayEdit)
            throw new UnauthorizedAccessException("This request can no longer be changed.");

        var d = c.Data;
        t.Edit(d.Subject, d.Description, d.Link, d.Priority);
        if (d.CategoryId != t.CategoryId)
        {
            if (!access.IsAgent)
                throw new UnauthorizedAccessException("Only the helpdesk team can move a request to another category.");
            if (t.Status == TicketStatus.PendingApproval)
                throw new DomainException("Wait for the manager's approval before changing the category.");
            var category = await db.HelpdeskCategories.FirstOrDefaultAsync(x => x.Id == d.CategoryId, ct)
                ?? throw new NotFoundException("Category", d.CategoryId);
            if (!category.IsActive)
                throw new DomainException("This category is no longer available.");
            t.ChangeCategory(category, currentUser.UserId, DateTime.UtcNow);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ApproveTicketCommand c, CancellationToken ct)
    {
        var t = await TicketAsync(c.Id, ct);
        await EnsureApproverAsync(t, ct);
        var category = await db.HelpdeskCategories.FirstAsync(x => x.Id == t.CategoryId, ct);
        t.Approve(c.Note, currentUser.UserId, category, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RejectTicketCommand c, CancellationToken ct)
    {
        var t = await TicketAsync(c.Id, ct);
        await EnsureApproverAsync(t, ct);
        t.Reject(c.Note ?? "", currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(AssignTicketCommand c, CancellationToken ct)
    {
        if (!access.IsAgent)
            throw new UnauthorizedAccessException("Only the helpdesk team can assign requests.");
        var t = await TicketAsync(c.Id, ct);
        string? name = null;
        if (c.AssigneeEmployeeId is { } id && id != Guid.Empty)
        {
            var person = await db.Employees.AsNoTracking().Where(e => e.Id == id)
                .Select(e => new { Name = e.FirstName + " " + e.LastName, e.EmploymentStatus }).FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Employee", id);
            if (person.EmploymentStatus == EmploymentStatus.Exited)
                throw new DomainException("This person has left the company.");
            if (id == t.EmployeeId)
                throw new DomainException("A request cannot be assigned to the person who raised it.");
            name = person.Name;
        }
        t.Assign(c.AssigneeEmployeeId, name, currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SetTicketStatusCommand c, CancellationToken ct)
    {
        var t = await TicketAsync(c.Id, ct);
        await EnsureCanWorkAsync(t, ct);
        t.SetStatus(c.Status, c.Note, currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CommentTicketCommand c, CancellationToken ct)
    {
        var t = await TicketAsync(c.Id, ct);
        var me = await access.MeAsync(ct);
        var canWork = access.CanWork(t, me);
        if (!canWork && !access.IsRequester(t, me))
            throw new UnauthorizedAccessException("You cannot comment on this request.");
        // Requester khud agent ho to bhi apne ticket par employee ki tarah jawab deta hai
        var asAgent = canWork && !access.IsRequester(t, me);
        if (c.Internal && !canWork)
            throw new UnauthorizedAccessException("Only the helpdesk team can add internal notes.");
        t.Comment(c.Body, c.Internal, asAgent || c.Internal, currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ConfirmTicketCommand c, CancellationToken ct)
    {
        var t = await TicketAsync(c.Id, ct);
        await EnsureRequesterAsync(t, ct);
        t.Confirm(c.Rating, currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RateTicketCommand c, CancellationToken ct)
    {
        var t = await TicketAsync(c.Id, ct);
        await EnsureRequesterAsync(t, ct);
        t.Rate(c.Rating, currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ReopenTicketCommand c, CancellationToken ct)
    {
        var t = await TicketAsync(c.Id, ct);
        var me = await access.MeAsync(ct);
        if (!access.IsRequester(t, me) && !access.CanWork(t, me))
            throw new UnauthorizedAccessException("You cannot reopen this request.");
        t.Reopen(c.Reason ?? "", currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelTicketCommand c, CancellationToken ct)
    {
        var t = await TicketAsync(c.Id, ct);
        var me = await access.MeAsync(ct);
        if (!access.IsRequester(t, me) && !access.IsAgent)
            throw new UnauthorizedAccessException("Only the person who raised this request can cancel it.");
        t.Cancel(c.Reason, currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<HelpdeskTicket> TicketAsync(Guid id, CancellationToken ct)
    {
        var t = await db.HelpdeskTickets.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Request", id);
        return await access.SeeAsync(t, ct);
    }

    private async Task EnsureApproverAsync(HelpdeskTicket t, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        if (!access.IsApprover(t, me) && !access.IsAgent)
            throw new UnauthorizedAccessException("Only the employee's manager can approve this request.");
        if (access.IsRequester(t, me))
            throw new UnauthorizedAccessException("You cannot approve your own request.");
    }

    private async Task EnsureCanWorkAsync(HelpdeskTicket t, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        if (!access.CanWork(t, me))
            throw new UnauthorizedAccessException("Only the helpdesk team or the assignee can work on this request.");
        if (access.IsRequester(t, me) && !access.IsAgent)
            throw new UnauthorizedAccessException("You cannot work on your own request.");
    }

    private async Task EnsureRequesterAsync(HelpdeskTicket t, CancellationToken ct)
    {
        if (!access.IsRequester(t, await access.MeAsync(ct)))
            throw new UnauthorizedAccessException("Only the person who raised this request can do this.");
    }

    /// <summary>HD-0001, HD-0002 ... (deleted bhi gino taake code dobara na mile).</summary>
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var tenantId = currentUser.RequireTenantId();
        var codes = await db.HelpdeskTickets.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Code.StartsWith(HelpdeskTicket.CodePrefix))
            .Select(t => t.Code).ToListAsync(ct);
        var max = codes.Select(x => int.TryParse(x[HelpdeskTicket.CodePrefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{HelpdeskTicket.CodePrefix}{max + 1:D4}";
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_HelpdeskTickets_TenantId_Code") == true)
        {
            throw new ConflictException("Someone raised a request at the same moment. Try again.");
        }
    }
}

#endregion

#region Category commands

public sealed class HelpdeskCategoryCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateCategoryCommand, Guid>,
    IRequestHandler<UpdateCategoryCommand>,
    IRequestHandler<DeleteCategoryCommand>,
    IRequestHandler<AddDefaultCategoriesCommand, int>
{
    private readonly HelpdeskAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateCategoryCommand c, CancellationToken ct)
    {
        access.EnsureCanConfigure();
        var details = await DetailsAsync(c.Data, null, ct);
        var category = HelpdeskCategory.Create(currentUser.RequireTenantId(), details);
        db.HelpdeskCategories.Add(category);
        await SaveAsync(ct);
        return category.Id;
    }

    public async Task Handle(UpdateCategoryCommand c, CancellationToken ct)
    {
        access.EnsureCanConfigure();
        var category = await db.HelpdeskCategories.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Category", c.Id);
        category.Update(await DetailsAsync(c.Data, category, ct));
        await SaveAsync(ct);
    }

    public async Task Handle(DeleteCategoryCommand c, CancellationToken ct)
    {
        access.EnsureCanConfigure();
        var category = await db.HelpdeskCategories.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Category", c.Id);
        if (await db.HelpdeskTickets.IgnoreQueryFilters().AnyAsync(t => t.CategoryId == category.Id, ct))
            throw new ConflictException("This category has requests. Turn it off instead of deleting it.");
        db.HelpdeskCategories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> Handle(AddDefaultCategoriesCommand c, CancellationToken ct)
    {
        access.EnsureCanConfigure();
        var tenantId = currentUser.RequireTenantId();
        var existing = (await db.HelpdeskCategories.Select(x => x.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var d in Defaults.Where(d => !existing.Contains(d.Name)))
        {
            db.HelpdeskCategories.Add(HelpdeskCategory.Create(tenantId, d));
            added++;
        }
        await SaveAsync(ct);
        return added;
    }

    private static readonly CategoryDetails[] Defaults =
    [
        new("Salary certificate", "A letter confirming your salary, for a bank, landlord or embassy.", "request_quote", false, false, 48, null, 10, true),
        new("Employment / experience letter", "A letter confirming your job, role and dates of employment.", "workspace_premium", false, false, 72, null, 20, true),
        new("NOC or visa letter", "No-objection or travel letter. Your manager approves it first.", "flight", true, false, 72, null, 30, true),
        new("Payroll question", "Questions about your payslip, deductions or tax. Only HR sees these.", "payments", false, true, 48, null, 40, true),
        new("Leave and attendance", "Balances, missing punches the attendance page cannot fix, holidays.", "event_busy", false, false, 48, null, 50, true),
        new("Update my details", "Bank account, address, emergency contact or name changes.", "badge", false, false, 72, null, 60, true),
        new("IT and system access", "Logins, email, software and access to company systems.", "computer", false, false, 24, null, 70, true),
        new("Equipment request", "A laptop, phone or other equipment. Your manager approves it first.", "devices", true, false, 120, null, 80, true),
        new("Confidential concern", "Something you want to raise privately with HR. Only HR sees these.", "shield_person", false, true, 24, null, 90, true),
        new("Something else", "Anything that does not fit the other categories.", "help", false, false, 72, null, 100, true)
    ];

    private async Task<CategoryDetails> DetailsAsync(SaveCategoryRequest r, HelpdeskCategory? existing, CancellationToken ct)
    {
        if (r.DefaultAssigneeEmployeeId is { } id && id != Guid.Empty && id != existing?.DefaultAssigneeEmployeeId
            && !await db.Employees.AnyAsync(e => e.Id == id && e.EmploymentStatus != EmploymentStatus.Exited, ct))
            throw new DomainException("Pick an active employee as the default assignee.");
        var name = (r.Name ?? "").Trim().ToLower();
        var existingId = existing?.Id;
        if (await db.HelpdeskCategories.AnyAsync(c => c.Id != existingId && c.Name.ToLower() == name, ct))
            throw new ConflictException("A category with this name already exists.");
        return new CategoryDetails(r.Name, r.Description, r.Icon, r.NeedsManagerApproval, r.IsConfidential, r.ResolutionHours,
            r.DefaultAssigneeEmployeeId, r.SortOrder, r.IsActive);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_HelpdeskCategories_TenantId_Name") == true)
        {
            throw new ConflictException("A category with this name already exists.");
        }
    }
}

#endregion
