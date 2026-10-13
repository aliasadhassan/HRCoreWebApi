namespace HR.Employee.API.Domain.ReportBuilder;

using System.Text.RegularExpressions;
using HR.Employee.API.Domain.Common;

public enum ReportChart : byte { None = 1, Bar = 2, Pie = 3, Line = 4 }

/// <summary>Saved report ki muddat: report chalate waqt aaj ke hisaab se from/to banta hai. Custom = saved tareekhein.</summary>
public enum ReportPeriod : byte { None = 1, ThisMonth = 2, LastMonth = 3, Last30Days = 4, ThisQuarter = 5, ThisYear = 6, LastYear = 7, Custom = 8 }

public enum LetterCategory : byte { Experience = 1, Salary = 2, Employment = 3, Offer = 4, Warning = 5, NoObjection = 6, Other = 7 }

public enum LetterLanguages : byte { English = 1, Arabic = 2, Both = 3 }

public enum PaperSize : byte { A4 = 1, Letter = 2 }

public enum TemplateStatus : byte { Draft = 1, Published = 2, Archived = 3 }

/// <summary>
/// Report builder mein save hua table report. Source = "employee:attendance" / "payroll:payslips";
/// Spec = HR.Shared.Library.Reporting.ReportSpec ka JSON (jsonb). Spec ki jaanch application layer engine se karti hai.
/// </summary>
public sealed class SavedReport : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string Source { get; private set; } = default!;
    public string Spec { get; private set; } = default!;
    public ReportChart Chart { get; private set; }
    public ReportPeriod Period { get; private set; }
    public DateOnly? FromDate { get; private set; }
    public DateOnly? ToDate { get; private set; }
    public string? OwnerName { get; private set; }
    public DateTime? LastRunAt { get; private set; }

    private SavedReport() { }

    public static SavedReport Create(Guid tenantId, ReportDetails d, string? ownerName)
    {
        var r = new SavedReport { TenantId = Guard.NotEmpty(tenantId, "Tenant"), OwnerName = Guard.Optional(ownerName, "Owner", 120) };
        r.Apply(d);
        return r;
    }

    public void Update(ReportDetails d) => Apply(d);

    public void MarkRun(DateTime now) => LastRunAt = now;

    public void Remove() => IsDeleted = true;

    private void Apply(ReportDetails d)
    {
        if (!Enum.IsDefined(d.Chart))
            throw new DomainException("Select a valid chart type.");
        if (!Enum.IsDefined(d.Period))
            throw new DomainException("Select a valid period.");
        Name = Guard.Required(d.Name, "Name", 150);
        Description = Guard.Optional(d.Description, "Description", 500);
        Source = Guard.Required(d.Source, "Source", 60);
        Spec = Guard.Required(d.Spec, "Report definition", 20000);
        Chart = d.Chart;
        Period = d.Period;
        if (d.Period == ReportPeriod.Custom)
        {
            if (d.FromDate is null || d.ToDate is null)
                throw new DomainException("A custom period needs a from and a to date.");
            if (d.ToDate < d.FromDate)
                throw new DomainException("The to date cannot be before the from date.");
            FromDate = d.FromDate;
            ToDate = d.ToDate;
        }
        else
        {
            FromDate = null;
            ToDate = null;
        }
    }

    /// <summary>Period ko asal tareekhon mein badalna (None = koi range nahi).</summary>
    public static (DateOnly From, DateOnly To)? Resolve(ReportPeriod period, DateOnly? from, DateOnly? to, DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var quarterStart = new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1);
        return period switch
        {
            ReportPeriod.ThisMonth => (monthStart, monthStart.AddMonths(1).AddDays(-1)),
            ReportPeriod.LastMonth => (monthStart.AddMonths(-1), monthStart.AddDays(-1)),
            ReportPeriod.Last30Days => (today.AddDays(-29), today),
            ReportPeriod.ThisQuarter => (quarterStart, quarterStart.AddMonths(3).AddDays(-1)),
            ReportPeriod.ThisYear => (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
            ReportPeriod.LastYear => (new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31)),
            ReportPeriod.Custom when from is { } f && to is { } t => (f, t),
            _ => null,
        };
    }
}

public sealed record ReportDetails(
    string Name, string? Description, string Source, string Spec, ReportChart Chart, ReportPeriod Period, DateOnly? FromDate, DateOnly? ToDate);

/// <summary>
/// Letter / certificate ka template (TipTap editor ka HTML). Draft mein kaam hota hai; Publish par nayi version banti hai
/// aur letters hamesha published version se bante hain. RefPrefix = letter number ka shuru (EXP-2026-00012).
/// </summary>
public sealed class LetterTemplate : AuditableEntity
{
    public const int MaxHtml = 200_000;
    private static readonly Regex PrefixRule = new("^[A-Z]{2,6}$", RegexOptions.Compiled);

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public LetterCategory Category { get; private set; }
    public LetterLanguages Languages { get; private set; }
    public PaperSize Paper { get; private set; }
    public bool ShowLetterhead { get; private set; }
    public bool ShowPageNumbers { get; private set; }
    public string RefPrefix { get; private set; } = default!;
    public string? SignatoryName { get; private set; }
    public string? SignatoryTitle { get; private set; }
    public string? DraftHtmlEn { get; private set; }
    public string? DraftHtmlAr { get; private set; }
    public TemplateStatus Status { get; private set; }
    public int PublishedVersion { get; private set; }
    public bool HasUnpublishedChanges { get; private set; }

    private readonly List<LetterTemplateVersion> versions = [];
    public IReadOnlyList<LetterTemplateVersion> Versions => versions;

    private LetterTemplate() { }

    public static LetterTemplate Create(Guid tenantId, TemplateSettings s)
    {
        var t = new LetterTemplate { TenantId = Guard.NotEmpty(tenantId, "Tenant"), Status = TemplateStatus.Draft };
        t.ApplySettings(s);
        t.HasUnpublishedChanges = true;
        return t;
    }

    public void UpdateSettings(TemplateSettings s)
    {
        EnsureNotArchived();
        ApplySettings(s);
        HasUnpublishedChanges = true;
    }

    public void SaveDraft(string? htmlEn, string? htmlAr)
    {
        EnsureNotArchived();
        DraftHtmlEn = Html(htmlEn, "English");
        DraftHtmlAr = Html(htmlAr, "Arabic");
        HasUnpublishedChanges = true;
    }

    /// <summary>Draft ko nayi version bana deta hai. Jo zaban template mein hai us ka HTML khaali nahi ho sakta.</summary>
    public LetterTemplateVersion Publish(Guid? userId, string? userName, DateTime now)
    {
        EnsureNotArchived();
        if (Languages is LetterLanguages.English or LetterLanguages.Both && string.IsNullOrWhiteSpace(DraftHtmlEn))
            throw new DomainException("Write the English letter before publishing.");
        if (Languages is LetterLanguages.Arabic or LetterLanguages.Both && string.IsNullOrWhiteSpace(DraftHtmlAr))
            throw new DomainException("Write the Arabic letter before publishing.");
        PublishedVersion++;
        var v = LetterTemplateVersion.Create(TenantId, Id, PublishedVersion,
            Languages == LetterLanguages.Arabic ? null : DraftHtmlEn,
            Languages == LetterLanguages.English ? null : DraftHtmlAr,
            this, userId, userName, now);
        versions.Add(v);
        Status = TemplateStatus.Published;
        HasUnpublishedChanges = false;
        return v;
    }

    public void Archive()
    {
        EnsureNotArchived();
        Status = TemplateStatus.Archived;
    }

    public void Restore()
    {
        if (Status != TemplateStatus.Archived)
            throw new DomainException("Only an archived template can be restored.");
        Status = PublishedVersion > 0 ? TemplateStatus.Published : TemplateStatus.Draft;
    }

    public void Remove()
    {
        if (PublishedVersion > 0)
            throw new DomainException("A published template cannot be deleted because letters point to it. Archive it instead.");
        IsDeleted = true;
    }

    public bool HasLanguage(string lang) => lang switch
    {
        "en" => Languages is LetterLanguages.English or LetterLanguages.Both,
        "ar" => Languages is LetterLanguages.Arabic or LetterLanguages.Both,
        _ => false,
    };

    private void EnsureNotArchived()
    {
        if (Status == TemplateStatus.Archived)
            throw new DomainException("This template is archived. Restore it first.");
    }

    private void ApplySettings(TemplateSettings s)
    {
        if (!Enum.IsDefined(s.Category))
            throw new DomainException("Select a valid category.");
        if (!Enum.IsDefined(s.Languages))
            throw new DomainException("Select the letter language.");
        if (!Enum.IsDefined(s.Paper))
            throw new DomainException("Select a paper size.");
        var prefix = (s.RefPrefix ?? "").Trim().ToUpperInvariant();
        if (!PrefixRule.IsMatch(prefix))
            throw new DomainException("The reference prefix must be 2 to 6 letters (A-Z).");
        Name = Guard.Required(s.Name, "Name", 150);
        Description = Guard.Optional(s.Description, "Description", 500);
        Category = s.Category;
        Languages = s.Languages;
        Paper = s.Paper;
        ShowLetterhead = s.ShowLetterhead;
        ShowPageNumbers = s.ShowPageNumbers;
        RefPrefix = prefix;
        SignatoryName = Guard.Optional(s.SignatoryName, "Signatory name", 120);
        SignatoryTitle = Guard.Optional(s.SignatoryTitle, "Signatory title", 120);
    }

    private static string? Html(string? html, string language)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;
        if (html.Length > MaxHtml)
            throw new DomainException($"The {language} letter is too long.");
        return html;
    }

    public static string DefaultPrefix(LetterCategory c) => c switch
    {
        LetterCategory.Experience => "EXP",
        LetterCategory.Salary => "SAL",
        LetterCategory.Employment => "EMP",
        LetterCategory.Offer => "OFR",
        LetterCategory.Warning => "WRN",
        LetterCategory.NoObjection => "NOC",
        _ => "LTR",
    };
}

public sealed record TemplateSettings(
    string Name, string? Description, LetterCategory Category, LetterLanguages Languages, PaperSize Paper,
    bool ShowLetterhead, bool ShowPageNumbers, string? RefPrefix, string? SignatoryName, string? SignatoryTitle);

/// <summary>Publish ki hui version — badalti nahi. Letter isi ka HTML aur settings use karta hai.</summary>
public sealed class LetterTemplateVersion : TenantChildEntity
{
    public Guid TemplateId { get; private set; }
    public int Version { get; private set; }
    public string? HtmlEn { get; private set; }
    public string? HtmlAr { get; private set; }
    public PaperSize Paper { get; private set; }
    public bool ShowLetterhead { get; private set; }
    public bool ShowPageNumbers { get; private set; }
    public string? SignatoryName { get; private set; }
    public string? SignatoryTitle { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public string? PublishedByName { get; private set; }

    private LetterTemplateVersion() { }

    internal static LetterTemplateVersion Create(
        Guid tenantId, Guid templateId, int version, string? htmlEn, string? htmlAr, LetterTemplate t,
        Guid? userId, string? userName, DateTime now) => new()
    {
        TenantId = tenantId,
        TemplateId = templateId,
        Version = version,
        HtmlEn = htmlEn,
        HtmlAr = htmlAr,
        Paper = t.Paper,
        ShowLetterhead = t.ShowLetterhead,
        ShowPageNumbers = t.ShowPageNumbers,
        SignatoryName = t.SignatoryName,
        SignatoryTitle = t.SignatoryTitle,
        PublishedAt = now,
        PublishedByUserId = userId,
        PublishedByName = userName is null ? null : userName.Length > 120 ? userName[..120] : userName,
    };
}

/// <summary>
/// Jari kiya hua letter — bhara hua HTML (snapshot) jaisa print hua, reference number ke saath. Badalta nahi;
/// ghalat ho to Void (wajah ke saath) aur naya jari karo.
/// </summary>
public sealed class IssuedLetter : AuditableEntity
{
    public string Ref { get; private set; } = default!;
    public Guid TemplateId { get; private set; }
    public int TemplateVersion { get; private set; }
    public string TemplateName { get; private set; } = default!;
    public LetterCategory Category { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string EmployeeName { get; private set; } = default!;
    public string EmployeeCode { get; private set; } = default!;
    public string Language { get; private set; } = default!;
    public string Html { get; private set; } = default!;
    public PaperSize Paper { get; private set; }
    public Guid? BatchId { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public Guid? IssuedByUserId { get; private set; }
    public string? IssuedByName { get; private set; }
    public string? Purpose { get; private set; }
    public bool IsVoid { get; private set; }
    public string? VoidReason { get; private set; }
    public DateTime? VoidedAt { get; private set; }

    private IssuedLetter() { }

    public static IssuedLetter Issue(
        Guid tenantId, string reference, LetterTemplate template, LetterTemplateVersion version, Guid employeeId,
        string employeeName, string employeeCode, string language, string html, Guid? batchId, string? purpose,
        Guid? userId, string? userName, DateTime now)
    {
        if (language is not ("en" or "ar"))
            throw new DomainException("Letter language must be English or Arabic.");
        if (string.IsNullOrWhiteSpace(html))
            throw new DomainException("The letter is empty.");
        if (html.Length > LetterTemplate.MaxHtml)
            throw new DomainException("The letter is too long.");
        return new IssuedLetter
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Ref = Guard.Required(reference, "Reference", 30),
            TemplateId = template.Id,
            TemplateVersion = version.Version,
            TemplateName = template.Name,
            Category = template.Category,
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            EmployeeName = Guard.Required(employeeName, "Employee name", 200),
            EmployeeCode = Guard.Required(employeeCode, "Employee code", 30),
            Language = language,
            Html = html,
            Paper = version.Paper,
            BatchId = batchId,
            Purpose = Guard.Optional(purpose, "Purpose", 300),
            IssuedAt = now,
            IssuedByUserId = userId,
            IssuedByName = userName is null ? null : userName.Length > 120 ? userName[..120] : userName,
        };
    }

    public void Void(string? reason, DateTime now)
    {
        if (IsVoid)
            throw new DomainException("This letter is already void.");
        VoidReason = Guard.Required(reason, "Reason", 500);
        IsVoid = true;
        VoidedAt = now;
    }
}
