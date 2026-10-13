namespace HR.Employee.API.Application.ReportBuilder;

using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.ReportBuilder;
using HR.Shared.Library.Reporting;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record ReportRequest(
    string Name, string? Description, string Source, ReportSpec Spec, ReportChart Chart, ReportPeriod Period, DateOnly? FromDate, DateOnly? ToDate);

public sealed record TemplateSettingsRequest(
    string Name, string? Description, LetterCategory Category, LetterLanguages Languages, PaperSize Paper,
    bool ShowLetterhead, bool ShowPageNumbers, string? RefPrefix, string? SignatoryName, string? SignatoryTitle);

public sealed record LetterItemRequest(Guid EmployeeId, string Html);

public sealed record IssueLettersRequest(Guid TemplateId, int Version, string Language, string? Purpose, IReadOnlyList<LetterItemRequest> Items);

public sealed record IssuedLetterResultDto(Guid Id, Guid EmployeeId, string Ref, string Html);

public sealed record SaveResultDto(Guid Id, uint RowVersion);

public sealed record CreateReportCommand(ReportRequest Data) : IRequest<Guid>;
public sealed record UpdateReportCommand(Guid Id, ReportRequest Data) : IRequest;
public sealed record DeleteReportCommand(Guid Id) : IRequest;
public sealed record MarkReportRunCommand(Guid Id) : IRequest;

public sealed record CreateTemplateCommand(TemplateSettingsRequest Settings, string? HtmlEn, string? HtmlAr) : IRequest<Guid>;
public sealed record UpdateTemplateSettingsCommand(Guid Id, TemplateSettingsRequest Settings, uint? RowVersion) : IRequest<SaveResultDto>;
public sealed record SaveTemplateDraftCommand(Guid Id, string? HtmlEn, string? HtmlAr, uint? RowVersion) : IRequest<SaveResultDto>;
public sealed record PublishTemplateCommand(Guid Id, uint? RowVersion) : IRequest<SaveResultDto>;
public sealed record ArchiveTemplateCommand(Guid Id, bool Archive) : IRequest;
public sealed record DeleteTemplateCommand(Guid Id) : IRequest;
public sealed record DuplicateTemplateCommand(Guid Id) : IRequest<Guid>;

/// <summary>
/// Client template ko bhar kar (employee + pay + company data) HTML bhejta hai; yahan reference number milta hai
/// ("[[REF]]" ki jagah), HTML saaf hota hai aur snapshot save. Ek batch = ek BatchId.
/// </summary>
public sealed record IssueLettersCommand(IssueLettersRequest Data) : IRequest<IReadOnlyList<IssuedLetterResultDto>>;
public sealed record VoidLetterCommand(Guid Id, string? Reason) : IRequest;

public sealed class ReportBuilderCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateReportCommand, Guid>,
    IRequestHandler<UpdateReportCommand>,
    IRequestHandler<DeleteReportCommand>,
    IRequestHandler<MarkReportRunCommand>,
    IRequestHandler<CreateTemplateCommand, Guid>,
    IRequestHandler<UpdateTemplateSettingsCommand, SaveResultDto>,
    IRequestHandler<SaveTemplateDraftCommand, SaveResultDto>,
    IRequestHandler<PublishTemplateCommand, SaveResultDto>,
    IRequestHandler<ArchiveTemplateCommand>,
    IRequestHandler<DeleteTemplateCommand>,
    IRequestHandler<DuplicateTemplateCommand, Guid>,
    IRequestHandler<IssueLettersCommand, IReadOnlyList<IssuedLetterResultDto>>,
    IRequestHandler<VoidLetterCommand>
{
    public const string RefMarker = "[[REF]]";

    private readonly ReportBuilderAccess access = new(currentUser);

    // ---- Saved reports ----

    public async Task<Guid> Handle(CreateReportCommand c, CancellationToken ct)
    {
        access.EnsureCanBuild();
        var r = SavedReport.Create(access.TenantId, Details(c.Data), access.UserName);
        db.SavedReports.Add(r);
        await db.SaveChangesAsync(ct);
        return r.Id;
    }

    public async Task Handle(UpdateReportCommand c, CancellationToken ct)
    {
        access.EnsureCanBuild();
        var r = await db.SavedReports.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Report", c.Id);
        r.Update(Details(c.Data));
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(DeleteReportCommand c, CancellationToken ct)
    {
        access.EnsureCanBuild();
        var r = await db.SavedReports.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Report", c.Id);
        r.Remove();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(MarkReportRunCommand c, CancellationToken ct)
    {
        var r = await db.SavedReports.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Report", c.Id);
        if (!access.CanRun(r.Source))
            throw new NotFoundException("Report", c.Id);
        r.MarkRun(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Employee source ka spec poora jaancha jata hai; payroll ka source naam aur shakal (Payroll API chalate waqt jaanchti hai).</summary>
    private ReportDetails Details(ReportRequest d)
    {
        var source = (d.Source ?? "").Trim();
        if (source.StartsWith(ReportBuilderAccess.EmployeePrefix, StringComparison.Ordinal))
        {
            var key = source[ReportBuilderAccess.EmployeePrefix.Length..];
            var def = EmployeeReportSources.Find(key, access.Has)
                ?? throw new DomainException("Pick a data source you can see.");
            ReportEngine.Validate(def, d.Spec ?? new ReportSpec());
            if (def.NeedsRange && d.Period == ReportPeriod.None)
                throw new DomainException($"{def.Label} needs a period.");
        }
        else if (source.StartsWith(ReportBuilderAccess.PayrollPrefix, StringComparison.Ordinal))
        {
            if (!access.CanSeePayroll || !ReportBuilderAccess.PayrollSources.Contains(source[ReportBuilderAccess.PayrollPrefix.Length..]))
                throw new DomainException("Pick a data source you can see.");
            if (d.Spec is null || (d.Spec.Columns.Count == 0 && d.Spec.GroupBy is null))
                throw new DomainException("Pick at least one column.");
        }
        else
            throw new DomainException("Pick a data source.");
        return new ReportDetails(d.Name, d.Description, source, ReportJson.Write(d.Spec!), d.Chart, d.Period, d.FromDate, d.ToDate);
    }

    // ---- Templates ----

    public async Task<Guid> Handle(CreateTemplateCommand c, CancellationToken ct)
    {
        access.EnsureCanBuild();
        var t = LetterTemplate.Create(access.TenantId, Settings(c.Settings));
        t.SaveDraft(LetterHtml.Clean(c.HtmlEn), LetterHtml.Clean(c.HtmlAr));
        db.LetterTemplates.Add(t);
        await db.SaveChangesAsync(ct);
        return t.Id;
    }

    public async Task<SaveResultDto> Handle(UpdateTemplateSettingsCommand c, CancellationToken ct)
    {
        var t = await LoadForEditAsync(c.Id, c.RowVersion, ct);
        t.UpdateSettings(Settings(c.Settings));
        await SaveAsync(ct);
        return new SaveResultDto(t.Id, t.RowVersion);
    }

    public async Task<SaveResultDto> Handle(SaveTemplateDraftCommand c, CancellationToken ct)
    {
        var t = await LoadForEditAsync(c.Id, c.RowVersion, ct);
        t.SaveDraft(LetterHtml.Clean(c.HtmlEn), LetterHtml.Clean(c.HtmlAr));
        await SaveAsync(ct);
        return new SaveResultDto(t.Id, t.RowVersion);
    }

    public async Task<SaveResultDto> Handle(PublishTemplateCommand c, CancellationToken ct)
    {
        var t = await LoadForEditAsync(c.Id, c.RowVersion, ct, withVersions: true);
        var v = t.Publish(access.UserId, access.UserName, DateTime.UtcNow);
        db.LetterTemplateVersions.Add(v);
        await SaveAsync(ct);
        return new SaveResultDto(t.Id, t.RowVersion);
    }

    public async Task Handle(ArchiveTemplateCommand c, CancellationToken ct)
    {
        var t = await LoadForEditAsync(c.Id, null, ct);
        if (c.Archive) t.Archive(); else t.Restore();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(DeleteTemplateCommand c, CancellationToken ct)
    {
        var t = await LoadForEditAsync(c.Id, null, ct);
        t.Remove();
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> Handle(DuplicateTemplateCommand c, CancellationToken ct)
    {
        var src = await LoadForEditAsync(c.Id, null, ct);
        var name = src.Name.Length > 140 ? src.Name[..140] : src.Name;
        var copy = LetterTemplate.Create(access.TenantId, new TemplateSettings($"{name} (copy)", src.Description, src.Category, src.Languages,
            src.Paper, src.ShowLetterhead, src.ShowPageNumbers, src.RefPrefix, src.SignatoryName, src.SignatoryTitle));
        copy.SaveDraft(src.DraftHtmlEn, src.DraftHtmlAr);
        db.LetterTemplates.Add(copy);
        await db.SaveChangesAsync(ct);
        return copy.Id;
    }

    private async Task<LetterTemplate> LoadForEditAsync(Guid id, uint? rowVersion, CancellationToken ct, bool withVersions = false)
    {
        access.EnsureCanBuild();
        IQueryable<LetterTemplate> q = withVersions ? db.LetterTemplates.Include(x => x.Versions) : db.LetterTemplates;
        var t = await q.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Template", id);
        if (rowVersion is { } rv && rv != t.RowVersion)
            throw new ConflictException("Someone else changed this template. Reload it to see their changes.");
        return t;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Someone else changed this template. Reload it to see their changes.");
        }
    }

    private static TemplateSettings Settings(TemplateSettingsRequest s) => new(s.Name, s.Description, s.Category, s.Languages, s.Paper,
        s.ShowLetterhead, s.ShowPageNumbers, string.IsNullOrWhiteSpace(s.RefPrefix) ? LetterTemplate.DefaultPrefix(s.Category) : s.RefPrefix,
        s.SignatoryName, s.SignatoryTitle);

    // ---- Letters ----

    public async Task<IReadOnlyList<IssuedLetterResultDto>> Handle(IssueLettersCommand c, CancellationToken ct)
    {
        access.EnsureCanIssue();
        var d = c.Data;
        if (d.Items is null || d.Items.Count is 0 or > ReportBuilderQueryHandlers.MaxLetterBatch)
            throw new DomainException($"Issue 1 to {ReportBuilderQueryHandlers.MaxLetterBatch} letters at a time.");
        if (d.Items.Select(i => i.EmployeeId).Distinct().Count() != d.Items.Count)
            throw new DomainException("An employee is listed twice.");

        var t = await db.LetterTemplates.AsNoTracking().Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == d.TemplateId, ct)
            ?? throw new NotFoundException("Template", d.TemplateId);
        if (t.Status != TemplateStatus.Published)
            throw new DomainException("Only a published template can be used to issue letters.");
        if (!t.HasLanguage(d.Language))
            throw new DomainException("This template has no letter in that language.");
        // Client ne jis version se banaya wahi abhi latest ho — beech mein publish hua ho to dobara bhar kar bhejo
        var version = t.Versions.OrderByDescending(v => v.Version).First();
        if (version.Version != d.Version)
            throw new ConflictException("A newer version of this template was published. Open it again and re-generate.");

        var ids = d.Items.Select(i => i.EmployeeId).ToList();
        var people = await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.EmployeeCode, Name = e.FirstName + " " + e.LastName }).ToDictionaryAsync(e => e.Id, ct);
        var missing = ids.Where(i => !people.ContainsKey(i)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException("Employee", string.Join(", ", missing));

        var year = DateTime.UtcNow.Year;
        var prefix = $"{t.RefPrefix}-{year}-";
        var existing = await db.IssuedLetters.IgnoreQueryFilters().AsNoTracking()
            .Where(l => l.TenantId == access.TenantId && l.Ref.StartsWith(prefix)).Select(l => l.Ref).ToListAsync(ct);
        var next = existing.Select(r => int.TryParse(r[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();

        var batch = d.Items.Count > 1 ? Guid.NewGuid() : (Guid?)null;
        var now = DateTime.UtcNow;
        var issued = new List<IssuedLetter>();
        foreach (var item in d.Items)
        {
            var reference = $"{prefix}{++next:D5}";
            var html = LetterHtml.Clean((item.Html ?? "").Replace(RefMarker, reference, StringComparison.Ordinal))
                ?? throw new DomainException("A letter is empty.");
            var p = people[item.EmployeeId];
            var letter = IssuedLetter.Issue(access.TenantId, reference, t, version, item.EmployeeId, p.Name, p.EmployeeCode, d.Language,
                html, batch, d.Purpose, access.UserId, access.UserName, now);
            db.IssuedLetters.Add(letter);
            issued.Add(letter);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_RbIssuedLetters_TenantId_Ref") == true)
        {
            throw new ConflictException("Someone issued a letter at the same moment. Try again.");
        }
        return issued.Select(l => new IssuedLetterResultDto(l.Id, l.EmployeeId, l.Ref, l.Html)).ToList();
    }

    public async Task Handle(VoidLetterCommand c, CancellationToken ct)
    {
        access.EnsureCanIssue();
        var l = await db.IssuedLetters.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Letter", c.Id);
        l.Void(c.Reason, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}
