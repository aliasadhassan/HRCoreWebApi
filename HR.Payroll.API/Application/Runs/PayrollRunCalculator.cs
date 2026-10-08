namespace HR.Payroll.API.Application.Runs;

using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Calculation;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Employees;
using HR.Payroll.API.Domain.Runs;
using HR.Payroll.API.Domain.Salaries;
using HR.Payroll.API.Domain.Setup;
using HR.Payroll.API.Domain.Tax;
using Microsoft.EntityFrameworkCore;

public sealed record EmployeeCalculation(PayrollEmployee Employee, PayslipCalculationResult Result, decimal UnpaidLeaveDays);

/// <summary>Issues khali = sab employees calculate ho gaye. Ek bhi issue ho to run Fail hota hai, koi payslip nahi badalti.</summary>
public sealed record RunCalculationOutcome(
    PayPeriod Period, PayrollSettings Settings,
    IReadOnlyList<EmployeeCalculation> Calculations, IReadOnlyList<string> Issues);

/// <summary>
/// Run ka data EK DAFA load karta hai (N+1 queries nahi), phir har employee ke liye PayslipCalculator.
/// Sirf padhta hai — koi entity change nahi karta, is liye fail hone pe kuch "aadha save" nahi hota.
/// </summary>
public sealed class PayrollRunCalculator(IAppDbContext db)
{
    public async Task<RunCalculationOutcome> CalculateAsync(PayrollRun run, CancellationToken ct)
    {
        var period = await db.PayPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == run.PayPeriodId, ct)
                     ?? throw new NotFoundException("Pay period", run.PayPeriodId);
        var group = await db.PayGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == run.PayGroupId, ct)
                    ?? throw new NotFoundException("Pay group", run.PayGroupId);
        var settings = await db.PayrollSettings.AsNoTracking().FirstOrDefaultAsync(ct)
                       ?? throw new ConflictException("Payroll is not initialized for this company.");

        var issues = new List<string>();
        var calculations = new List<EmployeeCalculation>();
        int decimals = settings.RoundingDecimals;

        // ── Employees jo is period mein kisi bhi din kaam pe the ──
        var employees = await db.PayrollEmployees.AsNoTracking()
            .Where(e => e.PayGroupId == group.Id
                        && e.JoiningDate <= period.PeriodEnd
                        && (e.ExitDate == null || e.ExitDate >= period.PeriodStart))
            .OrderBy(e => e.EmployeeCode)
            .ToListAsync(ct);

        if (employees.Count == 0)
            return new RunCalculationOutcome(period, settings, calculations,
                new[] { "No employees are assigned to this pay group for this period." });

        var employeeIds = employees.Select(e => e.Id).ToList();

        // ── Setup data ──
        var componentEntities = await db.PayComponents.AsNoTracking().ToListAsync(ct);
        var components = componentEntities.ToDictionary(c => c.Id, ComponentInfo.From);
        var incomeTaxId = componentEntities.FirstOrDefault(c => c.SystemCode == SystemComponentCodes.IncomeTax)?.Id;

        var salaries = await db.EmployeeSalaries.AsNoTracking().Include(s => s.Overrides)
            .Where(s => employeeIds.Contains(s.EmployeeId)
                        && s.EffectiveFrom <= period.PeriodEnd
                        && (s.EffectiveTo == null || s.EffectiveTo >= period.PeriodStart))
            .ToListAsync(ct);

        var templateIds = salaries.Select(s => s.SalaryTemplateId).Distinct().ToList();
        var templates = await db.SalaryTemplates.AsNoTracking().Include(t => t.Lines)
            .Where(t => templateIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var unpaidLeave = await db.UnpaidLeaveDays.AsNoTracking()
            .Where(u => employeeIds.Contains(u.EmployeeId) && u.LeaveDate >= period.PeriodStart && u.LeaveDate <= period.PeriodEnd)
            .Select(u => new { u.EmployeeId, u.LeaveDate, u.DayFraction })
            .ToListAsync(ct);

        var inputs = await db.PayrollInputs.AsNoTracking()
            .Where(i => i.PayPeriodId == period.Id && employeeIds.Contains(i.EmployeeId))
            .ToListAsync(ct);

        var loans = await db.EmployeeLoans.AsNoTracking()
            .Where(l => employeeIds.Contains(l.EmployeeId) && l.Status == LoanStatus.Active)
            .ToListAsync(ct);

        // ── Benefits: employee ka hissa jahan plan pe deduction component laga ho (period ke aakhri din coverage) ──
        var periodEnd = period.PeriodEnd;
        var benefitDeductions = await (
                from e in db.BenefitEnrolments.AsNoTracking()
                join p in db.BenefitPlans.AsNoTracking() on e.BenefitPlanId equals p.Id
                where employeeIds.Contains(e.EmployeeId)
                      && (e.Status == EnrolmentStatus.Active || e.Status == EnrolmentStatus.Ended)
                      && e.StartDate <= periodEnd && (e.EndDate == null || e.EndDate >= periodEnd)
                      && e.EmployeeMonthlyCost > 0 && p.DeductionComponentId != null
                select new { e.Id, e.EmployeeId, e.EmployeeMonthlyCost, ComponentId = p.DeductionComponentId!.Value })
            .ToListAsync(ct);
        var perPeriod = 12m / group.PayFrequency.PeriodsPerYear();

        // ── Tax: tenant ki apni regime pehle, warna platform wali ──
        var regime = await db.TaxRegimes.AsNoTracking().Include(r => r.Slabs)
            .Where(r => r.CountryCode == group.CountryCode && r.IsActive
                        && r.EffectiveFrom <= period.PeriodEnd
                        && (r.EffectiveTo == null || r.EffectiveTo >= period.PeriodEnd))
            .OrderByDescending(r => r.TenantId != null)
            .ThenByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        var taxYearStart = regime?.TaxYearStart(period.PeriodEnd);
        var ytd = new Dictionary<Guid, (decimal Taxable, decimal Tax)>();
        var openings = new Dictionary<Guid, (decimal Taxable, decimal Tax)>();
        var remainingPeriods = 1;

        if (regime is not null && taxYearStart is { } tys)
        {
            if (incomeTaxId is null)
                return new RunCalculationOutcome(period, settings, calculations,
                    new[] { "The INCOME_TAX system component is missing. Initialize payroll setup again." });

            // Sirf approved/paid runs ki payslips — draft ya cancelled ka tax count nahi hota
            var ytdRows = await db.Payslips.AsNoTracking()
                .Where(p => employeeIds.Contains(p.EmployeeId)
                            && p.PeriodStart >= tys && p.PeriodEnd < period.PeriodStart
                            && db.PayrollRuns.Any(r => r.Id == p.PayrollRunId
                                                       && (r.Status == RunStatus.Approved || r.Status == RunStatus.Paid)))
                .GroupBy(p => p.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Taxable = g.Sum(p => p.TaxableIncome), Tax = g.Sum(p => p.TaxAmount) })
                .ToListAsync(ct);
            ytd = ytdRows.ToDictionary(x => x.EmployeeId, x => (x.Taxable, x.Tax));

            var openingRows = await db.EmployeeTaxOpeningBalances.AsNoTracking()
                .Where(o => employeeIds.Contains(o.EmployeeId) && o.TaxYearStart == tys)
                .ToListAsync(ct);
            openings = openingRows.ToDictionary(o => o.EmployeeId, o => (o.PriorTaxableIncome, o.PriorTaxPaid));

            remainingPeriods = TaxYearPeriods.RemainingIncludingCurrent(group.PayFrequency, tys, period.PeriodStart);
        }

        var periodDays = ProrationDays.PeriodDays(settings.ProrationMethod, period.PeriodStart, period.PeriodEnd);

        // ── Har employee ──
        foreach (var employee in employees)
        {
            try
            {
                var leave = unpaidLeave.Where(u => u.EmployeeId == employee.Id)
                                       .Select(u => (u.LeaveDate, u.DayFraction)).ToList();

                var segments = BuildSegments(employee, salaries, templates, leave, period, group, settings.ProrationMethod,
                                             periodDays, run.CurrencyCode, decimals);
                if (segments.Count == 0)
                {
                    issues.Add($"{employee.EmployeeCode} {employee.FullName}: no salary is assigned for this period.");
                    continue;
                }

                var adHoc = new List<AdHocLine>();
                foreach (var input in inputs.Where(i => i.EmployeeId == employee.Id))
                {
                    if (input.Amount is not { } amount)
                        throw new DomainException("A payroll input has only a quantity; quantity-based pay is not supported yet.");
                    adHoc.Add(new AdHocLine(input.PayComponentId, amount, LineSource.Input, input.Id, input.Quantity));
                }

                foreach (var loan in loans.Where(l => l.EmployeeId == employee.Id))
                {
                    var due = loan.DueFor(period.PeriodEnd);
                    if (due > 0)
                        adHoc.Add(new AdHocLine(loan.DeductionComponentId, due, LineSource.Loan, loan.Id));
                }

                foreach (var benefit in benefitDeductions.Where(b => b.EmployeeId == employee.Id))
                    adHoc.Add(new AdHocLine(benefit.ComponentId, Math.Round(benefit.EmployeeMonthlyCost * perPeriod, decimals),
                                            LineSource.Benefit, benefit.Id));

                TaxInput? tax = null;
                if (regime is not null && !employee.IsTaxExempt)
                {
                    var paid = ytd.GetValueOrDefault(employee.Id);
                    var opening = openings.GetValueOrDefault(employee.Id);
                    tax = new TaxInput(regime, paid.Taxable + opening.Taxable, paid.Tax + opening.Tax, remainingPeriods);
                }

                var result = PayslipCalculator.Calculate(new PayslipCalculationInput(
                    periodDays, segments, components, adHoc, tax, incomeTaxId, decimals));

                calculations.Add(new EmployeeCalculation(employee, result, leave.Sum(l => l.DayFraction)));
            }
            catch (DomainException ex)
            {
                issues.Add($"{employee.EmployeeCode} {employee.FullName}: {ex.Message}");
            }
        }

        return new RunCalculationOutcome(period, settings, calculations, issues);
    }

    /// <summary>
    /// Period ke andar har salary row ka hissa = ek segment. Mid-month increment → 2 segments.
    /// Joining se pehle / exit ke baad ke din kisi segment mein nahi aate, is liye khud hi prorate ho jate hain.
    /// </summary>
    private static List<SalarySegment> BuildSegments(
        PayrollEmployee employee, IReadOnlyList<EmployeeSalary> salaries, IReadOnlyDictionary<Guid, SalaryTemplate> templates,
        IReadOnlyList<(DateOnly Date, decimal Fraction)> leave, PayPeriod period, PayGroup group,
        ProrationMethod method, decimal periodDays, string runCurrency, int decimals)
    {
        var employedFrom = employee.JoiningDate > period.PeriodStart ? employee.JoiningDate : period.PeriodStart;
        var employedTo = employee.ExitDate is { } exit && exit < period.PeriodEnd ? exit : period.PeriodEnd;

        var segments = new List<SalarySegment>();
        var usedDays = 0m;

        foreach (var salary in salaries.Where(s => s.EmployeeId == employee.Id).OrderBy(s => s.EffectiveFrom))
        {
            var from = salary.EffectiveFrom > employedFrom ? salary.EffectiveFrom : employedFrom;
            var to = salary.EffectiveTo is { } end && end < employedTo ? end : employedTo;
            if (to < from)
                continue;

            if (!string.Equals(salary.CurrencyCode, runCurrency, StringComparison.OrdinalIgnoreCase))
                throw new DomainException($"Salary currency {salary.CurrencyCode} does not match the pay group currency {runCurrency}.");

            if (!templates.TryGetValue(salary.SalaryTemplateId, out var template))
                throw new DomainException("The salary template assigned to this employee was not found.");

            var days = ProrationDays.PayableDays(method, period.PeriodStart, period.PeriodEnd, from, to, leave);
            days = Math.Min(days, periodDays - usedDays);   // Fixed30: 31 din ke mahine mein total 30 se upar na jaye
            usedDays += days;

            segments.Add(new SalarySegment(
                SalaryMath.PeriodGross(salary.SalaryBasis, salary.BasisAmount, group.PayFrequency, decimals),
                SalaryRules.Build(template.Lines, salary.Overrides),
                days));
        }

        return segments;
    }
}
