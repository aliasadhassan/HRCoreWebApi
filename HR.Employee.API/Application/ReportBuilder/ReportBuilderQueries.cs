namespace HR.Employee.API.Application.ReportBuilder;

using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.ReportBuilder;
using HR.Shared.Library.Reporting;
using MediatR;
using Microsoft.EntityFrameworkCore;

// ---------- DTOs ----------

public sealed record ReportSourceDto(string Key, string Label, string Description, bool NeedsRange, IReadOnlyList<ReportFieldDef> Fields);

public sealed record ReportBuilderHomeDto(
    bool CanBuild, bool CanIssue, bool CanSeePayroll, IReadOnlyList<ReportSourceDto> Sources,
    IReadOnlyList<SavedReportDto> Reports, IReadOnlyList<TemplateListItemDto> Templates);

public sealed record SavedReportDto(
    Guid Id, string Name, string? Description, string Source, ReportSpec Spec, ReportChart Chart, ReportPeriod Period,
    DateOnly? FromDate, DateOnly? ToDate, string? OwnerName, DateTime CreatedAt, DateTime? UpdatedAt, DateTime? LastRunAt, bool CanRun,
    DateOnly? RunFrom, DateOnly? RunTo);

public sealed record RunResultDto(ReportResult Result, DateOnly? From, DateOnly? To);

public sealed record TemplateListItemDto(
    Guid Id, string Name, string? Description, LetterCategory Category, LetterLanguages Languages, TemplateStatus Status,
    int PublishedVersion, bool HasUnpublishedChanges, string RefPrefix, DateTime? UpdatedAt, DateTime CreatedAt, int IssuedCount, DateTime? LastIssuedAt);

public sealed record TemplateVersionDto(int Version, DateTime PublishedAt, string? PublishedByName);

/// <summary>Builder ko draft milta hai; sirf issuer ko published version ka HTML (Draft* null).</summary>
public sealed record TemplateDetailDto(
    Guid Id, string Name, string? Description, LetterCategory Category, LetterLanguages Languages, PaperSize Paper,
    bool ShowLetterhead, bool ShowPageNumbers, string RefPrefix, string? SignatoryName, string? SignatoryTitle,
    TemplateStatus Status, int PublishedVersion, bool HasUnpublishedChanges, string? DraftHtmlEn, string? DraftHtmlAr,
    PublishedTemplateDto? Published, IReadOnlyList<TemplateVersionDto> Versions, uint RowVersion);

public sealed record PublishedTemplateDto(
    int Version, string? HtmlEn, string? HtmlAr, PaperSize Paper, bool ShowLetterhead, bool ShowPageNumbers,
    string? SignatoryName, string? SignatoryTitle, DateTime PublishedAt);

public sealed record LetterPersonDto(Guid Id, string Code, string Name, string? Department, string? Designation, string Status);

public sealed record LetterEmployeeData(
    Guid Id, string Code, string FullName, string FirstName, string? MiddleName, string LastName, string? Gender,
    string? Title, string? TitleAr, string? HeShe, string? HisHer, DateOnly? DateOfBirth, string? Nationality,
    string WorkEmail, string? WorkPhone, DateOnly JoiningDate, DateOnly? ProbationEnd, DateOnly? ConfirmationDate,
    DateOnly? ExitDate, string? ExitReason, string Status, string EmploymentType, int ServiceYears, int ServiceMonths);

public sealed record LetterJobData(
    string Position, string Department, string Location, string? LocationCity, int? Level, string? Manager, string? ManagerPosition);

public sealed record LetterDataDto(LetterEmployeeData Employee, LetterJobData Job);

public sealed record IssuedLetterListItemDto(
    Guid Id, string Ref, Guid TemplateId, string TemplateName, LetterCategory Category, int TemplateVersion, Guid EmployeeId,
    string EmployeeName, string EmployeeCode, string Language, DateTime IssuedAt, string? IssuedByName, string? Purpose,
    bool IsVoid, string? VoidReason, Guid? BatchId);

public sealed record IssuedLetterDto(IssuedLetterListItemDto Info, string Html, PaperSize Paper, DateTime? VoidedAt);

// ---------- Queries ----------

public sealed record GetReportBuilderHomeQuery : IRequest<ReportBuilderHomeDto>;

public sealed record GetSavedReportQuery(Guid Id) : IRequest<SavedReportDto>;

/// <summary>Bina save kiye (ya saved) report chalana. Source "employee:x" ya "x".</summary>
public sealed record RunReportQuery(string Source, ReportSpec Spec, DateOnly? From, DateOnly? To) : IRequest<RunResultDto>;

public sealed record GetTemplateQuery(Guid Id) : IRequest<TemplateDetailDto>;

public sealed record GetLetterPeopleQuery(string? Search, Guid? DepartmentId, bool IncludeExited) : IRequest<IReadOnlyList<LetterPersonDto>>;

public sealed record GetLetterDataQuery(IReadOnlyList<Guid> EmployeeIds) : IRequest<IReadOnlyList<LetterDataDto>>;

public sealed record GetLettersQuery(Guid? EmployeeId, Guid? TemplateId, DateOnly? From, DateOnly? To, string? Search, bool? Void)
    : IRequest<IReadOnlyList<IssuedLetterListItemDto>>;

public sealed record GetLetterQuery(Guid Id) : IRequest<IssuedLetterDto>;

public sealed class ReportBuilderQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetReportBuilderHomeQuery, ReportBuilderHomeDto>,
    IRequestHandler<GetSavedReportQuery, SavedReportDto>,
    IRequestHandler<RunReportQuery, RunResultDto>,
    IRequestHandler<GetTemplateQuery, TemplateDetailDto>,
    IRequestHandler<GetLetterPeopleQuery, IReadOnlyList<LetterPersonDto>>,
    IRequestHandler<GetLetterDataQuery, IReadOnlyList<LetterDataDto>>,
    IRequestHandler<GetLettersQuery, IReadOnlyList<IssuedLetterListItemDto>>,
    IRequestHandler<GetLetterQuery, IssuedLetterDto>
{
    public const int MaxLetterBatch = 200;

    private readonly ReportBuilderAccess access = new(currentUser);
    private readonly EmployeeReportSources sources = new(db);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<ReportBuilderHomeDto> Handle(GetReportBuilderHomeQuery q, CancellationToken ct)
    {
        var visible = EmployeeReportSources.Visible(access.Has)
            .Select(s => new ReportSourceDto(ReportBuilderAccess.EmployeePrefix + s.Key, s.Label, s.Description, s.NeedsRange, s.Fields))
            .ToList();

        var reports = (await db.SavedReports.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct))
            .Select(ToDto).Where(r => r.CanRun || access.CanBuild).ToList();

        IReadOnlyList<TemplateListItemDto> templates = [];
        if (access.CanBuild || access.CanIssue)
        {
            var stats = await db.IssuedLetters.AsNoTracking().GroupBy(l => l.TemplateId)
                .Select(g => new { g.Key, Count = g.Count(), Last = g.Max(l => l.IssuedAt) }).ToDictionaryAsync(x => x.Key, ct);
            var list = await db.LetterTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
            templates = list
                .Where(t => access.CanBuild || t.Status == TemplateStatus.Published)
                .Select(t => new TemplateListItemDto(t.Id, t.Name, t.Description, t.Category, t.Languages, t.Status, t.PublishedVersion,
                    t.HasUnpublishedChanges, t.RefPrefix, t.UpdatedAt, t.CreatedAt,
                    stats.TryGetValue(t.Id, out var s) ? s.Count : 0, stats.TryGetValue(t.Id, out var s2) ? s2.Last : null))
                .ToList();
        }

        return new ReportBuilderHomeDto(access.CanBuild, access.CanIssue, access.CanSeePayroll, visible, reports, templates);
    }

    public async Task<SavedReportDto> Handle(GetSavedReportQuery q, CancellationToken ct)
    {
        var r = await db.SavedReports.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Report", q.Id);
        var dto = ToDto(r);
        if (!dto.CanRun && !access.CanBuild)
            throw new NotFoundException("Report", q.Id);
        return dto;
    }

    public async Task<RunResultDto> Handle(RunReportQuery q, CancellationToken ct)
    {
        var key = ReportBuilderAccess.EmployeeKey(q.Source);
        var source = EmployeeReportSources.Find(key, access.Has)
            ?? (EmployeeReportSources.Exists(key)
                ? throw new UnauthorizedAccessException("You do not have permission to see this data.")
                : throw new ReportSpecException($"Unknown source '{q.Source}'."));
        ReportEngine.Validate(source, q.Spec);
        var rows = await sources.LoadAsync(key, q.From, q.To, Today, ct);
        return new RunResultDto(ReportEngine.Run(source, q.Spec, rows), q.From, q.To);
    }

    public async Task<TemplateDetailDto> Handle(GetTemplateQuery q, CancellationToken ct)
    {
        access.EnsureCanUseLetters();
        var t = await db.LetterTemplates.AsNoTracking().Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Template", q.Id);
        if (!access.CanBuild && t.Status != TemplateStatus.Published)
            throw new NotFoundException("Template", q.Id);
        var latest = t.Versions.OrderByDescending(v => v.Version).FirstOrDefault();
        return new TemplateDetailDto(t.Id, t.Name, t.Description, t.Category, t.Languages, t.Paper, t.ShowLetterhead, t.ShowPageNumbers,
            t.RefPrefix, t.SignatoryName, t.SignatoryTitle, t.Status, t.PublishedVersion, t.HasUnpublishedChanges,
            access.CanBuild ? t.DraftHtmlEn : null, access.CanBuild ? t.DraftHtmlAr : null,
            latest is null ? null : new PublishedTemplateDto(latest.Version, latest.HtmlEn, latest.HtmlAr, latest.Paper, latest.ShowLetterhead,
                latest.ShowPageNumbers, latest.SignatoryName, latest.SignatoryTitle, latest.PublishedAt),
            t.Versions.OrderByDescending(v => v.Version).Select(v => new TemplateVersionDto(v.Version, v.PublishedAt, v.PublishedByName)).ToList(),
            t.RowVersion);
    }

    public async Task<IReadOnlyList<LetterPersonDto>> Handle(GetLetterPeopleQuery q, CancellationToken ct)
    {
        access.EnsureCanUseLetters();
        var query = db.Employees.AsNoTracking();
        if (!q.IncludeExited)
            query = query.Where(e => e.EmploymentStatus != EmploymentStatus.Exited);
        if (q.DepartmentId is { } d)
            query = query.Where(e => e.DepartmentId == d);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = $"%{q.Search.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.FirstName + " " + e.LastName, s) || EF.Functions.ILike(e.EmployeeCode, s));
        }
        var list = await query.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).Take(500)
            .Select(e => new { e.Id, e.EmployeeCode, e.FirstName, e.LastName, Department = e.Department.Name, Designation = e.Designation.Title, e.EmploymentStatus })
            .ToListAsync(ct);
        return list.Select(e => new LetterPersonDto(e.Id, e.EmployeeCode, $"{e.FirstName} {e.LastName}", e.Department, e.Designation,
            EmployeeReportSources.Words(e.EmploymentStatus)!)).ToList();
    }

    public async Task<IReadOnlyList<LetterDataDto>> Handle(GetLetterDataQuery q, CancellationToken ct)
    {
        access.EnsureCanUseLetters();
        var ids = q.EmployeeIds.Distinct().ToList();
        if (ids.Count is 0 or > MaxLetterBatch)
            throw new DomainException($"Pick 1 to {MaxLetterBatch} employees.");
        var list = await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).Select(e => new
        {
            e.Id, e.EmployeeCode, e.FirstName, e.MiddleName, e.LastName, e.Gender, e.DateOfBirth, e.NationalityCode, e.WorkEmail, e.WorkPhone,
            e.JoiningDate, e.ProbationEndDate, e.ConfirmationDate, e.ExitDate, e.ExitReason, e.EmploymentStatus, e.EmploymentType,
            Position = e.Designation.Title, e.Designation.Level, Department = e.Department.Name, Location = e.Location.Name, e.Location.City,
            Manager = e.Manager == null ? null : e.Manager.FirstName + " " + e.Manager.LastName,
            ManagerPosition = e.Manager == null ? null : e.Manager.Designation.Title,
        }).ToListAsync(ct);
        if (list.Count != ids.Count)
            throw new NotFoundException("Employee", string.Join(", ", ids.Except(list.Select(e => e.Id))));

        var today = Today;
        return ids.Select(id => list.First(e => e.Id == id)).Select(e =>
        {
            var end = e.ExitDate is { } x && x < today ? x : today;
            var months = Math.Max(0, (end.Year - e.JoiningDate.Year) * 12 + end.Month - e.JoiningDate.Month - (end.Day < e.JoiningDate.Day ? 1 : 0));
            var female = e.Gender == Gender.Female;
            var male = e.Gender == Gender.Male;
            return new LetterDataDto(
                new LetterEmployeeData(e.Id, e.EmployeeCode, string.Join(" ", new[] { e.FirstName, e.MiddleName, e.LastName }.Where(n => !string.IsNullOrWhiteSpace(n))),
                    e.FirstName, e.MiddleName, e.LastName, EmployeeReportSources.Words(e.Gender),
                    male ? "Mr." : female ? "Ms." : null, male ? "السيد" : female ? "السيدة" : null,
                    male ? "he" : female ? "she" : "they", male ? "his" : female ? "her" : "their",
                    e.DateOfBirth, e.NationalityCode, e.WorkEmail, e.WorkPhone, e.JoiningDate, e.ProbationEndDate, e.ConfirmationDate,
                    e.ExitDate, e.ExitReason, EmployeeReportSources.Words(e.EmploymentStatus)!, EmployeeReportSources.Words(e.EmploymentType)!,
                    months / 12, months % 12),
                new LetterJobData(e.Position, e.Department, e.Location, e.City, e.Level, e.Manager, e.ManagerPosition));
        }).ToList();
    }

    public async Task<IReadOnlyList<IssuedLetterListItemDto>> Handle(GetLettersQuery q, CancellationToken ct)
    {
        access.EnsureCanUseLetters();
        var query = db.IssuedLetters.AsNoTracking();
        if (q.EmployeeId is { } e) query = query.Where(l => l.EmployeeId == e);
        if (q.TemplateId is { } t) query = query.Where(l => l.TemplateId == t);
        if (q.From is { } f) query = query.Where(l => l.IssuedAt >= f.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (q.To is { } to) query = query.Where(l => l.IssuedAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (q.Void is { } v) query = query.Where(l => l.IsVoid == v);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = $"%{q.Search.Trim()}%";
            query = query.Where(l => EF.Functions.ILike(l.Ref, s) || EF.Functions.ILike(l.EmployeeName, s) || EF.Functions.ILike(l.EmployeeCode, s));
        }
        return await query.OrderByDescending(l => l.IssuedAt).Take(500).Select(l => new IssuedLetterListItemDto(
            l.Id, l.Ref, l.TemplateId, l.TemplateName, l.Category, l.TemplateVersion, l.EmployeeId, l.EmployeeName, l.EmployeeCode,
            l.Language, l.IssuedAt, l.IssuedByName, l.Purpose, l.IsVoid, l.VoidReason, l.BatchId)).ToListAsync(ct);
    }

    public async Task<IssuedLetterDto> Handle(GetLetterQuery q, CancellationToken ct)
    {
        access.EnsureCanUseLetters();
        var l = await db.IssuedLetters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Letter", q.Id);
        return new IssuedLetterDto(ListItem(l), l.Html, l.Paper, l.VoidedAt);
    }

    internal static IssuedLetterListItemDto ListItem(IssuedLetter l) => new(
        l.Id, l.Ref, l.TemplateId, l.TemplateName, l.Category, l.TemplateVersion, l.EmployeeId, l.EmployeeName, l.EmployeeCode,
        l.Language, l.IssuedAt, l.IssuedByName, l.Purpose, l.IsVoid, l.VoidReason, l.BatchId);

    /// <summary>RunFrom/RunTo = period aaj ke hisaab se (This month waghaira) — client inhi se chalata hai.</summary>
    private SavedReportDto ToDto(SavedReport r)
    {
        var range = SavedReport.Resolve(r.Period, r.FromDate, r.ToDate, Today);
        return new(r.Id, r.Name, r.Description, r.Source, ReportJson.Read(r.Spec), r.Chart, r.Period, r.FromDate, r.ToDate, r.OwnerName,
            r.CreatedAt, r.UpdatedAt, r.LastRunAt, access.CanRun(r.Source), range?.From, range?.To);
    }
}
