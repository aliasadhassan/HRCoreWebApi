namespace HR.Employee.API.Application.Ai;

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
    string Name, string Label, string Description, object Schema, AiArea Area, string[] AnyOf, bool SelfService,
    Func<JsonElement, IReadOnlyList<AiHttpCall>> Calls);

/// <summary>Model ko dikhne wale tools aur unke HTTP raste. Har tool sirf GET (ya report run) — AI kuch badal nahi sakta.</summary>
public static class AiToolCatalog
{
    private static readonly string[] Hr = [Permissions.EmployeesView];
    private static readonly string[] LeaveAll = [Permissions.LeavesViewAll, Permissions.LeavesApprove];
    private static readonly string[] Pay = [Permissions.PayrollViewAll, Permissions.PayrollApprove, Permissions.PayrollRun];
    private static readonly string[] Anyone = [];

    private static object Obj(object properties, params string[] required)
        => new { type = "object", properties, required };

    private static readonly object NoArgs = Obj(new { });

    public static readonly IReadOnlyList<AiToolDef> All =
    [
        new("report_sources", "Report sources",
            "List the data tables you can query with run_report (employees, attendance, leave, documents, assets, payslips, salaries, loans) and their field keys. Call this before run_report.",
            NoArgs, AiArea.None, Anyone, false, _ => []),

        new("run_report", "Ran a report",
            "Query one data table: pick columns, filters, an optional group-by with aggregates (count/sum/avg/min/max), sort and limit. " +
            "Use it for counts, totals, lists and comparisons. Sources that need a period require from and to (yyyy-MM-dd). " +
            "Filter ops: eq, ne, contains, starts, gt, gte, lt, lte, between (value, value2), empty, notempty, in (comma separated).",
            Obj(new
            {
                source = new { type = "string", description = "Source key from report_sources, e.g. employee:attendance or payroll:payslips" },
                columns = new { type = "array", items = new { type = "string" }, description = "Field keys to show (when not grouping)" },
                filters = new
                {
                    type = "array",
                    items = Obj(new
                    {
                        field = new { type = "string" }, op = new { type = "string" },
                        value = new { type = "string" }, value2 = new { type = "string" },
                    }, "field", "op"),
                },
                groupBy = new { type = "string", description = "Field key to group by" },
                aggregates = new { type = "array", items = Obj(new { fn = new { type = "string" }, field = new { type = "string" } }, "fn") },
                sort = Obj(new { key = new { type = "string" }, desc = new { type = "boolean" } }, "key"),
                limit = new { type = "integer", description = "Max rows (default 50)" },
                from = new { type = "string", description = "yyyy-MM-dd" },
                to = new { type = "string", description = "yyyy-MM-dd" },
            }, "source"),
            AiArea.None, Anyone, false, RunReport),

        new("find_employees", "Looked up employees",
            "Search the employee directory by name, code or email. Returns job, department, manager and status.",
            Obj(new
            {
                search = new { type = "string" },
                status = new { type = "string", @enum = new[] { "Active", "Probation", "OnNotice", "Suspended", "Exited" } },
                includeExited = new { type = "boolean" },
            }),
            AiArea.People, Hr, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/employees" + Query(("pageSize", "25"), ("search", Str(a, "search")),
                ("status", Str(a, "status")), ("includeExited", Bool(a, "includeExited"))))]),

        new("get_employee", "Opened an employee",
            "Full profile of one employee by id (from find_employees): job, dates, manager, contact. ID numbers and bank details are never shown.",
            Obj(new { id = new { type = "string" } }, "id"),
            AiArea.People, Hr, false, a => [new(AiService.Employee, HttpMethod.Get, $"/api/employees/{IdArg(a)}")]),

        new("company_overview", "Company overview",
            "Headcount, joiners and leavers, who is away today, pending approvals and upcoming events for the company.",
            NoArgs, AiArea.People, Anyone, false, _ => [new(AiService.Employee, HttpMethod.Get, "/api/dashboard/summary")]),

        new("people_lifecycle", "Onboarding and exits",
            "Open onboarding and exit cases with their progress, and probation and contract end dates coming up.",
            NoArgs, AiArea.People, Hr, false, _ => [new(AiService.Employee, HttpMethod.Get, "/api/lifecycle/summary")]),

        new("performance_overview", "Performance overview",
            "Review cycles, overdue reviews and goal progress.",
            NoArgs, AiArea.People, Hr, false, _ => [new(AiService.Employee, HttpMethod.Get, "/api/performance/summary")]),

        new("leave_requests", "Leave requests",
            "Leave requests across the company for a year, optionally only one status.",
            Obj(new
            {
                status = new { type = "string", @enum = new[] { "Pending", "Approved", "Rejected", "Cancelled" } },
                year = new { type = "integer" },
            }),
            AiArea.TimeLeave, LeaveAll, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/leave/requests" + Query(("scope", "All"), ("status", Str(a, "status")), ("year", Int(a, "year"))))]),

        new("leave_rules", "Leave types and policies",
            "Leave types, yearly entitlements, carry-over and approval rules.",
            NoArgs, AiArea.TimeLeave, Anyone, false,
            _ => [new(AiService.Employee, HttpMethod.Get, "/api/leave/types"), new(AiService.Employee, HttpMethod.Get, "/api/leave/policies")]),

        new("attendance_day", "Attendance for a day",
            "Present, absent, late and on-leave counts for one date.",
            Obj(new { date = new { type = "string", description = "yyyy-MM-dd" } }, "date"),
            AiArea.TimeLeave, Hr, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/attendance/summary" + Query(("date", DateArg(a, "date"))))]),

        new("late_arrivals", "Late arrivals",
            "Days where people arrived late between two dates (max 62 days).",
            Obj(new { from = new { type = "string" }, to = new { type = "string" } }, "from", "to"),
            AiArea.TimeLeave, Hr, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/attendance/timesheet" + Query(("from", DateArg(a, "from")), ("to", DateArg(a, "to")),
                ("lateOnly", "true"), ("pageSize", "100")))]),

        new("payroll_overview", "Payroll overview",
            "Payroll cost this month and the trend, the current run and its status.",
            NoArgs, AiArea.Payroll, Pay, false, _ => [new(AiService.Payroll, HttpMethod.Get, "/api/payroll/dashboard")]),

        new("payroll_runs", "Payroll runs",
            "All payroll runs with period, status and totals.",
            NoArgs, AiArea.Payroll, Pay, false, _ => [new(AiService.Payroll, HttpMethod.Get, "/api/payroll/runs")]),

        new("payroll_run", "Opened a payroll run",
            "One payroll run by id with totals per component and the changes against the previous run.",
            Obj(new { id = new { type = "string" } }, "id"),
            AiArea.Payroll, Pay, false, a => [new(AiService.Payroll, HttpMethod.Get, $"/api/payroll/runs/{IdArg(a)}")]),

        new("loans", "Loans and advances",
            "Loans and salary advances with what is still owed, plus pending loan requests.",
            NoArgs, AiArea.LoansExpenses, Pay, false,
            _ => [new(AiService.Payroll, HttpMethod.Get, "/api/payroll/loans"), new(AiService.Payroll, HttpMethod.Get, "/api/payroll/loans/requests")]),

        new("expense_claims", "Expense claims",
            "Expense claims with status and amounts.",
            NoArgs, AiArea.LoansExpenses, Pay, false, _ => [new(AiService.Payroll, HttpMethod.Get, "/api/payroll/expenses/claims")]),

        new("job_openings", "Job openings",
            "Job openings with department, status and number of applicants.",
            Obj(new { status = new { type = "string", @enum = new[] { "Draft", "Submitted", "Open", "OnHold", "Filled", "Cancelled" } } }),
            AiArea.Recruitment, Anyone, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/recruitment/jobs" + Query(("status", Str(a, "status"))))]),

        new("job_applications", "Job applications",
            "Applications for a job (or all), with candidate, stage and rating.",
            Obj(new
            {
                jobId = new { type = "string" },
                stage = new { type = "string", @enum = new[] { "Applied", "Screening", "Interview", "Offer", "Hired", "Rejected", "Withdrawn" } },
            }),
            AiArea.Recruitment, Anyone, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/recruitment/applications" + Query(("jobId", GuidArg(a, "jobId")), ("stage", Str(a, "stage"))))]),

        new("policies", "Company policies",
            "Company policies (title, category, status). Use get_policy to read one.",
            Obj(new { search = new { type = "string" } }),
            AiArea.PoliciesLearning, Anyone, false,
            a => [new(AiService.Employee, HttpMethod.Get, "/api/compliance/policies" + Query(("status", "Published"), ("search", Str(a, "search"))))]),

        new("get_policy", "Read a policy",
            "The full text of one policy by id.",
            Obj(new { id = new { type = "string" } }, "id"),
            AiArea.PoliciesLearning, Anyone, false, a => [new(AiService.Employee, HttpMethod.Get, $"/api/compliance/policies/{IdArg(a)}")]),

        new("learning_overview", "Learning overview",
            "Courses, enrolments and completion.",
            NoArgs, AiArea.PoliciesLearning, Anyone, false, _ => [new(AiService.Employee, HttpMethod.Get, "/api/learning/summary")]),

        new("relations_overview", "Employee relations",
            "Counts of open grievance and disciplinary cases by stage. No case details.",
            NoArgs, AiArea.RelationsSafety, [Permissions.RelationsManage], false,
            _ => [new(AiService.Employee, HttpMethod.Get, "/api/relations/summary")]),

        new("safety_overview", "Health and safety",
            "Open incidents, overdue actions and inspections.",
            NoArgs, AiArea.RelationsSafety, [Permissions.SafetyManage], false,
            _ => [new(AiService.Employee, HttpMethod.Get, "/api/safety/summary")]),

        new("my_leave", "Your leave",
            "The signed-in person's own leave balances and requests.",
            NoArgs, AiArea.None, [Permissions.LeavesViewOwn], true, _ => [new(AiService.Employee, HttpMethod.Get, "/api/leave/me")]),

        new("my_payslips", "Your payslips",
            "The signed-in person's own payslips.",
            NoArgs, AiArea.None, [Permissions.PayrollViewOwn], true, _ => [new(AiService.Payroll, HttpMethod.Get, "/api/payroll/me/payslips")]),
    ];

    /// <summary>Is shakhs + company settings ke hisaab se kaun se tools model ko dikhen.</summary>
    public static IReadOnlyList<AiToolDef> For(Func<string, bool> has, AiArea areas, bool selfService)
        => All.Where(t =>
                (t.Area == AiArea.None || (areas & t.Area) != 0)
                && (t.AnyOf.Length == 0 || t.AnyOf.Any(has))
                && (!t.SelfService || selfService))
            .ToList();

    public static AiToolDef? Find(string name) => All.FirstOrDefault(t => t.Name == name);

    /// <summary>Report source kis area ka hai — band area ka source na dikhe na chale.</summary>
    public static AiArea SourceArea(string source) => Key(source) switch
    {
        "employee:employees" or "employee:documents" or "employee:assets" => AiArea.People,
        "employee:attendance" or "employee:leave-requests" or "employee:leave-balances" => AiArea.TimeLeave,
        "payroll:payslips" or "payroll:payslip-lines" or "payroll:salaries" => AiArea.Payroll,
        "payroll:loans" => AiArea.LoansExpenses,
        _ => AiArea.None,
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
            ["source"] = source.StartsWith("payroll:", StringComparison.Ordinal) ? source : source,
            ["spec"] = spec,
            ["from"] = DateArg(a, "from"),
            ["to"] = DateArg(a, "to"),
        };
        return source.StartsWith("payroll:", StringComparison.Ordinal)
            ? [new(AiService.Payroll, HttpMethod.Post, "/api/payroll/report-builder/run", body)]
            : source.StartsWith("employee:", StringComparison.Ordinal)
                ? [new(AiService.Employee, HttpMethod.Post, "/api/report-builder/run", body)]
                : throw new AiToolException("Unknown source. Call report_sources first.");
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
        return DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d) ? d.ToString("yyyy-MM-dd") : throw new AiToolException($"{name} must be a date like 2026-10-13.");
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
        AiToolDef tool, JsonElement input, AiCallContext ctx, AiArea areas, CancellationToken ct)
    {
        try
        {
            if (tool.Name == "report_sources")
                return (await SourcesAsync(ctx, areas, ct), false);
            if (tool.Name == "run_report")
            {
                var source = input.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString()! : "";
                var area = AiToolCatalog.SourceArea(source);
                if (area == AiArea.None || (areas & area) == 0)
                    return ("This source is not available to the assistant. Call report_sources to see what is.", true);
            }

            var calls = tool.Calls(input);
            var parts = new List<string>();
            foreach (var call in calls)
            {
                var (status, body) = await SendAsync(call, ctx, ct);
                if (status == HttpStatusCode.Forbidden || status == HttpStatusCode.Unauthorized)
                    return ("The signed-in person does not have permission to see this.", true);
                if (status == HttpStatusCode.NotFound)
                    return ("Not found.", true);
                if ((int)status >= 400)
                    return ($"The request was not accepted: {Shorten(ProblemText(body), 600)}", true);
                parts.Add(Clean(body));
            }
            return (Shorten(parts.Count == 1 ? parts[0] : "[" + string.Join(",", parts) + "]", MaxResultChars), false);
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

    private async Task<string> SourcesAsync(AiCallContext ctx, AiArea areas, CancellationToken ct)
    {
        var result = new JsonArray();
        var (status, home) = await SendAsync(new(AiService.Employee, HttpMethod.Get, "/api/report-builder/home"), ctx, ct);
        if (status == HttpStatusCode.OK && JsonNode.Parse(home)?["sources"] is JsonArray emp)
            Add(emp);
        if ((areas & (AiArea.Payroll | AiArea.LoansExpenses)) != 0)
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
                if (area == AiArea.None || (areas & area) == 0)
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
