namespace HR.Payroll.API.Application.Reports;

using FluentValidation;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Pay reports: saal bhar ka payroll (mahine, department, component), loans, aur ek mahine ka register.
// Sirf final runs (Approved / Paid) — Calculated abhi badal sakta hai. Period ka mahina PeriodStart se.
// payroll.view.all ya payroll.approve — Dashboard wala hi rule.

public sealed record PayTotalsDto(int Runs, int Payslips, decimal Gross, decimal Deductions, decimal Tax, decimal EmployerCost, decimal Net);

public sealed record PayMonthRowDto(int Month, int Runs, int Payslips, decimal Gross, decimal Deductions, decimal Tax, decimal EmployerCost, decimal Net);

public sealed record PayDepartmentRowDto(string Name, int Employees, decimal Gross, decimal EmployerCost, decimal Net, decimal TotalCost);

public sealed record PayComponentRowDto(string Code, string Name, ComponentType Type, int Employees, decimal Amount);

public sealed record LoanBookDto(int ActiveLoans, decimal Outstanding, int ActiveAdvances, decimal AdvancesOutstanding);

public sealed record PayReportDto(
    int Year, string? CurrencyCode, PayTotalsDto Totals, IReadOnlyList<PayMonthRowDto> Months,
    IReadOnlyList<PayDepartmentRowDto> Departments, IReadOnlyList<PayComponentRowDto> Components, LoanBookDto Loans);

public sealed record RegisterRowDto(
    Guid PayslipId, string PayslipNumber, string EmployeeCode, string EmployeeName, string? Department, string? Designation,
    RunType RunType, PayslipStatus Status, decimal PayableDays, decimal Gross, decimal Deductions, decimal Tax, decimal Net, decimal EmployerCost);

public sealed record PayRegisterDto(int Year, int Month, string? CurrencyCode, PayTotalsDto Totals, IReadOnlyList<RegisterRowDto> Rows);

public sealed record GetPayReportQuery(int Year) : IRequest<PayReportDto>;

public sealed record GetPayRegisterQuery(int Year, int Month) : IRequest<PayRegisterDto>;

public sealed class GetPayReportValidator : AbstractValidator<GetPayReportQuery>
{
    public GetPayReportValidator() => RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
}

public sealed class GetPayRegisterValidator : AbstractValidator<GetPayRegisterQuery>
{
    public GetPayRegisterValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class PayReportHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetPayReportQuery, PayReportDto>,
    IRequestHandler<GetPayRegisterQuery, PayRegisterDto>
{
    public async Task<PayReportDto> Handle(GetPayReportQuery q, CancellationToken ct)
    {
        EnsureCanView();
        var from = new DateOnly(q.Year, 1, 1);
        var to = new DateOnly(q.Year, 12, 31);
        var slips = FinalSlips(from, to);

        var byMonth = await slips
            .GroupBy(x => x.PeriodStart.Month)
            .Select(g => new PayMonthRowDto(g.Key, g.Select(x => x.Slip.PayrollRunId).Distinct().Count(), g.Count(),
                g.Sum(x => x.Slip.GrossEarnings), g.Sum(x => x.Slip.TotalDeductions), g.Sum(x => x.Slip.TaxAmount),
                g.Sum(x => x.Slip.EmployerContributions), g.Sum(x => x.Slip.NetPay)))
            .ToListAsync(ct);
        var months = Enumerable.Range(1, 12)
            .Select(m => byMonth.FirstOrDefault(r => r.Month == m) ?? new PayMonthRowDto(m, 0, 0, 0, 0, 0, 0, 0))
            .ToList();

        var totals = new PayTotalsDto(
            await slips.Select(x => x.Slip.PayrollRunId).Distinct().CountAsync(ct), months.Sum(m => m.Payslips),
            months.Sum(m => m.Gross), months.Sum(m => m.Deductions), months.Sum(m => m.Tax), months.Sum(m => m.EmployerCost), months.Sum(m => m.Net));

        var departments = (await slips
                .GroupBy(x => x.Slip.DepartmentName)
                .Select(g => new
                {
                    Name = g.Key, Employees = g.Select(x => x.Slip.EmployeeId).Distinct().Count(),
                    Gross = g.Sum(x => x.Slip.GrossEarnings), Employer = g.Sum(x => x.Slip.EmployerContributions), Net = g.Sum(x => x.Slip.NetPay)
                })
                .ToListAsync(ct))
            .Select(d => new PayDepartmentRowDto(d.Name ?? "", d.Employees, d.Gross, d.Employer, d.Net, d.Gross + d.Employer))
            .OrderByDescending(d => d.TotalCost)
            .ToList();

        var components = (await slips
                .SelectMany(x => x.Slip.Lines, (x, l) => new { x.Slip.EmployeeId, l.ComponentCode, l.ComponentName, l.ComponentType, l.Amount })
                .GroupBy(l => new { l.ComponentCode, l.ComponentType })
                .Select(g => new
                {
                    g.Key.ComponentCode, g.Key.ComponentType, Name = g.Max(l => l.ComponentName),
                    Employees = g.Select(l => l.EmployeeId).Distinct().Count(), Amount = g.Sum(l => l.Amount)
                })
                .ToListAsync(ct))
            .Select(c => new PayComponentRowDto(c.ComponentCode, c.Name, c.ComponentType, c.Employees, c.Amount))
            .OrderBy(c => c.Type).ThenByDescending(c => c.Amount)
            .ToList();

        var loans = await db.EmployeeLoans.AsNoTracking()
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Paused)
            .GroupBy(l => l.LoanType)
            .Select(g => new { Type = g.Key, Count = g.Count(), Outstanding = g.Sum(l => l.OutstandingAmount) })
            .ToListAsync(ct);
        var loan = loans.FirstOrDefault(l => l.Type == LoanType.Loan);
        var advance = loans.FirstOrDefault(l => l.Type == LoanType.SalaryAdvance);

        return new PayReportDto(q.Year, await CurrencyAsync(from, to, ct), totals, months, departments, components,
            new LoanBookDto(loan?.Count ?? 0, loan?.Outstanding ?? 0, advance?.Count ?? 0, advance?.Outstanding ?? 0));
    }

    public async Task<PayRegisterDto> Handle(GetPayRegisterQuery q, CancellationToken ct)
    {
        EnsureCanView();
        var from = new DateOnly(q.Year, q.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var rows = await FinalSlips(from, to)
            .OrderBy(x => x.Slip.DepartmentName).ThenBy(x => x.Slip.EmployeeName).ThenBy(x => x.Slip.PayslipNumber)
            .Select(x => new RegisterRowDto(x.Slip.Id, x.Slip.PayslipNumber, x.Slip.EmployeeCode, x.Slip.EmployeeName,
                x.Slip.DepartmentName, x.Slip.DesignationTitle, x.RunType, x.Slip.Status, x.Slip.PayableDays,
                x.Slip.GrossEarnings, x.Slip.TotalDeductions, x.Slip.TaxAmount, x.Slip.NetPay, x.Slip.EmployerContributions))
            .ToListAsync(ct);

        var runs = await FinalSlips(from, to).Select(x => x.Slip.PayrollRunId).Distinct().CountAsync(ct);
        var totals = new PayTotalsDto(runs, rows.Count, rows.Sum(r => r.Gross), rows.Sum(r => r.Deductions), rows.Sum(r => r.Tax),
            rows.Sum(r => r.EmployerCost), rows.Sum(r => r.Net));
        return new PayRegisterDto(q.Year, q.Month, await CurrencyAsync(from, to, ct), totals, rows);
    }

    private void EnsureCanView()
    {
        if (!currentUser.HasPermission(Permissions.PayrollViewAll) && !currentUser.HasPermission(Permissions.PayrollApprove))
            throw new UnauthorizedAccessException("You do not have permission to see company payroll.");
    }

    /// <summary>Approved/Paid runs ki payslips jin ka period [start, end] mein shuru hua.</summary>
    private IQueryable<SlipRow> FinalSlips(DateOnly start, DateOnly end)
        => from s in db.Payslips.AsNoTracking()
           join r in db.PayrollRuns.AsNoTracking() on s.PayrollRunId equals r.Id
           join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
           where (r.Status == RunStatus.Approved || r.Status == RunStatus.Paid) && p.PeriodStart >= start && p.PeriodStart <= end
           select new SlipRow { Slip = s, RunType = r.RunType, CurrencyCode = r.CurrencyCode, PeriodStart = p.PeriodStart };

    private async Task<string?> CurrencyAsync(DateOnly from, DateOnly to, CancellationToken ct)
        => await FinalSlips(from, to).GroupBy(x => x.CurrencyCode).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefaultAsync(ct)
           ?? await db.PayrollSettings.AsNoTracking().Select(x => x.BaseCurrency).FirstOrDefaultAsync(ct);

    // Member-init (constructor nahi) taake EF is projection ke upar GroupBy/SelectMany compose kar sake
    private sealed class SlipRow
    {
        public Domain.Runs.Payslip Slip { get; init; } = default!;
        public RunType RunType { get; init; }
        public string CurrencyCode { get; init; } = default!;
        public DateOnly PeriodStart { get; init; }
    }
}
