namespace HR.Employee.API.Application.Performance;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Performance;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Performance page: review cycles (HR), reviews (HR sab, manager apni team / jin ka reviewer hai),
// goals + check-ins (employee apne, manager team ke, HR sab) aur "My performance".
// HR: employees.view dekhna, employees.edit chalana. Manager = Employees.ManagerId ya review ka ReviewerEmployeeId.

#region DTOs

public sealed record PerformanceSummaryDto(
    Guid? ActiveCycleId, string? ActiveCycleName, DateOnly? SelfReviewDue, DateOnly? ManagerReviewDue,
    int Reviews, int SelfPending, int ManagerPending, int Shared, int Acknowledged, int OverdueReviews, decimal? AverageRating,
    int GoalsOpen, int GoalsAtRisk, int GoalsCompleted, int GoalsOverdue, int MyActions, bool CanManage);

public sealed record ReviewCycleDto(
    Guid Id, string Name, string? Description, DateOnly PeriodStart, DateOnly PeriodEnd, bool IncludeSelfReview,
    DateOnly? SelfReviewDue, DateOnly ManagerReviewDue, CycleStatus Status, DateTime? LaunchedAt, DateTime? ClosedAt,
    int Reviews, int SelfPending, int ManagerPending, int Shared, int Acknowledged, decimal? AverageRating,
    IReadOnlyList<int> RatingCounts, uint RowVersion);

public sealed record ReviewListItemDto(
    Guid Id, Guid CycleId, string CycleName, CycleStatus CycleStatus,
    Guid EmployeeId, string EmployeeCode, string EmployeeName, string? DepartmentName, string? DesignationTitle,
    Guid? ReviewerEmployeeId, string? ReviewerName, ReviewStatus Status, byte? SelfRating, byte? ManagerRating,
    DateTime? SelfSubmittedAt, DateTime? ManagerSubmittedAt, DateTime? AcknowledgedAt, DateOnly? DueOn, bool IsOverdue);

public sealed record GoalCheckInDto(Guid Id, byte Progress, GoalStatus Status, string? Note, string? ByName, DateTime At);

public sealed record GoalListItemDto(
    Guid Id, Guid EmployeeId, string EmployeeCode, string EmployeeName, string? DepartmentName,
    Guid? CycleId, string? CycleName, string Title, string? Description, byte? Weight, DateOnly? StartDate, DateOnly DueDate,
    byte Progress, GoalStatus Status, DateTime? CompletedAt, DateTime? LastCheckInAt, bool IsOverdue, bool CanEdit);

public sealed record GoalDto(GoalListItemDto Goal, IReadOnlyList<GoalCheckInDto> CheckIns, uint RowVersion);

public sealed record ReviewDto(
    Guid Id, Guid CycleId, string CycleName, CycleStatus CycleStatus, DateOnly PeriodStart, DateOnly PeriodEnd,
    bool IncludeSelfReview, DateOnly? SelfReviewDue, DateOnly ManagerReviewDue,
    Guid EmployeeId, string EmployeeCode, string EmployeeName, string? DepartmentName, string? DesignationTitle,
    Guid? ReviewerEmployeeId, string? ReviewerName, ReviewStatus Status,
    byte? SelfRating, string? SelfSummary, DateTime? SelfSubmittedAt,
    byte? ManagerRating, string? ManagerSummary, string? Strengths, string? Improvements, DateTime? ManagerSubmittedAt, string? ManagerSubmittedByName,
    string? EmployeeComment, DateTime? AcknowledgedAt, DateOnly? DueOn, bool IsOverdue,
    bool IsSelf, bool CanEditSelf, bool CanEditManager, bool CanManage, bool CanAcknowledge,
    IReadOnlyList<GoalListItemDto> Goals);

public sealed record MyPerformanceDto(bool Linked, IReadOnlyList<ReviewListItemDto> Reviews, IReadOnlyList<GoalListItemDto> Goals,
    IReadOnlyList<ReviewListItemDto> ToReview);

public sealed record PerformancePersonDto(Guid Id, string EmployeeCode, string Name, string? DepartmentName, bool IsSelf);

public sealed record LaunchResultDto(int Created, int Skipped);

#endregion

#region Requests

public sealed record GetPerformanceSummaryQuery : IRequest<PerformanceSummaryDto>;
public sealed record GetReviewCyclesQuery : IRequest<IReadOnlyList<ReviewCycleDto>>;
public sealed record GetReviewsQuery(Guid? CycleId, ReviewStatus? Status, bool? Overdue, string? Search) : IRequest<IReadOnlyList<ReviewListItemDto>>;
public sealed record GetReviewQuery(Guid Id) : IRequest<ReviewDto>;
public sealed record GetGoalsQuery(Guid? EmployeeId, Guid? CycleId, GoalStatus? Status, bool? Overdue, string? Search) : IRequest<IReadOnlyList<GoalListItemDto>>;
public sealed record GetGoalQuery(Guid Id) : IRequest<GoalDto>;
public sealed record GetMyPerformanceQuery : IRequest<MyPerformanceDto>;
public sealed record GetPerformancePeopleQuery : IRequest<IReadOnlyList<PerformancePersonDto>>;

public sealed record SaveReviewCycleRequest(
    string Name, string? Description, DateOnly PeriodStart, DateOnly PeriodEnd, bool IncludeSelfReview,
    DateOnly? SelfReviewDue, DateOnly ManagerReviewDue);
public sealed record CreateReviewCycleCommand(SaveReviewCycleRequest Data) : IRequest<Guid>;
public sealed record UpdateReviewCycleCommand(Guid Id, SaveReviewCycleRequest Data) : IRequest;
public sealed record DeleteReviewCycleCommand(Guid Id) : IRequest;

/// <summary>Launch: departments khali = sab active/probation employees jo period khatam hone tak join kar chuke.</summary>
public sealed record LaunchCycleRequest(IReadOnlyList<Guid>? DepartmentIds);
public sealed record LaunchReviewCycleCommand(Guid Id, LaunchCycleRequest Data) : IRequest<LaunchResultDto>;
public sealed record AddReviewsRequest(IReadOnlyList<Guid> EmployeeIds);
public sealed record AddReviewsCommand(Guid CycleId, AddReviewsRequest Data) : IRequest<LaunchResultDto>;
public sealed record CloseReviewCycleCommand(Guid Id) : IRequest;

public sealed record SaveSelfReviewRequest(byte? Rating, string? Summary, bool Submit);
public sealed record SaveSelfReviewCommand(Guid Id, SaveSelfReviewRequest Data) : IRequest;
public sealed record SaveManagerReviewRequest(byte? Rating, string? Summary, string? Strengths, string? Improvements, bool Submit);
public sealed record SaveManagerReviewCommand(Guid Id, SaveManagerReviewRequest Data) : IRequest;
public sealed record AcknowledgeReviewCommand(Guid Id, string? Comment) : IRequest;
public sealed record ReopenReviewCommand(Guid Id, bool SelfReview) : IRequest;
public sealed record ChangeReviewerCommand(Guid Id, Guid? ReviewerEmployeeId) : IRequest;
public sealed record DeleteReviewCommand(Guid Id) : IRequest;

public sealed record SaveGoalRequest(Guid EmployeeId, string Title, string? Description, Guid? CycleId, byte? Weight, DateOnly? StartDate, DateOnly DueDate);
public sealed record CreateGoalCommand(SaveGoalRequest Data) : IRequest<Guid>;
public sealed record UpdateGoalCommand(Guid Id, SaveGoalRequest Data) : IRequest;
public sealed record DeleteGoalCommand(Guid Id) : IRequest;
public sealed record GoalCheckInRequest(byte Progress, GoalStatus Status, string? Note);
public sealed record CheckInGoalCommand(Guid Id, GoalCheckInRequest Data) : IRequest;
public sealed record CancelGoalCommand(Guid Id, string? Note) : IRequest;
public sealed record ReopenGoalCommand(Guid Id) : IRequest;

#endregion

#region Validators

public sealed class SaveReviewCycleRequestValidator : AbstractValidator<SaveReviewCycleRequest>
{
    public SaveReviewCycleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart).WithMessage("Period end cannot be before the period start.");
        RuleFor(x => x.SelfReviewDue).NotNull().When(x => x.IncludeSelfReview).WithMessage("Set the self review due date.");
        RuleFor(x => x.SelfReviewDue).LessThanOrEqualTo(x => x.ManagerReviewDue)
            .When(x => x.IncludeSelfReview && x.SelfReviewDue is not null)
            .WithMessage("Self reviews must be due on or before manager reviews.");
    }
}

public sealed class CreateReviewCycleValidator : AbstractValidator<CreateReviewCycleCommand>
{
    public CreateReviewCycleValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveReviewCycleRequestValidator());
}

public sealed class UpdateReviewCycleValidator : AbstractValidator<UpdateReviewCycleCommand>
{
    public UpdateReviewCycleValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveReviewCycleRequestValidator());
}

public sealed class SaveSelfReviewValidator : AbstractValidator<SaveSelfReviewCommand>
{
    public SaveSelfReviewValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Rating).InclusiveBetween((byte)1, (byte)5).When(x => x.Data.Rating is not null);
        RuleFor(x => x.Data.Summary).MaximumLength(4000);
    }
}

public sealed class SaveManagerReviewValidator : AbstractValidator<SaveManagerReviewCommand>
{
    public SaveManagerReviewValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Rating).InclusiveBetween((byte)1, (byte)5).When(x => x.Data.Rating is not null);
        RuleFor(x => x.Data.Summary).MaximumLength(4000);
        RuleFor(x => x.Data.Strengths).MaximumLength(2000);
        RuleFor(x => x.Data.Improvements).MaximumLength(2000);
    }
}

public sealed class AcknowledgeReviewValidator : AbstractValidator<AcknowledgeReviewCommand>
{
    public AcknowledgeReviewValidator() => RuleFor(x => x.Comment).MaximumLength(2000);
}

public sealed class AddReviewsValidator : AbstractValidator<AddReviewsCommand>
{
    public AddReviewsValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.EmployeeIds).NotEmpty().WithMessage("Select at least one employee.");
        RuleFor(x => x.Data.EmployeeIds.Count).LessThanOrEqualTo(500);
    }
}

public sealed class SaveGoalRequestValidator : AbstractValidator<SaveGoalRequest>
{
    public SaveGoalRequestValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Weight).LessThanOrEqualTo((byte)100).When(x => x.Weight is not null);
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.StartDate!.Value).When(x => x.StartDate is not null)
            .WithMessage("Due date cannot be before the start date.");
    }
}

public sealed class CreateGoalValidator : AbstractValidator<CreateGoalCommand>
{
    public CreateGoalValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveGoalRequestValidator());
}

public sealed class UpdateGoalValidator : AbstractValidator<UpdateGoalCommand>
{
    public UpdateGoalValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveGoalRequestValidator());
}

public sealed class CheckInGoalValidator : AbstractValidator<CheckInGoalCommand>
{
    public CheckInGoalValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Progress).LessThanOrEqualTo((byte)100);
        RuleFor(x => x.Data.Status).IsInEnum().NotEqual(GoalStatus.Cancelled);
        RuleFor(x => x.Data.Note).MaximumLength(1000);
    }
}

public sealed class CancelGoalValidator : AbstractValidator<CancelGoalCommand>
{
    public CancelGoalValidator() => RuleFor(x => x.Note).MaximumLength(1000);
}

#endregion

#region Access

public sealed class PerformanceAccess(IAppDbContext db, ICurrentUser currentUser)
{
    private Guid? me;
    private bool loaded;

    public bool IsHr => currentUser.HasPermission(Permissions.EmployeesView);
    public bool CanManage => currentUser.HasPermission(Permissions.EmployeesEdit);

    public void EnsureCanManage()
    {
        if (!CanManage)
            throw new UnauthorizedAccessException("You do not have permission to manage performance reviews.");
    }

    public void EnsureHr()
    {
        if (!IsHr)
            throw new UnauthorizedAccessException("You do not have permission to see review cycles.");
    }

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

    public async Task<Guid> RequireMeAsync(CancellationToken ct)
        => await MeAsync(ct) ?? throw new UnauthorizedAccessException("Your login is not linked to an employee record.");

    /// <summary>HR ke ilawa: khud ya direct report.</summary>
    public async Task<bool> CanWorkOnGoalsOfAsync(Guid employeeId, CancellationToken ct)
    {
        if (CanManage)
            return true;
        if (await MeAsync(ct) is not { } my)
            return false;
        return employeeId == my || await db.Employees.AnyAsync(e => e.Id == employeeId && e.ManagerId == my, ct);
    }

    public async Task<bool> CanSeeEmployeeAsync(Guid employeeId, CancellationToken ct)
        => IsHr || await CanWorkOnGoalsOfAsync(employeeId, ct);
}

#endregion

#region Queries

public sealed class PerformanceQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetPerformanceSummaryQuery, PerformanceSummaryDto>,
    IRequestHandler<GetReviewCyclesQuery, IReadOnlyList<ReviewCycleDto>>,
    IRequestHandler<GetReviewsQuery, IReadOnlyList<ReviewListItemDto>>,
    IRequestHandler<GetReviewQuery, ReviewDto>,
    IRequestHandler<GetGoalsQuery, IReadOnlyList<GoalListItemDto>>,
    IRequestHandler<GetGoalQuery, GoalDto>,
    IRequestHandler<GetMyPerformanceQuery, MyPerformanceDto>,
    IRequestHandler<GetPerformancePeopleQuery, IReadOnlyList<PerformancePersonDto>>
{
    private readonly PerformanceAccess access = new(db, currentUser);

    public async Task<PerformanceSummaryDto> Handle(GetPerformanceSummaryQuery q, CancellationToken ct)
    {
        var today = Today;
        var me = await access.MeAsync(ct);

        var active = await db.ReviewCycles.AsNoTracking()
            .Where(c => c.Status == CycleStatus.Active)
            .OrderByDescending(c => c.PeriodEnd)
            .Select(c => new { c.Id, c.Name, c.SelfReviewDue, c.ManagerReviewDue })
            .FirstOrDefaultAsync(ct);

        var reviews = await VisibleReviews(me).Where(x => x.c.Status == CycleStatus.Active)
            .Select(x => new
            {
                x.r.Status, x.r.ManagerRating,
                Overdue = (x.r.Status == ReviewStatus.SelfReview && x.c.SelfReviewDue < today) ||
                          (x.r.Status <= ReviewStatus.ManagerReview && x.c.ManagerReviewDue < today)
            })
            .ToListAsync(ct);
        int Count(ReviewStatus s) => reviews.Count(r => r.Status == s);
        var rated = reviews.Where(r => r.Status >= ReviewStatus.Shared && r.ManagerRating != null).Select(r => (decimal)r.ManagerRating!.Value).ToList();

        var goals = await VisibleGoals(me)
            .Select(x => new { x.g.Status, Overdue = x.g.DueDate < today && x.g.Status != GoalStatus.Completed && x.g.Status != GoalStatus.Cancelled })
            .ToListAsync(ct);

        var myActions = 0;
        if (me is { } my)
        {
            myActions = await (from r in db.PerformanceReviews
                               join c in db.ReviewCycles on r.ReviewCycleId equals c.Id
                               where c.Status == CycleStatus.Active
                                     && ((r.EmployeeId == my && (r.Status == ReviewStatus.SelfReview || r.Status == ReviewStatus.Shared))
                                         || (r.ReviewerEmployeeId == my && r.Status == ReviewStatus.ManagerReview))
                               select r.Id).CountAsync(ct);
        }

        return new PerformanceSummaryDto(
            active?.Id, active?.Name, active?.SelfReviewDue, active?.ManagerReviewDue,
            reviews.Count, Count(ReviewStatus.SelfReview), Count(ReviewStatus.ManagerReview), Count(ReviewStatus.Shared),
            Count(ReviewStatus.Acknowledged), reviews.Count(r => r.Overdue), rated.Count == 0 ? null : Math.Round(rated.Average(), 2),
            goals.Count(g => g.Status is not (GoalStatus.Completed or GoalStatus.Cancelled)),
            goals.Count(g => g.Status is GoalStatus.AtRisk or GoalStatus.OffTrack),
            goals.Count(g => g.Status == GoalStatus.Completed), goals.Count(g => g.Overdue), myActions, access.CanManage);
    }

    public async Task<IReadOnlyList<ReviewCycleDto>> Handle(GetReviewCyclesQuery q, CancellationToken ct)
    {
        // Filters ke liye sab ko list chahiye; draft aur ginti sirf HR ko.
        var cycles = await db.ReviewCycles.AsNoTracking()
            .Where(c => access.IsHr || c.Status != CycleStatus.Draft)
            .OrderByDescending(c => c.Status == CycleStatus.Active).ThenByDescending(c => c.PeriodEnd).ThenBy(c => c.Name)
            .ToListAsync(ct);

        var stats = access.IsHr
            ? await db.PerformanceReviews.AsNoTracking()
                .GroupBy(r => new { r.ReviewCycleId, r.Status, r.ManagerRating })
                .Select(g => new { g.Key.ReviewCycleId, g.Key.Status, g.Key.ManagerRating, Count = g.Count() })
                .ToListAsync(ct)
            : [];

        return cycles.Select(c =>
        {
            var s = stats.Where(x => x.ReviewCycleId == c.Id).ToList();
            int Count(ReviewStatus status) => s.Where(x => x.Status == status).Sum(x => x.Count);
            var final = s.Where(x => x.Status >= ReviewStatus.Shared && x.ManagerRating != null).ToList();
            var counts = Enumerable.Range(1, 5).Select(n => final.Where(x => x.ManagerRating == n).Sum(x => x.Count)).ToList();
            var total = counts.Sum();
            decimal? avg = total == 0 ? null : Math.Round((decimal)counts.Select((n, i) => n * (i + 1)).Sum() / total, 2);
            return new ReviewCycleDto(c.Id, c.Name, c.Description, c.PeriodStart, c.PeriodEnd, c.IncludeSelfReview, c.SelfReviewDue,
                c.ManagerReviewDue, c.Status, c.LaunchedAt, c.ClosedAt, s.Sum(x => x.Count), Count(ReviewStatus.SelfReview),
                Count(ReviewStatus.ManagerReview), Count(ReviewStatus.Shared), Count(ReviewStatus.Acknowledged), avg, counts, c.RowVersion);
        }).ToList();
    }

    public async Task<IReadOnlyList<ReviewListItemDto>> Handle(GetReviewsQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        if (!access.IsHr && me is null)
            return [];
        var today = Today;

        var rows = VisibleReviews(me).Where(x => x.c.Status != CycleStatus.Draft);
        if (q.CycleId is { } cycleId) rows = rows.Where(x => x.r.ReviewCycleId == cycleId);
        if (q.Status is { } status) rows = rows.Where(x => x.r.Status == status);
        if (q.Overdue == true) rows = rows.Where(x => x.c.Status == CycleStatus.Active &&
            ((x.r.Status == ReviewStatus.SelfReview && x.c.SelfReviewDue < today) ||
             (x.r.Status <= ReviewStatus.ManagerReview && x.c.ManagerReviewDue < today)));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            rows = rows.Where(x => EF.Functions.ILike(x.e.FirstName + " " + x.e.LastName, term) || EF.Functions.ILike(x.e.EmployeeCode, term)
                                   || (x.rv != null && EF.Functions.ILike(x.rv.FirstName + " " + x.rv.LastName, term)));
        }

        return await rows
            .OrderByDescending(x => x.c.PeriodEnd).ThenBy(x => x.r.Status).ThenBy(x => x.e.FirstName).ThenBy(x => x.e.LastName)
            .Take(1000)
            .Select(ToListItem(today))
            .ToListAsync(ct);
    }

    public async Task<ReviewDto> Handle(GetReviewQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var today = Today;

        var x = await ReviewRows().Where(x => x.r.Id == q.Id).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Review", q.Id);
        var isSelf = me == x.r.EmployeeId;
        var isReviewer = me is not null && me == x.r.ReviewerEmployeeId;
        var isLineManager = me is not null && me == x.e.ManagerId;
        if (!access.IsHr && !isSelf && !isReviewer && !isLineManager)
            throw new UnauthorizedAccessException("You cannot see this review.");

        var active = x.c.Status == CycleStatus.Active;
        // Employee ko manager ka hissa share hone ke baad; doosron ko self review submit hone ke baad.
        var showManager = !isSelf || x.r.Status >= ReviewStatus.Shared;
        var showSelf = isSelf || x.r.SelfSubmittedAt != null;

        string? submittedBy = x.r.ManagerSubmittedByUserId is { } byUser && showManager
            ? await db.Employees.AsNoTracking().Where(e => e.UserId == byUser).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync(ct)
            : null;

        var goals = await GoalRows(db.Goals.AsNoTracking().Where(g => g.EmployeeId == x.r.EmployeeId &&
                (g.ReviewCycleId == x.r.ReviewCycleId || (g.ReviewCycleId == null && g.DueDate >= x.c.PeriodStart && g.DueDate <= x.c.PeriodEnd))))
            .Where(g => g.g.Status != GoalStatus.Cancelled)
            .OrderBy(g => g.g.DueDate).Take(100)
            .Select(ToGoalItem(today, false)).ToListAsync(ct);
        var canEditGoals = access.CanManage || isSelf || isLineManager;

        var dueOn = x.r.Status == ReviewStatus.SelfReview ? x.c.SelfReviewDue : x.r.Status == ReviewStatus.ManagerReview ? x.c.ManagerReviewDue : null;
        return new ReviewDto(
            x.r.Id, x.c.Id, x.c.Name, x.c.Status, x.c.PeriodStart, x.c.PeriodEnd, x.c.IncludeSelfReview, x.c.SelfReviewDue, x.c.ManagerReviewDue,
            x.e.Id, x.e.EmployeeCode, x.e.FirstName + " " + x.e.LastName, x.DepartmentName, x.DesignationTitle,
            x.r.ReviewerEmployeeId, x.rv == null ? null : x.rv.FirstName + " " + x.rv.LastName, x.r.Status,
            showSelf ? x.r.SelfRating : null, showSelf ? x.r.SelfSummary : null, x.r.SelfSubmittedAt,
            showManager ? x.r.ManagerRating : null, showManager ? x.r.ManagerSummary : null, showManager ? x.r.Strengths : null,
            showManager ? x.r.Improvements : null, x.r.ManagerSubmittedAt, submittedBy,
            x.r.EmployeeComment, x.r.AcknowledgedAt, active ? dueOn : null, active && dueOn < today,
            isSelf,
            CanEditSelf: active && isSelf && x.r.Status == ReviewStatus.SelfReview,
            CanEditManager: active && !isSelf && (isReviewer || access.CanManage) && x.r.Status <= ReviewStatus.ManagerReview,
            CanManage: access.CanManage && !isSelf && active,
            CanAcknowledge: isSelf && x.r.Status == ReviewStatus.Shared,
            goals.Select(g => g with { CanEdit = canEditGoals && g.Status is not (GoalStatus.Completed or GoalStatus.Cancelled) }).ToList());
    }

    public async Task<IReadOnlyList<GoalListItemDto>> Handle(GetGoalsQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        if (!access.IsHr && me is null)
            return [];
        var today = Today;

        var rows = VisibleGoals(me);
        if (q.EmployeeId is { } employeeId) rows = rows.Where(x => x.g.EmployeeId == employeeId);
        if (q.CycleId is { } cycleId) rows = rows.Where(x => x.g.ReviewCycleId == cycleId);
        if (q.Status is { } status) rows = rows.Where(x => x.g.Status == status);
        if (q.Overdue == true) rows = rows.Where(x => x.g.DueDate < today && x.g.Status != GoalStatus.Completed && x.g.Status != GoalStatus.Cancelled);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            rows = rows.Where(x => EF.Functions.ILike(x.g.Title, term) || EF.Functions.ILike(x.e.FirstName + " " + x.e.LastName, term)
                                   || EF.Functions.ILike(x.e.EmployeeCode, term));
        }

        var list = await rows
            .OrderBy(x => x.g.Status == GoalStatus.Completed || x.g.Status == GoalStatus.Cancelled)
            .ThenBy(x => x.g.DueDate).ThenBy(x => x.e.FirstName)
            .Take(1000)
            .Select(ToGoalItem(today, false))
            .ToListAsync(ct);
        return await WithCanEditAsync(list, me, ct);
    }

    public async Task<GoalDto> Handle(GetGoalQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var today = Today;
        var item = await GoalRows(db.Goals.AsNoTracking().Where(g => g.Id == q.Id)).Select(ToGoalItem(today, false)).FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Goal", q.Id);
        if (!await access.CanSeeEmployeeAsync(item.EmployeeId, ct))
            throw new UnauthorizedAccessException("You cannot see this goal.");

        var rowVersion = await db.Goals.AsNoTracking().Where(g => g.Id == q.Id).Select(g => g.RowVersion).FirstAsync(ct);
        var checkIns = await (from g in db.Goals.AsNoTracking()
                              where g.Id == q.Id
                              from c in g.CheckIns
                              orderby c.At descending
                              select new GoalCheckInDto(c.Id, c.Progress, c.Status, c.Note,
                                  c.ByUserId == null ? null : db.Employees.Where(e => e.UserId == c.ByUserId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
                                  c.At))
            .Take(200).ToListAsync(ct);

        var edited = await WithCanEditAsync([item], me, ct);
        return new GoalDto(edited[0], checkIns, rowVersion);
    }

    public async Task<MyPerformanceDto> Handle(GetMyPerformanceQuery q, CancellationToken ct)
    {
        if (await access.MeAsync(ct) is not { } me)
            return new MyPerformanceDto(false, [], [], []);
        var today = Today;

        var reviews = await ReviewRows().Where(x => x.r.EmployeeId == me && x.c.Status != CycleStatus.Draft)
            .OrderByDescending(x => x.c.PeriodEnd).Take(50).Select(ToListItem(today)).ToListAsync(ct);
        // Apna manager wala hissa share hone se pehle nahi
        reviews = reviews.Select(r => r.Status >= ReviewStatus.Shared ? r : r with { ManagerRating = null }).ToList();

        var toReview = await ReviewRows()
            .Where(x => x.r.ReviewerEmployeeId == me && x.c.Status == CycleStatus.Active && x.r.Status <= ReviewStatus.ManagerReview)
            .OrderBy(x => x.r.Status == ReviewStatus.ManagerReview ? 0 : 1).ThenBy(x => x.e.FirstName)
            .Take(200).Select(ToListItem(today)).ToListAsync(ct);

        var since = today.AddMonths(-12);
        var goals = await GoalRows(db.Goals.AsNoTracking().Where(g => g.EmployeeId == me &&
                ((g.Status != GoalStatus.Completed && g.Status != GoalStatus.Cancelled) || g.DueDate >= since)))
            .OrderBy(x => x.g.Status == GoalStatus.Completed || x.g.Status == GoalStatus.Cancelled).ThenBy(x => x.g.DueDate)
            .Take(200).Select(ToGoalItem(today, true)).ToListAsync(ct);
        goals = goals.Select(g => g with { CanEdit = g.Status is not (GoalStatus.Completed or GoalStatus.Cancelled) }).ToList();

        return new MyPerformanceDto(true, reviews, goals, toReview);
    }

    /// <summary>Goal form / reviewer ke liye log: HR ko sab, warna khud + direct reports.</summary>
    public async Task<IReadOnlyList<PerformancePersonDto>> Handle(GetPerformancePeopleQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var people = db.Employees.AsNoTracking().Where(e => e.EmploymentStatus != EmploymentStatus.Exited);
        if (!access.CanManage)
        {
            if (me is null)
                return [];
            people = people.Where(e => e.Id == me || e.ManagerId == me);
        }

        return await people.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).Take(2000)
            .Select(e => new PerformancePersonDto(e.Id, e.EmployeeCode, e.FirstName + " " + e.LastName, e.Department.Name, e.Id == me))
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<GoalListItemDto>> WithCanEditAsync(IReadOnlyList<GoalListItemDto> list, Guid? me, CancellationToken ct)
    {
        HashSet<Guid> team = [];
        if (!access.CanManage && me is { } my)
        {
            var ids = list.Select(g => g.EmployeeId).Distinct().ToList();
            team = (await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id) && e.ManagerId == my).Select(e => e.Id).ToListAsync(ct)).ToHashSet();
            team.Add(my);
        }
        return list.Select(g => g with
        {
            CanEdit = (access.CanManage || team.Contains(g.EmployeeId)) && g.Status is not (GoalStatus.Completed or GoalStatus.Cancelled)
        }).ToList();
    }

    /// <summary>Init properties (constructor record nahi) — EF baad ke Where/OrderBy mein in members ko SQL bana sake.</summary>
    internal sealed class ReviewRow
    {
        public PerformanceReview r { get; init; } = default!;
        public ReviewCycle c { get; init; } = default!;
        public Employee e { get; init; } = default!;
        public Employee? rv { get; init; }
        public string? DepartmentName { get; init; }
        public string? DesignationTitle { get; init; }
    }

    private IQueryable<ReviewRow> ReviewRows()
        => from r in db.PerformanceReviews.AsNoTracking()
           join c in db.ReviewCycles on r.ReviewCycleId equals c.Id
           join e in db.Employees on r.EmployeeId equals e.Id
           join rv in db.Employees on r.ReviewerEmployeeId equals rv.Id into rvs
           from rv in rvs.DefaultIfEmpty()
           select new ReviewRow { r = r, c = c, e = e, rv = rv, DepartmentName = e.Department.Name, DesignationTitle = e.Designation.Title };

    /// <summary>HR: sab. Warna: jin ka main reviewer hoon ya jo mujhe report karte hain (apna review "My" tab mein).</summary>
    private IQueryable<ReviewRow> VisibleReviews(Guid? me)
    {
        var rows = ReviewRows();
        if (access.IsHr)
            return rows;
        return me is { } my
            ? rows.Where(x => x.r.ReviewerEmployeeId == my || x.e.ManagerId == my)
            : rows.Where(x => false);
    }

    private static System.Linq.Expressions.Expression<Func<ReviewRow, ReviewListItemDto>> ToListItem(DateOnly today)
        => x => new ReviewListItemDto(
            x.r.Id, x.c.Id, x.c.Name, x.c.Status, x.e.Id, x.e.EmployeeCode, x.e.FirstName + " " + x.e.LastName, x.DepartmentName, x.DesignationTitle,
            x.r.ReviewerEmployeeId, x.rv == null ? null : x.rv.FirstName + " " + x.rv.LastName, x.r.Status,
            x.r.SelfSubmittedAt == null ? null : x.r.SelfRating, x.r.ManagerRating,
            x.r.SelfSubmittedAt, x.r.ManagerSubmittedAt, x.r.AcknowledgedAt,
            x.c.Status != CycleStatus.Active ? null
                : x.r.Status == ReviewStatus.SelfReview ? x.c.SelfReviewDue
                : x.r.Status == ReviewStatus.ManagerReview ? x.c.ManagerReviewDue : null,
            x.c.Status == CycleStatus.Active &&
            ((x.r.Status == ReviewStatus.SelfReview && x.c.SelfReviewDue < today) ||
             (x.r.Status <= ReviewStatus.ManagerReview && x.c.ManagerReviewDue < today)));

    internal sealed class GoalRow
    {
        public Goal g { get; init; } = default!;
        public Employee e { get; init; } = default!;
        public string? DepartmentName { get; init; }
        public string? CycleName { get; init; }
        public DateTime? LastCheckInAt { get; init; }
    }

    private IQueryable<GoalRow> GoalRows(IQueryable<Goal> goals)
        => from g in goals
           join e in db.Employees on g.EmployeeId equals e.Id
           join c in db.ReviewCycles on g.ReviewCycleId equals c.Id into cs
           from c in cs.DefaultIfEmpty()
           select new GoalRow
           {
               g = g, e = e, DepartmentName = e.Department.Name, CycleName = c == null ? null : c.Name,
               LastCheckInAt = g.CheckIns.Max(x => (DateTime?)x.At)
           };

    private IQueryable<GoalRow> VisibleGoals(Guid? me)
    {
        var rows = GoalRows(db.Goals.AsNoTracking());
        if (access.IsHr)
            return rows;
        return me is { } my ? rows.Where(x => x.g.EmployeeId == my || x.e.ManagerId == my) : rows.Where(x => false);
    }

    private static System.Linq.Expressions.Expression<Func<GoalRow, GoalListItemDto>> ToGoalItem(DateOnly today, bool canEdit)
        => x => new GoalListItemDto(
            x.g.Id, x.e.Id, x.e.EmployeeCode, x.e.FirstName + " " + x.e.LastName, x.DepartmentName,
            x.g.ReviewCycleId, x.CycleName, x.g.Title, x.g.Description, x.g.Weight, x.g.StartDate, x.g.DueDate,
            x.g.Progress, x.g.Status, x.g.CompletedAt, x.LastCheckInAt,
            x.g.DueDate < today && x.g.Status != GoalStatus.Completed && x.g.Status != GoalStatus.Cancelled, canEdit);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}

#endregion

#region Cycle commands

public sealed class ReviewCycleHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateReviewCycleCommand, Guid>,
    IRequestHandler<UpdateReviewCycleCommand>,
    IRequestHandler<DeleteReviewCycleCommand>,
    IRequestHandler<LaunchReviewCycleCommand, LaunchResultDto>,
    IRequestHandler<AddReviewsCommand, LaunchResultDto>,
    IRequestHandler<CloseReviewCycleCommand>
{
    private readonly PerformanceAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateReviewCycleCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var d = c.Data;
        await EnsureNameIsFreeAsync(d.Name, null, ct);

        var cycle = ReviewCycle.Create(currentUser.RequireTenantId(), d.Name, d.Description, d.PeriodStart, d.PeriodEnd,
            d.IncludeSelfReview, d.SelfReviewDue, d.ManagerReviewDue);
        db.ReviewCycles.Add(cycle);
        await SaveAsync(ct);
        return cycle.Id;
    }

    public async Task Handle(UpdateReviewCycleCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var cycle = await CycleAsync(c.Id, ct);
        var d = c.Data;
        await EnsureNameIsFreeAsync(d.Name, cycle.Id, ct);

        cycle.Update(d.Name, d.Description, d.PeriodStart, d.PeriodEnd, d.IncludeSelfReview, d.SelfReviewDue, d.ManagerReviewDue);
        await SaveAsync(ct);
    }

    /// <summary>Sirf draft (launch ke baad reviews ki history bachani hai).</summary>
    public async Task Handle(DeleteReviewCycleCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var cycle = await CycleAsync(c.Id, ct);
        if (cycle.Status != CycleStatus.Draft)
            throw new ConflictException("Only a draft cycle can be deleted. Close it instead.");
        if (await db.Goals.AnyAsync(g => g.ReviewCycleId == cycle.Id, ct))
            throw new ConflictException("Goals are linked to this cycle. Move them to another cycle first.");

        db.ReviewCycles.Remove(cycle);
        await SaveAsync(ct);
    }

    public async Task<LaunchResultDto> Handle(LaunchReviewCycleCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var cycle = await CycleAsync(c.Id, ct);
        if (cycle.Status != CycleStatus.Draft)
            throw new ConflictException("Only a draft cycle can be launched.");

        var employees = db.Employees.AsNoTracking().Where(e =>
            (e.EmploymentStatus == EmploymentStatus.Active || e.EmploymentStatus == EmploymentStatus.Probation) && e.JoiningDate <= cycle.PeriodEnd);
        if (c.Data.DepartmentIds is { Count: > 0 } departments)
            employees = employees.Where(e => departments.Contains(e.DepartmentId));
        var people = await employees.Select(e => new { e.Id, e.ManagerId }).ToListAsync(ct);
        if (people.Count == 0)
            throw new ConflictException("No active employees match. Check the departments and the review period.");

        cycle.Launch(DateTime.UtcNow);
        var tenantId = currentUser.RequireTenantId();
        foreach (var p in people)
            db.PerformanceReviews.Add(PerformanceReview.Create(tenantId, cycle, p.Id, p.ManagerId));
        await SaveAsync(ct);
        return new LaunchResultDto(people.Count, 0);
    }

    /// <summary>Launch ke baad naye/chhoote hue employees ko cycle mein daalna.</summary>
    public async Task<LaunchResultDto> Handle(AddReviewsCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var cycle = await CycleAsync(c.CycleId, ct);
        cycle.EnsureActive();

        var ids = c.Data.EmployeeIds.Distinct().ToList();
        var people = await db.Employees.AsNoTracking()
            .Where(e => ids.Contains(e.Id) && e.EmploymentStatus != EmploymentStatus.Exited)
            .Select(e => new { e.Id, e.ManagerId }).ToListAsync(ct);
        var existing = await db.PerformanceReviews.AsNoTracking()
            .Where(r => r.ReviewCycleId == cycle.Id && ids.Contains(r.EmployeeId)).Select(r => r.EmployeeId).ToListAsync(ct);

        var tenantId = currentUser.RequireTenantId();
        var created = 0;
        foreach (var p in people.Where(p => !existing.Contains(p.Id)))
        {
            db.PerformanceReviews.Add(PerformanceReview.Create(tenantId, cycle, p.Id, p.ManagerId));
            created++;
        }
        await SaveAsync(ct);
        return new LaunchResultDto(created, ids.Count - created);
    }

    public async Task Handle(CloseReviewCycleCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var cycle = await CycleAsync(c.Id, ct);
        cycle.Close(DateTime.UtcNow);
        await SaveAsync(ct);
    }

    private async Task<ReviewCycle> CycleAsync(Guid id, CancellationToken ct)
        => await db.ReviewCycles.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Review cycle", id);

    private async Task EnsureNameIsFreeAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await db.ReviewCycles.AnyAsync(x => x.Name.ToLower() == trimmed.ToLower() && x.Id != exceptId, ct))
            throw new ConflictException($"A review cycle called {trimmed} already exists.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_PerformanceReviews_Cycle_Employee") == true)
        {
            throw new ConflictException("Some employees were just added to this cycle by someone else. Refresh and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_ReviewCycles_TenantId_Name") == true)
        {
            throw new ConflictException("A review cycle with this name already exists.");
        }
    }
}

#endregion

#region Review commands

public sealed class ReviewCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<SaveSelfReviewCommand>,
    IRequestHandler<SaveManagerReviewCommand>,
    IRequestHandler<AcknowledgeReviewCommand>,
    IRequestHandler<ReopenReviewCommand>,
    IRequestHandler<ChangeReviewerCommand>,
    IRequestHandler<DeleteReviewCommand>
{
    private readonly PerformanceAccess access = new(db, currentUser);

    public async Task Handle(SaveSelfReviewCommand c, CancellationToken ct)
    {
        var me = await access.RequireMeAsync(ct);
        var (review, cycle) = await ReviewAsync(c.Id, ct);
        if (review.EmployeeId != me)
            throw new UnauthorizedAccessException("Only the employee can write their self review.");
        cycle.EnsureActive();

        var d = c.Data;
        review.SaveSelf(d.Rating, d.Summary, d.Submit, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SaveManagerReviewCommand c, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var (review, cycle) = await ReviewAsync(c.Id, ct);
        if (me is not null && review.EmployeeId == me)
            throw new UnauthorizedAccessException("You cannot write the manager review of your own performance.");
        if (!access.CanManage && (me is null || review.ReviewerEmployeeId != me))
            throw new UnauthorizedAccessException("Only the reviewer can write this review.");
        cycle.EnsureActive();

        var d = c.Data;
        review.SaveManager(d.Rating, d.Summary, d.Strengths, d.Improvements, d.Submit, currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Employee ne padh liya (agree karna zaroori nahi — comment mein apni baat likh sakta hai).</summary>
    public async Task Handle(AcknowledgeReviewCommand c, CancellationToken ct)
    {
        var me = await access.RequireMeAsync(ct);
        var (review, _) = await ReviewAsync(c.Id, ct);
        if (review.EmployeeId != me)
            throw new UnauthorizedAccessException("Only the employee can acknowledge their review.");

        review.Acknowledge(c.Comment, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ReopenReviewCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var (review, cycle) = await ReviewAsync(c.Id, ct);
        await EnsureNotOwnAsync(review, ct);
        cycle.EnsureActive();

        if (c.SelfReview) review.ReopenSelf(cycle);
        else review.Reopen();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ChangeReviewerCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var (review, cycle) = await ReviewAsync(c.Id, ct);
        await EnsureNotOwnAsync(review, ct);
        cycle.EnsureActive();

        if (c.ReviewerEmployeeId is { } reviewerId && reviewerId != Guid.Empty)
        {
            var status = await db.Employees.AsNoTracking().Where(e => e.Id == reviewerId).Select(e => (EmploymentStatus?)e.EmploymentStatus).FirstOrDefaultAsync(ct)
                         ?? throw new NotFoundException("Employee", reviewerId);
            if (status == EmploymentStatus.Exited)
                throw new ConflictException("The reviewer has exited.");
        }
        review.ChangeReviewer(c.ReviewerEmployeeId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Galti se cycle mein aaya employee — sirf jab kisi ne kuch likha na ho.</summary>
    public async Task Handle(DeleteReviewCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var (review, cycle) = await ReviewAsync(c.Id, ct);
        cycle.EnsureActive();
        if (review.HasStarted)
            throw new ConflictException("This review has been started. It cannot be removed.");

        db.PerformanceReviews.Remove(review);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureNotOwnAsync(PerformanceReview review, CancellationToken ct)
    {
        if (await access.MeAsync(ct) == review.EmployeeId)
            throw new UnauthorizedAccessException("You cannot change your own review.");
    }

    private async Task<(PerformanceReview, ReviewCycle)> ReviewAsync(Guid id, CancellationToken ct)
    {
        var review = await db.PerformanceReviews.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Review", id);
        var cycle = await db.ReviewCycles.FirstAsync(c => c.Id == review.ReviewCycleId, ct);
        return (review, cycle);
    }
}

#endregion

#region Goal commands

public sealed class GoalCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateGoalCommand, Guid>,
    IRequestHandler<UpdateGoalCommand>,
    IRequestHandler<DeleteGoalCommand>,
    IRequestHandler<CheckInGoalCommand>,
    IRequestHandler<CancelGoalCommand>,
    IRequestHandler<ReopenGoalCommand>
{
    private readonly PerformanceAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateGoalCommand c, CancellationToken ct)
    {
        var d = c.Data;
        await EnsureCanWorkOnAsync(d.EmployeeId, ct);
        var status = await db.Employees.AsNoTracking().Where(e => e.Id == d.EmployeeId).Select(e => (EmploymentStatus?)e.EmploymentStatus).FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Employee", d.EmployeeId);
        if (status == EmploymentStatus.Exited)
            throw new ConflictException("This employee has exited.");
        await EnsureCycleAsync(d.CycleId, null, ct);

        var goal = Goal.Create(currentUser.RequireTenantId(), d.EmployeeId, ToDetails(d));
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);
        return goal.Id;
    }

    public async Task Handle(UpdateGoalCommand c, CancellationToken ct)
    {
        var goal = await GoalAsync(c.Id, ct);
        await EnsureCanWorkOnAsync(goal.EmployeeId, ct);
        if (c.Data.EmployeeId != goal.EmployeeId)
            throw new ConflictException("A goal cannot move to another employee.");
        await EnsureCycleAsync(c.Data.CycleId, goal.ReviewCycleId, ct);

        goal.Update(ToDetails(c.Data));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Check-in na hua ho to delete; warna cancel (history bachao).</summary>
    public async Task Handle(DeleteGoalCommand c, CancellationToken ct)
    {
        var goal = await GoalAsync(c.Id, ct);
        await EnsureCanWorkOnAsync(goal.EmployeeId, ct);
        if (await db.GoalCheckIns.AnyAsync(x => x.GoalId == goal.Id, ct))
            throw new ConflictException("This goal has progress updates. Cancel it instead of deleting.");

        db.Goals.Remove(goal);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CheckInGoalCommand c, CancellationToken ct)
    {
        var goal = await GoalAsync(c.Id, ct);
        await EnsureCanWorkOnAsync(goal.EmployeeId, ct);
        goal.CheckIn(c.Data.Progress, c.Data.Status, c.Data.Note, currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelGoalCommand c, CancellationToken ct)
    {
        var goal = await GoalAsync(c.Id, ct);
        await EnsureCanWorkOnAsync(goal.EmployeeId, ct);
        goal.Cancel(c.Note, currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ReopenGoalCommand c, CancellationToken ct)
    {
        var goal = await GoalAsync(c.Id, ct);
        await EnsureCanWorkOnAsync(goal.EmployeeId, ct);
        goal.Reopen(currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureCanWorkOnAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await access.CanWorkOnGoalsOfAsync(employeeId, ct))
            throw new UnauthorizedAccessException("You can only manage your own goals and your team's goals.");
    }

    /// <summary>Band cycle mein naya goal nahi (pehle se juda goal wahin rahe to theek).</summary>
    private async Task EnsureCycleAsync(Guid? cycleId, Guid? currentCycleId, CancellationToken ct)
    {
        if (cycleId is not { } id || id == Guid.Empty || id == currentCycleId)
            return;
        var status = await db.ReviewCycles.AsNoTracking().Where(x => x.Id == id).Select(x => (CycleStatus?)x.Status).FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Review cycle", id);
        if (status == CycleStatus.Closed)
            throw new ConflictException("This review cycle is closed.");
    }

    private async Task<Goal> GoalAsync(Guid id, CancellationToken ct)
        => await db.Goals.FirstOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Goal", id);

    private static GoalDetails ToDetails(SaveGoalRequest d) => new(d.Title, d.Description, d.CycleId, d.Weight, d.StartDate, d.DueDate);
}

#endregion
