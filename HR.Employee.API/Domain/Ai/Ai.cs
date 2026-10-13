namespace HR.Employee.API.Domain.Ai;

using HR.Employee.API.Domain.Common;

/// <summary>AI kaun sa data parh sakta hai (company setting). Har shakhs ki apni permissions upar se bhi lagti hain.</summary>
[Flags]
public enum AiAreas
{
    None = 0,
    People = 1,
    TimeLeave = 2,
    Payroll = 4,
    LoansExpenses = 8,
    Recruitment = 16,
    PoliciesLearning = 32,
    RelationsSafety = 64,
}

public enum AiRole : byte { User = 1, Assistant = 2 }

public enum InsightLevel : byte { Attention = 1, Look = 2, Info = 3 }

public enum InsightStatus : byte { Open = 1, Dismissed = 2 }

public enum ScreeningFit : byte { Strong = 1, Partial = 2, Weak = 3 }

/// <summary>
/// Company ki AI settings (ek row per tenant). Na ho to Default — matlab sab areas siwaye relations/safety,
/// aur mahine ki had 200 AI calls (chat sawal + CV screening + insights run).
/// </summary>
public sealed class AiSettings : AuditableEntity
{
    public const AiAreas DefaultAreas = AiAreas.People | AiAreas.TimeLeave | AiAreas.Payroll | AiAreas.LoansExpenses
        | AiAreas.Recruitment | AiAreas.PoliciesLearning;
    public const AiAreas AllAreas = DefaultAreas | AiAreas.RelationsSafety;
    public const int DefaultMonthlyLimit = 200;
    public const int MaxMonthlyLimit = 100_000;

    public AiAreas Areas { get; private set; }
    public int MonthlyLimit { get; private set; }
    public short HistoryDays { get; private set; }
    /// <summary>Employee apne data (apni leave, apne payslips) ke liye AI istemal kar sakta hai.</summary>
    public bool SelfService { get; private set; }

    private AiSettings() { }

    public static AiSettings Create(Guid tenantId)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Areas = DefaultAreas,
            MonthlyLimit = DefaultMonthlyLimit,
            HistoryDays = 90,
            SelfService = true,
        };

    public void Update(AiAreas areas, int monthlyLimit, short historyDays, bool selfService)
    {
        if ((areas & ~AllAreas) != 0)
            throw new DomainException("Select valid data areas.");
        if (monthlyLimit is < 0 or > MaxMonthlyLimit)
            throw new DomainException($"The monthly limit must be between 0 and {MaxMonthlyLimit}.");
        if (historyDays is not (30 or 90 or 180 or 365))
            throw new DomainException("Keep chat history for 30, 90, 180 or 365 days.");
        Areas = areas;
        MonthlyLimit = monthlyLimit;
        HistoryDays = historyDays;
        SelfService = selfService;
    }
}

/// <summary>Ek shakhs ki chat. Sirf wohi shakhs dekh sakta hai (UserId).</summary>
public sealed class AiConversation : AuditableEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = default!;
    public DateTime LastMessageAt { get; private set; }
    public int TurnCount { get; private set; }

    private readonly List<AiMessage> messages = [];
    public IReadOnlyList<AiMessage> Messages => messages;

    private AiConversation() { }

    public static AiConversation Start(Guid tenantId, Guid userId, string firstQuestion, DateTime now)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            UserId = Guard.NotEmpty(userId, "User"),
            Title = TitleFrom(firstQuestion),
            LastMessageAt = now,
        };

    /// <summary>
    /// Ek sawal-jawab. Payload = model ke saath poori guftagu ka JSON (tool calls, results, thinking) — agle sawal par
    /// bilkul isi tarah wapas jata hai (history sirf aage barhti hai, kabhi badli nahi jati).
    /// </summary>
    public void AddTurn(string question, AiAnswer answer, DateTime now)
    {
        var seq = messages.Count == 0 ? 0 : messages.Max(m => m.Seq);
        var asked = new AiAnswer(Guard.Required(question, "Question", AiMessage.MaxQuestion), null, null, default, false);
        messages.Add(AiMessage.Create(TenantId, Id, seq + 1, AiRole.User, asked, now));
        messages.Add(AiMessage.Create(TenantId, Id, seq + 2, AiRole.Assistant, answer, now));
        TurnCount++;
        LastMessageAt = now;
    }

    public void Rename(string title) => Title = Guard.Required(title, "Title", 200);

    public void Remove() => IsDeleted = true;

    private static string TitleFrom(string question)
    {
        var q = Guard.Required(question, "Question", AiMessage.MaxQuestion).ReplaceLineEndings(" ");
        return q.Length <= 80 ? q : q[..77].TrimEnd() + "…";
    }
}

public readonly record struct AiUsage(int InputTokens, int OutputTokens, int CacheReadTokens);

/// <summary>Ek message ka mawaad: text, model ki guftagu (payload), tools ke chips, tokens, nakaam hua ya nahi.</summary>
public readonly record struct AiAnswer(string Text, string? Payload, string? Tools, AiUsage Usage, bool Failed);

/// <summary>CV jaize ka nateeja: score, khulasa aur requirements/sawal ka JSON.</summary>
public sealed record AiScreeningResult(byte Score, string Summary, string Json);

/// <summary>Chat ki ek line. User: sawal. Assistant: jawab (markdown) + Payload (model ki asal guftagu) + Tools (UI ke chips).</summary>
public sealed class AiMessage : TenantChildEntity
{
    public const int MaxQuestion = 4000;
    public const int MaxAnswer = 40_000;

    public Guid ConversationId { get; private set; }
    public int Seq { get; private set; }
    public AiRole Role { get; private set; }
    public string Text { get; private set; } = default!;
    public string? Payload { get; private set; }
    public string? Tools { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int CacheReadTokens { get; private set; }
    public bool Failed { get; private set; }
    public DateTime At { get; private set; }

    private AiMessage() { }

    internal static AiMessage Create(Guid tenantId, Guid conversationId, int seq, AiRole role, AiAnswer content, DateTime at)
        => new()
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            Seq = seq,
            Role = role,
            Text = content.Text.Length > MaxAnswer ? content.Text[..MaxAnswer] : content.Text,
            Payload = content.Payload,
            Tools = content.Tools,
            InputTokens = content.Usage.InputTokens,
            OutputTokens = content.Usage.OutputTokens,
            CacheReadTokens = content.Usage.CacheReadTokens,
            Failed = content.Failed,
            At = at,
        };
}

/// <summary>Insights run ka ek card. Ek run ke saare cards ka ek RunId.</summary>
public sealed class AiInsight : AuditableEntity
{
    public Guid RunId { get; private set; }
    public string Area { get; private set; } = default!;
    public InsightLevel Level { get; private set; }
    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public string? Metric { get; private set; }
    public string? MetricLabel { get; private set; }
    public string? Source { get; private set; }
    public string? Link { get; private set; }
    public InsightStatus Status { get; private set; }
    public DateTime RunAt { get; private set; }
    public string? RunByName { get; private set; }

    private AiInsight() { }

    public static AiInsight Create(Guid tenantId, Guid runId, InsightCard c, DateTime runAt, string? runBy)
    {
        if (!Enum.IsDefined(c.Level))
            throw new DomainException("Insight level is not valid.");
        return new AiInsight
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            RunId = runId,
            Area = Guard.Required(c.Area, "Area", 40),
            Level = c.Level,
            Title = Guard.Required(c.Title, "Title", 200),
            Body = Guard.Required(c.Body, "Body", 1000),
            Metric = Guard.Optional(c.Metric, "Metric", 40),
            MetricLabel = Guard.Optional(c.MetricLabel, "Metric label", 120),
            Source = Guard.Optional(c.Source, "Source", 200),
            Link = SafeLink(c.Link),
            Status = InsightStatus.Open,
            RunAt = runAt,
            RunByName = Guard.Optional(runBy, "Run by", 120),
        };
    }

    public void Dismiss() => Status = InsightStatus.Dismissed;

    public void Reopen() => Status = InsightStatus.Open;

    /// <summary>Sirf app ke andar ka rasta (/app/…) — model ka diya hua koi bahar ka link nahi.</summary>
    private static string? SafeLink(string? link)
    {
        var l = Guard.Optional(link, "Link", 200);
        return l is not null && l.StartsWith("/app/", StringComparison.Ordinal) && !l.Contains("//") && !l.Contains(':') ? l : null;
    }
}

public sealed record InsightCard(
    string Area, InsightLevel Level, string Title, string Body, string? Metric, string? MetricLabel, string? Source, string? Link);

/// <summary>Ek job application ka AI CV jaiza. Score aur nateeja sirf mashwara — stage insaan badalta hai.</summary>
public sealed class AiScreening : AuditableEntity
{
    public Guid JobApplicationId { get; private set; }
    public byte Score { get; private set; }
    public ScreeningFit Fit { get; private set; }
    public string Summary { get; private set; } = default!;
    /// <summary>Requirements (met + evidence) aur interview sawal ka JSON.</summary>
    public string Result { get; private set; } = default!;
    public string? ByName { get; private set; }
    public DateTime At { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }

    private AiScreening() { }

    public static AiScreening Create(Guid tenantId, Guid applicationId, AiScreeningResult result, string? byName, AiUsage usage, DateTime at)
    {
        var (score, summary, resultJson) = result;
        if (score > 100)
            throw new DomainException("Score must be between 0 and 100.");
        return new AiScreening
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            JobApplicationId = Guard.NotEmpty(applicationId, "Application"),
            Score = score,
            Fit = FitFor(score),
            Summary = Guard.Required(summary, "Summary", 1000),
            Result = Guard.Required(resultJson, "Result", 20_000),
            ByName = Guard.Optional(byName, "By", 120),
            At = at,
            InputTokens = usage.InputTokens,
            OutputTokens = usage.OutputTokens,
        };
    }

    public static ScreeningFit FitFor(byte score) => score switch
    {
        >= 75 => ScreeningFit.Strong,
        >= 50 => ScreeningFit.Partial,
        _ => ScreeningFit.Weak,
    };
}
