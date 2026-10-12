namespace HR.Employee.API.Application.Engagement;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Engagement;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Engagement: surveys / pulse (banana, publish, jawab, results). Recognition Recognitions.cs mein.

#region DTOs

public sealed record EngagementSummaryDto(
    int MyPendingSurveys, int OpenSurveys, int DraftSurveys, decimal? AverageResponseRate,
    int RecognitionsThisMonth, int MyReceived, int MyGiven, int MyReceivedThisMonth,
    bool CanManage, bool CanSeeResults, Guid? MyEmployeeId, bool HasSurveys);

public sealed record EngagementOptionDto(Guid Id, string Name, string? Extra);

public sealed record EngagementLookupsDto(IReadOnlyList<EngagementOptionDto> Departments, IReadOnlyList<EngagementOptionDto> People);

public sealed record SurveyListItemDto(
    Guid Id, string Title, string? Description, bool IsAnonymous, SurveyAudience Audience, Guid? DepartmentId, string? DepartmentName,
    DateOnly? ClosesOn, SurveyStatus Status, DateTime? PublishedAt, DateTime? ClosedAt, int QuestionCount,
    int? Eligible, int? Responded, decimal? ResponseRate, int? DaysLeft,
    bool ForMe, bool IHaveResponded, bool CanRespond, DateTime CreatedAt, uint RowVersion);

public sealed record SurveyQuestionDto(Guid Id, short Order, string Text, QuestionType Type, IReadOnlyList<string> Options, bool IsRequired);

public sealed record SurveyDetailDto(
    SurveyListItemDto Survey, IReadOnlyList<SurveyQuestionDto> Questions,
    bool CanEdit, bool CanPublish, bool CanClose, bool CanReopen, bool CanDelete, bool CanSeeResults);

public sealed record SurveyCommentDto(string Text, string? Name, DateOnly SubmittedOn);

public sealed record QuestionResultDto(
    Guid QuestionId, short Order, string Text, QuestionType Type, IReadOnlyList<string> Options,
    int Answered, decimal? Average, int? Score, IReadOnlyList<int> Distribution, IReadOnlyList<SurveyCommentDto> Comments);

public sealed record DepartmentResultDto(
    Guid DepartmentId, string Name, int Responded, int Eligible, decimal? ResponseRate, decimal? AverageRating, int? Nps, bool Hidden);

public sealed record SurveyResultsDto(
    Guid SurveyId, string Title, bool IsAnonymous, int Responded, int Eligible, decimal? ResponseRate, bool Hidden, int Threshold,
    decimal? AverageRating, int? Nps, IReadOnlyList<QuestionResultDto> Questions, IReadOnlyList<DepartmentResultDto> Departments);

public sealed record SurveyTemplateDto(string Key, string Title, string? Description, bool IsAnonymous, int QuestionCount);

#endregion

#region Requests

public sealed record GetEngagementSummaryQuery : IRequest<EngagementSummaryDto>;
public sealed record GetEngagementLookupsQuery : IRequest<EngagementLookupsDto>;

/// <summary>Scope: mine (mujhe bheje gaye) | all (HR/results wale: drafts samet sab).</summary>
public sealed record GetSurveysQuery(string? Scope, SurveyStatus? Status, string? Search) : IRequest<IReadOnlyList<SurveyListItemDto>>;
public sealed record GetSurveyQuery(Guid Id) : IRequest<SurveyDetailDto>;
public sealed record GetSurveyResultsQuery(Guid Id) : IRequest<SurveyResultsDto>;
public sealed record GetSurveyTemplatesQuery : IRequest<IReadOnlyList<SurveyTemplateDto>>;

public sealed record SurveyQuestionRequest(string Text, QuestionType Type, IReadOnlyList<string>? Options, bool IsRequired);
public sealed record SaveSurveyRequest(
    string Title, string? Description, bool IsAnonymous, SurveyAudience Audience, Guid? DepartmentId, DateOnly? ClosesOn,
    IReadOnlyList<SurveyQuestionRequest>? Questions);
public sealed record CreateSurveyCommand(SaveSurveyRequest Data) : IRequest<Guid>;
public sealed record CreateSurveyFromTemplateCommand(string Key) : IRequest<Guid>;
public sealed record UpdateSurveyCommand(Guid Id, SaveSurveyRequest Data) : IRequest;
public sealed record DeleteSurveyCommand(Guid Id) : IRequest;
public sealed record PublishSurveyCommand(Guid Id) : IRequest;
public sealed record CloseSurveyCommand(Guid Id) : IRequest;
public sealed record ReopenSurveyCommand(Guid Id, DateOnly? ClosesOn) : IRequest;

public sealed record SurveyAnswerRequest(Guid QuestionId, short? Value, string? Text);
public sealed record SubmitSurveyCommand(Guid Id, IReadOnlyList<SurveyAnswerRequest> Answers) : IRequest;

public sealed class SaveSurveyRequestValidator : AbstractValidator<SaveSurveyRequest>
{
    public SaveSurveyRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Audience).IsInEnum();
        RuleFor(x => x.Questions!).Must(q => q.Count <= Survey.MaxQuestions)
            .WithMessage($"A survey can have at most {Survey.MaxQuestions} questions.").When(x => x.Questions is not null);
        RuleForEach(x => x.Questions!).ChildRules(q =>
        {
            q.RuleFor(x => x.Text).NotEmpty().MaximumLength(300);
            q.RuleFor(x => x.Type).IsInEnum();
        }).When(x => x.Questions is not null);
    }
}

public sealed class CreateSurveyValidator : AbstractValidator<CreateSurveyCommand>
{
    public CreateSurveyValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveSurveyRequestValidator());
}

public sealed class UpdateSurveyValidator : AbstractValidator<UpdateSurveyCommand>
{
    public UpdateSurveyValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveSurveyRequestValidator());
}

public sealed class SubmitSurveyValidator : AbstractValidator<SubmitSurveyCommand>
{
    public SubmitSurveyValidator()
    {
        RuleFor(x => x.Answers).NotNull().Must(a => a.Count <= Survey.MaxQuestions).WithMessage("Too many answers.");
        RuleForEach(x => x.Answers).ChildRules(a => a.RuleFor(x => x.Text).MaximumLength(2000));
    }
}

#endregion

/// <summary>Tayyar surveys jinhein HR ek click mein draft bana kar badal sakta hai.</summary>
internal static class SurveyTemplates
{
    public sealed record Template(string Key, string Title, string Description, bool IsAnonymous, SurveyQuestionInput[] Questions);

    public static readonly Template[] All =
    [
        new("pulse", "Quarterly pulse", "Five quick questions on how work feels right now. Answers are anonymous.", true,
        [
            new("How likely are you to recommend working here to a friend?", QuestionType.Nps, null, true),
            new("I have what I need to do my job well.", QuestionType.Rating, null, true),
            new("My manager supports my growth.", QuestionType.Rating, null, true),
            new("I feel recognised for good work.", QuestionType.Rating, null, true),
            new("Do you see yourself working here a year from now?", QuestionType.YesNo, null, true),
            new("What one thing would make work better for you?", QuestionType.Text, null, false)
        ]),
        new("enps", "eNPS check-in", "One score and one reason. Answers are anonymous.", true,
        [
            new("How likely are you to recommend working here to a friend?", QuestionType.Nps, null, true),
            new("What is the main reason for your score?", QuestionType.Text, null, false)
        ]),
        new("onboarding", "New joiner check-in", "How the first weeks went for people who joined recently.", false,
        [
            new("My first weeks were well organised.", QuestionType.Rating, null, true),
            new("What helped you most?", QuestionType.Choice, ["My manager", "My team", "Documentation", "Training sessions"], true),
            new("I know what is expected of me in my role.", QuestionType.Rating, null, true),
            new("What could we do better for new joiners?", QuestionType.Text, null, false)
        ])
    ];
}

internal static class SurveyMath
{
    /// <summary>eNPS = % promoters (9-10) minus % detractors (0-6).</summary>
    public static int? Nps(IReadOnlyCollection<short> scores)
        => scores.Count == 0 ? null : (int)Math.Round(100m * (scores.Count(s => s >= 9) - scores.Count(s => s <= 6)) / scores.Count, MidpointRounding.AwayFromZero);

    public static decimal? Average(IReadOnlyCollection<short> values)
        => values.Count == 0 ? null : Math.Round((decimal)values.Average(v => (double)v), 2, MidpointRounding.AwayFromZero);

    public static decimal? Rate(int responded, int eligible)
        => eligible == 0 ? null : Math.Min(100m, Math.Round(100m * responded / eligible, 1, MidpointRounding.AwayFromZero));
}

public sealed class SurveyQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetEngagementSummaryQuery, EngagementSummaryDto>,
    IRequestHandler<GetEngagementLookupsQuery, EngagementLookupsDto>,
    IRequestHandler<GetSurveysQuery, IReadOnlyList<SurveyListItemDto>>,
    IRequestHandler<GetSurveyQuery, SurveyDetailDto>,
    IRequestHandler<GetSurveyResultsQuery, SurveyResultsDto>,
    IRequestHandler<GetSurveyTemplatesQuery, IReadOnlyList<SurveyTemplateDto>>
{
    private readonly EngagementAccess access = new(db, currentUser);

    private IQueryable<Employee> Active => db.Employees.AsNoTracking().Where(e => e.EmploymentStatus != EmploymentStatus.Exited);

    public async Task<EngagementSummaryDto> Handle(GetEngagementSummaryQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var mine = await Handle(new GetSurveysQuery("mine", null, null), ct);
        var all = access.CanSeeResults ? await Handle(new GetSurveysQuery("all", null, null), ct) : mine;
        var rates = all.Where(s => s.Status != SurveyStatus.Draft && s.ResponseRate is not null).Select(s => s.ResponseRate!.Value).ToList();

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var recognitions = db.Recognitions.AsNoTracking();
        var myId = me?.Id;
        return new EngagementSummaryDto(
            mine.Count(s => s.CanRespond),
            all.Count(s => s.Status == SurveyStatus.Open),
            access.CanManage ? all.Count(s => s.Status == SurveyStatus.Draft) : 0,
            rates.Count == 0 ? null : Math.Round(rates.Average(), 1, MidpointRounding.AwayFromZero),
            await recognitions.CountAsync(r => r.CreatedAt >= monthStart, ct),
            myId is null ? 0 : await recognitions.CountAsync(r => r.ToEmployeeId == myId, ct),
            myId is null ? 0 : await recognitions.CountAsync(r => r.FromEmployeeId == myId, ct),
            myId is null ? 0 : await recognitions.CountAsync(r => r.ToEmployeeId == myId && r.CreatedAt >= monthStart, ct),
            access.CanManage, access.CanSeeResults, myId, await db.Surveys.AnyAsync(ct));
    }

    public async Task<EngagementLookupsDto> Handle(GetEngagementLookupsQuery q, CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name)
            .Select(d => new EngagementOptionDto(d.Id, d.Name, null)).ToListAsync(ct);
        // Recognition kisi ko bhi di ja sakti hai: sab active log
        var people = await Active.OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new EngagementOptionDto(e.Id, e.FirstName + " " + e.LastName, e.Designation.Title + " · " + e.Department.Name))
            .ToListAsync(ct);
        return new EngagementLookupsDto(departments, people);
    }

    public async Task<IReadOnlyList<SurveyListItemDto>> Handle(GetSurveysQuery q, CancellationToken ct)
    {
        var today = EngagementClock.Today;
        var me = await access.MeAsync(ct);
        var all = string.Equals(q.Scope, "all", StringComparison.OrdinalIgnoreCase);
        if (all)
            access.EnsureCanSeeResults();

        var surveys = db.Surveys.AsNoTracking().Include(s => s.Questions).AsQueryable();
        if (!all)
        {
            if (me is not { } my)
                return [];
            surveys = surveys.Where(s => s.Status != SurveyStatus.Draft
                && (s.Audience == SurveyAudience.Everyone || s.DepartmentId == my.DepartmentId));
        }
        else if (!access.CanManage)
            surveys = surveys.Where(s => s.Status != SurveyStatus.Draft);

        if (!string.IsNullOrWhiteSpace(q.Search))
            surveys = surveys.Where(s => EF.Functions.ILike(s.Title, $"%{q.Search.Trim()}%"));

        var list = await surveys.OrderByDescending(s => s.CreatedAt).Take(500).ToListAsync(ct);
        var items = await ToItemsAsync(list, today, ct);
        return q.Status is { } status ? items.Where(i => i.Status == status).ToList() : items;
    }

    public async Task<SurveyDetailDto> Handle(GetSurveyQuery q, CancellationToken ct)
    {
        var today = EngagementClock.Today;
        var survey = await SurveyAsync(q.Id, ct);
        var item = (await ToItemsAsync([survey], today, ct))[0];
        var hasResponses = await db.SurveyParticipations.AnyAsync(p => p.SurveyId == survey.Id, ct);
        var questions = survey.Questions.OrderBy(x => x.Order)
            .Select(x => new SurveyQuestionDto(x.Id, x.Order, x.Text, x.Type, x.OptionList, x.IsRequired)).ToList();
        var status = item.Status;
        return new SurveyDetailDto(item, questions,
            access.CanManage,
            access.CanManage && status == SurveyStatus.Draft,
            access.CanManage && survey.Status == SurveyStatus.Open,
            access.CanManage && status == SurveyStatus.Closed,
            access.CanManage && (status == SurveyStatus.Draft || !hasResponses),
            access.CanSeeResults && status != SurveyStatus.Draft);
    }

    public async Task<SurveyResultsDto> Handle(GetSurveyResultsQuery q, CancellationToken ct)
    {
        access.EnsureCanSeeResults();
        var survey = await SurveyAsync(q.Id, ct);
        var eligibleByDept = await EligibleQuery(survey).GroupBy(e => e.DepartmentId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var eligible = eligibleByDept.Values.Sum();
        var responded = await db.SurveyParticipations.CountAsync(p => p.SurveyId == survey.Id, ct);
        var threshold = Survey.AnonymityThreshold;
        var hidden = survey.IsAnonymous && responded < threshold;
        var questions = survey.Questions.OrderBy(x => x.Order).ToList();

        if (hidden)
        {
            var empty = questions.Select(x => new QuestionResultDto(x.Id, x.Order, x.Text, x.Type, x.OptionList, 0, null, null, [], [])).ToList();
            return new SurveyResultsDto(survey.Id, survey.Title, true, responded, eligible, SurveyMath.Rate(responded, eligible), true, threshold,
                null, null, empty, []);
        }

        var responses = await db.SurveyResponses.AsNoTracking().Include(r => r.Answers).Where(r => r.SurveyId == survey.Id).ToListAsync(ct);
        var answers = responses.SelectMany(r => r.Answers.Select(a => new { r.EmployeeId, r.DepartmentId, r.SubmittedOn, a.QuestionId, a.Value, a.Text })).ToList();
        var types = questions.ToDictionary(x => x.Id, x => x.Type);

        Dictionary<Guid, string> names = [];
        if (!survey.IsAnonymous)
        {
            var ids = responses.Where(r => r.EmployeeId != null).Select(r => r.EmployeeId!.Value).Distinct().ToList();
            names = await db.Employees.AsNoTracking().IgnoreQueryFilters().Where(e => ids.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName, ct);
        }

        var results = questions.Select(x =>
        {
            var rows = answers.Where(a => a.QuestionId == x.Id).ToList();
            var values = rows.Where(a => a.Value != null).Select(a => a.Value!.Value).ToList();
            IReadOnlyList<int> distribution = x.Type switch
            {
                QuestionType.Rating => Enumerable.Range(1, 5).Select(v => values.Count(s => s == v)).ToList(),
                QuestionType.Nps => Enumerable.Range(0, 11).Select(v => values.Count(s => s == v)).ToList(),
                QuestionType.YesNo => [values.Count(s => s == 1), values.Count(s => s == 0)],
                QuestionType.Choice => Enumerable.Range(0, x.OptionList.Count).Select(v => values.Count(s => s == v)).ToList(),
                _ => []
            };
            // Anonymous: comments bina naam, sirf din ke hisaab se (phir text) — waqt ya order se pehchaan na ho
            var comments = x.Type == QuestionType.Text
                ? rows.Where(a => a.Text != null).OrderByDescending(a => a.SubmittedOn).ThenBy(a => a.Text).Take(200)
                    .Select(a => new SurveyCommentDto(a.Text!, a.EmployeeId is { } id ? names.GetValueOrDefault(id) : null, a.SubmittedOn)).ToList()
                : [];
            return new QuestionResultDto(x.Id, x.Order, x.Text, x.Type, x.OptionList, rows.Count,
                x.Type is QuestionType.Rating or QuestionType.Nps ? SurveyMath.Average(values) : null,
                x.Type switch
                {
                    QuestionType.Nps => SurveyMath.Nps(values),
                    QuestionType.YesNo when values.Count > 0 => (int)Math.Round(100m * values.Count(v => v == 1) / values.Count, MidpointRounding.AwayFromZero),
                    _ => null
                },
                distribution, comments);
        }).ToList();

        var ratingAll = answers.Where(a => a.Value != null && types.GetValueOrDefault(a.QuestionId) == QuestionType.Rating).Select(a => a.Value!.Value).ToList();
        var npsAll = answers.Where(a => a.Value != null && types.GetValueOrDefault(a.QuestionId) == QuestionType.Nps).Select(a => a.Value!.Value).ToList();

        var deptNames = await db.Departments.AsNoTracking().IgnoreQueryFilters()
            .Where(d => eligibleByDept.Keys.Contains(d.Id) || responses.Select(r => r.DepartmentId).Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var departments = deptNames.Select(d =>
        {
            var deptResponses = responses.Count(r => r.DepartmentId == d.Key);
            var deptAnswers = answers.Where(a => a.DepartmentId == d.Key && a.Value != null).ToList();
            var isHidden = survey.IsAnonymous && deptResponses < threshold;
            var deptEligible = eligibleByDept.GetValueOrDefault(d.Key);
            return new DepartmentResultDto(d.Key, d.Value, deptResponses, deptEligible, SurveyMath.Rate(deptResponses, deptEligible),
                isHidden ? null : SurveyMath.Average(deptAnswers.Where(a => types.GetValueOrDefault(a.QuestionId) == QuestionType.Rating).Select(a => a.Value!.Value).ToList()),
                isHidden ? null : SurveyMath.Nps(deptAnswers.Where(a => types.GetValueOrDefault(a.QuestionId) == QuestionType.Nps).Select(a => a.Value!.Value).ToList()),
                isHidden);
        }).OrderByDescending(d => d.Responded).ThenBy(d => d.Name).ToList();

        return new SurveyResultsDto(survey.Id, survey.Title, survey.IsAnonymous, responded, eligible, SurveyMath.Rate(responded, eligible), false, threshold,
            SurveyMath.Average(ratingAll), SurveyMath.Nps(npsAll), results, departments);
    }

    public Task<IReadOnlyList<SurveyTemplateDto>> Handle(GetSurveyTemplatesQuery q, CancellationToken ct)
    {
        access.EnsureCanManage();
        IReadOnlyList<SurveyTemplateDto> list = SurveyTemplates.All
            .Select(t => new SurveyTemplateDto(t.Key, t.Title, t.Description, t.IsAnonymous, t.Questions.Length)).ToList();
        return Task.FromResult(list);
    }

    private IQueryable<Employee> EligibleQuery(Survey s)
        => s.Audience == SurveyAudience.Department ? Active.Where(e => e.DepartmentId == s.DepartmentId) : Active;

    private async Task<Survey> SurveyAsync(Guid id, CancellationToken ct)
    {
        var survey = await db.Surveys.AsNoTracking().Include(s => s.Questions).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Survey", id);
        if (access.CanSeeResults && (survey.Status != SurveyStatus.Draft || access.CanManage))
            return survey;
        var me = await access.MeAsync(ct);
        if (survey.Status == SurveyStatus.Draft || me is not { } my || !survey.Targets(my.DepartmentId))
            throw new NotFoundException("Survey", id);
        return survey;
    }

    private async Task<List<SurveyListItemDto>> ToItemsAsync(List<Survey> list, DateOnly today, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var ids = list.Select(s => s.Id).ToList();
        var responded = await db.SurveyParticipations.AsNoTracking().Where(p => ids.Contains(p.SurveyId))
            .GroupBy(p => p.SurveyId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var mine = me is { } my
            ? (await db.SurveyParticipations.AsNoTracking().Where(p => p.EmployeeId == my.Id && ids.Contains(p.SurveyId))
                .Select(p => p.SurveyId).ToListAsync(ct)).ToHashSet()
            : [];
        var eligibleByDept = await Active.GroupBy(e => e.DepartmentId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var everyone = eligibleByDept.Values.Sum();
        var deptIds = list.Where(s => s.DepartmentId != null).Select(s => s.DepartmentId!.Value).Distinct().ToList();
        var deptNames = await db.Departments.AsNoTracking().IgnoreQueryFilters().Where(d => deptIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var showCounts = access.CanSeeResults;

        return list.Select(s =>
        {
            var status = s.StatusOn(today);
            var count = responded.GetValueOrDefault(s.Id);
            var eligible = s.Audience == SurveyAudience.Department ? eligibleByDept.GetValueOrDefault(s.DepartmentId!.Value) : everyone;
            var forMe = me is { } m && s.Targets(m.DepartmentId);
            var done = mine.Contains(s.Id);
            return new SurveyListItemDto(
                s.Id, s.Title, s.Description, s.IsAnonymous, s.Audience, s.DepartmentId,
                s.DepartmentId is { } d ? deptNames.GetValueOrDefault(d) : null,
                s.ClosesOn, status, s.PublishedAt, s.ClosedAt, s.Questions.Count,
                showCounts ? eligible : null, showCounts ? count : null, showCounts ? SurveyMath.Rate(count, eligible) : null,
                status == SurveyStatus.Open && s.ClosesOn is { } c ? c.DayNumber - today.DayNumber : null,
                forMe, done, forMe && !done && s.IsAcceptingOn(today), s.CreatedAt, s.RowVersion);
        }).ToList();
    }
}

public sealed class SurveyCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateSurveyCommand, Guid>,
    IRequestHandler<CreateSurveyFromTemplateCommand, Guid>,
    IRequestHandler<UpdateSurveyCommand>,
    IRequestHandler<DeleteSurveyCommand>,
    IRequestHandler<PublishSurveyCommand>,
    IRequestHandler<CloseSurveyCommand>,
    IRequestHandler<ReopenSurveyCommand>,
    IRequestHandler<SubmitSurveyCommand>
{
    private readonly EngagementAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateSurveyCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        await EnsureDepartmentAsync(c.Data, ct);
        var survey = Survey.Create(access.TenantId, Details(c.Data), Questions(c.Data) ?? [], EngagementClock.Today);
        db.Surveys.Add(survey);
        await db.SaveChangesAsync(ct);
        return survey.Id;
    }

    public async Task<Guid> Handle(CreateSurveyFromTemplateCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var t = SurveyTemplates.All.FirstOrDefault(x => x.Key == c.Key) ?? throw new NotFoundException("Template", c.Key);
        var survey = Survey.Create(access.TenantId, new SurveyDetails(t.Title, t.Description, t.IsAnonymous, SurveyAudience.Everyone, null, null),
            t.Questions, EngagementClock.Today);
        db.Surveys.Add(survey);
        await db.SaveChangesAsync(ct);
        return survey.Id;
    }

    public async Task Handle(UpdateSurveyCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var survey = await SurveyAsync(c.Id, ct);
        if (survey.Status == SurveyStatus.Draft)
            await EnsureDepartmentAsync(c.Data, ct);
        survey.Update(Details(c.Data), survey.Status == SurveyStatus.Draft ? Questions(c.Data) : null, EngagementClock.Today);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(DeleteSurveyCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var survey = await SurveyAsync(c.Id, ct);
        if (survey.Status != SurveyStatus.Draft && await db.SurveyParticipations.AnyAsync(p => p.SurveyId == survey.Id, ct))
            throw new ConflictException("People have answered this survey. Close it instead of deleting it.");
        db.Surveys.Remove(survey);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(PublishSurveyCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var survey = await SurveyAsync(c.Id, ct);
        survey.Publish(EngagementClock.Today, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CloseSurveyCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var survey = await SurveyAsync(c.Id, ct);
        survey.Close(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ReopenSurveyCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var survey = await SurveyAsync(c.Id, ct);
        var today = EngagementClock.Today;
        // Close date guzarne se band hua survey: status Open hai, sirf nayi date chahiye
        if (survey.Status == SurveyStatus.Open && !survey.IsAcceptingOn(today))
        {
            if (c.ClosesOn is { } d && d < today)
                throw new DomainException("Close date cannot be in the past.");
            survey.Update(new SurveyDetails(survey.Title, survey.Description, survey.IsAnonymous, survey.Audience, survey.DepartmentId, c.ClosesOn), null, today);
        }
        else
            survey.Reopen(c.ClosesOn, today);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(SubmitSurveyCommand c, CancellationToken ct)
    {
        var me = await access.RequireMeAsync(ct);
        var survey = await db.Surveys.Include(s => s.Questions).FirstOrDefaultAsync(s => s.Id == c.Id, ct)
            ?? throw new NotFoundException("Survey", c.Id);
        if (survey.Status == SurveyStatus.Draft || !survey.Targets(me.DepartmentId))
            throw new NotFoundException("Survey", c.Id);
        if (await db.SurveyParticipations.AnyAsync(p => p.SurveyId == survey.Id && p.EmployeeId == me.Id, ct))
            throw new ConflictException("You have already answered this survey.");

        var response = SurveyResponse.Create(survey, me.Id, me.DepartmentId,
            c.Answers.Select(a => new SurveyAnswerInput(a.QuestionId, a.Value, a.Text)).ToList(), EngagementClock.Today);
        db.SurveyParticipations.Add(SurveyParticipation.Create(survey.Id, me.Id, DateTime.UtcNow));
        db.SurveyResponses.Add(response);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_SurveyParticipations_Survey_Employee") == true)
        {
            throw new ConflictException("You have already answered this survey.");
        }
    }

    private async Task<Survey> SurveyAsync(Guid id, CancellationToken ct)
        => await db.Surveys.Include(s => s.Questions).FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Survey", id);

    private async Task EnsureDepartmentAsync(SaveSurveyRequest r, CancellationToken ct)
    {
        if (r.Audience == SurveyAudience.Department && r.DepartmentId is { } d && d != Guid.Empty
            && !await db.Departments.AnyAsync(x => x.Id == d && x.IsActive, ct))
            throw new DomainException("Pick an active department.");
    }

    private static SurveyDetails Details(SaveSurveyRequest r)
        => new(r.Title, r.Description, r.IsAnonymous, r.Audience, r.DepartmentId, r.ClosesOn);

    private static List<SurveyQuestionInput>? Questions(SaveSurveyRequest r)
        => r.Questions?.Select(x => new SurveyQuestionInput(x.Text, x.Type, x.Options, x.IsRequired)).ToList();
}
