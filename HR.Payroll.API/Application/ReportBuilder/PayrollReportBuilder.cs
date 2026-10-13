namespace HR.Payroll.API.Application.ReportBuilder;

using System.Text.RegularExpressions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Salaries;
using HR.Payroll.API.Domain.Setup;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Reporting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using F = HR.Shared.Library.Reporting.ReportFieldDef;
using T = HR.Shared.Library.Reporting.ReportFieldType;

using Row = System.Collections.Generic.Dictionary<string, object?>;

/// <summary>
/// Report builder ke payroll sources (sirf payroll.view.all / payroll.approve). Payslips sirf Approved/Paid runs ki —
/// draft ya calculated run ke figures report mein nahi aate. Saved reports Employee API mein rehte hain ("payroll:payslips").
/// </summary>
public static class PayrollReportSources
{
    public const int MaxLoad = 50_000;
    public const int MaxRangeDays = 366;

    private static readonly F[] Person =
    [
        new("code", "Employee code", T.Text, "Employee"),
        new("employee", "Employee", T.Text, "Employee"),
        new("department", "Department", T.Text, "Employee"),
        new("designation", "Designation", T.Text, "Employee"),
    ];

    public static readonly ReportSourceDef[] All =
    [
        new("payslips", "Payslips", "One row per payslip from approved and paid payroll runs in the period.", true,
        [
            new("month", "Month", T.Text, "Period"),
            new("periodStart", "Period start", T.Date, "Period"),
            new("periodEnd", "Period end", T.Date, "Period"),
            .. Person,
            new("payslipNumber", "Payslip number", T.Text, "Payslip"),
            new("runType", "Run type", T.Text, "Payslip"),
            new("currency", "Currency", T.Text, "Payslip"),
            new("payableDays", "Payable days", T.Number, "Payslip"),
            new("unpaidDays", "Unpaid leave days", T.Number, "Payslip"),
            new("gross", "Gross earnings", T.Money, "Amounts"),
            new("deductions", "Total deductions", T.Money, "Amounts"),
            new("tax", "Tax", T.Money, "Amounts"),
            new("net", "Net pay", T.Money, "Amounts"),
            new("employerCost", "Employer contributions", T.Money, "Amounts"),
        ]),
        new("payslip-lines", "Payslip lines", "Each earning and deduction on approved and paid payslips in the period.", true,
        [
            new("month", "Month", T.Text, "Period"),
            .. Person,
            new("component", "Pay component", T.Text, "Line"),
            new("componentCode", "Component code", T.Text, "Line"),
            new("componentType", "Component type", T.Text, "Line"),
            new("taxable", "Taxable", T.Bool, "Line"),
            new("amount", "Amount", T.Money, "Line"),
            new("currency", "Currency", T.Text, "Line"),
        ]),
        new("salaries", "Current salaries", "Each employee's salary in effect today.", false,
        [
            .. Person,
            new("active", "Active employee", T.Bool, "Employee"),
            new("joiningDate", "Joining date", T.Date, "Employee"),
            new("template", "Salary template", T.Text, "Salary"),
            new("grade", "Salary grade", T.Text, "Salary"),
            new("currency", "Currency", T.Text, "Salary"),
            new("basis", "Salary basis", T.Text, "Salary"),
            new("amount", "Salary amount", T.Money, "Salary"),
            new("annual", "Annual equivalent", T.Money, "Salary"),
            new("effectiveFrom", "Effective from", T.Date, "Salary"),
            new("changeReason", "Last change", T.Text, "Salary"),
        ]),
        new("loans", "Loans & advances", "Loans and salary advances with what is still owed.", false,
        [
            .. Person,
            new("loanType", "Type", T.Text, "Loan"),
            new("status", "Status", T.Text, "Loan"),
            new("currency", "Currency", T.Text, "Loan"),
            new("principal", "Amount given", T.Money, "Loan"),
            new("installment", "Monthly installment", T.Money, "Loan"),
            new("outstanding", "Still owed", T.Money, "Loan"),
            new("repaid", "Repaid", T.Money, "Loan"),
            new("startDate", "Start date", T.Date, "Loan"),
        ]),
    ];

    public static string? Words<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var s = Regex.Replace(value.ToString(), "(?<=[a-z])(?=[A-Z])", " ", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        return s.Length > 1 ? s[0] + s[1..].ToLowerInvariant() : s;
    }
}

public sealed record PayrollSourceDto(string Key, string Label, string Description, bool NeedsRange, IReadOnlyList<ReportFieldDef> Fields);

public sealed record PayrollRunResultDto(ReportResult Result, DateOnly? From, DateOnly? To);

public sealed record LetterPayLineDto(string Name, string Code, decimal Amount);

/// <summary>Letter (salary certificate) ke liye: aakhri Approved/Paid payslip aur maujooda salary.</summary>
public sealed record LetterPayDto(
    Guid EmployeeId, string? Currency, string? Period, DateOnly? PeriodStart, decimal? Basic, decimal? Gross, decimal? Deductions,
    decimal? Net, IReadOnlyList<LetterPayLineDto> Earnings, IReadOnlyList<LetterPayLineDto> DeductionLines,
    decimal? SalaryAmount, string? SalaryBasis, decimal? AnnualSalary, string? BankName, string? Iban);

public sealed record GetPayrollSourcesQuery : IRequest<IReadOnlyList<PayrollSourceDto>>;
public sealed record RunPayrollReportQuery(string Source, ReportSpec Spec, DateOnly? From, DateOnly? To) : IRequest<PayrollRunResultDto>;
public sealed record GetLetterPayQuery(IReadOnlyList<Guid> EmployeeIds) : IRequest<IReadOnlyList<LetterPayDto>>;

public sealed class PayrollReportBuilderHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetPayrollSourcesQuery, IReadOnlyList<PayrollSourceDto>>,
    IRequestHandler<RunPayrollReportQuery, PayrollRunResultDto>,
    IRequestHandler<GetLetterPayQuery, IReadOnlyList<LetterPayDto>>
{
    public const string Prefix = "payroll:";
    private static readonly RunStatus[] Closed = [RunStatus.Approved, RunStatus.Paid];

    private bool CanSeePayroll => currentUser.HasPermission(Permissions.PayrollViewAll) || currentUser.HasPermission(Permissions.PayrollApprove);

    public Task<IReadOnlyList<PayrollSourceDto>> Handle(GetPayrollSourcesQuery q, CancellationToken ct)
    {
        IReadOnlyList<PayrollSourceDto> list = CanSeePayroll
            ? PayrollReportSources.All.Select(s => new PayrollSourceDto(Prefix + s.Key, s.Label, s.Description, s.NeedsRange, s.Fields)).ToList()
            : [];
        return Task.FromResult(list);
    }

    public async Task<PayrollRunResultDto> Handle(RunPayrollReportQuery q, CancellationToken ct)
    {
        if (!CanSeePayroll)
            throw new UnauthorizedAccessException("You do not have permission to see company payroll.");
        var key = q.Source.StartsWith(Prefix, StringComparison.Ordinal) ? q.Source[Prefix.Length..] : q.Source;
        var source = PayrollReportSources.All.FirstOrDefault(s => s.Key == key) ?? throw new ReportSpecException($"Unknown source '{q.Source}'.");
        ReportEngine.Validate(source, q.Spec);
        if (source.NeedsRange)
        {
            if (q.From is null || q.To is null)
                throw new ReportSpecException($"{source.Label} needs a from and a to date.");
            if (q.To < q.From)
                throw new ReportSpecException("The to date cannot be before the from date.");
            if (q.To.Value.DayNumber - q.From.Value.DayNumber + 1 > PayrollReportSources.MaxRangeDays)
                throw new ReportSpecException($"Pick a period of at most {PayrollReportSources.MaxRangeDays} days.");
        }
        var rows = key switch
        {
            "payslips" => await PayslipsAsync(q.From!.Value, q.To!.Value, ct),
            "payslip-lines" => await LinesAsync(q.From!.Value, q.To!.Value, ct),
            "salaries" => await SalariesAsync(ct),
            _ => await LoansAsync(ct),
        };
        if (rows.Count > PayrollReportSources.MaxLoad)
            throw new ReportSpecException($"This report has more than {PayrollReportSources.MaxLoad:N0} rows. Pick a shorter period.");
        return new PayrollRunResultDto(ReportEngine.Run(source, q.Spec, rows), q.From, q.To);
    }

    /// <summary>Salary certificate ke figures — payroll.view.all / approve ya letters.issue / reports.build (HR letters).</summary>
    public async Task<IReadOnlyList<LetterPayDto>> Handle(GetLetterPayQuery q, CancellationToken ct)
    {
        if (!CanSeePayroll && !currentUser.HasPermission(Permissions.LettersIssue) && !currentUser.HasPermission(Permissions.ReportsBuild))
            throw new UnauthorizedAccessException("You do not have permission to see salary figures for letters.");
        var ids = q.EmployeeIds.Distinct().ToList();
        if (ids.Count is 0 or > 200)
            throw new DomainException("Pick 1 to 200 employees.");

        var lastSlipIds = await (
            from s in db.Payslips.AsNoTracking()
            join r in db.PayrollRuns.AsNoTracking() on s.PayrollRunId equals r.Id
            where ids.Contains(s.EmployeeId) && Closed.Contains(r.Status) && r.RunType == RunType.Regular
            group s by s.EmployeeId into g
            select g.OrderByDescending(x => x.PeriodStart).Select(x => x.Id).First()).ToListAsync(ct);
        var slips = await db.Payslips.AsNoTracking().Include(s => s.Lines).Where(s => lastSlipIds.Contains(s.Id)).ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var salaries = await db.EmployeeSalaries.AsNoTracking()
            .Where(s => ids.Contains(s.EmployeeId) && s.EffectiveFrom <= today && (s.EffectiveTo == null || s.EffectiveTo >= today))
            .Select(s => new { s.EmployeeId, s.SalaryBasis, s.BasisAmount, s.CurrencyCode }).ToListAsync(ct);
        var banks = await db.PayrollEmployees.AsNoTracking().Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.BankName, e.Iban }).ToDictionaryAsync(e => e.Id, ct);

        return ids.Select(id =>
        {
            var slip = slips.FirstOrDefault(s => s.EmployeeId == id);
            var salary = salaries.FirstOrDefault(s => s.EmployeeId == id);
            var lines = slip?.Lines.OrderBy(l => l.SortOrder).ToList() ?? [];
            var bank = banks.GetValueOrDefault(id);
            return new LetterPayDto(id, slip?.CurrencyCode ?? salary?.CurrencyCode, slip?.PeriodStart.ToString("yyyy-MM"), slip?.PeriodStart,
                lines.Where(l => l.ComponentCode == SystemComponentCodes.Basic).Sum(l => (decimal?)l.Amount),
                slip?.GrossEarnings, slip?.TotalDeductions, slip?.NetPay,
                lines.Where(l => l.ComponentType == ComponentType.Earning).Select(l => new LetterPayLineDto(l.ComponentName, l.ComponentCode, l.Amount)).ToList(),
                lines.Where(l => l.ComponentType == ComponentType.Deduction).Select(l => new LetterPayLineDto(l.ComponentName, l.ComponentCode, l.Amount)).ToList(),
                salary?.BasisAmount, salary is null ? null : PayrollReportSources.Words(salary.SalaryBasis),
                salary is null ? null : SalaryMath.AnnualEquivalent(salary.SalaryBasis, salary.BasisAmount),
                bank?.BankName, bank?.Iban);
        }).ToList();
    }

    private sealed record Slip(
        Guid EmployeeId, string PayslipNumber, string EmployeeCode, string EmployeeName, string? DepartmentName, string? DesignationTitle,
        string CurrencyCode, DateOnly PeriodStart, DateOnly PeriodEnd, decimal PayableDays, decimal UnpaidLeaveDays, decimal GrossEarnings,
        decimal TotalDeductions, decimal TaxAmount, decimal NetPay, decimal EmployerContributions, RunType RunType);

    private IQueryable<Domain.Runs.Payslip> ClosedSlips(DateOnly from, DateOnly to)
        => from s in db.Payslips.AsNoTracking()
           join r in db.PayrollRuns.AsNoTracking() on s.PayrollRunId equals r.Id
           join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
           where Closed.Contains(r.Status) && p.PeriodStart >= @from && p.PeriodStart <= to
           select s;

    private static Row PersonRow(string code, string name, string? department, string? designation) => new()
    {
        ["code"] = code,
        ["employee"] = name,
        ["department"] = department,
        ["designation"] = designation,
    };

    private async Task<List<Row>> PayslipsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var list = await (
            from s in db.Payslips.AsNoTracking()
            join r in db.PayrollRuns.AsNoTracking() on s.PayrollRunId equals r.Id
            join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
            where Closed.Contains(r.Status) && p.PeriodStart >= @from && p.PeriodStart <= to
            select new Slip(s.EmployeeId, s.PayslipNumber, s.EmployeeCode, s.EmployeeName, s.DepartmentName, s.DesignationTitle, s.CurrencyCode,
                s.PeriodStart, s.PeriodEnd, s.PayableDays, s.UnpaidLeaveDays, s.GrossEarnings, s.TotalDeductions, s.TaxAmount, s.NetPay,
                s.EmployerContributions, r.RunType))
            .Take(PayrollReportSources.MaxLoad + 1).ToListAsync(ct);
        return list.Select(s =>
        {
            var row = PersonRow(s.EmployeeCode, s.EmployeeName, s.DepartmentName, s.DesignationTitle);
            row["month"] = s.PeriodStart.ToString("yyyy-MM");
            row["periodStart"] = s.PeriodStart;
            row["periodEnd"] = s.PeriodEnd;
            row["payslipNumber"] = s.PayslipNumber;
            row["runType"] = PayrollReportSources.Words(s.RunType);
            row["currency"] = s.CurrencyCode;
            row["payableDays"] = s.PayableDays;
            row["unpaidDays"] = s.UnpaidLeaveDays;
            row["gross"] = s.GrossEarnings;
            row["deductions"] = s.TotalDeductions;
            row["tax"] = s.TaxAmount;
            row["net"] = s.NetPay;
            row["employerCost"] = s.EmployerContributions;
            return row;
        }).ToList();
    }

    private async Task<List<Row>> LinesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var list = await ClosedSlips(from, to)
            .SelectMany(s => s.Lines, (s, l) => new
            {
                s.EmployeeCode, s.EmployeeName, s.DepartmentName, s.DesignationTitle, s.PeriodStart, s.CurrencyCode,
                l.ComponentName, l.ComponentCode, l.ComponentType, l.IsTaxable, l.Amount,
            })
            .Take(PayrollReportSources.MaxLoad + 1).ToListAsync(ct);
        return list.Select(l =>
        {
            var row = PersonRow(l.EmployeeCode, l.EmployeeName, l.DepartmentName, l.DesignationTitle);
            row["month"] = l.PeriodStart.ToString("yyyy-MM");
            row["component"] = l.ComponentName;
            row["componentCode"] = l.ComponentCode;
            row["componentType"] = PayrollReportSources.Words(l.ComponentType);
            row["taxable"] = l.IsTaxable;
            row["amount"] = l.Amount;
            row["currency"] = l.CurrencyCode;
            return row;
        }).ToList();
    }

    private async Task<List<Row>> SalariesAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var list = await (
            from s in db.EmployeeSalaries.AsNoTracking()
            join e in db.PayrollEmployees.AsNoTracking() on s.EmployeeId equals e.Id
            join t in db.SalaryTemplates.AsNoTracking().IgnoreQueryFilters() on s.SalaryTemplateId equals t.Id
            where s.EffectiveFrom <= today && (s.EffectiveTo == null || s.EffectiveTo >= today)
            select new
            {
                e.EmployeeCode, e.FullName, e.DepartmentName, e.DesignationTitle, e.IsActive, e.JoiningDate,
                Template = t.Name, s.SalaryGradeId, s.CurrencyCode, s.SalaryBasis, s.BasisAmount, s.EffectiveFrom, s.ChangeReason,
            })
            .Take(PayrollReportSources.MaxLoad + 1).ToListAsync(ct);
        var grades = await db.SalaryGrades.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        return list.Select(s =>
        {
            var row = PersonRow(s.EmployeeCode, s.FullName, s.DepartmentName, s.DesignationTitle);
            row["active"] = s.IsActive;
            row["joiningDate"] = s.JoiningDate;
            row["template"] = s.Template;
            row["grade"] = s.SalaryGradeId is { } g ? grades.GetValueOrDefault(g) : null;
            row["currency"] = s.CurrencyCode;
            row["basis"] = PayrollReportSources.Words(s.SalaryBasis);
            row["amount"] = s.BasisAmount;
            row["annual"] = SalaryMath.AnnualEquivalent(s.SalaryBasis, s.BasisAmount);
            row["effectiveFrom"] = s.EffectiveFrom;
            row["changeReason"] = PayrollReportSources.Words(s.ChangeReason);
            return row;
        }).ToList();
    }

    private async Task<List<Row>> LoansAsync(CancellationToken ct)
    {
        var list = await (
            from l in db.EmployeeLoans.AsNoTracking()
            join e in db.PayrollEmployees.AsNoTracking() on l.EmployeeId equals e.Id
            select new
            {
                e.EmployeeCode, e.FullName, e.DepartmentName, e.DesignationTitle,
                l.LoanType, l.Status, l.CurrencyCode, l.PrincipalAmount, l.InstallmentAmount, l.OutstandingAmount, l.StartDate,
            })
            .Take(PayrollReportSources.MaxLoad + 1).ToListAsync(ct);
        return list.Select(l =>
        {
            var row = PersonRow(l.EmployeeCode, l.FullName, l.DepartmentName, l.DesignationTitle);
            row["loanType"] = PayrollReportSources.Words(l.LoanType);
            row["status"] = PayrollReportSources.Words(l.Status);
            row["currency"] = l.CurrencyCode;
            row["principal"] = l.PrincipalAmount;
            row["installment"] = l.InstallmentAmount;
            row["outstanding"] = l.OutstandingAmount;
            row["repaid"] = l.PrincipalAmount - l.OutstandingAmount;
            row["startDate"] = l.StartDate;
            return row;
        }).ToList();
    }
}
