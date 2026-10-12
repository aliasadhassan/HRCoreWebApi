namespace HR.Employee.API.Domain.Engagement;

using HR.Employee.API.Domain.Common;

public sealed record SurveyQuestionInput(string Text, QuestionType Type, IReadOnlyList<string>? Options, bool IsRequired);

public sealed record SurveyDetails(
    string Title, string? Description, bool IsAnonymous, SurveyAudience Audience, Guid? DepartmentId, DateOnly? ClosesOn);

/// <summary>
/// Survey / pulse. Draft mein sab kuch badal sakta hai; publish ke baad sawal aur anonymity lock (jawab inhi par hain),
/// sirf title, description aur close date. Anonymous survey mein jawab kis ne diya yeh kahin save nahi hota
/// (SurveyParticipation sirf "de diya" yaad rakhta hai, SurveyResponse mein EmployeeId null).
/// </summary>
public sealed class Survey : AuditableEntity
{
    public const int MaxQuestions = 30;
    public const int MaxOptions = 10;
    /// <summary>Anonymous results is se kam jawab par nahi dikhte (department breakdown bhi).</summary>
    public const int AnonymityThreshold = 3;

    private readonly List<SurveyQuestion> questions = [];

    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsAnonymous { get; private set; }
    public SurveyAudience Audience { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public DateOnly? ClosesOn { get; private set; }
    public SurveyStatus Status { get; private set; } = SurveyStatus.Draft;
    public DateTime? PublishedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    public IReadOnlyList<SurveyQuestion> Questions => questions;

    private Survey() { }

    public static Survey Create(Guid tenantId, SurveyDetails details, IReadOnlyList<SurveyQuestionInput> items, DateOnly today)
    {
        var survey = new Survey { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        survey.Update(details, items, today);
        return survey;
    }

    /// <summary>Draft: poora survey. Open/closed: sirf title, description, close date (sawal null bhejein).</summary>
    public void Update(SurveyDetails d, IReadOnlyList<SurveyQuestionInput>? items, DateOnly today)
    {
        if (Status != SurveyStatus.Draft)
        {
            if (items is not null || d.IsAnonymous != IsAnonymous || d.Audience != Audience || d.DepartmentId != DepartmentId)
                throw new DomainException("Questions, audience and anonymity are locked once a survey is published.");
            Title = Guard.Required(d.Title, "Title", 150);
            Description = Guard.Optional(d.Description, "Description", 1000);
            SetClosesOn(d.ClosesOn, today);
            return;
        }

        if (!Enum.IsDefined(d.Audience))
            throw new DomainException("Select a valid audience.");
        var dept = d.DepartmentId == Guid.Empty ? null : d.DepartmentId;
        if (d.Audience == SurveyAudience.Department && dept is null)
            throw new DomainException("Pick the department this survey is for.");

        Title = Guard.Required(d.Title, "Title", 150);
        Description = Guard.Optional(d.Description, "Description", 1000);
        IsAnonymous = d.IsAnonymous;
        Audience = d.Audience;
        DepartmentId = d.Audience == SurveyAudience.Department ? dept : null;
        SetClosesOn(d.ClosesOn, today);
        if (items is not null)
            SetQuestions(items);
    }

    private void SetClosesOn(DateOnly? closesOn, DateOnly today)
    {
        if (closesOn is { } c && c < today && closesOn != ClosesOn)
            throw new DomainException("Close date cannot be in the past.");
        ClosesOn = closesOn;
    }

    private void SetQuestions(IReadOnlyList<SurveyQuestionInput> items)
    {
        if (items.Count > MaxQuestions)
            throw new DomainException($"A survey can have at most {MaxQuestions} questions.");
        questions.Clear();
        short order = 1;
        foreach (var item in items)
            questions.Add(SurveyQuestion.Create(Id, order++, item));
    }

    public void Publish(DateOnly today, DateTime now)
    {
        if (Status != SurveyStatus.Draft)
            throw new DomainException("Only a draft survey can be published.");
        if (questions.Count == 0)
            throw new DomainException("Add at least one question before publishing.");
        if (ClosesOn is { } c && c < today)
            throw new DomainException("Close date cannot be in the past.");
        Status = SurveyStatus.Open;
        PublishedAt = now;
    }

    public void Close(DateTime now)
    {
        if (Status != SurveyStatus.Open)
            throw new DomainException("Only an open survey can be closed.");
        Status = SurveyStatus.Closed;
        ClosedAt = now;
    }

    public void Reopen(DateOnly? closesOn, DateOnly today)
    {
        if (Status != SurveyStatus.Closed)
            throw new DomainException("Only a closed survey can be reopened.");
        if (closesOn is { } c && c < today)
            throw new DomainException("Close date cannot be in the past.");
        Status = SurveyStatus.Open;
        ClosesOn = closesOn;
        ClosedAt = null;
    }

    /// <summary>Open + close date guzri nahi. Close date guzar jaye to survey khud band samjha jata hai.</summary>
    public bool IsAcceptingOn(DateOnly today) => Status == SurveyStatus.Open && (ClosesOn is null || ClosesOn >= today);

    public SurveyStatus StatusOn(DateOnly today) => Status == SurveyStatus.Open && !IsAcceptingOn(today) ? SurveyStatus.Closed : Status;

    /// <summary>Is employee ko yeh survey milna chahiye? (audience)</summary>
    public bool Targets(Guid employeeDepartmentId) => Audience == SurveyAudience.Everyone || DepartmentId == employeeDepartmentId;
}

public sealed class SurveyQuestion : TenantChildEntity
{
    public Guid SurveyId { get; private set; }
    public short Order { get; private set; }
    public string Text { get; private set; } = default!;
    public QuestionType Type { get; private set; }
    /// <summary>Choice ke options, ek line per option.</summary>
    public string? Options { get; private set; }
    public bool IsRequired { get; private set; }

    private SurveyQuestion() { }

    internal static SurveyQuestion Create(Guid surveyId, short order, SurveyQuestionInput item)
    {
        if (!Enum.IsDefined(item.Type))
            throw new DomainException("Select a valid question type.");
        string? options = null;
        if (item.Type == QuestionType.Choice)
        {
            var list = (item.Options ?? []).Select(o => (o ?? "").Trim()).Where(o => o.Length > 0).ToList();
            if (list.Count < 2 || list.Count > Survey.MaxOptions)
                throw new DomainException($"A choice question needs 2 to {Survey.MaxOptions} options.");
            if (list.Any(o => o.Length > 100))
                throw new DomainException("Each option can be at most 100 characters.");
            if (list.Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Count)
                throw new DomainException("Options must be different from each other.");
            options = string.Join('\n', list);
        }

        return new SurveyQuestion
        {
            SurveyId = surveyId,
            Order = order,
            Text = Guard.Required(item.Text, "Question", 300),
            Type = item.Type,
            Options = options,
            IsRequired = item.IsRequired
        };
    }

    public IReadOnlyList<string> OptionList => Options is null ? [] : Options.Split('\n');

    /// <summary>Is sawal ka jawab theek hai? Text = text, baaqi = number.</summary>
    internal void Check(short? value, string? text)
    {
        switch (Type)
        {
            case QuestionType.Text:
                if (value is not null)
                    throw new DomainException($"\"{Text}\" needs a written answer.");
                Guard.Optional(text, "Answer", 2000);
                return;
            case QuestionType.Rating when value is < 1 or > 5:
            case QuestionType.Nps when value is < 0 or > 10:
            case QuestionType.YesNo when value is < 0 or > 1:
                throw new DomainException($"\"{Text}\" has an answer out of range.");
            case QuestionType.Choice when value < 0 || value >= OptionList.Count:
                throw new DomainException($"\"{Text}\" has an unknown option.");
        }
        if (value is null || text is not null)
            throw new DomainException($"\"{Text}\" needs a choice, not text.");
    }
}

/// <summary>Kis ne survey de diya (ek baar). Anonymous survey mein bhi — jawab se juda nahi.</summary>
public sealed class SurveyParticipation : TenantChildEntity
{
    public Guid SurveyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateTime SubmittedAt { get; private set; }

    private SurveyParticipation() { }

    public static SurveyParticipation Create(Guid surveyId, Guid employeeId, DateTime now)
        => new() { SurveyId = Guard.NotEmpty(surveyId, "Survey"), EmployeeId = Guard.NotEmpty(employeeId, "Employee"), SubmittedAt = now };
}

public sealed record SurveyAnswerInput(Guid QuestionId, short? Value, string? Text);

/// <summary>
/// Ek jawab-set. Anonymous mein EmployeeId null aur sirf din (waqt nahi) taake participation se mila kar pehchana na ja sake.
/// DepartmentId breakdown ke liye (anonymous mein bhi, threshold ke saath dikhta hai).
/// </summary>
public sealed class SurveyResponse : TenantChildEntity
{
    private readonly List<SurveyAnswer> answers = [];

    public Guid SurveyId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public DateOnly SubmittedOn { get; private set; }

    public IReadOnlyList<SurveyAnswer> Answers => answers;

    private SurveyResponse() { }

    public static SurveyResponse Create(Survey survey, Guid employeeId, Guid? departmentId, IReadOnlyList<SurveyAnswerInput> input, DateOnly today)
    {
        if (!survey.IsAcceptingOn(today))
            throw new DomainException("This survey is closed.");
        if (input.Select(a => a.QuestionId).Distinct().Count() != input.Count)
            throw new DomainException("Each question can be answered once.");

        var byId = survey.Questions.ToDictionary(q => q.Id);
        var response = new SurveyResponse
        {
            SurveyId = survey.Id,
            EmployeeId = survey.IsAnonymous ? null : Guard.NotEmpty(employeeId, "Employee"),
            DepartmentId = departmentId,
            SubmittedOn = today
        };

        foreach (var a in input)
        {
            if (!byId.TryGetValue(a.QuestionId, out var q))
                throw new DomainException("An answer is for a question that is not in this survey.");
            var text = string.IsNullOrWhiteSpace(a.Text) ? null : a.Text.Trim();
            if (a.Value is null && text is null)
                continue;
            q.Check(a.Value, text);
            response.answers.Add(new SurveyAnswer(q.Id, a.Value, text));
        }

        var answered = response.answers.Select(x => x.QuestionId).ToHashSet();
        var missing = survey.Questions.FirstOrDefault(q => q.IsRequired && !answered.Contains(q.Id));
        if (missing is not null)
            throw new DomainException($"Please answer \"{missing.Text}\".");
        if (response.answers.Count == 0)
            throw new DomainException("Answer at least one question.");
        return response;
    }
}

public sealed class SurveyAnswer : TenantChildEntity
{
    public Guid ResponseId { get; private set; }
    public Guid QuestionId { get; private set; }
    public short? Value { get; private set; }
    public string? Text { get; private set; }

    private SurveyAnswer() { }

    internal SurveyAnswer(Guid questionId, short? value, string? text)
    {
        QuestionId = questionId;
        Value = value;
        Text = text;
    }
}
