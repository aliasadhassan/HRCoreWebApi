namespace HR.Employee.API.Application.ReportBuilder;

using System.Text.Json;
using Ganss.Xss;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Reporting;

/// <summary>
/// Report builder ki ijazatein.
/// reports.build = table reports save / badalna aur letter templates banana, publish karna.
/// letters.issue = published template se letter jari karna, jari shuda letters dekhna aur void karna.
/// Report chalana = source ka apna permission (Employees: employees.view, Leaves: leaves.view.all, Payroll: payroll.view.all…).
/// </summary>
public sealed class ReportBuilderAccess(ICurrentUser currentUser)
{
    public const string EmployeePrefix = "employee:";
    public const string PayrollPrefix = "payroll:";

    /// <summary>Payroll API ke sources (wahan validate hote hain). Yahan sirf naam aur permission.</summary>
    public static readonly string[] PayrollSources = ["payslips", "payslip-lines", "salaries", "loans"];

    public bool CanBuild => currentUser.HasPermission(Permissions.ReportsBuild);
    public bool CanIssue => currentUser.HasPermission(Permissions.LettersIssue);
    public bool Has(string permission) => currentUser.HasPermission(permission);
    public Guid TenantId => currentUser.RequireTenantId();
    public Guid? UserId => currentUser.UserId;
    public string? UserName => currentUser.Name ?? currentUser.Email;

    public bool CanSeePayroll => Has(Permissions.PayrollViewAll) || Has(Permissions.PayrollApprove);

    public void EnsureCanBuild()
    {
        if (!CanBuild)
            throw new UnauthorizedAccessException("You do not have permission to build reports and templates.");
    }

    public void EnsureCanIssue()
    {
        if (!CanIssue)
            throw new UnauthorizedAccessException("You do not have permission to issue letters.");
    }

    public void EnsureCanUseLetters()
    {
        if (!CanBuild && !CanIssue)
            throw new UnauthorizedAccessException("You do not have permission to use letter templates.");
    }

    /// <summary>Saved report ka source user chala sakta hai? (prefix ke saath key)</summary>
    public bool CanRun(string source)
    {
        if (source.StartsWith(EmployeePrefix, StringComparison.Ordinal))
            return EmployeeReportSources.Find(source[EmployeePrefix.Length..], Has) is not null;
        if (source.StartsWith(PayrollPrefix, StringComparison.Ordinal))
            return CanSeePayroll && PayrollSources.Contains(source[PayrollPrefix.Length..]);
        return false;
    }

    /// <summary>"employee:attendance" ya sirf "attendance" → employee source key.</summary>
    public static string EmployeeKey(string source)
        => source.StartsWith(EmployeePrefix, StringComparison.Ordinal) ? source[EmployeePrefix.Length..] : source;
}

internal static class ReportJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write(ReportSpec spec) => JsonSerializer.Serialize(spec, Options);

    public static ReportSpec Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ReportSpec>(json, Options) ?? new ReportSpec();
        }
        catch (JsonException)
        {
            throw new ReportSpecException("The saved report definition is damaged.");
        }
    }
}

/// <summary>
/// Template / letter HTML ki safai: script, event handler, iframe, form waghaira nikal do. TipTap ke tags,
/// tables, style aur hamare data-* (field, repeat, if) rehte hain. Tasveer sirf https / http se.
/// </summary>
public static class LetterHtml
{
    private static readonly HtmlSanitizer Sanitizer = Create();

    private static HtmlSanitizer Create()
    {
        var s = new HtmlSanitizer();
        foreach (var a in new[] { "data-field", "data-format", "data-repeat", "data-if", "data-op", "data-value", "data-type",
                     "data-label", "dir", "class", "colspan", "rowspan", "colwidth", "data-colwidth", "lang" })
            s.AllowedAttributes.Add(a);
        foreach (var t in new[] { "section", "colgroup", "col", "mark" })
            s.AllowedTags.Add(t);
        s.AllowedSchemes.Clear();
        s.AllowedSchemes.Add("https");
        s.AllowedSchemes.Add("http");
        s.AllowedCssProperties.Add("page-break-after");
        s.AllowedCssProperties.Add("break-after");
        return s;
    }

    public static string? Clean(string? html) => string.IsNullOrWhiteSpace(html) ? null : Sanitizer.Sanitize(html);
}
