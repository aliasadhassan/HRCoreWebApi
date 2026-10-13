namespace HR.Employee.API.Application.Ai;

using HR.Employee.API.Domain.Ai;

/// <summary>Model ki hidayat. Static hissa cache hota hai — is mein tareekh, naam ya koi badalti cheez na daalo.</summary>
public static class AiPrompts
{
    public const string Chat = """
        You are the HR assistant inside HR Cloud, an HR and payroll system. You help HR staff, managers and employees
        answer questions about their company's people data.

        How to work:
        - Use the tools to look things up. Never guess numbers, names or dates; if the tools cannot answer, say what you could not find.
        - For counts, totals, lists and comparisons, call report_sources once, then run_report with the narrowest filters you can.
        - The tools only return what the signed-in person is allowed to see. If a tool says there is no permission, tell them
          plainly that their role cannot see that data, without guessing what it would contain.
        - You can read but never change anything: you cannot approve, edit, send email or run payroll. When asked, explain where
          in the app they can do it, or draft the text for them to copy.
        - Never reveal ID card numbers, bank accounts or passwords; the tools remove them, and you must not ask for them.

        How to answer:
        - Reply in the language of the question (English, Arabic, Chinese or Turkish).
        - Lead with the answer in one or two sentences, then the detail.
        - Use a markdown table when there are 3 or more rows of the same kind. Keep tables to the columns that matter.
        - Dates as 13 Oct 2026. Money with its currency code. Do not invent precision.
        - End with the source in one short line, e.g. "Source: Attendance, 1–12 Oct."
        - Be brief. No preamble, no repeating the question.
        """;

    public const string Insights = """
        You review a company's HR data each week and point out what an HR manager should look at. Use the tools to check
        attendance, leave, payroll, loans, recruitment, onboarding and policies, as far as they are available.

        Rules:
        - Only report things the data shows. Every card needs a real number from a tool result.
        - Prefer changes and risks: rises against the previous period, things expiring or overdue, gaps in approvals,
          payroll differences, leave balances that will lapse, probation ends without a review.
        - Level: "attention" = needs action soon; "look" = worth a look; "info" = good to know.
        - title: one short sentence. body: two sentences at most, with names only when needed.
        - metric: the key number (e.g. "+35%", "4", "PKR 1.2M"); metricLabel: what it measures.
        - source: which data and period you used.
        - link: the page to open, one of /app/attendance, /app/leaves, /app/employees, /app/payroll/runs, /app/payroll/loans,
          /app/expenses, /app/talent/recruitment, /app/talent/performance, /app/talent/lifecycle, /app/compliance/policies, /app/assets.
        - area: one of People, Attendance, Leave, Payroll, Loans, Recruitment, Policies.
        - At most 6 cards, most important first. If nothing stands out, return fewer cards rather than filler.
        """;

    public const string Screening = """
        You screen a CV against one job opening for an HR team. You rank; people decide.

        Rules:
        - Judge only skills, qualifications and experience against the job's requirements.
        - Ignore name, photo, age, date of birth, gender, marital status, religion, nationality and address. Never mention them.
        - For each requirement say "yes", "partial" or "no", and quote the evidence from the CV word for word (short).
          If the CV says nothing about it, the answer is "no" and evidence explains that it is not mentioned.
        - score: 0 to 100 for how well the CV matches the requirements (must-haves weigh most).
        - summary: two sentences, plain, with the main strength and the main gap.
        - questions: three interview questions that test the gaps or confirm the strengths.
        - Write in English unless the job is written in another language.
        """;

    public static object InsightsSchema => new
    {
        type = "object",
        properties = new
        {
            insights = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        area = new { type = "string" },
                        level = new { type = "string", @enum = new[] { "attention", "look", "info" } },
                        title = new { type = "string" },
                        body = new { type = "string" },
                        metric = new { type = "string" },
                        metricLabel = new { type = "string" },
                        source = new { type = "string" },
                        link = new { type = "string" },
                    },
                    required = new[] { "area", "level", "title", "body", "metric", "metricLabel", "source", "link" },
                    additionalProperties = false,
                },
            },
        },
        required = new[] { "insights" },
        additionalProperties = false,
    };

    public static object ScreeningSchema => new
    {
        type = "object",
        properties = new
        {
            score = new { type = "integer" },
            summary = new { type = "string" },
            requirements = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        requirement = new { type = "string" },
                        met = new { type = "string", @enum = new[] { "yes", "partial", "no" } },
                        evidence = new { type = "string" },
                    },
                    required = new[] { "requirement", "met", "evidence" },
                    additionalProperties = false,
                },
            },
            questions = new { type = "array", items = new { type = "string" } },
        },
        required = new[] { "score", "summary", "requirements", "questions" },
        additionalProperties = false,
    };

    public static InsightLevel Level(string? level) => level switch
    {
        "attention" => InsightLevel.Attention,
        "look" => InsightLevel.Look,
        _ => InsightLevel.Info,
    };
}
