namespace HR.Employee.API.Application.Ai;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HR.Employee.API.Domain.Ai;
using HR.Shared.Library.Authorization;
using Microsoft.Extensions.Options;

public enum AiService { Employee, Payroll }

/// <summary>Tool ka ek HTTP call (sirf parhna). Body sirf report run (POST) ke liye.</summary>
public sealed record AiHttpCall(AiService Service, HttpMethod Method, string Path, object? Body = null);

/// <summary>
/// Ek read-only tool. AnyOf = in mein se koi permission ho to model ko dikhta hai (khaali = har login wala).
/// Asal hifazat API khud karti hai — call user ke apne token se jati hai, 403 aaye to model ko bata diya jata hai.
/// </summary>
public sealed record AiToolDef(
    string Name, string Label, string Description, object Schema, AiAreas Area, string[] AnyOf, bool SelfService,
    Func<JsonElement, IReadOnlyList<AiHttpCall>> Calls);

/// <summary>Model ko dikhne wale tools aur unke HTTP raste. Har tool sirf GET (ya report run) — AI kuch badal nahi sakta.</summary>
public static class AiToolCatalog
{
    private static readonly string[] Hr = [Permissions.EmployeesView];
    private static readonly string[] LeaveAll = [Permissions.LeavesViewAll, Permissions.LeavesApprove];
    private static readonly string[] Pay = [Permissions.PayrollViewAll, Permissions.PayrollApprove, Permissions.PayrollRun];
    private static readonly string[] Anyone = [];
    private const string DateFormat = "yyyy-MM-dd";
    private const string SearchArg = "search";
    private const string StatusArg = "status";

    private static object Obj(object properties, params string[] required)
        => new { type = JsonType.Object, properties, required };

    private static readonly object NoArgs = Obj(new { });

    public static readonly IReadOnlyList<AiToolDef> All =
    [
        new("report_sources", "Report sources",
            "List the data tables you can query with run_report (employees, attendance, leave, documents, assets, payslips, salaries, loans) and their field keys. Call this before run_report.",
            NoArgs, AiAreas.None, Anyone, false, _ => []),

        new("run_report", "Ran a report",
            "Query one data table: pick columns, filters, an optional group-by with aggregates (count/sum/avg/min/max), sort and limit. " +
            "Use it for counts, totals, lists and comparisons. Sources that need a period require from and to (yyyy-MM-dd). " +
            "Filter ops: eq, ne, contains, starts, gt, gte, lt, lte, between (value, value2), empty, notempty, in (comma separated).",
            Obj(new
            {
                source = new { type = JsonType.String, description = "Source key from report_sources, e.g. employee:attendance or payroll:payslips" },
                columns = new { type = "array", items = new { type = JsonType.String }, description = "Field keys to show (when not grouping)" },
                filters = new
                {
                    type = "array",
                    items = Obj(new
                    {
                        field = new { type = JsonType.String }, op = new { type = JsonType.String },
                        value = new { type = JsonType.String }, value2 = new { type = JsonType.String },
                    }, "field", "op"),
                },
                groupBy = new { type = JsonType.String, description = "Field key to group by" },
                aggregates = new { type = "array", items = Obj(new { fn = new { type = JsonType.String }, field = new { type = JsonType.String } }, "fn") },
                sort = Obj(new { key = new { type = JsonType.String }, desc = new { type = "boolean" } }, "key"),
                limit = new { type = "integer", description = "Max rows (default 50)" },
                from = new { type = JsonType.String, description = DateFormat },
                to = new { type = JsonType.String, description = DateFormat },
            }, "source"),
            AiAreas.None, Anyone, false, RunReport),

        new("find_employees", "Looked up employees",
            "Search the employee directory by name, code or email. Returns job, department, manager and status.",
            Obj(new
            {
                search = new { type = JsonType.String },
                status = new { type = JsonType.String, @enum = new[] { "Active", "Probation", "OnNotice", "Suspended", "Exited" } },
                includeExited = new { type = "boolean" },
            }),
            AiAreas.People, Hr, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/employees" + Query(("pageSize", "25"), (SearchArg, Str(a, SearchArg)),
                (StatusArg, Str(a, StatusArg)), ("includeExited", Bool(a, "includeExited"))))]),

        new("get_employee", "Opened an employee",
            "Full profile of one employee by id (from find_employees): job, dates, manager, contact. ID numbers and bank details are never shown.",
            Obj(new { id = new { type = JsonType.String } }, "id"),
            AiAreas.People, Hr, false, a => [new(AiService.Employee, HttpMethod.Get, $"/api/employees/{IdArg(a)}")]),

        new("company_overview", "Company overview",
            "Headcount, joiners and leavers, who is away today, pending approvals and upcoming events for the company.",
            NoArgs, AiAreas.People, Anyone, false, _ => [new(AiService.Employee, HttpMethod.Get, "/api/dashboard/summary")]),

        new("people_lifecycle", "Onboarding and exits",
            "Open onboarding and exit cases with their progress, and probation and contract end dates coming up.",
            NoArgs, AiAreas.People, Hr, false, _ => [new(AiService.Employee, HttpMethod.Get, "/api/lifecycle/summary")]),

        new("performance_overview", "Performance overview",
            "Review cycles, overdue reviews and goal progress.",
            NoArgs, AiAreas.People, Hr, false, _ => [new(AiService.Employee, HttpMethod.Get, "/api/performance/summary")]),

        new("leave_requests", "Leave requests",
            "Leave requests across the company for a year, optionally only one status.",
            Obj(new
            {
                status = new { type = JsonType.String, @enum = new[] { "Pending", "Approved", "Rejected", "Cancelled" } },
                year = new { type = "integer" },
            }),
            AiAreas.TimeLeave, LeaveAll, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/leave/requests" + Query(("scope", "All"), (StatusArg, Str(a, StatusArg)), ("year", Int(a, "year"))))]),

        new("leave_rules", "Leave types and policies",
            "Leave types, yearly entitlements, carry-over and approval rules.",
            NoArgs, AiAreas.TimeLeave, Anyone, false,
            _ => [new(AiService.Employee, HttpMethod.Get, "/api/leave/types"), new(AiService.Employee, HttpMethod.Get, "/api/leave/policies")]),

        new("attendance_day", "Attendance for a day",
            "Present, absent, late and on-leave counts for one date.",
            Obj(new { date = new { type = JsonType.String, description = DateFormat } }, "date"),
            AiAreas.TimeLeave, Hr, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/attendance/summary" + Query(("date", DateArg(a, "date"))))]),

        new("late_arrivals", "Late arrivals",
            "Days where people arrived late between two dates (max 62 days).",
            Obj(new { from = new { type = JsonType.String }, to = new { type = JsonType.String } }, "from", "to"),
            AiAreas.TimeLeave, Hr, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/attendance/timesheet" + Query(("from", DateArg(a, "from")), ("to", DateArg(a, "to")),
                ("lateOnly", "true"), ("pageSize", "100")))]),

        new("payroll_overview", "Payroll overview",
            "Payroll cost this month and the trend, the current run and its status.",
            NoArgs, AiAreas.Payroll, Pay, false, _ => [new(AiService.Payroll, HttpMethod.Get, "/api/payroll/dashboard")]),

        new("payroll_runs", "Payroll runs",
            "All payroll runs with period, status and totals.",
            NoArgs, AiAreas.Payroll, Pay, false, _ => [new(AiService.Payroll, HttpMethod.Get, "/api/payroll/runs")]),

        new("payroll_run", "Opened a payroll run",
            "One payroll run by id with totals per component and the changes against the previous run.",
            Obj(new { id = new { type = JsonType.String } }, "id"),
            AiAreas.Payroll, Pay, false, a => [new(AiService.Payroll, HttpMethod.Get, $"/api/payroll/runs/{IdArg(a)}")]),

        new("loans", "Loans and advances",
            "Loans and salary advances with what is still owed, plus pending loan requests.",
            NoArgs, AiAreas.LoansExpenses, Pay, false,
            _ => [new(AiService.Payroll, HttpMethod.Get, "/api/payroll/loans"), new(AiService.Payroll, HttpMethod.Get, "/api/payroll/loans/requests")]),

        new("expense_claims", "Expense claims",
            "Expense claims with status and amounts.",
            NoArgs, AiAreas.LoansExpenses, Pay, false, _ => [new(AiService.Payroll, HttpMethod.Get, "/api/payroll/expenses/claims")]),

        new("job_openings", "Job openings",
            "Job openings with department, status and number of applicants.",
            Obj(new { status = new { type = JsonType.String, @enum = new[] { "Draft", "Submitted", "Open", "OnHold", "Filled", "Cancelled" } } }),
            AiAreas.Recruitment, Anyone, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/recruitment/jobs" + Query((StatusArg, Str(a, StatusArg))))]),

        new("job_applications", "Job applications",
            "Applications for a job (or all), with candidate, stage and rating.",
            Obj(new
            {
                jobId = new { type = JsonType.String },
                stage = new { type = JsonType.String, @enum = new[] { "Applied", "Screening", "Interview", "Offer", "Hired", "Rejected", "Withdrawn" } },
            }),
            AiAreas.Recruitment, Anyone, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/recruitment/applications" + Query(("jobId", GuidArg(a, "jobId")), ("stage", Str(a, "stage"))))]),

        new("policies", "Company policies",
            "Company policies (title, category, status). Use get_policy to read one.",
            Obj(new { search = new { type = JsonType.String } }),
            AiAreas.PoliciesLearning, Anyone, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/compliance/policies" + Query((StatusArg, "Published"), (SearchArg, Str(a, SearchArg))))]),

        new("get_policy", "Read a policy",
            "The full text of one policy by id.",
            Obj(new { id = new { type = JsonType.String } }, "id"),
            AiAreas.PoliciesLearning, Anyone, false, a => [new(AiService.Employee, HttpMethod.Get, $"/api/compliance/policies/{IdArg(a)}")]),

        new("learning_overview", "Learning overview",
            "Courses, enrolments and completion.",
            NoArgs, AiAreas.PoliciesLearning, Anyone, false, _ => [new(AiService.Employee, HttpMethod.Get, "/api/learning/summary")]),

        new("relations_overview", "Employee relations",
            "Counts of open grievance and disciplinary cases by stage. No case details.",
            NoArgs, AiAreas.RelationsSafety, [Permissions.RelationsManage], false,
            _ => [new(AiService.Employee, HttpMethod.Get, "/api/relations/summary")]),

        new("safety_overview", "Health and safety",
            "Open incidents, overdue actions and inspections.",
            NoArgs, AiAreas.RelationsSafety, [Permissions.SafetyManage], false,
            _ => [new(AiService.Employee, HttpMethod.Get, "/api/safety/summary")]),

        new("my_leave", "Your leave",
            "The signed-in person's own leave balances and requests.",
            NoArgs, AiAreas.None, [Permissions.LeavesViewOwn], true, _ => [new(AiService.Employee, HttpMethod.Get, "/api/leave/me")]),

        new("my_payslips", "Your payslips",
            "The signed-in person's own payslips.",
            NoArgs, AiAreas.None, [Permissions.PayrollViewOwn], true, _ => [new(AiService.Payroll, HttpMethod.Get, "/api/payroll/me/payslips")]),
    ];

    /// <summary>Is shakhs + company settings ke hisaab se kaun se tools model ko dikhen.</summary>
    public static IReadOnlyList<AiToolDef> For(Func<string, bool> has, AiAreas areas, bool selfService)
        => All.Where(t =>
                (t.Area == AiAreas.None || (areas & t.Area) != 0)
                && (t.AnyOf.Length == 0 || t.AnyOf.Any(has))
                && (!t.SelfService || selfService))
            .ToList();

    public static AiToolDef? Find(string name) => All.FirstOrDefault(t => t.Name == name);

    /// <summary>Report source kis area ka hai — band area ka source na dikhe na chale.</summary>
    public static AiAreas SourceArea(string source) => Key(source) switch
    {
        "employee:employees" or "employee:documents" or "employee:assets" => AiAreas.People,
        "employee:attendance" or "employee:leave-requests" or "employee:leave-balances" => AiAreas.TimeLeave,
        "payroll:payslips" or "payroll:payslip-lines" or "payroll:salaries" => AiAreas.Payroll,
        "payroll:loans" => AiAreas.LoansExpenses,
        _ => AiAreas.None,
    };

    public static string Key(string source) => source.Contains(':') ? source : "employee:" + source;

    private static IReadOnlyList<AiHttpCall> RunReport(JsonElement a)
    {
        var source = Key(Str(a, "source") ?? throw new AiToolException("source is required."));
        var spec = new JsonObject
        {
            ["columns"] = Node(a, "columns") ?? new JsonArray(),
            ["filters"] = Node(a, "filters") ?? new JsonArray(),
            ["groupBy"] = Str(a, "groupBy"),
            ["aggregates"] = Node(a, "aggregates") ?? new JsonArray(),
            ["sort"] = Node(a, "sort"),
            ["limit"] = Math.Clamp(a.TryGetProperty("limit", out var l) && l.TryGetInt32(out var n) ? n : 50, 1, 200),
        };
        var body = new JsonObject
        {
            ["source"] = source,
            ["spec"] = spec,
            ["from"] = DateArg(a, "from"),
            ["to"] = DateArg(a, "to"),
        };
        if (source.StartsWith("payroll:", StringComparison.Ordinal))
            return [new(AiService.Payroll, HttpMethod.Post, "/api/payroll/report-builder/run", body)];
        if (source.StartsWith("employee:", StringComparison.Ordinal))
            return [new(AiService.Employee, HttpMethod.Post, "/api/report-builder/run", body)];
        throw new AiToolException("Unknown source. Call report_sources first.");
    }

    // ---- argument helpers (model ke arguments par bharosa nahi — sab saaf karke URL mein) ----

    private static string? Str(JsonElement a, string name)
        => a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;

    private static string? Bool(JsonElement a, string name)
        => a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True ? "true" : null;

    private static string? Int(JsonElement a, string name)
        => a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n.ToString() : null;

    private static JsonNode? Node(JsonElement a, string name)
        => a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.Array or JsonValueKind.Object
            ? JsonNode.Parse(v.GetRawText()) : null;

    private static string IdArg(JsonElement a) => GuidArg(a, "id") ?? throw new AiToolException("A valid id is required.");

    private static string? GuidArg(JsonElement a, string name)
    {
        var s = Str(a, name);
        if (s is null) return null;
        return Guid.TryParse(s, out var g) ? g.ToString() : throw new AiToolException($"{name} must be an id from an earlier result.");
    }

    private static string? DateArg(JsonElement a, string name)
    {
        var s = Str(a, name);
        if (s is null) return null;
        return DateOnly.TryParseExact(s, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString(DateFormat, CultureInfo.InvariantCulture) : throw new AiToolException($"{name} must be a date like 2026-10-13.");
    }

    private static string Query(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs.Where(p => p.Value is not null).Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}").ToList();
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }
}

public sealed class AiToolException(string message) : Exception(message);

/// <summary>Tool chalane ke liye: user ka token aur Employee API ka apna pata (isi process ka port).</summary>
public sealed record AiCallContext(string Bearer, string EmployeeBaseUrl);

/// <summary>
/// Tools ko HTTP se chalata hai (user ke token ke saath — wahi permissions aur RLS jo page par). Jawab ko chhota karta hai
/// aur niji cheezen (ID number, bank, password) nikal deta hai taake model tak kabhi na jayen.
/// </summary>
public sealed class AiToolRunner(IHttpClientFactory httpFactory, IOptions<AiOptions> options, ILogger<AiToolRunner> logger)
{
    public const string HttpClientName = "ai-tools";
    public const int MaxResultChars = 24_000;

    public async Task<(string Content, bool IsError)> RunAsync(
        AiToolDef tool, JsonElement input, AiCallContext ctx, AiAreas areas, CancellationToken ct)
    {
        try
        {
            if (tool.Name == "report_sources")
                return (await SourcesAsync(ctx, areas, ct), false);
            if (tool.Name == "run_report" && !SourceAllowed(input, areas))
                return ("This source is not available to the assistant. Call report_sources to see what is.", true);
            return await CallAllAsync(tool.Calls(input), ctx, ct);
        }
        catch (AiToolException ex)
        {
            return (ex.Message, true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "AI tool {Tool} failed", tool.Name);
            return ("The data service did not answer. Try a different question.", true);
        }
    }

    private static bool SourceAllowed(JsonElement input, AiAreas areas)
    {
        var source = input.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString()! : "";
        var area = AiToolCatalog.SourceArea(source);
        return area != AiAreas.None && (areas & area) != 0;
    }

    private async Task<(string Content, bool IsError)> CallAllAsync(IReadOnlyList<AiHttpCall> calls, AiCallContext ctx, CancellationToken ct)
    {
        var parts = new List<string>();
        foreach (var call in calls)
        {
            var (status, body) = await SendAsync(call, ctx, ct);
            var error = ErrorText(status, body);
            if (error is not null)
                return (error, true);
            parts.Add(Clean(body));
        }
        return (Shorten(parts.Count == 1 ? parts[0] : "[" + string.Join(",", parts) + "]", MaxResultChars), false);
    }

    private static string? ErrorText(HttpStatusCode status, string body) => status switch
    {
        HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => "The signed-in person does not have permission to see this.",
        HttpStatusCode.NotFound => "Not found.",
        >= HttpStatusCode.BadRequest => $"The request was not accepted: {Shorten(ProblemText(body), 600)}",
        _ => null,
    };

    private async Task<string> SourcesAsync(AiCallContext ctx, AiAreas areas, CancellationToken ct)
    {
        var result = new JsonArray();
        var (status, home) = await SendAsync(new(AiService.Employee, HttpMethod.Get, "/api/report-builder/home"), ctx, ct);
        if (status == HttpStatusCode.OK && JsonNode.Parse(home) is JsonObject h && h["sources"] is JsonArray emp)
            Add(emp);
        if ((areas & (AiAreas.Payroll | AiAreas.LoansExpenses)) != 0)
        {
            var (ps, pay) = await SendAsync(new(AiService.Payroll, HttpMethod.Get, "/api/payroll/report-builder/sources"), ctx, ct);
            if (ps == HttpStatusCode.OK && JsonNode.Parse(pay) is JsonArray p)
                Add(p);
        }
        return result.Count == 0 ? "No report sources are available to this person." : Shorten(result.ToJsonString(), MaxResultChars);

        void Add(JsonArray sources)
        {
            foreach (var s in sources.OfType<JsonObject>())
            {
                var key = AiToolCatalog.Key(s["key"]?.GetValue<string>() ?? "");
                var area = AiToolCatalog.SourceArea(key);
                if (area == AiAreas.None || (areas & area) == 0)
                    continue;
                result.Add(new JsonObject
                {
                    ["source"] = key,
                    ["label"] = s["label"]?.GetValue<string>(),
                    ["needsPeriod"] = s["needsRange"]?.GetValue<bool>(),
                    ["fields"] = new JsonArray(s["fields"]?.AsArray().OfType<JsonObject>()
                        .Select(f => (JsonNode)$"{f["key"]} ({f["type"]})").ToArray() ?? []),
                });
            }
        }
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(AiHttpCall call, AiCallContext ctx, CancellationToken ct)
    {
        var baseUrl = call.Service == AiService.Payroll ? options.Value.PayrollUrl : ctx.EmployeeBaseUrl;
        using var req = new HttpRequestMessage(call.Method, baseUrl.TrimEnd('/') + call.Path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.Bearer);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (call.Body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(call.Body, LlmJson.Options), Encoding.UTF8, "application/json");
        using var res = await httpFactory.CreateClient(HttpClientName).SendAsync(req, ct);
        return (res.StatusCode, await res.Content.ReadAsStringAsync(ct));
    }

    private static string ProblemText(string body)
    {
        try
        {
            var n = JsonNode.Parse(body);
            return n?["detail"]?.GetValue<string>() ?? n?["title"]?.GetValue<string>() ?? body;
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + " …(cut: ask a narrower question)";

    // ---- PII scrub ----

    private static readonly Regex Sensitive = new(
        "(cnic|nationalid|idnumber|passport(no|number)|documentnumber|iban|accountnumber|accountno|bankaccount|swift|routing|password|secret|token|hash|salt|photo|storagekey)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    /// <summary>JSON se niji keys hatao (har gehraai par). JSON na ho to jaisa hai.</summary>
    public static string Clean(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return json;
        }
        Scrub(node);
        return node?.ToJsonString() ?? "null";
    }

    private static void Scrub(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var key in o.Select(p => p.Key).ToList())
                {
                    if (Sensitive.IsMatch(key) || o[key] is null)
                        o.Remove(key);
                    else
                        Scrub(o[key]);
                }
                break;
            case JsonArray a:
                foreach (var item in a)
                    Scrub(item);
                break;
        }
    }
}
