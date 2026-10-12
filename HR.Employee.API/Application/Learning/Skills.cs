namespace HR.Employee.API.Application.Learning;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Learning;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Skills (library, har shakhs ke levels, team matrix) aur career (role ki zaroori skills, agla role, readiness, gaps).

#region DTOs

public sealed record SkillDto(
    Guid Id, string Name, SkillCategory Category, string? Description, bool IsActive,
    int PeopleCount, decimal? AverageLevel, int CourseCount, int RoleCount, uint RowVersion);

public sealed record PersonSkillDto(
    Guid SkillId, string Name, SkillCategory Category, byte Level, SkillSource Source, DateTime UpdatedAt, string? ByName,
    byte? RequiredNow, byte? RequiredNext);

public sealed record SkillGapDto(Guid SkillId, string Name, byte Required, byte? Current, bool Met);

public sealed record CourseSuggestionDto(
    Guid CourseId, string Title, Guid SkillId, byte? SkillLevel, decimal? DurationHours, bool AllowSelfEnrol, EnrollmentStatus? MyStatus);

public sealed record CareerGoalDto(
    Guid TargetDesignationId, string TargetTitle, DateOnly? TargetDate, string? Notes, CareerGoalStatus Status, DateOnly? AchievedOn,
    int? Readiness, IReadOnlyList<SkillGapDto> Gaps, IReadOnlyList<CourseSuggestionDto> Suggestions, DateTime UpdatedAt, string? ByName);

public sealed record PersonGrowthDto(
    Guid EmployeeId, string Name, string Code, string DepartmentName, Guid DesignationId, string DesignationName,
    IReadOnlyList<PersonSkillDto> Skills, int? CurrentRoleReadiness, IReadOnlyList<SkillGapDto> CurrentRoleGaps, CareerGoalDto? Goal,
    bool IsMe, bool CanRate, bool CanAssess, bool CanSetGoal, bool CanMarkAchieved);

public sealed record MatrixSkillDto(Guid Id, string Name, SkillCategory Category);
public sealed record MatrixRowDto(Guid EmployeeId, string Name, string Code, string DepartmentName, string DesignationName, IReadOnlyDictionary<Guid, byte> Levels);
public sealed record SkillMatrixDto(IReadOnlyList<MatrixSkillDto> Skills, IReadOnlyList<MatrixRowDto> People);

public sealed record RoleSkillDto(Guid SkillId, string Name, byte RequiredLevel);
public sealed record CareerRoleDto(Guid DesignationId, string Title, byte? Level, int PeopleCount, int AimingCount, IReadOnlyList<RoleSkillDto> Skills);

public sealed record CareerGoalRowDto(
    Guid EmployeeId, string Name, string Code, string DepartmentName, string CurrentTitle, Guid TargetDesignationId, string TargetTitle,
    DateOnly? TargetDate, CareerGoalStatus Status, DateOnly? AchievedOn, int? Readiness, int GapCount, DateTime UpdatedAt);

#endregion

#region Requests

public sealed record GetSkillsQuery(bool IncludeInactive) : IRequest<IReadOnlyList<SkillDto>>;
public sealed record SaveSkillRequest(string Name, SkillCategory Category, string? Description, bool IsActive);
public sealed record CreateSkillCommand(SaveSkillRequest Data) : IRequest<Guid>;
public sealed record UpdateSkillCommand(Guid Id, SaveSkillRequest Data) : IRequest;
public sealed record DeleteSkillCommand(Guid Id) : IRequest;
/// <summary>Aam skills jo pehle se na hon. Wapas = kitni banin.</summary>
public sealed record AddStarterSkillsCommand : IRequest<int>;

/// <summary>EmployeeId null = main khud.</summary>
public sealed record GetPersonGrowthQuery(Guid? EmployeeId) : IRequest<PersonGrowthDto>;
public sealed record RateSkillCommand(Guid EmployeeId, Guid SkillId, byte Level) : IRequest;
public sealed record RemoveSkillCommand(Guid EmployeeId, Guid SkillId) : IRequest;
public sealed record GetSkillMatrixQuery(Guid? DepartmentId, Guid? SkillId, string? Search) : IRequest<SkillMatrixDto>;

public sealed record GetCareerRolesQuery : IRequest<IReadOnlyList<CareerRoleDto>>;
public sealed record RoleSkillItem(Guid SkillId, byte Level);
public sealed record SaveRoleSkillsCommand(Guid DesignationId, IReadOnlyList<RoleSkillItem> Skills) : IRequest;
public sealed record GetCareerGoalsQuery(Guid? DepartmentId, string? Search) : IRequest<IReadOnlyList<CareerGoalRowDto>>;
public sealed record SetCareerGoalRequest(Guid TargetDesignationId, DateOnly? TargetDate, string? Notes);
public sealed record SetCareerGoalCommand(Guid EmployeeId, SetCareerGoalRequest Data) : IRequest;
public sealed record AchieveCareerGoalCommand(Guid EmployeeId) : IRequest;
public sealed record ClearCareerGoalCommand(Guid EmployeeId) : IRequest;

public sealed class SaveSkillRequestValidator : AbstractValidator<SaveSkillRequest>
{
    public SaveSkillRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class CreateSkillValidator : AbstractValidator<CreateSkillCommand>
{
    public CreateSkillValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveSkillRequestValidator());
}

public sealed class UpdateSkillValidator : AbstractValidator<UpdateSkillCommand>
{
    public UpdateSkillValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveSkillRequestValidator());
}

public sealed class RateSkillValidator : AbstractValidator<RateSkillCommand>
{
    public RateSkillValidator() => RuleFor(x => x.Level).InclusiveBetween((byte)1, (byte)5);
}

public sealed class SaveRoleSkillsValidator : AbstractValidator<SaveRoleSkillsCommand>
{
    public SaveRoleSkillsValidator()
    {
        RuleFor(x => x.Skills).NotNull().Must(s => s.Count <= 30).WithMessage("A role can list at most 30 skills.");
        RuleForEach(x => x.Skills).ChildRules(i => i.RuleFor(s => s.Level).InclusiveBetween((byte)1, (byte)5));
    }
}

public sealed class SetCareerGoalValidator : AbstractValidator<SetCareerGoalCommand>
{
    public SetCareerGoalValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.TargetDesignationId).NotEmpty().WithMessage("Pick the role to grow into.");
        RuleFor(x => x.Data.Notes).MaximumLength(1000);
    }
}

#endregion

/// <summary>Role ki zaroori skills vs shakhs ke levels.</summary>
internal static class Readiness
{
    public static List<SkillGapDto> Gaps(IEnumerable<(Guid SkillId, string Name, byte Required)> required, IReadOnlyDictionary<Guid, byte> levels)
        => required.Select(r =>
        {
            byte? current = levels.TryGetValue(r.SkillId, out var l) ? l : null;
            return new SkillGapDto(r.SkillId, r.Name, r.Required, current, current >= r.Required);
        }).OrderBy(g => g.Met).ThenBy(g => g.Name).ToList();

    /// <summary>Zaroori skills mein se kitne % poori. Role par koi skill na ho to null.</summary>
    public static int? Percent(IReadOnlyCollection<SkillGapDto> gaps)
        => gaps.Count == 0 ? null : (int)Math.Round(100.0 * gaps.Count(g => g.Met) / gaps.Count);
}

public sealed class SkillQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetSkillsQuery, IReadOnlyList<SkillDto>>,
    IRequestHandler<GetPersonGrowthQuery, PersonGrowthDto>,
    IRequestHandler<GetSkillMatrixQuery, SkillMatrixDto>,
    IRequestHandler<GetCareerRolesQuery, IReadOnlyList<CareerRoleDto>>,
    IRequestHandler<GetCareerGoalsQuery, IReadOnlyList<CareerGoalRowDto>>
{
    private readonly LearningAccess access = new(db, currentUser);

    public async Task<IReadOnlyList<SkillDto>> Handle(GetSkillsQuery q, CancellationToken ct)
    {
        var skills = db.Skills.AsNoTracking();
        if (!(access.CanManage && q.IncludeInactive))
            skills = skills.Where(s => s.IsActive);
        var list = await skills.OrderBy(s => s.Name).ToListAsync(ct);
        var ids = list.Select(s => s.Id).ToList();

        var people = await db.EmployeeSkills.AsNoTracking().Where(e => ids.Contains(e.SkillId))
            .GroupBy(e => e.SkillId).Select(g => new { g.Key, Count = g.Count(), Avg = g.Average(e => (double)e.Level) })
            .ToDictionaryAsync(x => x.Key, ct);
        var courses = await db.Courses.AsNoTracking().Where(c => c.SkillId != null && ids.Contains(c.SkillId.Value))
            .GroupBy(c => c.SkillId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var roles = await db.RoleSkills.AsNoTracking().Where(r => ids.Contains(r.SkillId))
            .GroupBy(r => r.SkillId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return list.Select(s =>
        {
            people.TryGetValue(s.Id, out var p);
            return new SkillDto(s.Id, s.Name, s.Category, s.Description, s.IsActive,
                p?.Count ?? 0, p is null ? null : Math.Round((decimal)p.Avg, 1),
                courses.GetValueOrDefault(s.Id), roles.GetValueOrDefault(s.Id), s.RowVersion);
        }).ToList();
    }

    public async Task<PersonGrowthDto> Handle(GetPersonGrowthQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var employeeId = q.EmployeeId ?? me ?? throw new UnauthorizedAccessException("Your login is not linked to an employee record.");
        await access.EnsureCanSeeAsync(employeeId, ct);

        var person = await db.Employees.AsNoTracking().Where(e => e.Id == employeeId)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, Dept = e.Department.Name, e.DesignationId, Title = e.Designation.Title, e.EmploymentStatus })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Employee", employeeId);

        var rows = await db.EmployeeSkills.AsNoTracking().Where(s => s.EmployeeId == employeeId)
            .Join(db.Skills.IgnoreQueryFilters(), s => s.SkillId, k => k.Id, (s, k) => new { s, k.Name, k.Category })
            .ToListAsync(ct);
        var levels = rows.ToDictionary(r => r.s.SkillId, r => r.s.Level);
        var goal = await db.CareerGoals.AsNoTracking().FirstOrDefaultAsync(g => g.EmployeeId == employeeId, ct);

        var roleIds = goal is null ? [person.DesignationId] : new[] { person.DesignationId, goal.TargetDesignationId };
        var roleSkills = await RoleSkillsAsync(roleIds, ct);
        var nowReqs = roleSkills.Where(r => r.DesignationId == person.DesignationId).Select(r => (r.SkillId, r.Name, r.RequiredLevel)).ToList();
        var nowGaps = Readiness.Gaps(nowReqs, levels);

        var userIds = rows.Where(r => r.s.ByUserId != null).Select(r => r.s.ByUserId!.Value)
            .Concat(goal?.ByUserId is { } gb ? [gb] : []).Distinct().ToList();
        var names = await NamesByUserAsync(userIds, ct);

        CareerGoalDto? goalDto = null;
        Dictionary<Guid, byte> nextReqs = [];
        if (goal is not null)
        {
            var reqs = roleSkills.Where(r => r.DesignationId == goal.TargetDesignationId).Select(r => (r.SkillId, r.Name, r.RequiredLevel)).ToList();
            nextReqs = reqs.ToDictionary(r => r.SkillId, r => r.RequiredLevel);
            var gaps = Readiness.Gaps(reqs, levels);
            var title = await db.Designations.IgnoreQueryFilters().Where(d => d.Id == goal.TargetDesignationId).Select(d => d.Title).FirstAsync(ct);
            var suggestions = goal.Status == CareerGoalStatus.Active ? await SuggestAsync(gaps, employeeId, ct) : [];
            goalDto = new CareerGoalDto(goal.TargetDesignationId, title, goal.TargetDate, goal.Notes, goal.Status, goal.AchievedOn,
                Readiness.Percent(gaps), gaps, suggestions, goal.UpdatedAt, goal.ByUserId is { } u ? names.GetValueOrDefault(u) : null);
        }
        var nowMap = nowReqs.ToDictionary(r => r.SkillId, r => r.RequiredLevel);

        var skills = rows.OrderByDescending(r => r.s.Level).ThenBy(r => r.Name)
            .Select(r => new PersonSkillDto(r.s.SkillId, r.Name, r.Category, r.s.Level, r.s.Source, r.s.UpdatedAt,
                r.s.ByUserId is { } by ? names.GetValueOrDefault(by) : null,
                nowMap.TryGetValue(r.s.SkillId, out var rn) ? rn : null, nextReqs.TryGetValue(r.s.SkillId, out var rx) ? rx : null))
            .ToList();

        var isMe = employeeId == me;
        var lead = await access.CanLeadAsync(employeeId, ct);
        var active = person.EmploymentStatus != EmploymentStatus.Exited;
        return new PersonGrowthDto(person.Id, person.Name, person.EmployeeCode, person.Dept, person.DesignationId, person.Title,
            skills, Readiness.Percent(nowGaps), nowGaps, goalDto,
            isMe, active && (isMe || lead), active && lead && !isMe, active && (isMe || lead),
            active && lead && !isMe && goal?.Status == CareerGoalStatus.Active);
    }

    private sealed record RoleSkillRow(Guid DesignationId, Guid SkillId, string Name, byte RequiredLevel);

    private async Task<List<RoleSkillRow>> RoleSkillsAsync(IEnumerable<Guid> designationIds, CancellationToken ct)
    {
        var ids = designationIds.Distinct().ToList();
        return await db.RoleSkills.AsNoTracking().Where(r => ids.Contains(r.DesignationId))
            .Join(db.Skills, r => r.SkillId, s => s.Id, (r, s) => new RoleSkillRow(r.DesignationId, r.SkillId, s.Name, r.RequiredLevel))
            .ToListAsync(ct);
    }

    /// <summary>Jo skills kam hain unhein sikhane wale active courses, sab se chhota zaroori level pehle.</summary>
    private async Task<List<CourseSuggestionDto>> SuggestAsync(List<SkillGapDto> gaps, Guid employeeId, CancellationToken ct)
    {
        var missing = gaps.Where(g => !g.Met).Select(g => g.SkillId).ToList();
        if (missing.Count == 0)
            return [];
        var courses = await db.Courses.AsNoTracking().Where(c => c.IsActive && c.SkillId != null && missing.Contains(c.SkillId.Value))
            .OrderBy(c => c.SkillLevel).ThenBy(c => c.Title).Take(12).ToListAsync(ct);
        var ids = courses.Select(c => c.Id).ToList();
        var mine = await db.CourseEnrollments.AsNoTracking().Where(e => e.EmployeeId == employeeId && ids.Contains(e.CourseId))
            .OrderByDescending(e => e.CreatedAt).Select(e => new { e.CourseId, e.Status }).ToListAsync(ct);
        return courses.Select(c => new CourseSuggestionDto(c.Id, c.Title, c.SkillId!.Value, c.SkillLevel, c.DurationHours, c.AllowSelfEnrol,
            mine.FirstOrDefault(m => m.CourseId == c.Id)?.Status)).ToList();
    }

    private async Task<Dictionary<Guid, string>> NamesByUserAsync(List<Guid> userIds, CancellationToken ct)
        => userIds.Count == 0
            ? []
            : await db.Employees.AsNoTracking().IgnoreQueryFilters().Where(e => e.UserId != null && userIds.Contains(e.UserId.Value))
                .GroupBy(e => e.UserId!.Value).Select(g => new { g.Key, Name = g.Select(e => e.FirstName + " " + e.LastName).First() })
                .ToDictionaryAsync(x => x.Key, x => x.Name, ct);

    public async Task<SkillMatrixDto> Handle(GetSkillMatrixQuery q, CancellationToken ct)
    {
        if (!access.CanSeeAll && !await access.IsManagerAsync(ct))
            throw new UnauthorizedAccessException("Only HR and managers can see the skills matrix.");
        var people = await access.TeamAsync(ct);
        if (q.DepartmentId is { } dept)
            people = people.Where(e => e.DepartmentId == dept);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            people = people.Where(e => EF.Functions.ILike(e.FirstName + " " + e.LastName, term) || EF.Functions.ILike(e.EmployeeCode, term));
        }

        var list = await people.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).Take(500)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, Dept = e.Department.Name, Title = e.Designation.Title })
            .ToListAsync(ct);
        var ids = list.Select(p => p.Id).ToList();
        var ratings = await db.EmployeeSkills.AsNoTracking().Where(s => ids.Contains(s.EmployeeId)).ToListAsync(ct);
        if (q.SkillId is { } only)
        {
            var with = ratings.Where(r => r.SkillId == only).Select(r => r.EmployeeId).ToHashSet();
            list = list.Where(p => with.Contains(p.Id)).ToList();
        }

        var used = ratings.Select(r => r.SkillId).Distinct().ToList();
        var skills = await db.Skills.AsNoTracking().Where(s => used.Contains(s.Id) || s.Id == q.SkillId).OrderBy(s => s.Category).ThenBy(s => s.Name)
            .Select(s => new MatrixSkillDto(s.Id, s.Name, s.Category)).ToListAsync(ct);
        var byPerson = ratings.GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => (IReadOnlyDictionary<Guid, byte>)g.ToDictionary(r => r.SkillId, r => r.Level));

        return new SkillMatrixDto(skills, list.Select(p => new MatrixRowDto(p.Id, p.Name, p.EmployeeCode, p.Dept, p.Title,
            byPerson.GetValueOrDefault(p.Id) ?? new Dictionary<Guid, byte>())).ToList());
    }

    public async Task<IReadOnlyList<CareerRoleDto>> Handle(GetCareerRolesQuery q, CancellationToken ct)
    {
        var roles = await db.Designations.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Level).ThenBy(d => d.Title)
            .Select(d => new { d.Id, d.Title, d.Level }).ToListAsync(ct);
        var reqs = await RoleSkillsAsync(roles.Select(r => r.Id), ct);
        var holders = await db.Employees.AsNoTracking().Where(e => e.EmploymentStatus != EmploymentStatus.Exited)
            .GroupBy(e => e.DesignationId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var aiming = await db.CareerGoals.AsNoTracking().Where(g => g.Status == CareerGoalStatus.Active)
            .GroupBy(g => g.TargetDesignationId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return roles.Select(r => new CareerRoleDto(r.Id, r.Title, r.Level, holders.GetValueOrDefault(r.Id), aiming.GetValueOrDefault(r.Id),
            reqs.Where(x => x.DesignationId == r.Id).OrderByDescending(x => x.RequiredLevel).ThenBy(x => x.Name)
                .Select(x => new RoleSkillDto(x.SkillId, x.Name, x.RequiredLevel)).ToList())).ToList();
    }

    public async Task<IReadOnlyList<CareerGoalRowDto>> Handle(GetCareerGoalsQuery q, CancellationToken ct)
    {
        if (!access.CanSeeAll && !await access.IsManagerAsync(ct))
            throw new UnauthorizedAccessException("Only HR and managers can see career goals of others.");
        var people = await access.TeamAsync(ct);
        if (q.DepartmentId is { } dept)
            people = people.Where(e => e.DepartmentId == dept);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            people = people.Where(e => EF.Functions.ILike(e.FirstName + " " + e.LastName, term) || EF.Functions.ILike(e.EmployeeCode, term));
        }

        var rows = await db.CareerGoals.AsNoTracking()
            .Join(people, g => g.EmployeeId, e => e.Id, (g, e) => new
            {
                g, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, Dept = e.Department.Name, Current = e.Designation.Title
            })
            .Join(db.Designations.IgnoreQueryFilters(), x => x.g.TargetDesignationId, d => d.Id, (x, d) => new { x.g, x.Name, x.EmployeeCode, x.Dept, x.Current, Target = d.Title })
            .ToListAsync(ct);

        var people2 = rows.Select(r => r.g.EmployeeId).ToList();
        var levels = (await db.EmployeeSkills.AsNoTracking().Where(s => people2.Contains(s.EmployeeId)).ToListAsync(ct))
            .GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => (IReadOnlyDictionary<Guid, byte>)g.ToDictionary(s => s.SkillId, s => s.Level));
        var reqs = await RoleSkillsAsync(rows.Select(r => r.g.TargetDesignationId), ct);

        return rows.Select(r =>
        {
            var gaps = Readiness.Gaps(reqs.Where(x => x.DesignationId == r.g.TargetDesignationId).Select(x => (x.SkillId, x.Name, x.RequiredLevel)),
                levels.GetValueOrDefault(r.g.EmployeeId) ?? new Dictionary<Guid, byte>());
            return new CareerGoalRowDto(r.g.EmployeeId, r.Name, r.EmployeeCode, r.Dept, r.Current, r.g.TargetDesignationId, r.Target,
                r.g.TargetDate, r.g.Status, r.g.AchievedOn, Readiness.Percent(gaps), gaps.Count(g => !g.Met), r.g.UpdatedAt);
        })
        .OrderBy(r => r.Status).ThenByDescending(r => r.Readiness ?? -1).ThenBy(r => r.Name)
        .ToList();
    }
}

public sealed class SkillCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateSkillCommand, Guid>,
    IRequestHandler<UpdateSkillCommand>,
    IRequestHandler<DeleteSkillCommand>,
    IRequestHandler<AddStarterSkillsCommand, int>,
    IRequestHandler<RateSkillCommand>,
    IRequestHandler<RemoveSkillCommand>,
    IRequestHandler<SaveRoleSkillsCommand>,
    IRequestHandler<SetCareerGoalCommand>,
    IRequestHandler<AchieveCareerGoalCommand>,
    IRequestHandler<ClearCareerGoalCommand>
{
    private readonly LearningAccess access = new(db, currentUser);

    private static readonly (string Name, SkillCategory Category, string Description)[] Starter =
    [
        ("Communication", SkillCategory.Communication, "Explains ideas clearly in writing and in person."),
        ("Presentation", SkillCategory.Communication, "Prepares and delivers presentations to a group."),
        ("Customer service", SkillCategory.Communication, "Handles customer questions and complaints well."),
        ("Team leadership", SkillCategory.Leadership, "Sets direction, delegates and develops people."),
        ("Coaching and feedback", SkillCategory.Leadership, "Gives useful feedback and helps others grow."),
        ("Project management", SkillCategory.Leadership, "Plans work, tracks progress and manages risks."),
        ("Problem solving", SkillCategory.Domain, "Finds the cause of a problem and a workable fix."),
        ("Time management", SkillCategory.Domain, "Prioritises work and meets deadlines."),
        ("Microsoft Excel", SkillCategory.Tools, "Formulas, tables, pivot tables and charts."),
        ("Data analysis", SkillCategory.Technical, "Turns data into findings and decisions."),
        ("Workplace safety", SkillCategory.Domain, "Knows and follows health and safety practice."),
        ("English", SkillCategory.Language, "Business English, written and spoken.")
    ];

    public async Task<Guid> Handle(CreateSkillCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        await EnsureUniqueAsync(c.Data.Name, null, ct);
        var skill = Skill.Create(access.TenantId, c.Data.Name, c.Data.Category, c.Data.Description);
        if (!c.Data.IsActive)
            skill.Update(c.Data.Name, c.Data.Category, c.Data.Description, false);
        db.Skills.Add(skill);
        await SaveSkillAsync(ct);
        return skill.Id;
    }

    public async Task Handle(UpdateSkillCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == c.Id, ct) ?? throw new NotFoundException("Skill", c.Id);
        await EnsureUniqueAsync(c.Data.Name, skill.Id, ct);
        skill.Update(c.Data.Name, c.Data.Category, c.Data.Description, c.Data.IsActive);
        await SaveSkillAsync(ct);
    }

    public async Task Handle(DeleteSkillCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == c.Id, ct) ?? throw new NotFoundException("Skill", c.Id);
        if (await db.EmployeeSkills.AnyAsync(s => s.SkillId == skill.Id, ct) || await db.RoleSkills.AnyAsync(r => r.SkillId == skill.Id, ct)
            || await db.Courses.AnyAsync(x => x.SkillId == skill.Id, ct))
            throw new ConflictException("This skill is in use by people, roles or courses. Make it inactive instead of deleting it.");
        db.Skills.Remove(skill);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> Handle(AddStarterSkillsCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var existing = (await db.Skills.Select(s => s.Name.ToLower()).ToListAsync(ct)).ToHashSet();
        var added = 0;
        foreach (var (name, category, description) in Starter.Where(s => !existing.Contains(s.Name.ToLower())))
        {
            db.Skills.Add(Skill.Create(access.TenantId, name, category, description));
            added++;
        }
        if (added > 0)
            await SaveSkillAsync(ct);
        return added;
    }

    /// <summary>Khud = Self rating; manager/HR = Assessed.</summary>
    public async Task Handle(RateSkillCommand c, CancellationToken ct)
    {
        var source = await RaterAsync(c.EmployeeId, ct);
        if (!await db.Skills.AnyAsync(s => s.Id == c.SkillId && s.IsActive, ct))
            throw new DomainException("Pick an active skill.");
        var now = DateTime.UtcNow;
        var row = await db.EmployeeSkills.FirstOrDefaultAsync(s => s.EmployeeId == c.EmployeeId && s.SkillId == c.SkillId, ct);
        if (row is null)
            db.EmployeeSkills.Add(EmployeeSkill.Create(c.EmployeeId, c.SkillId, c.Level, source, access.UserId, now));
        else
            row.Rate(c.Level, source, access.UserId, now);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_EmployeeSkills_Employee_Skill") == true)
        {
            throw new ConflictException("This skill was just rated. Refresh the page.");
        }
    }

    public async Task Handle(RemoveSkillCommand c, CancellationToken ct)
    {
        await RaterAsync(c.EmployeeId, ct);
        var row = await db.EmployeeSkills.FirstOrDefaultAsync(s => s.EmployeeId == c.EmployeeId && s.SkillId == c.SkillId, ct)
            ?? throw new NotFoundException("Skill", c.SkillId);
        db.EmployeeSkills.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SaveRoleSkillsCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        if (!await db.Designations.AnyAsync(d => d.Id == c.DesignationId, ct))
            throw new NotFoundException("Role", c.DesignationId);
        var wanted = c.Skills.GroupBy(s => s.SkillId).Select(g => g.Last()).ToList();
        if (wanted.Count != c.Skills.Count)
            throw new DomainException("Each skill can be listed once.");
        var wantedIds = wanted.Select(s => s.SkillId).ToList();
        var known = await db.Skills.Where(s => wantedIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
        if (known.Count != wantedIds.Count)
            throw new DomainException("Pick skills from the skill library.");

        var current = await db.RoleSkills.Where(r => r.DesignationId == c.DesignationId).ToListAsync(ct);
        foreach (var row in current.Where(r => !wantedIds.Contains(r.SkillId)))
            db.RoleSkills.Remove(row);
        foreach (var item in wanted)
        {
            var row = current.FirstOrDefault(r => r.SkillId == item.SkillId);
            if (row is null)
                db.RoleSkills.Add(RoleSkill.Create(c.DesignationId, item.SkillId, item.Level));
            else
                row.SetLevel(item.Level);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SetCareerGoalCommand c, CancellationToken ct)
    {
        await RaterAsync(c.EmployeeId, ct);
        var current = await db.Employees.Where(e => e.Id == c.EmployeeId).Select(e => (Guid?)e.DesignationId).FirstOrDefaultAsync(ct);
        if (!await db.Designations.AnyAsync(d => d.Id == c.Data.TargetDesignationId && d.IsActive, ct))
            throw new DomainException("Pick an active role.");

        var goal = await db.CareerGoals.FirstOrDefaultAsync(g => g.EmployeeId == c.EmployeeId, ct);
        if (goal is null)
        {
            goal = CareerGoal.Create(c.EmployeeId);
            db.CareerGoals.Add(goal);
        }
        goal.Set(c.Data.TargetDesignationId, current, c.Data.TargetDate, c.Data.Notes, access.UserId, LearningClock.Today, DateTime.UtcNow);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_CareerGoals_EmployeeId") == true)
        {
            throw new ConflictException("A career goal was just set for this person. Refresh the page.");
        }
    }

    public async Task Handle(AchieveCareerGoalCommand c, CancellationToken ct)
    {
        if (c.EmployeeId == await access.MeAsync(ct) || !await access.CanLeadAsync(c.EmployeeId, ct))
            throw new UnauthorizedAccessException("Only HR or the manager can mark a career goal achieved.");
        var goal = await db.CareerGoals.FirstOrDefaultAsync(g => g.EmployeeId == c.EmployeeId, ct) ?? throw new NotFoundException("Career goal", c.EmployeeId);
        goal.MarkAchieved(LearningClock.Today, access.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ClearCareerGoalCommand c, CancellationToken ct)
    {
        await RaterAsync(c.EmployeeId, ct);
        var goal = await db.CareerGoals.FirstOrDefaultAsync(g => g.EmployeeId == c.EmployeeId, ct) ?? throw new NotFoundException("Career goal", c.EmployeeId);
        db.CareerGoals.Remove(goal);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Khud (Self) ya manager/HR (Assessed). Exited employee par koi kaam nahi.</summary>
    private async Task<SkillSource> RaterAsync(Guid employeeId, CancellationToken ct)
    {
        var self = employeeId == await access.MeAsync(ct);
        if (!self && !await access.CanLeadAsync(employeeId, ct))
            throw new UnauthorizedAccessException("You can only update your own skills or your team's.");
        if (!await db.Employees.AnyAsync(e => e.Id == employeeId && e.EmploymentStatus != EmploymentStatus.Exited, ct))
            throw new NotFoundException("Employee", employeeId);
        return self ? SkillSource.Self : SkillSource.Assessed;
    }

    private async Task EnsureUniqueAsync(string? name, Guid? existingId, CancellationToken ct)
    {
        var key = (name ?? "").Trim().ToLower();
        if (await db.Skills.AnyAsync(s => s.Id != existingId && s.Name.ToLower() == key, ct))
            throw new ConflictException("A skill with this name already exists.");
    }

    private async Task SaveSkillAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_Skills_TenantId_Name") == true)
        {
            throw new ConflictException("A skill with this name already exists.");
        }
    }
}
