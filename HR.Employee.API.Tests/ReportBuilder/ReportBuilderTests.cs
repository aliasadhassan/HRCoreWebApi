namespace HR.Employee.API.Tests.ReportBuilder;

using HR.Employee.API.Application.ReportBuilder;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.ReportBuilder;
using HR.Shared.Library.Reporting;
using Xunit;
using Row = System.Collections.Generic.Dictionary<string, object?>;

public class ReportBuilderTests
{
    static readonly Guid Tenant = Guid.NewGuid();
    static readonly DateTime Now = new(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);

    static readonly ReportSourceDef People = new("people", "People", "", false,
    [
        new("name", "Name", ReportFieldType.Text, "P"),
        new("dept", "Department", ReportFieldType.Text, "P"),
        new("salary", "Salary", ReportFieldType.Money, "P"),
        new("age", "Age", ReportFieldType.Number, "P", NoTotal: true),
        new("joined", "Joined", ReportFieldType.Date, "P"),
        new("active", "Active", ReportFieldType.Bool, "P"),
    ]);

    static readonly Row[] Rows =
    [
        new() { ["name"] = "Sara", ["dept"] = "Finance", ["salary"] = 9000m, ["age"] = 31m, ["joined"] = new DateOnly(2021, 3, 1), ["active"] = true },
        new() { ["name"] = "Omar", ["dept"] = "Sales", ["salary"] = 7000m, ["age"] = 28m, ["joined"] = new DateOnly(2023, 6, 15), ["active"] = true },
        new() { ["name"] = "Lina", ["dept"] = "Finance", ["salary"] = 11000m, ["age"] = 40m, ["joined"] = new DateOnly(2019, 1, 7), ["active"] = false },
        new() { ["name"] = "Ali", ["dept"] = null, ["salary"] = null, ["age"] = null, ["joined"] = new DateOnly(2024, 2, 2), ["active"] = true },
    ];

    static T WithId<T>(T entity) where T : Entity
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, Guid.NewGuid());
        return entity;
    }

    // ---------- Engine ----------

    [Fact]
    public void Plain_report_filters_sorts_and_totals_money_but_not_age()
    {
        var spec = new ReportSpec
        {
            Columns = ["name", "salary", "age"],
            Filters = [new("active", "eq", "true")],
            Sort = new("salary", true),
        };
        var r = ReportEngine.Run(People, spec, Rows);

        Assert.Equal(["Sara", "Omar", "Ali"], r.Rows.Select(x => (string)x[0]!).Take(2).Append((string)r.Rows[2][0]!));
        Assert.Equal(9000m, r.Rows[0][1]);
        Assert.Null(r.Rows[2][1]);                    // null sab se neeche jab desc
        Assert.Equal(16000m, r.Totals![1]);
        Assert.Null(r.Totals[2]);                     // NoTotal
        Assert.Equal(3, r.TotalRows);
    }

    [Fact]
    public void Grouped_report_counts_sums_and_averages_per_group_with_grand_total()
    {
        var spec = new ReportSpec
        {
            GroupBy = "dept",
            Aggregates = [new("count", null), new("sum", "salary"), new("avg", "salary"), new("max", "joined")],
        };
        var r = ReportEngine.Run(People, spec, Rows);

        Assert.Equal(["dept", "count", "sum:salary", "avg:salary", "max:joined"], r.Columns.Select(c => c.Key));
        Assert.Equal("Total Salary", r.Columns[2].Label);
        var finance = r.Rows.Single(x => (string?)x[0] == "Finance");
        Assert.Equal(2m, finance[1]);
        Assert.Equal(20000m, finance[2]);
        Assert.Equal(10000m, finance[3]);
        Assert.Equal("2021-03-01", finance[4]);
        Assert.Null(r.Rows[0][0]);                    // khaali department pehle
        Assert.Equal(4m, r.Totals![1]);
        Assert.Equal(27000m, r.Totals[2]);
    }

    [Theory]
    [InlineData("name", "contains", "ar", null, 2)]
    [InlineData("name", "starts", "o", null, 1)]
    [InlineData("dept", "in", "Finance, Sales", null, 3)]
    [InlineData("dept", "empty", null, null, 1)]
    [InlineData("salary", "between", "8000", "11000", 2)]
    [InlineData("joined", "gte", "2023-01-01", null, 2)]
    [InlineData("dept", "ne", "Finance", null, 2)]
    public void Filters_work_by_field_type(string field, string op, string? value, string? value2, int expected)
    {
        var spec = new ReportSpec { Columns = ["name"], Filters = [new(field, op, value, value2)] };
        Assert.Equal(expected, ReportEngine.Run(People, spec, Rows).TotalRows);
    }

    [Theory]
    [InlineData("{\"columns\":[\"nope\"]}", "Unknown column")]
    [InlineData("{\"columns\":[]}", "at least one column")]
    [InlineData("{\"columns\":[\"name\"],\"filters\":[{\"field\":\"name\",\"op\":\"gt\",\"value\":\"a\"}]}", "cannot be compared")]
    [InlineData("{\"columns\":[\"name\"],\"filters\":[{\"field\":\"salary\",\"op\":\"eq\",\"value\":\"abc\"}]}", "not a valid value")]
    [InlineData("{\"groupBy\":\"dept\",\"aggregates\":[{\"fn\":\"sum\",\"field\":\"name\"}]}", "not a number")]
    [InlineData("{\"groupBy\":\"dept\",\"aggregates\":[]}", "needs 1 to")]
    [InlineData("{\"columns\":[\"name\"],\"sort\":{\"key\":\"salary\",\"desc\":true}}", "Cannot sort")]
    [InlineData("{\"columns\":[\"name\"],\"limit\":999999}", "Row limit")]
    public void Bad_specs_are_rejected_with_a_clear_message(string json, string message)
    {
        var spec = System.Text.Json.JsonSerializer.Deserialize<ReportSpec>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        var ex = Assert.Throws<ReportSpecException>(() => ReportEngine.Validate(People, spec));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void Limit_cuts_rows_and_marks_result_as_truncated()
    {
        var r = ReportEngine.Run(People, new ReportSpec { Columns = ["name"], Limit = 2 }, Rows);
        Assert.Equal(2, r.Rows.Count);
        Assert.Equal(4, r.TotalRows);
        Assert.True(r.Truncated);
    }

    [Fact]
    public void Every_employee_source_has_unique_field_keys()
    {
        foreach (var s in EmployeeReportSources.Definitions)
            Assert.Equal(s.Fields.Count, s.Fields.Select(f => f.Key).Distinct().Count());
    }

    [Fact]
    public void Enum_words_are_readable()
    {
        Assert.Equal("On notice", EmployeeReportSources.Words(Domain.Employees.EmploymentStatus.OnNotice));
        Assert.Equal("Full time, Contract",
            EmployeeReportSources.Words(Domain.Employees.EmploymentType.FullTime | Domain.Employees.EmploymentType.Contract));
    }

    // ---------- Saved reports ----------

    [Fact]
    public void Periods_resolve_against_today()
    {
        var today = new DateOnly(2026, 10, 13);
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), SavedReport.Resolve(ReportPeriod.ThisMonth, null, null, today));
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)), SavedReport.Resolve(ReportPeriod.LastMonth, null, null, today));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)), SavedReport.Resolve(ReportPeriod.ThisQuarter, null, null, today));
        Assert.Equal((new DateOnly(2026, 9, 14), today), SavedReport.Resolve(ReportPeriod.Last30Days, null, null, today));
        Assert.Null(SavedReport.Resolve(ReportPeriod.None, null, null, today));
    }

    [Fact]
    public void Custom_period_needs_both_dates_in_order()
    {
        var d = new ReportDetails("Overtime", null, "employee:attendance", "{}", ReportChart.Bar, ReportPeriod.Custom, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 1));
        Assert.Throws<DomainException>(() => SavedReport.Create(Tenant, d, "Ali"));
        var ok = SavedReport.Create(Tenant, d with { ToDate = new DateOnly(2026, 10, 31) }, "Ali");
        Assert.Equal(new DateOnly(2026, 10, 5), ok.FromDate);
        ok.Update(d with { Period = ReportPeriod.ThisMonth });
        Assert.Null(ok.FromDate);
    }

    // ---------- Templates ----------

    static TemplateSettings Settings(LetterLanguages lang = LetterLanguages.Both, string? prefix = "exp")
        => new("Experience letter", null, LetterCategory.Experience, lang, PaperSize.A4, true, true, prefix, "Huda Saleh", "HR Manager");

    [Fact]
    public void Publishing_freezes_a_version_and_clears_unpublished_flag()
    {
        var t = WithId(LetterTemplate.Create(Tenant, Settings()));
        Assert.Equal("EXP", t.RefPrefix);
        t.SaveDraft("<p>Hello</p>", null);
        var ex = Assert.Throws<DomainException>(() => t.Publish(null, "Ali", Now));
        Assert.Contains("Arabic", ex.Message);

        t.SaveDraft("<p>Hello</p>", "<p>مرحبا</p>");
        var v1 = t.Publish(null, "Ali", Now);
        Assert.Equal(1, v1.Version);
        Assert.Equal(TemplateStatus.Published, t.Status);
        Assert.False(t.HasUnpublishedChanges);
        Assert.Equal("Huda Saleh", v1.SignatoryName);

        t.SaveDraft("<p>Hello again</p>", "<p>مرحبا</p>");
        Assert.True(t.HasUnpublishedChanges);
        Assert.Equal("<p>Hello</p>", v1.HtmlEn);       // purani version nahi badalti
        Assert.Equal(2, t.Publish(null, "Ali", Now).Version);
    }

    [Fact]
    public void English_only_template_does_not_keep_arabic_in_the_version()
    {
        var t = WithId(LetterTemplate.Create(Tenant, Settings(LetterLanguages.English)));
        t.SaveDraft("<p>Hi</p>", "<p>left over</p>");
        var v = t.Publish(null, null, Now);
        Assert.Null(v.HtmlAr);
        Assert.True(t.HasLanguage("en"));
        Assert.False(t.HasLanguage("ar"));
    }

    [Theory]
    [InlineData("E")]
    [InlineData("EXP1")]
    [InlineData("TOOLONG")]
    public void Reference_prefix_must_be_two_to_six_letters(string prefix)
        => Assert.Throws<DomainException>(() => LetterTemplate.Create(Tenant, Settings(prefix: prefix)));

    [Fact]
    public void Published_template_is_archived_not_deleted()
    {
        var t = WithId(LetterTemplate.Create(Tenant, Settings(LetterLanguages.English)));
        t.SaveDraft("<p>Hi</p>", null);
        t.Publish(null, null, Now);
        Assert.Throws<DomainException>(() => t.Remove());
        t.Archive();
        Assert.Throws<DomainException>(() => t.SaveDraft("<p>x</p>", null));
        t.Restore();
        Assert.Equal(TemplateStatus.Published, t.Status);
    }

    // ---------- Letters ----------

    [Fact]
    public void Issued_letter_keeps_snapshot_and_can_be_voided_once()
    {
        var t = WithId(LetterTemplate.Create(Tenant, Settings(LetterLanguages.English)));
        t.SaveDraft("<p>Hi</p>", null);
        var v = t.Publish(null, null, Now);
        var l = IssuedLetter.Issue(Tenant, "EXP-2026-00001", t, v, Guid.NewGuid(), "Sara Ali", "E-001", "en", "<p>Hi Sara</p>", null, "Bank", null, "Ali", Now);
        Assert.Equal(1, l.TemplateVersion);
        Assert.Equal(LetterCategory.Experience, l.Category);
        Assert.Throws<DomainException>(() => l.Void("  ", Now));
        l.Void("Wrong date", Now);
        Assert.True(l.IsVoid);
        Assert.Throws<DomainException>(() => l.Void("again", Now));
        Assert.Throws<DomainException>(() =>
            IssuedLetter.Issue(Tenant, "EXP-2026-00002", t, v, Guid.NewGuid(), "Sara", "E-1", "fr", "<p>x</p>", null, null, null, null, Now));
    }

    [Fact]
    public void Letter_html_is_sanitised_but_keeps_fields_tables_and_direction()
    {
        var html = "<p dir=\"rtl\" style=\"text-align:right\" onclick=\"x()\">مرحبا <span data-field=\"employee.fullName\" data-format=\"upper\">{{employee.fullName}}</span></p>"
                 + "<script>alert(1)</script><table><tr data-repeat=\"pay.earnings\"><td colspan=\"2\">x</td></tr></table>"
                 + "<section data-if=\"employee.gender\" data-op=\"eq\" data-value=\"Female\"><p>she</p></section>"
                 + "<img src=\"javascript:alert(1)\"><iframe src=\"https://x\"></iframe>";
        var clean = LetterHtml.Clean(html)!;
        Assert.DoesNotContain("script", clean);
        Assert.DoesNotContain("onclick", clean);
        Assert.DoesNotContain("iframe", clean);
        Assert.DoesNotContain("javascript", clean);
        Assert.Contains("dir=\"rtl\"", clean);
        Assert.Contains("data-field=\"employee.fullName\"", clean);
        Assert.Contains("data-repeat=\"pay.earnings\"", clean);
        Assert.Contains("data-if=\"employee.gender\"", clean);
        Assert.Contains("colspan=\"2\"", clean);
        Assert.Contains("text-align: right", clean);
    }
}
