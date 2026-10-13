namespace HR.Employee.API.Tests.Ai;

using System.Net;
using System.Text;
using System.Text.Json;
using HR.Employee.API.Application.Ai;
using HR.Employee.API.Domain.Ai;
using HR.Employee.API.Domain.Common;
using HR.Shared.Library.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class AiAssistantTests
{
    static readonly Guid Tenant = Guid.NewGuid();
    static readonly DateTime Now = new(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);
    static readonly AiCallContext Ctx = new("token-123", "http://employee");

    // ---- Settings / domain ----

    [Fact]
    public void Settings_default_leaves_relations_and_safety_off()
    {
        var s = AiSettings.Create(Tenant);
        Assert.Equal(200, s.MonthlyLimit);
        Assert.Equal(0, (int)(s.Areas & AiAreas.RelationsSafety));
        Assert.True(s.SelfService);
    }

    [Theory]
    [InlineData(-1, 90)]
    [InlineData(100_001, 90)]
    [InlineData(200, 45)]
    public void Settings_reject_bad_limit_or_history(int limit, short days)
        => Assert.Throws<DomainException>(() => AiSettings.Create(Tenant).Update(AiSettings.DefaultAreas, limit, days, true));

    [Fact]
    public void Settings_reject_unknown_area_bits()
        => Assert.Throws<DomainException>(() => AiSettings.Create(Tenant).Update((AiAreas)256, 200, 90, true));

    [Fact]
    public void Turn_adds_question_and_answer_in_order()
    {
        var c = AiConversation.Start(Tenant, Guid.NewGuid(), "Who was late most this month?", Now);
        c.AddTurn("Who was late most this month?", new AiAnswer("Sara, 4 times.", "[]", "[]", new AiUsage(10, 5, 0), false), Now);
        c.AddTurn("And last month?", new AiAnswer("Omar.", "[]", null, default, false), Now.AddMinutes(1));
        Assert.Equal(2, c.TurnCount);
        Assert.Equal([1, 2, 3, 4], c.Messages.Select(m => m.Seq));
        Assert.Equal(AiRole.Assistant, c.Messages[3].Role);
        Assert.Equal(Now.AddMinutes(1), c.LastMessageAt);
    }

    [Theory]
    [InlineData("/app/attendance", "/app/attendance")]
    [InlineData("https://evil.example", null)]
    [InlineData("/app//evil.example", null)]
    [InlineData("/app/x:y", null)]
    [InlineData("javascript:alert(1)", null)]
    public void Insight_keeps_only_in_app_links(string link, string? expected)
    {
        var i = AiInsight.Create(Tenant, Guid.NewGuid(),
            new InsightCard("Attendance", InsightLevel.Look, "Late arrivals up", "Up 35% on September.", "+35%", "late", "Attendance", link), Now, "Ali");
        Assert.Equal(expected, i.Link);
    }

    [Fact]
    public void Screening_score_sets_fit()
    {
        Assert.Equal(ScreeningFit.Strong, AiScreening.Create(Tenant, Guid.NewGuid(), new AiScreeningResult(75, "Good.", "{}"), null, default, Now).Fit);
        Assert.Equal(ScreeningFit.Partial, AiScreening.Create(Tenant, Guid.NewGuid(), new AiScreeningResult(50, "Ok.", "{}"), null, default, Now).Fit);
        Assert.Equal(ScreeningFit.Weak, AiScreening.Create(Tenant, Guid.NewGuid(), new AiScreeningResult(49, "Weak.", "{}"), null, default, Now).Fit);
        Assert.Throws<DomainException>(() => AiScreening.Create(Tenant, Guid.NewGuid(), new AiScreeningResult(101, "x", "{}"), null, default, Now));
    }

    // ---- Tools: permissions + areas ----

    [Fact]
    public void Tools_follow_permissions_areas_and_self_service()
    {
        var employeeOnly = AiToolCatalog.For(p => p is Permissions.LeavesViewOwn or Permissions.PayrollViewOwn, AiSettings.DefaultAreas, true)
            .Select(t => t.Name).ToList();
        Assert.Contains("my_leave", employeeOnly);
        Assert.Contains("my_payslips", employeeOnly);
        Assert.DoesNotContain("payroll_overview", employeeOnly);
        Assert.DoesNotContain("find_employees", employeeOnly);

        Assert.DoesNotContain("my_leave", AiToolCatalog.For(_ => true, AiSettings.DefaultAreas, false).Select(t => t.Name));

        var noPayroll = AiToolCatalog.For(_ => true, AiSettings.DefaultAreas & ~AiAreas.Payroll, false).Select(t => t.Name).ToList();
        Assert.DoesNotContain("payroll_overview", noPayroll);
        Assert.Contains("find_employees", noPayroll);

        Assert.DoesNotContain("relations_overview", AiToolCatalog.For(_ => true, AiSettings.DefaultAreas, false).Select(t => t.Name));
        Assert.Contains("relations_overview", AiToolCatalog.For(_ => true, AiSettings.AllAreas, false).Select(t => t.Name));
    }

    [Fact]
    public void Every_tool_has_a_unique_name_and_object_schema()
    {
        Assert.Equal(AiToolCatalog.All.Count, AiToolCatalog.All.Select(t => t.Name).Distinct().Count());
        foreach (var t in AiToolCatalog.All)
            Assert.Equal("object", LlmJson.Schema(t.Schema).GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("attendance", AiAreas.TimeLeave)]
    [InlineData("employee:employees", AiAreas.People)]
    [InlineData("payroll:salaries", AiAreas.Payroll)]
    [InlineData("payroll:loans", AiAreas.LoansExpenses)]
    [InlineData("something-new", AiAreas.None)]
    public void Report_sources_map_to_areas(string source, AiAreas area)
        => Assert.Equal(area, AiToolCatalog.SourceArea(source));

    // ---- PII scrub ----

    [Fact]
    public void Clean_removes_private_keys_at_any_depth()
    {
        var json = """{"name":"Sara","cnic":"35202-1","bank":{"iban":"PK00","bankName":"HBL"},"items":[{"passportNumber":"X1","title":"Dev","photoUrl":"a.png"}],"note":null}""";
        var clean = AiToolRunner.Clean(json);
        Assert.Contains("Sara", clean);
        Assert.Contains("HBL", clean);
        Assert.Contains("Dev", clean);
        foreach (var bad in new[] { "cnic", "35202", "iban", "PK00", "passport", "X1", "photo", "note" })
            Assert.DoesNotContain(bad, clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Clean_leaves_non_json_alone()
        => Assert.Equal("plain text", AiToolRunner.Clean("plain text"));

    // ---- Runner ----

    [Fact]
    public async Task Runner_sends_user_token_and_scrubs_result()
    {
        var http = new FakeHttp(_ => (HttpStatusCode.OK, """{"items":[{"fullName":"Sara","nationalId":"123"}]}"""));
        var (content, isError) = await Runner(http).RunAsync(AiToolCatalog.Find("find_employees")!, Json("""{"search":"Sara"}"""), Ctx, AiSettings.DefaultAreas, default);
        Assert.False(isError);
        Assert.Contains("Sara", content);
        Assert.DoesNotContain("nationalId", content);
        var req = Assert.Single(http.Requests);
        Assert.Equal("Bearer token-123", req.Auth);
        Assert.StartsWith("http://employee/api/employees", req.Url);
    }

    [Fact]
    public async Task Runner_turns_403_into_plain_no_permission()
    {
        var http = new FakeHttp(_ => (HttpStatusCode.Forbidden, ""));
        var (content, isError) = await Runner(http).RunAsync(AiToolCatalog.Find("payroll_overview")!, Json("{}"), Ctx, AiSettings.DefaultAreas, default);
        Assert.True(isError);
        Assert.Contains("permission", content);
    }

    [Fact]
    public async Task Runner_blocks_report_on_a_switched_off_area()
    {
        var http = new FakeHttp(_ => (HttpStatusCode.OK, "{}"));
        var (_, isError) = await Runner(http).RunAsync(AiToolCatalog.Find("run_report")!,
            Json("""{"source":"payroll:salaries","columns":["employee"]}"""), Ctx, AiSettings.DefaultAreas & ~AiAreas.Payroll, default);
        Assert.True(isError);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task Report_sources_keep_only_switched_on_areas()
    {
        var http = new FakeHttp(r => r.RequestUri!.AbsolutePath.Contains("/payroll/")
            ? (HttpStatusCode.OK, """[{"key":"payroll:salaries","label":"Salaries","fields":[]}]""")
            : (HttpStatusCode.OK, """{"sources":[{"key":"attendance","label":"Attendance","needsRange":true,"fields":[{"key":"date","type":"Date"}]},{"key":"employees","label":"Employees","fields":[]}]}"""));
        var (content, isError) = await Runner(http).RunAsync(AiToolCatalog.Find("report_sources")!, Json("{}"), Ctx, AiAreas.TimeLeave | AiAreas.Payroll, default);
        Assert.False(isError);
        Assert.Contains("employee:attendance", content);
        Assert.Contains("date (Date)", content);
        Assert.Contains("payroll:salaries", content);
        Assert.DoesNotContain("employee:employees", content);
    }

    // ---- Agent loop ----

    [Fact]
    public async Task Agent_runs_tools_then_answers_and_keeps_the_whole_turn()
    {
        var llm = new FakeLlm(
            new LlmResponse(
            [
                new LlmBlock { Type = "thinking", Text = "need data", Signature = "sig" },
                new LlmBlock { Type = "tool_use", Id = "t1", Name = "company_overview", Input = Json("{}") },
                new LlmBlock { Type = "tool_use", Id = "t2", Name = "no_such_tool", Input = Json("{}") },
            ], "tool_use", 100, 20, 0),
            new LlmResponse([LlmBlock.FromText("You have **42** employees.")], "end_turn", 150, 30, 80));
        var http = new FakeHttp(_ => (HttpStatusCode.OK, """{"headcount":42}"""));
        var agent = new AiAgent(llm, Runner(http), Options.Create(new AiOptions()));

        var result = await agent.RunAsync(new LlmRequest("sys", null, [], []), [], LlmMessage.User("How many people?"),
            AiToolCatalog.For(_ => true, AiSettings.DefaultAreas, false), Ctx, AiSettings.DefaultAreas, default);

        Assert.Equal("You have **42** employees.", result.Answer);
        Assert.Equal(new AiUsage(250, 50, 80), result.Usage);
        Assert.Equal(["user", "assistant", "user", "assistant"], result.Turn.Select(m => m.Role));
        Assert.Equal("sig", result.Turn[1].Content[0].Signature);   // thinking wapas bilkul waisa
        var results = result.Turn[2].Content;
        Assert.Equal(2, results.Count);
        Assert.Contains("42", results.Single(b => b.ToolUseId == "t1").Text);
        Assert.True(results.Single(b => b.ToolUseId == "t2").IsError);
        Assert.Single(result.ToolsUsed);
        // Doosri call ko pehli turn poori mili
        Assert.Equal(3, llm.Requests[1].Messages.Count);
    }

    [Fact]
    public async Task Agent_stops_tool_rounds_at_the_limit()
    {
        var loop = new LlmResponse([new LlmBlock { Type = "tool_use", Id = "x", Name = "company_overview", Input = Json("{}") }], "tool_use", 1, 1, 0);
        var llm = new FakeLlm(loop, loop, new LlmResponse([LlmBlock.FromText("Partial answer.")], "end_turn", 1, 1, 0));
        var agent = new AiAgent(llm, Runner(new FakeHttp(_ => (HttpStatusCode.OK, "{}"))), Options.Create(new AiOptions { MaxToolRounds = 2 }));

        var result = await agent.RunAsync(new LlmRequest("sys", null, [], []), [], LlmMessage.User("q"),
            AiToolCatalog.All, Ctx, AiSettings.AllAreas, default);

        Assert.Equal("Partial answer.", result.Answer);
        Assert.Contains(result.Turn[^2].Content, b => b.Type == "text" && b.Text!.Contains("Tool limit reached"));
    }

    [Fact]
    public async Task Agent_turns_refusal_into_a_plain_message()
    {
        var llm = new FakeLlm(new LlmResponse([], "refusal", 1, 1, 0));
        var agent = new AiAgent(llm, Runner(new FakeHttp(_ => (HttpStatusCode.OK, "{}"))), Options.Create(new AiOptions()));
        var result = await agent.RunAsync(new LlmRequest("sys", null, [], []), [], LlmMessage.User("q"), [], Ctx, AiAreas.None, default);
        Assert.Equal("I can't help with that request.", result.Answer);
    }

    [Fact]
    public void Turn_payload_round_trips()
    {
        IReadOnlyList<LlmMessage> turn =
        [
            LlmMessage.User("hi"),
            new("assistant", [new LlmBlock { Type = "tool_use", Id = "a", Name = "x", Input = Json("""{"q":1}""") }]),
        ];
        var back = AiAgent.Deserialize(AiAgent.Serialize(turn));
        Assert.Equal(2, back.Count);
        Assert.Equal(1, back[1].Content[0].Input!.Value.GetProperty("q").GetInt32());
        Assert.Empty(AiAgent.Deserialize("not json"));
    }

    // ---- Structured output parsing ----

    [Fact]
    public void Insights_parse_and_skip_cards_without_title()
    {
        var cards = AiParsing.Insights("""
            {"insights":[
              {"area":"Attendance","level":"attention","title":"Late arrivals up","body":"Up 35%.","metric":"+35%","metricLabel":"late","source":"Attendance","link":"/app/attendance"},
              {"area":"Leave","level":"look","body":"no title"}
            ]}
            """);
        var c = Assert.Single(cards);
        Assert.Equal(InsightLevel.Attention, c.Level);
        Assert.Empty(AiParsing.Insights("oops"));
    }

    [Fact]
    public void Screening_parse_clamps_score_and_unknown_met()
    {
        var s = AiParsing.Screening("""
            {"score":140,"summary":"Strong .NET.","requirements":[{"requirement":"5y .NET","met":"maybe","evidence":"6 years"}],"questions":["Q1","Q2","Q3"]}
            """)!;
        Assert.Equal(100, s.Score);
        Assert.Equal("no", s.Requirements[0].Met);
        Assert.Equal(3, s.Questions.Count);
        Assert.Null(AiParsing.Screening("""{"score":10}"""));
    }

    // ---- helpers ----

    static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement.Clone();

    static AiToolRunner Runner(FakeHttp http)
        => new(http, Options.Create(new AiOptions { PayrollUrl = "http://payroll" }), NullLogger<AiToolRunner>.Instance);

    sealed class FakeLlm(params LlmResponse[] responses) : ILlmClient
    {
        private int next;
        public List<LlmRequest> Requests { get; } = [];
        public bool IsConfigured => true;
        public string Model => "fake";

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(responses[Math.Min(next++, responses.Length - 1)]);
        }
    }

    sealed class FakeHttp(Func<HttpRequestMessage, (HttpStatusCode, string)> respond) : HttpMessageHandler, IHttpClientFactory
    {
        public List<(string Url, string? Auth)> Requests { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Requests)
                Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString()));
            var (status, body) = respond(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
