namespace HR.Employee.API.Application.Ai;

using System.Text.Json;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Ai;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Recruitment;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// ---------- DTOs ----------

public sealed record AiUsageDto(int Used, int Limit, int Chat, int Screens, int InsightRuns, long InputTokens, long OutputTokens);

public sealed record AiSettingsDto(AiArea Areas, int MonthlyLimit, short HistoryDays, bool SelfService, uint RowVersion);

public sealed record AiHomeDto(
    bool Configured, string Model, bool CanChat, bool CanInsights, bool CanScreen, bool CanManage,
    AiUsageDto Usage, AiSettingsDto Settings, IReadOnlyList<string> Areas);

public sealed record ConversationItemDto(Guid Id, string Title, DateTime LastMessageAt, int TurnCount);

public sealed record ChatMessageDto(Guid Id, int Seq, AiRole Role, string Text, IReadOnlyList<string> Tools, bool Failed, DateTime At);

public sealed record ConversationDto(Guid Id, string Title, IReadOnlyList<ChatMessageDto> Messages, bool CanContinue);

public sealed record AskResultDto(Guid ConversationId, string Title, ChatMessageDto Question, ChatMessageDto Answer, AiUsageDto Usage);

public sealed record InsightDto(
    Guid Id, Guid RunId, string Area, InsightLevel Level, string Title, string Body, string? Metric, string? MetricLabel,
    string? Source, string? Link, InsightStatus Status, DateTime RunAt, string? RunByName);

public sealed record InsightsDto(DateTime? LastRunAt, string? LastRunBy, IReadOnlyList<InsightDto> Items);

public sealed record ScreeningJobDto(Guid Id, string Code, string Title, string? Department, JobStatus Status, int Applicants, bool HasRequirements);

public sealed record ScreeningRowDto(
    Guid ApplicationId, Guid CandidateId, string CandidateName, string? CurrentTitle, decimal? ExperienceYears, string? City,
    ApplicationStage Stage, DateTime AppliedAt, Guid? ScreeningId, byte? Score, ScreeningFit? Fit, DateTime? ScreenedAt);

public sealed record ScreeningBoardDto(IReadOnlyList<ScreeningJobDto> Jobs, Guid? JobId, IReadOnlyList<ScreeningRowDto> Rows);

public sealed record RequirementCheckDto(string Requirement, string Met, string Evidence);

public sealed record ScreeningDto(
    Guid Id, Guid ApplicationId, byte Score, ScreeningFit Fit, string Summary, IReadOnlyList<RequirementCheckDto> Requirements,
    IReadOnlyList<string> Questions, string? ByName, DateTime At);

public sealed record AiSettingsRequest(AiArea Areas, int MonthlyLimit, short HistoryDays, bool SelfService, uint? RowVersion);

// ---------- Requests ----------

public sealed record GetAiHomeQuery : IRequest<AiHomeDto>;
public sealed record UpdateAiSettingsCommand(AiSettingsRequest Data) : IRequest<AiSettingsDto>;

public sealed record GetConversationsQuery : IRequest<IReadOnlyList<ConversationItemDto>>;
public sealed record GetConversationQuery(Guid Id) : IRequest<ConversationDto>;
public sealed record AskCommand(Guid? ConversationId, string Question, string? Language) : IRequest<AskResultDto>;
public sealed record DeleteConversationCommand(Guid Id) : IRequest;

public sealed record GetInsightsQuery(bool Dismissed) : IRequest<InsightsDto>;
public sealed record RunInsightsCommand : IRequest<InsightsDto>;
public sealed record SetInsightStatusCommand(Guid Id, bool Dismissed) : IRequest;

public sealed record GetScreeningBoardQuery(Guid? JobId) : IRequest<ScreeningBoardDto>;
public sealed record GetScreeningQuery(Guid ApplicationId) : IRequest<ScreeningDto?>;
/// <summary>CV: likha hua text ya PDF (base64). Dono mein se ek.</summary>
public sealed record ScreenApplicationCommand(Guid ApplicationId, string? CvText, string? CvPdfBase64) : IRequest<ScreeningDto>;

/// <summary>AI ki ijazatein aur company settings ek jagah.</summary>
public sealed class AiAccess(ICurrentUser user)
{
    public bool CanChat => user.HasPermission(Permissions.AiUse);
    public bool CanInsights => user.HasPermission(Permissions.AiInsights);
    public bool CanScreen => user.HasPermission(Permissions.AiScreening);
    public bool CanManage => user.HasPermission(Permissions.AiManage);
    public Guid TenantId => user.RequireTenantId();
    public Guid UserId => user.UserId ?? throw new UnauthorizedAccessException("Sign in again.");
    public string? UserName => user.Name ?? user.Email;
    public bool Has(string p) => user.HasPermission(p);

    public void Ensure(bool allowed, string what)
    {
        if (!allowed)
            throw new UnauthorizedAccessException($"You do not have permission to {what}.");
    }
}

public sealed class AiHandlers(
    IAppDbContext db, ICurrentUser currentUser, ILlmClient llm, AiAgent agent, IAiCallContextProvider callContext) :
    IRequestHandler<GetAiHomeQuery, AiHomeDto>,
    IRequestHandler<UpdateAiSettingsCommand, AiSettingsDto>,
    IRequestHandler<GetConversationsQuery, IReadOnlyList<ConversationItemDto>>,
    IRequestHandler<GetConversationQuery, ConversationDto>,
    IRequestHandler<AskCommand, AskResultDto>,
    IRequestHandler<DeleteConversationCommand>,
    IRequestHandler<GetInsightsQuery, InsightsDto>,
    IRequestHandler<RunInsightsCommand, InsightsDto>,
    IRequestHandler<SetInsightStatusCommand>,
    IRequestHandler<GetScreeningBoardQuery, ScreeningBoardDto>,
    IRequestHandler<GetScreeningQuery, ScreeningDto?>,
    IRequestHandler<ScreenApplicationCommand, ScreeningDto>
{
    public const int MaxTurns = 30;
    public const int MaxCvChars = 60_000;
    public const int MaxPdfBytes = 5 * 1024 * 1024;

    private readonly AiAccess access = new(currentUser);

    // ---------- Home / settings ----------

    public async Task<AiHomeDto> Handle(GetAiHomeQuery q, CancellationToken ct)
    {
        var settings = await SettingsAsync(ct);
        var usage = await UsageAsync(settings, ct);
        return new AiHomeDto(
            llm.IsConfigured, llm.Model, access.CanChat, access.CanInsights, access.CanScreen, access.CanManage,
            usage, ToDto(settings), AreaNames(settings.Areas));
    }

    public async Task<AiSettingsDto> Handle(UpdateAiSettingsCommand c, CancellationToken ct)
    {
        access.Ensure(access.CanManage, "change AI settings");
        var s = await db.AiSettings.FirstOrDefaultAsync(ct);
        if (s is null)
        {
            s = AiSettings.Create(access.TenantId);
            db.AiSettings.Add(s);
        }
        else if (c.Data.RowVersion is { } rv && rv != s.RowVersion)
            throw new ConflictException("Someone else changed the AI settings. Reload and try again.");
        s.Update(c.Data.Areas, c.Data.MonthlyLimit, c.Data.HistoryDays, c.Data.SelfService);
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    // ---------- Chat ----------

    public async Task<IReadOnlyList<ConversationItemDto>> Handle(GetConversationsQuery q, CancellationToken ct)
    {
        access.Ensure(access.CanChat, "use the AI assistant");
        var settings = await SettingsAsync(ct);
        var since = DateTime.UtcNow.AddDays(-settings.HistoryDays);
        var userId = access.UserId;
        return await db.AiConversations.AsNoTracking()
            .Where(x => x.UserId == userId && x.LastMessageAt >= since)
            .OrderByDescending(x => x.LastMessageAt)
            .Take(100)
            .Select(x => new ConversationItemDto(x.Id, x.Title, x.LastMessageAt, x.TurnCount))
            .ToListAsync(ct);
    }

    public async Task<ConversationDto> Handle(GetConversationQuery q, CancellationToken ct)
    {
        access.Ensure(access.CanChat, "use the AI assistant");
        var c = await MineAsync(q.Id, ct);
        return new ConversationDto(c.Id, c.Title, c.Messages.OrderBy(m => m.Seq).Select(ToDto).ToList(), c.TurnCount < MaxTurns);
    }

    public async Task<AskResultDto> Handle(AskCommand c, CancellationToken ct)
    {
        access.Ensure(access.CanChat, "use the AI assistant");
        var question = CleanQuestion(c.Question);
        var settings = await SettingsAsync(ct);
        await EnsureCanCallAsync(settings, ct);

        var now = DateTime.UtcNow;
        AiConversation conv;
        if (c.ConversationId is { } id)
        {
            conv = await MineAsync(id, ct);
            if (conv.TurnCount >= MaxTurns)
                throw new DomainException("This chat is long. Start a new chat to keep answers quick.");
        }
        else
        {
            conv = AiConversation.Start(access.TenantId, access.UserId, question, now);
            db.AiConversations.Add(conv);
        }

        var history = conv.Messages
            .Where(m => m.Role == AiRole.Assistant && !m.Failed)
            .OrderBy(m => m.Seq)
            .SelectMany(m => AiAgent.Deserialize(m.Payload))
            .ToList();

        var tools = AiToolCatalog.For(access.Has, settings.Areas, settings.SelfService);
        var request = new LlmRequest(AiPrompts.Chat, Dynamic(c.Language), [], []);
        try
        {
            var result = await agent.RunAsync(request, history, LlmMessage.User(question), tools, callContext.Get(), settings.Areas, ct);
            conv.AddTurn(question, result.Answer, AiAgent.Serialize(result.Turn),
                JsonSerializer.Serialize(result.ToolsUsed, LlmJson.Options), result.Usage, false, now);
        }
        catch (LlmException ex)
        {
            // Sawal mehfooz rahe; jawab ki jagah wajah. History mein nahi jata (Failed).
            conv.AddTurn(question, ex.Message, null, null, default, true, now);
        }
        await db.SaveChangesAsync(ct);

        var last = conv.Messages.OrderByDescending(m => m.Seq).Take(2).OrderBy(m => m.Seq).ToList();
        return new AskResultDto(conv.Id, conv.Title, ToDto(last[0]), ToDto(last[1]), await UsageAsync(settings, ct));
    }

    public async Task Handle(DeleteConversationCommand c, CancellationToken ct)
    {
        access.Ensure(access.CanChat, "use the AI assistant");
        var conv = await MineAsync(c.Id, ct);
        conv.Remove();
        await db.SaveChangesAsync(ct);
    }

    // ---------- Insights ----------

    public async Task<InsightsDto> Handle(GetInsightsQuery q, CancellationToken ct)
    {
        access.Ensure(access.CanInsights, "see AI insights");
        var lastRun = await db.AiInsights.AsNoTracking().OrderByDescending(x => x.RunAt)
            .Select(x => new { x.RunId, x.RunAt, x.RunByName }).FirstOrDefaultAsync(ct);
        if (lastRun is null)
            return new InsightsDto(null, null, []);
        // Khule = sirf aakhri run ke; Dismissed = pichhle 60 din ke hataye hue
        var since = DateTime.UtcNow.AddDays(-60);
        var query = q.Dismissed
            ? db.AiInsights.AsNoTracking().Where(x => x.Status == InsightStatus.Dismissed && x.RunAt >= since)
            : db.AiInsights.AsNoTracking().Where(x => x.Status == InsightStatus.Open && x.RunId == lastRun.RunId);
        var items = await query
            .OrderByDescending(x => x.RunAt).ThenBy(x => x.Level)
            .Take(60)
            .Select(x => new InsightDto(x.Id, x.RunId, x.Area, x.Level, x.Title, x.Body, x.Metric, x.MetricLabel, x.Source, x.Link,
                x.Status, x.RunAt, x.RunByName))
            .ToListAsync(ct);
        return new InsightsDto(lastRun.RunAt, lastRun.RunByName, items);
    }

    public async Task<InsightsDto> Handle(RunInsightsCommand c, CancellationToken ct)
    {
        access.Ensure(access.CanInsights, "run AI insights");
        var settings = await SettingsAsync(ct);
        await EnsureCanCallAsync(settings, ct);

        var tools = AiToolCatalog.For(access.Has, settings.Areas, false);
        var request = new LlmRequest(AiPrompts.Insights, Dynamic("en"), [], [], Effort: "medium",
            OutputSchema: LlmJson.Schema(AiPrompts.InsightsSchema));
        var result = await agent.RunAsync(request, [],
            LlmMessage.User("Review the data available to you and return this week's insights."), tools, callContext.Get(), settings.Areas, ct);

        var cards = ParseInsights(result.Answer);
        var runId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        foreach (var card in cards.Take(6))
            db.AiInsights.Add(AiInsight.Create(access.TenantId, runId, card, now, access.UserName));
        if (cards.Count == 0)   // run ginna zaroori (had) — "kuch khaas nahi" ka ek card
            db.AiInsights.Add(AiInsight.Create(access.TenantId, runId,
                new InsightCard("General", InsightLevel.Info, "Nothing stands out this week", "The data the assistant could read showed no unusual changes.",
                    null, null, null, null), now, access.UserName));
        await db.SaveChangesAsync(ct);
        return await Handle(new GetInsightsQuery(false), ct);
    }

    public async Task Handle(SetInsightStatusCommand c, CancellationToken ct)
    {
        access.Ensure(access.CanInsights, "change AI insights");
        var i = await db.AiInsights.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Insight", c.Id);
        if (c.Dismissed) i.Dismiss(); else i.Reopen();
        await db.SaveChangesAsync(ct);
    }

    internal static List<InsightCard> ParseInsights(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("insights", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return [];
            var list = new List<InsightCard>();
            foreach (var e in arr.EnumerateArray())
            {
                string? S(string n, int max) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String
                    ? Cut(v.GetString(), max) : null;
                var title = S("title", 200);
                var body = S("body", 1000);
                if (title is null || body is null)
                    continue;
                list.Add(new InsightCard(S("area", 40) ?? "General", AiPrompts.Level(S("level", 20)), title, body,
                    S("metric", 40), S("metricLabel", 120), S("source", 200), S("link", 200)));
            }
            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // ---------- Screening ----------

    public async Task<ScreeningBoardDto> Handle(GetScreeningBoardQuery q, CancellationToken ct)
    {
        access.Ensure(access.CanScreen, "screen CVs");
        var jobs = await (
                from j in db.JobOpenings.AsNoTracking()
                where j.Status == JobStatus.Open || j.Status == JobStatus.OnHold || j.Id == q.JobId
                join d in db.Departments.AsNoTracking() on j.DepartmentId equals d.Id into dj
                from d in dj.DefaultIfEmpty()
                orderby j.OpenedAt descending
                select new ScreeningJobDto(j.Id, j.Code, j.Title, d != null ? d.Name : null, j.Status,
                    db.JobApplications.Count(a => a.JobOpeningId == j.Id),
                    j.Requirements != null || j.Description != null))
            .Take(100)
            .ToListAsync(ct);

        var jobId = q.JobId ?? jobs.FirstOrDefault()?.Id;
        if (jobId is null)
            return new ScreeningBoardDto(jobs, null, []);

        var rows = await (
                from a in db.JobApplications.AsNoTracking()
                where a.JobOpeningId == jobId
                join cnd in db.Candidates.AsNoTracking() on a.CandidateId equals cnd.Id
                let s = db.AiScreenings.Where(x => x.JobApplicationId == a.Id).OrderByDescending(x => x.At).FirstOrDefault()
                select new ScreeningRowDto(a.Id, cnd.Id, cnd.FirstName + " " + cnd.LastName, cnd.CurrentTitle, cnd.ExperienceYears, cnd.City,
                    a.Stage, a.AppliedAt, s != null ? s.Id : null, s != null ? s.Score : null, s != null ? s.Fit : null, s != null ? s.At : null))
            .ToListAsync(ct);

        rows = rows.OrderByDescending(r => r.Score ?? -1).ThenByDescending(r => r.AppliedAt).ToList();
        return new ScreeningBoardDto(jobs, jobId, rows);
    }

    public async Task<ScreeningDto?> Handle(GetScreeningQuery q, CancellationToken ct)
    {
        access.Ensure(access.CanScreen, "screen CVs");
        var s = await db.AiScreenings.AsNoTracking().Where(x => x.JobApplicationId == q.ApplicationId)
            .OrderByDescending(x => x.At).FirstOrDefaultAsync(ct);
        return s is null ? null : ToDto(s);
    }

    public async Task<ScreeningDto> Handle(ScreenApplicationCommand c, CancellationToken ct)
    {
        access.Ensure(access.CanScreen, "screen CVs");
        var text = string.IsNullOrWhiteSpace(c.CvText) ? null : c.CvText.Trim();
        var pdf = string.IsNullOrWhiteSpace(c.CvPdfBase64) ? null : c.CvPdfBase64.Trim();
        if (text is null && pdf is null)
            throw new DomainException("Paste the CV text or upload the CV as a PDF.");
        if (text is { Length: > MaxCvChars })
            throw new DomainException("The CV text is too long. Paste the main part (up to 60,000 characters).");
        if (pdf is not null && !IsPdf(pdf))
            throw new DomainException("Upload the CV as a PDF up to 5 MB.");

        var app = await db.JobApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.ApplicationId, ct)
            ?? throw new NotFoundException("Application", c.ApplicationId);
        var job = await db.JobOpenings.AsNoTracking().FirstAsync(x => x.Id == app.JobOpeningId, ct);
        if (string.IsNullOrWhiteSpace(job.Requirements) && string.IsNullOrWhiteSpace(job.Description))
            throw new DomainException("Add requirements to the job opening first, so the CV can be compared against them.");
        var dept = await db.Departments.AsNoTracking().Where(d => d.Id == job.DepartmentId).Select(d => d.Name).FirstOrDefaultAsync(ct);

        var settings = await SettingsAsync(ct);
        if ((settings.Areas & AiArea.Recruitment) == 0)
            throw new DomainException("Recruitment data is turned off for the AI assistant in AI settings.");
        await EnsureCanCallAsync(settings, ct);

        // Sirf naukri aur CV — candidate ka naam, shehar, umar waghaira hum nahi bhejte
        var prompt = $"""
            Job opening: {job.Title} ({dept ?? "no department"}), {job.EmploymentType}.

            Description:
            {job.Description ?? "(none)"}

            Requirements:
            {job.Requirements ?? "(none)"}

            {(text is not null ? "CV:\n" + text : "The CV is the attached PDF.")}
            """;
        var request = new LlmRequest(AiPrompts.Screening, null, [LlmMessage.User(prompt)], [], MaxTokens: 8000, Effort: "medium",
            OutputSchema: LlmJson.Schema(AiPrompts.ScreeningSchema), PdfBase64: pdf);
        var res = await llm.CompleteAsync(request, ct);
        if (res.StopReason == "refusal")
            throw new LlmException("The AI could not screen this CV.");

        var parsed = ParseScreening(res.Text) ?? throw new LlmException("The AI answer could not be read. Try again.");
        var s = AiScreening.Create(access.TenantId, app.Id, parsed.Score, parsed.Summary,
            JsonSerializer.Serialize(new { parsed.Requirements, parsed.Questions }, LlmJson.Options),
            access.UserName, new AiUsage(res.InputTokens, res.OutputTokens, res.CacheReadTokens), DateTime.UtcNow);
        db.AiScreenings.Add(s);
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    internal sealed record ParsedScreening(byte Score, string Summary, List<RequirementCheckDto> Requirements, List<string> Questions);

    internal static ParsedScreening? ParseScreening(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var score = r.TryGetProperty("score", out var sc) && sc.TryGetInt32(out var n) ? (byte)Math.Clamp(n, 0, 100) : (byte)0;
            var summary = Cut(r.TryGetProperty("summary", out var su) ? su.GetString() : null, 1000);
            if (summary is null)
                return null;
            var reqs = new List<RequirementCheckDto>();
            if (r.TryGetProperty("requirements", out var ra) && ra.ValueKind == JsonValueKind.Array)
                foreach (var e in ra.EnumerateArray().Take(30))
                {
                    var req = Cut(e.TryGetProperty("requirement", out var a) ? a.GetString() : null, 300);
                    if (req is null) continue;
                    var met = e.TryGetProperty("met", out var m) ? m.GetString() : null;
                    reqs.Add(new RequirementCheckDto(req, met is "yes" or "partial" ? met : "no",
                        Cut(e.TryGetProperty("evidence", out var ev) ? ev.GetString() : null, 500) ?? ""));
                }
            var qs = r.TryGetProperty("questions", out var qa) && qa.ValueKind == JsonValueKind.Array
                ? qa.EnumerateArray().Select(x => Cut(x.GetString(), 400)).OfType<string>().Take(5).ToList()
                : [];
            return new ParsedScreening(score, summary, reqs, qs);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    // ---------- helpers ----------

    private async Task<AiSettings> SettingsAsync(CancellationToken ct)
        => await db.AiSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? AiSettings.Create(access.TenantId);

    /// <summary>Key nahi to call nahi; mahine ki had puri to call nahi. Yahi "zero cost by default" ki zamanat hai.</summary>
    private async Task EnsureCanCallAsync(AiSettings settings, CancellationToken ct)
    {
        if (!llm.IsConfigured)
            throw new DomainException("The AI assistant is not set up yet. An admin needs to add the API key.");
        var usage = await UsageAsync(settings, ct);
        if (usage.Used >= usage.Limit)
            throw new DomainException(usage.Limit == 0
                ? "The AI assistant is turned off for this company (monthly limit is 0)."
                : $"This month's AI limit ({usage.Limit}) is used up. An admin can raise it in AI settings.");
    }

    private async Task<AiUsageDto> UsageAsync(AiSettings settings, CancellationToken ct)
    {
        var tenant = access.TenantId;
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var chat = await db.AiMessages.AsNoTracking()
            .Where(m => m.TenantId == tenant && m.At >= monthStart && m.Role == AiRole.Assistant && !m.Failed)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), In = g.Sum(m => (long)m.InputTokens), Out = g.Sum(m => (long)m.OutputTokens) })
            .FirstOrDefaultAsync(ct);
        var screens = await db.AiScreenings.AsNoTracking().Where(s => s.At >= monthStart)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), In = g.Sum(s => (long)s.InputTokens), Out = g.Sum(s => (long)s.OutputTokens) })
            .FirstOrDefaultAsync(ct);
        var runs = await db.AiInsights.AsNoTracking().IgnoreQueryFilters()
            .Where(i => i.TenantId == tenant && i.RunAt >= monthStart).Select(i => i.RunId).Distinct().CountAsync(ct);

        var used = (chat?.Count ?? 0) + (screens?.Count ?? 0) + runs;
        return new AiUsageDto(used, settings.MonthlyLimit, chat?.Count ?? 0, screens?.Count ?? 0, runs,
            (chat?.In ?? 0) + (screens?.In ?? 0), (chat?.Out ?? 0) + (screens?.Out ?? 0));
    }

    private async Task<AiConversation> MineAsync(Guid id, CancellationToken ct)
    {
        var userId = access.UserId;
        return await db.AiConversations.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new NotFoundException("Chat", id);
    }

    private string Dynamic(string? language)
    {
        var lang = language switch { "ar" => "Arabic", "zh" => "Chinese", "tr" => "Turkish", _ => "English" };
        return $"Today is {DateTime.UtcNow:dddd d MMMM yyyy} (UTC). The signed-in person is {access.UserName ?? "a user"}. "
            + $"Their app language is {lang}; answer in the language of their question.";
    }

    private static string CleanQuestion(string? question)
    {
        var q = question?.Trim();
        if (string.IsNullOrEmpty(q))
            throw new DomainException("Type a question.");
        if (q.Length > AiMessage.MaxQuestion)
            throw new DomainException($"Keep the question under {AiMessage.MaxQuestion} characters.");
        return q;
    }

    private static bool IsPdf(string base64)
    {
        if (base64.Length > MaxPdfBytes / 3 * 4 + 4)
            return false;
        var head = new byte[5];
        try
        {
            var bytes = Convert.FromBase64String(base64[..Math.Min(base64.Length, 8)].PadRight(8, '='));
            Array.Copy(bytes, head, Math.Min(5, bytes.Length));
        }
        catch (FormatException)
        {
            return false;
        }
        return head is [(byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-'];
    }

    private static string? Cut(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    private static IReadOnlyList<string> AreaNames(AiArea areas)
        => Enum.GetValues<AiArea>().Where(a => a != AiArea.None && areas.HasFlag(a)).Select(a => a.ToString()).ToList();

    private static AiSettingsDto ToDto(AiSettings s) => new(s.Areas, s.MonthlyLimit, s.HistoryDays, s.SelfService, s.RowVersion);

    private static ChatMessageDto ToDto(AiMessage m)
        => new(m.Id, m.Seq, m.Role, m.Text, ReadTools(m.Tools), m.Failed, m.At);

    private static IReadOnlyList<string> ReadTools(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static ScreeningDto ToDto(AiScreening s)
    {
        List<RequirementCheckDto> reqs = [];
        List<string> qs = [];
        try
        {
            using var doc = JsonDocument.Parse(s.Result);
            if (doc.RootElement.TryGetProperty("requirements", out var r))
                reqs = r.Deserialize<List<RequirementCheckDto>>(LlmJson.Options) ?? [];
            if (doc.RootElement.TryGetProperty("questions", out var q))
                qs = q.Deserialize<List<string>>(LlmJson.Options) ?? [];
        }
        catch (JsonException)
        {
        }
        return new ScreeningDto(s.Id, s.JobApplicationId, s.Score, s.Fit, s.Summary, reqs, qs, s.ByName, s.At);
    }
}
