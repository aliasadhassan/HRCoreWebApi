namespace HR.Employee.API.Application.Learning;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Learning;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Learning & growth: course catalogue, assignments/enrollments aur progress. Skills + career Skills.cs mein.

#region DTOs

public sealed record LearningSummaryDto(
    int MyOpen, int MyOverdue, int MyCompletedThisYear, decimal MyHoursThisYear,
    int TeamOpen, int TeamOverdue, int TeamCompletedThisYear, int ActiveCourses, int SkillCount, int ActiveGoals,
    bool CanManage, bool CanSeeAll, bool IsManager, Guid? MyEmployeeId, bool HasCourses, bool HasSkills);

public sealed record LearningOptionDto(Guid Id, string Name, string? Extra);

public sealed record LearningLookupsDto(
    IReadOnlyList<LearningOptionDto> Departments, IReadOnlyList<LearningOptionDto> People,
    IReadOnlyList<LearningOptionDto> Designations, IReadOnlyList<LearningOptionDto> Skills);

public sealed record CourseListItemDto(
    Guid Id, string Title, CourseCategory Category, CourseFormat Format, string? Provider, string? Description, string? LinkUrl,
    decimal? DurationHours, Guid? SkillId, string? SkillName, byte? SkillLevel, bool AllowSelfEnrol, short? CompleteWithinDays, bool IsActive,
    int OpenCount, int CompletedCount, decimal? AverageRating,
    Guid? MyEnrollmentId, EnrollmentStatus? MyStatus, byte? MyProgress, DateOnly? MyDueOn, DateOnly? MyCompletedOn, uint RowVersion);

public sealed record EnrollmentDto(
    Guid Id, Guid CourseId, string CourseTitle, CourseCategory Category, CourseFormat Format, decimal? DurationHours, string? LinkUrl,
    Guid EmployeeId, string EmployeeName, string EmployeeCode, string DepartmentName,
    EnrollmentSource Source, EnrollmentStatus Status, byte Progress, DateOnly? DueOn, bool IsOverdue, int? DaysLeft, string? AssignedByName,
    DateTime? StartedAt, DateOnly? CompletedOn, byte? Score, byte? Rating, string? Feedback, string? CertificateUrl, DateTime CreatedAt,
    bool CanUpdate, bool CanManage);

public sealed record CourseDetailDto(
    CourseListItemDto Course, IReadOnlyList<EnrollmentDto> Enrollments,
    bool CanEdit, bool CanAssign, bool CanEnrol, bool CanDelete);

public sealed record AssignResultDto(int Assigned, int Skipped);

#endregion

#region Requests

public sealed record GetLearningSummaryQuery : IRequest<LearningSummaryDto>;
public sealed record GetLearningLookupsQuery : IRequest<LearningLookupsDto>;
public sealed record GetCoursesQuery(CourseCategory? Category, string? Search, bool IncludeInactive) : IRequest<IReadOnlyList<CourseListItemDto>>;
public sealed record GetCourseQuery(Guid Id) : IRequest<CourseDetailDto>;

/// <summary>Scope: mine | team (HR = sab, manager = reports).</summary>
public sealed record GetEnrollmentsQuery(
    string? Scope, EnrollmentStatus? Status, bool OverdueOnly, Guid? CourseId, Guid? EmployeeId, Guid? DepartmentId, string? Search)
    : IRequest<IReadOnlyList<EnrollmentDto>>;

public sealed record SaveCourseRequest(
    string Title, CourseCategory Category, CourseFormat Format, string? Provider, string? Description, string? LinkUrl,
    decimal? DurationHours, Guid? SkillId, byte? SkillLevel, bool AllowSelfEnrol, short? CompleteWithinDays, bool IsActive);
public sealed record CreateCourseCommand(SaveCourseRequest Data) : IRequest<Guid>;
public sealed record UpdateCourseCommand(Guid Id, SaveCourseRequest Data) : IRequest;
public sealed record DeleteCourseCommand(Guid Id) : IRequest;

public sealed record EnrolInCourseCommand(Guid CourseId) : IRequest<Guid>;
public sealed record AssignCourseRequest(IReadOnlyList<Guid> EmployeeIds, DateOnly? DueOn);
public sealed record AssignCourseCommand(Guid CourseId, AssignCourseRequest Data) : IRequest<AssignResultDto>;

public sealed record SetEnrollmentProgressCommand(Guid Id, byte Progress) : IRequest;
public sealed record CompleteEnrollmentRequest(DateOnly? CompletedOn, byte? Score, byte? Rating, string? Feedback, string? CertificateUrl);
public sealed record CompleteEnrollmentCommand(Guid Id, CompleteEnrollmentRequest Data) : IRequest;
public sealed record CancelEnrollmentCommand(Guid Id) : IRequest;
public sealed record ChangeEnrollmentDueCommand(Guid Id, DateOnly? DueOn) : IRequest;

public sealed class SaveCourseRequestValidator : AbstractValidator<SaveCourseRequest>
{
    public SaveCourseRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Format).IsInEnum();
        RuleFor(x => x.Provider).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.LinkUrl).MaximumLength(1000);
        RuleFor(x => x.DurationHours).GreaterThan(0).LessThanOrEqualTo(1000).When(x => x.DurationHours is not null);
        RuleFor(x => x.SkillLevel).InclusiveBetween((byte)1, (byte)5).When(x => x.SkillLevel is not null);
        RuleFor(x => x.CompleteWithinDays).InclusiveBetween((short)1, (short)365).When(x => x.CompleteWithinDays is not null);
    }
}

public sealed class CreateCourseValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveCourseRequestValidator());
}

public sealed class UpdateCourseValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveCourseRequestValidator());
}

public sealed class AssignCourseValidator : AbstractValidator<AssignCourseCommand>
{
    public AssignCourseValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.EmployeeIds).NotEmpty().WithMessage("Pick at least one person.")
            .Must(ids => ids.Count <= 500).WithMessage("Assign to at most 500 people at a time.");
    }
}

public sealed class CompleteEnrollmentValidator : AbstractValidator<CompleteEnrollmentCommand>
{
    public CompleteEnrollmentValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Score).InclusiveBetween((byte)0, (byte)100).When(x => x.Data.Score is not null);
        RuleFor(x => x.Data.Rating).InclusiveBetween((byte)1, (byte)5).When(x => x.Data.Rating is not null);
        RuleFor(x => x.Data.Feedback).MaximumLength(1000);
        RuleFor(x => x.Data.CertificateUrl).MaximumLength(1000);
    }
}

#endregion

internal static class LearningClock
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    public static DateOnly YearStart => new(Today.Year, 1, 1);
}

public sealed class CourseQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetLearningSummaryQuery, LearningSummaryDto>,
    IRequestHandler<GetLearningLookupsQuery, LearningLookupsDto>,
    IRequestHandler<GetCoursesQuery, IReadOnlyList<CourseListItemDto>>,
    IRequestHandler<GetCourseQuery, CourseDetailDto>,
    IRequestHandler<GetEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>
{
    private readonly LearningAccess access = new(db, currentUser);

    public async Task<LearningSummaryDto> Handle(GetLearningSummaryQuery q, CancellationToken ct)
    {
        var today = LearningClock.Today;
        var yearStart = LearningClock.YearStart;
        var me = await access.MeAsync(ct);
        var isManager = await access.IsManagerAsync(ct);

        var mine = me is { } my
            ? await db.CourseEnrollments.AsNoTracking().Where(e => e.EmployeeId == my)
                .Join(db.Courses.IgnoreQueryFilters(), e => e.CourseId, c => c.Id, (e, c) => new { e.Status, e.DueOn, e.CompletedOn, c.DurationHours })
                .ToListAsync(ct)
            : [];
        bool Open(EnrollmentStatus s) => s is EnrollmentStatus.NotStarted or EnrollmentStatus.InProgress;
        var doneThisYear = mine.Where(e => e.Status == EnrollmentStatus.Completed && e.CompletedOn >= yearStart).ToList();

        int teamOpen = 0, teamOverdue = 0, teamDone = 0;
        if (access.CanSeeAll || isManager)
        {
            var team = (await access.TeamAsync(ct)).Select(e => e.Id);
            var rows = await db.CourseEnrollments.AsNoTracking().Where(e => team.Contains(e.EmployeeId))
                .Select(e => new { e.Status, e.DueOn, e.CompletedOn }).ToListAsync(ct);
            teamOpen = rows.Count(r => Open(r.Status));
            teamOverdue = rows.Count(r => Open(r.Status) && r.DueOn < today);
            teamDone = rows.Count(r => r.Status == EnrollmentStatus.Completed && r.CompletedOn >= yearStart);
        }

        var activeCourses = await db.Courses.CountAsync(c => c.IsActive, ct);
        var skills = await db.Skills.CountAsync(s => s.IsActive, ct);
        var goals = access.CanSeeAll || isManager
            ? await db.CareerGoals.CountAsync(g => g.Status == CareerGoalStatus.Active && (access.CanSeeAll || g.EmployeeId == me
                || db.Employees.Any(e => e.Id == g.EmployeeId && e.ManagerId == me)), ct)
            : await db.CareerGoals.CountAsync(g => g.Status == CareerGoalStatus.Active && g.EmployeeId == me, ct);

        return new LearningSummaryDto(
            mine.Count(e => Open(e.Status)), mine.Count(e => Open(e.Status) && e.DueOn < today), doneThisYear.Count,
            doneThisYear.Sum(e => e.DurationHours ?? 0),
            teamOpen, teamOverdue, teamDone, activeCourses, skills, goals,
            access.CanManage, access.CanSeeAll, isManager, me, await db.Courses.AnyAsync(ct), await db.Skills.AnyAsync(ct));
    }

    public async Task<LearningLookupsDto> Handle(GetLearningLookupsQuery q, CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name)
            .Select(d => new LearningOptionDto(d.Id, d.Name, null)).ToListAsync(ct);
        var designations = await db.Designations.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Level).ThenBy(d => d.Title)
            .Select(d => new LearningOptionDto(d.Id, d.Title, d.Level == null ? null : d.Level.ToString())).ToListAsync(ct);
        var skills = await db.Skills.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name)
            .Select(s => new LearningOptionDto(s.Id, s.Name, s.Category.ToString())).ToListAsync(ct);

        // Sirf woh log jinhein main assign/assess kar sakta hoon: HR (manage) sab, manager apne reports
        var reports = await access.ReportsAsync(ct);
        var people = access.CanManage || reports.Count > 0
            ? await db.Employees.AsNoTracking()
                .Where(e => e.EmploymentStatus != EmploymentStatus.Exited && (access.CanManage || reports.Contains(e.Id)))
                .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
                .Select(e => new LearningOptionDto(e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode + " · " + e.Department.Name))
                .ToListAsync(ct)
            : [];
        return new LearningLookupsDto(departments, people, designations, skills);
    }

    public async Task<IReadOnlyList<CourseListItemDto>> Handle(GetCoursesQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var courses = db.Courses.AsNoTracking();
        if (!(access.CanManage && q.IncludeInactive))
            courses = courses.Where(c => c.IsActive || db.CourseEnrollments.Any(e => e.CourseId == c.Id && e.EmployeeId == me));
        if (q.Category is { } cat)
            courses = courses.Where(c => c.Category == cat);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            courses = courses.Where(c => EF.Functions.ILike(c.Title, term) || (c.Provider != null && EF.Functions.ILike(c.Provider, term)));
        }

        var list = await courses.OrderBy(c => c.Title).ToListAsync(ct);
        return await ToListItemsAsync(list, me, ct);
    }

    public async Task<CourseDetailDto> Handle(GetCourseQuery q, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == q.Id, ct) ?? throw new NotFoundException("Course", q.Id);
        var me = await access.MeAsync(ct);
        var item = (await ToListItemsAsync([course], me, ct))[0];
        if (!course.IsActive && !access.CanManage && item.MyEnrollmentId is null)
            throw new NotFoundException("Course", q.Id);

        var isManager = await access.IsManagerAsync(ct);
        var enrollments = access.CanSeeAll || isManager
            ? await Handle(new GetEnrollmentsQuery("team", null, false, course.Id, null, null, null), ct)
            : [];
        var anyEnrollment = await db.CourseEnrollments.AnyAsync(e => e.CourseId == course.Id, ct);
        return new CourseDetailDto(item, enrollments,
            access.CanManage, course.IsActive && (access.CanManage || isManager),
            course.IsActive && course.AllowSelfEnrol && me is not null && item.MyStatus is not (EnrollmentStatus.NotStarted or EnrollmentStatus.InProgress),
            access.CanManage && !anyEnrollment);
    }

    private async Task<List<CourseListItemDto>> ToListItemsAsync(List<Course> list, Guid? me, CancellationToken ct)
    {
        var ids = list.Select(c => c.Id).ToList();
        var skillIds = list.Where(c => c.SkillId != null).Select(c => c.SkillId!.Value).Distinct().ToList();
        var skills = await db.Skills.AsNoTracking().IgnoreQueryFilters().Where(s => skillIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var stats = await db.CourseEnrollments.AsNoTracking().Where(e => ids.Contains(e.CourseId))
            .GroupBy(e => e.CourseId)
            .Select(g => new
            {
                CourseId = g.Key,
                Open = g.Count(e => e.Status == EnrollmentStatus.NotStarted || e.Status == EnrollmentStatus.InProgress),
                Done = g.Count(e => e.Status == EnrollmentStatus.Completed),
                Rating = g.Average(e => e.Rating == null ? (double?)null : e.Rating)
            })
            .ToDictionaryAsync(x => x.CourseId, ct);
        // Har course par mera taaza enrollment (khula ho to woh, warna aakhri)
        var mine = me is { } my
            ? (await db.CourseEnrollments.AsNoTracking().Where(e => e.EmployeeId == my && ids.Contains(e.CourseId)).ToListAsync(ct))
                .GroupBy(e => e.CourseId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.IsOpen).ThenByDescending(e => e.CreatedAt).First())
            : [];

        return list.Select(c =>
        {
            stats.TryGetValue(c.Id, out var s);
            mine.TryGetValue(c.Id, out var e);
            return new CourseListItemDto(
                c.Id, c.Title, c.Category, c.Format, c.Provider, c.Description, c.LinkUrl, c.DurationHours,
                c.SkillId, c.SkillId is { } sid ? skills.GetValueOrDefault(sid) : null, c.SkillLevel, c.AllowSelfEnrol, c.CompleteWithinDays, c.IsActive,
                s?.Open ?? 0, s?.Done ?? 0, s?.Rating is { } r ? Math.Round((decimal)r, 1) : null,
                e?.Id, e?.Status, e?.Progress, e?.DueOn, e?.CompletedOn, c.RowVersion);
        }).ToList();
    }

    public async Task<IReadOnlyList<EnrollmentDto>> Handle(GetEnrollmentsQuery q, CancellationToken ct)
    {
        var today = LearningClock.Today;
        var me = await access.MeAsync(ct);
        IQueryable<Employee> people;
        if (string.Equals(q.Scope, "team", StringComparison.OrdinalIgnoreCase))
        {
            if (!access.CanSeeAll && !await access.IsManagerAsync(ct))
                throw new UnauthorizedAccessException("You can only see your own learning.");
            people = await access.TeamAsync(ct);
        }
        else
        {
            if (me is not { } my)
                return [];
            people = db.Employees.AsNoTracking().Where(e => e.Id == my);
        }

        if (q.EmployeeId is { } emp)
            people = people.Where(e => e.Id == emp);
        if (q.DepartmentId is { } dept)
            people = people.Where(e => e.DepartmentId == dept);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            people = people.Where(e => EF.Functions.ILike(e.FirstName + " " + e.LastName, term) || EF.Functions.ILike(e.EmployeeCode, term));
        }

        var rows = db.CourseEnrollments.AsNoTracking().Where(e => people.Select(p => p.Id).Contains(e.EmployeeId));
        if (q.CourseId is { } course)
            rows = rows.Where(e => e.CourseId == course);
        if (q.Status is { } status)
            rows = rows.Where(e => e.Status == status);
        if (q.OverdueOnly)
            rows = rows.Where(e => (e.Status == EnrollmentStatus.NotStarted || e.Status == EnrollmentStatus.InProgress) && e.DueOn < today);

        var data = await rows
            .Join(db.Courses.IgnoreQueryFilters(), e => e.CourseId, c => c.Id, (e, c) => new { e, c })
            .Join(db.Employees.IgnoreQueryFilters(), x => x.e.EmployeeId, p => p.Id, (x, p) => new
            {
                x.e, x.c, Name = p.FirstName + " " + p.LastName, Code = p.EmployeeCode, Dept = p.Department.Name
            })
            .OrderBy(x => x.e.Status == EnrollmentStatus.Completed || x.e.Status == EnrollmentStatus.Cancelled)
            .ThenBy(x => x.e.DueOn == null).ThenBy(x => x.e.DueOn).ThenByDescending(x => x.e.CreatedAt)
            .Take(1000)
            .ToListAsync(ct);

        var byIds = data.Where(x => x.e.AssignedByUserId != null).Select(x => x.e.AssignedByUserId!.Value).Distinct().ToList();
        var byNames = await db.Employees.AsNoTracking().Where(p => p.UserId != null && byIds.Contains(p.UserId.Value))
            .Select(p => new { UserId = p.UserId!.Value, Name = p.FirstName + " " + p.LastName }).ToListAsync(ct);
        var reports = await access.ReportsAsync(ct);

        return data.Select(x =>
        {
            var e = x.e;
            var lead = access.CanManage || reports.Contains(e.EmployeeId);
            return new EnrollmentDto(
                e.Id, e.CourseId, x.c.Title, x.c.Category, x.c.Format, x.c.DurationHours, x.c.LinkUrl,
                e.EmployeeId, x.Name, x.Code, x.Dept,
                e.Source, e.Status, e.Progress, e.DueOn, e.IsOverdueOn(today), e.IsOpen && e.DueOn is { } d ? d.DayNumber - today.DayNumber : null,
                byNames.FirstOrDefault(n => n.UserId == e.AssignedByUserId)?.Name,
                e.StartedAt, e.CompletedOn, e.Score, e.Rating, e.Feedback, e.CertificateUrl, e.CreatedAt,
                e.IsOpen && (lead || e.EmployeeId == me), e.IsOpen && lead);
        }).ToList();
    }
}

public sealed class CourseCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateCourseCommand, Guid>,
    IRequestHandler<UpdateCourseCommand>,
    IRequestHandler<DeleteCourseCommand>,
    IRequestHandler<EnrolInCourseCommand, Guid>,
    IRequestHandler<AssignCourseCommand, AssignResultDto>,
    IRequestHandler<SetEnrollmentProgressCommand>,
    IRequestHandler<CompleteEnrollmentCommand>,
    IRequestHandler<CancelEnrollmentCommand>,
    IRequestHandler<ChangeEnrollmentDueCommand>
{
    private const string OpenIndex = "UX_CourseEnrollments_Open";
    private readonly LearningAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateCourseCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var course = Course.Create(access.TenantId, await DetailsAsync(c.Data, null, ct));
        db.Courses.Add(course);
        await SaveCourseAsync(ct);
        return course.Id;
    }

    public async Task Handle(UpdateCourseCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var course = await CourseAsync(c.Id, ct);
        course.Update(await DetailsAsync(c.Data, course, ct));
        await SaveCourseAsync(ct);
    }

    public async Task Handle(DeleteCourseCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var course = await CourseAsync(c.Id, ct);
        if (await db.CourseEnrollments.AnyAsync(e => e.CourseId == course.Id, ct))
            throw new ConflictException("People have taken this course. Make it inactive instead of deleting it.");
        db.Courses.Remove(course);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> Handle(EnrolInCourseCommand c, CancellationToken ct)
    {
        var me = await access.RequireMeAsync(ct);
        var course = await CourseAsync(c.CourseId, ct);
        await EnsureNotOpenAsync(course.Id, me, ct);
        var enrollment = CourseEnrollment.Create(course, me, EnrollmentSource.Self, null, access.UserId, LearningClock.Today);
        db.CourseEnrollments.Add(enrollment);
        await SaveEnrollmentAsync(ct);
        return enrollment.Id;
    }

    public async Task<AssignResultDto> Handle(AssignCourseCommand c, CancellationToken ct)
    {
        var course = await CourseAsync(c.CourseId, ct);
        var ids = c.Data.EmployeeIds.Where(id => id != Guid.Empty).Distinct().ToList();
        foreach (var id in ids)
        {
            if (!await access.CanLeadAsync(id, ct))
                throw new UnauthorizedAccessException("You can only assign courses to your own team.");
        }
        var active = await db.Employees.Where(e => ids.Contains(e.Id) && e.EmploymentStatus != EmploymentStatus.Exited).Select(e => e.Id).ToListAsync(ct);
        var busy = await db.CourseEnrollments
            .Where(e => e.CourseId == course.Id && active.Contains(e.EmployeeId)
                && (e.Status == EnrollmentStatus.NotStarted || e.Status == EnrollmentStatus.InProgress))
            .Select(e => e.EmployeeId).ToListAsync(ct);

        var today = LearningClock.Today;
        var assigned = 0;
        foreach (var id in active.Except(busy))
        {
            db.CourseEnrollments.Add(CourseEnrollment.Create(course, id, EnrollmentSource.Assigned, c.Data.DueOn, access.UserId, today));
            assigned++;
        }
        if (assigned > 0)
            await SaveEnrollmentAsync(ct);
        return new AssignResultDto(assigned, ids.Count - assigned);
    }

    public async Task Handle(SetEnrollmentProgressCommand c, CancellationToken ct)
    {
        var enrollment = await EnrollmentAsync(c.Id, allowSelf: true, ct);
        enrollment.SetProgress(c.Progress, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CompleteEnrollmentCommand c, CancellationToken ct)
    {
        var enrollment = await EnrollmentAsync(c.Id, allowSelf: true, ct);
        var d = c.Data;
        var now = DateTime.UtcNow;
        var today = LearningClock.Today;
        enrollment.Complete(d.CompletedOn ?? today, d.Score, d.Rating, d.Feedback, d.CertificateUrl, today, now);

        // Course jo skill sikhata hai uska level barhao (kabhi kam nahi)
        var course = await db.Courses.IgnoreQueryFilters().AsNoTracking().FirstAsync(x => x.Id == enrollment.CourseId, ct);
        if (course.SkillId is { } skillId && course.SkillLevel is { } level)
        {
            var row = await db.EmployeeSkills.FirstOrDefaultAsync(s => s.EmployeeId == enrollment.EmployeeId && s.SkillId == skillId, ct);
            if (row is null)
                db.EmployeeSkills.Add(EmployeeSkill.Create(enrollment.EmployeeId, skillId, level, SkillSource.Course, access.UserId, now));
            else
                row.RaiseTo(level, access.UserId, now);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelEnrollmentCommand c, CancellationToken ct)
    {
        var enrollment = await EnrollmentAsync(c.Id, allowSelf: true, ct);
        if (enrollment.Source == EnrollmentSource.Assigned && !await access.CanLeadAsync(enrollment.EmployeeId, ct))
            throw new UnauthorizedAccessException("Only HR or your manager can cancel an assigned course.");
        enrollment.Cancel(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ChangeEnrollmentDueCommand c, CancellationToken ct)
    {
        var enrollment = await EnrollmentAsync(c.Id, allowSelf: false, ct);
        enrollment.ChangeDue(c.DueOn, LearningClock.Today);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Course> CourseAsync(Guid id, CancellationToken ct)
        => await db.Courses.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Course", id);

    private async Task<CourseEnrollment> EnrollmentAsync(Guid id, bool allowSelf, CancellationToken ct)
    {
        var enrollment = await db.CourseEnrollments.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Enrollment", id);
        var mine = allowSelf && enrollment.EmployeeId == await access.MeAsync(ct);
        if (!mine && !await access.CanLeadAsync(enrollment.EmployeeId, ct))
            throw new UnauthorizedAccessException("You can only update your own learning or your team's.");
        return enrollment;
    }

    private async Task EnsureNotOpenAsync(Guid courseId, Guid employeeId, CancellationToken ct)
    {
        if (await db.CourseEnrollments.AnyAsync(e => e.CourseId == courseId && e.EmployeeId == employeeId
                && (e.Status == EnrollmentStatus.NotStarted || e.Status == EnrollmentStatus.InProgress), ct))
            throw new ConflictException("You are already enrolled in this course.");
    }

    private async Task<CourseDetails> DetailsAsync(SaveCourseRequest r, Course? existing, CancellationToken ct)
    {
        if (r.SkillId is { } skill && skill != Guid.Empty && skill != existing?.SkillId && !await db.Skills.AnyAsync(s => s.Id == skill && s.IsActive, ct))
            throw new DomainException("Pick an active skill.");
        var title = (r.Title ?? "").Trim().ToLower();
        var existingId = existing?.Id;
        if (await db.Courses.AnyAsync(c => c.Id != existingId && c.Title.ToLower() == title, ct))
            throw new ConflictException("A course with this title already exists.");
        return new CourseDetails(r.Title!, r.Category, r.Format, r.Provider, r.Description, r.LinkUrl, r.DurationHours,
            r.SkillId, r.SkillLevel, r.AllowSelfEnrol, r.CompleteWithinDays, r.IsActive);
    }

    private async Task SaveCourseAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_Courses_TenantId_Title") == true)
        {
            throw new ConflictException("A course with this title already exists.");
        }
    }

    private async Task SaveEnrollmentAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(OpenIndex) == true)
        {
            throw new ConflictException("This person already has this course open. Refresh the page.");
        }
    }
}
