namespace HR.Payroll.API.Domain.Calculation;

using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Runs;
using HR.Payroll.API.Domain.Salaries;
using HR.Payroll.API.Domain.Setup;
using HR.Payroll.API.Domain.Tax;

/// <summary>Engine ko component ki sirf itni info chahiye. PayComponent entity se banta hai.</summary>
public sealed record ComponentInfo(
    Guid Id, string Code, string Name, ComponentType Type, bool IsTaxable, bool IsProrated, short SortOrder)
{
    public static ComponentInfo From(PayComponent c)
        => new(c.Id, c.Code, c.Name, c.ComponentType, c.IsTaxable, c.IsProrated, c.SortOrder);
}

/// <summary>Ek component ka final formula, aur ye template se aaya ya override se.</summary>
public sealed record SalaryRule(Guid ComponentId, ComponentFormula Formula, LineSource Source);

public static class SalaryRules
{
    /// <summary>Template ki recipe + employee ke overrides = is employee ki final recipe.</summary>
    public static IReadOnlyList<SalaryRule> Build(
        IEnumerable<SalaryTemplateLine> templateLines, IEnumerable<EmployeeSalaryComponent> overrides)
    {
        var rules = templateLines.ToDictionary(
            l => l.PayComponentId, l => new SalaryRule(l.PayComponentId, l.Formula, LineSource.Template));

        foreach (var o in overrides)
        {
            if (o.Formula is { } formula)
                rules[o.PayComponentId] = new SalaryRule(o.PayComponentId, formula, LineSource.Override);
            else
                rules.Remove(o.PayComponentId);   // excluded
        }

        return rules.Values.ToList();
    }
}

/// <summary>
/// Period ka woh hissa jisme ek hi salary row active thi. Mid-month increment = 2 segments.
/// PeriodGross = SalaryMath.PeriodGross(...) — poori period ki gross, bina proration.
/// </summary>
public sealed record SalarySegment(decimal PeriodGross, IReadOnlyList<SalaryRule> Rules, decimal PayableDays);

/// <summary>One-time lines: PayrollInputs (bonus, OT, manual deduction) aur loan installments.</summary>
public sealed record AdHocLine(
    Guid ComponentId, decimal Amount, LineSource Source,
    Guid? SourceReference = null, decimal? Quantity = null, decimal? Rate = null);

public sealed record TaxInput(
    TaxRegime Regime, decimal YtdTaxableIncome, decimal YtdTaxPaid, int RemainingPeriodsIncludingCurrent);

public sealed record PayslipCalculationInput(
    decimal PeriodDays,
    IReadOnlyList<SalarySegment> Segments,
    IReadOnlyDictionary<Guid, ComponentInfo> Components,
    IReadOnlyList<AdHocLine> AdHocLines,
    TaxInput? Tax,                    // null = tax-exempt employee ya regime nahi mili
    Guid? IncomeTaxComponentId,
    int RoundingDecimals);

/// <summary>Seedha Payslip.ApplyCalculation(...) mein jata hai.</summary>
public sealed record PayslipCalculationResult(
    IReadOnlyList<PayslipLineData> Lines, decimal PeriodDays, decimal PayableDays, decimal TaxableIncome)
{
    public decimal Total(ComponentType type) => Lines.Where(l => l.ComponentType == type).Sum(l => l.Amount);
    public decimal NetPay => Total(ComponentType.Earning) - Total(ComponentType.Deduction);
}

/// <summary>
/// Pure domain service — na DB, na EF. Ek employee, ek period.
/// Order:
///   1. Har segment ki rules dependency order mein (Fixed / % of gross / % of component)
///   2. Remainder component = gross − baqi earnings
///   3. Proration (sirf IsProrated components)
///   4. Rounding; rounding ka paisa-farq remainder line mein, taake gross bilkul sahi rahe
///   5. Ad-hoc lines (inputs, loans)
///   6. Taxable income
///   7. Annualized tax
///   8. Result (net Payslip khud nikalta hai)
/// </summary>
public static class PayslipCalculator
{
    public static PayslipCalculationResult Calculate(PayslipCalculationInput input)
    {
        if (input.PeriodDays <= 0)
            throw new DomainException("Period days must be greater than zero.");

        var payableDays = input.Segments.Sum(s => s.PayableDays);
        if (input.Segments.Any(s => s.PayableDays < 0) || payableDays > input.PeriodDays)
            throw new DomainException("Payable days are invalid for this period.");

        var decimals = input.RoundingDecimals;
        var raw = new Dictionary<Guid, decimal>();
        var sources = new Dictionary<Guid, LineSource>();
        var fullOfLastSegment = new Dictionary<Guid, decimal>();
        Guid? remainderId = null;

        // 1-3: salary structure
        foreach (var segment in input.Segments)
        {
            var full = EvaluateRules(segment, input.Components);
            var factor = segment.PayableDays / input.PeriodDays;

            foreach (var rule in segment.Rules)
            {
                var component = Component(input, rule.ComponentId);
                var fullAmount = full[rule.ComponentId];

                raw[rule.ComponentId] = component.IsProrated
                    ? raw.GetValueOrDefault(rule.ComponentId) + fullAmount * factor
                    : fullAmount;   // non-prorated: aakhri segment ki value
                sources[rule.ComponentId] = rule.Source;

                if (rule.Formula.CalcType == CalcType.Remainder)
                    remainderId = rule.ComponentId;
            }
            fullOfLastSegment = full;
        }

        // 4: rounding
        var rounded = raw.ToDictionary(kv => kv.Key, kv => Money.Round(kv.Value, decimals));
        if (remainderId is { } rid && rounded.ContainsKey(rid))
        {
            var earningIds = raw.Keys.Where(id => Component(input, id).Type == ComponentType.Earning).ToList();
            var exactGross = Money.Round(earningIds.Sum(id => raw[id]), decimals);
            rounded[rid] += exactGross - earningIds.Sum(id => rounded[id]);
        }

        var lines = rounded
            .Where(kv => kv.Value != 0)
            .Select(kv => Line(Component(input, kv.Key), kv.Value, sources[kv.Key]))
            .ToList();

        // 5: inputs aur loans
        foreach (var adHoc in input.AdHocLines.Where(a => a.Amount != 0))
            lines.Add(Line(Component(input, adHoc.ComponentId), Money.Round(adHoc.Amount, decimals),
                adHoc.Source, adHoc.SourceReference, adHoc.Quantity, adHoc.Rate));

        // 6: taxable income
        var taxable = lines.Where(l => l.ComponentType == ComponentType.Earning && l.IsTaxable).Sum(l => l.Amount);

        // 7: tax
        if (input.Tax is { } tax)
        {
            var taxComponentId = input.IncomeTaxComponentId
                ?? throw new DomainException("Income tax component is not configured.");

            var fullPeriodTaxable = fullOfLastSegment
                .Where(kv => Component(input, kv.Key) is { Type: ComponentType.Earning, IsTaxable: true })
                .Sum(kv => kv.Value);

            var periodTax = AnnualizedTax.ForPeriod(
                tax.Regime, tax.YtdTaxableIncome, tax.YtdTaxPaid,
                taxable, fullPeriodTaxable, tax.RemainingPeriodsIncludingCurrent, decimals);

            if (periodTax > 0)
                lines.Add(Line(Component(input, taxComponentId), periodTax, LineSource.Tax));
        }

        // 8
        var ordered = lines.OrderBy(l => l.ComponentType).ThenBy(l => l.SortOrder).ToList();
        return new PayslipCalculationResult(ordered, input.PeriodDays, payableDays, taxable);
    }

    /// <summary>Ek segment ki poori (bina proration) amounts. % of component apne base ka intezar karta hai.</summary>
    private static Dictionary<Guid, decimal> EvaluateRules(
        SalarySegment segment, IReadOnlyDictionary<Guid, ComponentInfo> components)
    {
        var result = new Dictionary<Guid, decimal>();
        var pending = segment.Rules.Where(r => r.Formula.CalcType != CalcType.Remainder).ToList();

        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var rule in pending.ToList())
            {
                var f = rule.Formula;
                decimal? value = f.CalcType switch
                {
                    CalcType.Fixed => f.Amount ?? 0m,
                    CalcType.PercentOfGross => segment.PeriodGross * (f.Percentage ?? 0m) / 100m,
                    CalcType.PercentOfComponent =>
                        f.BaseComponentId is { } baseId && result.TryGetValue(baseId, out var baseValue)
                            ? baseValue * (f.Percentage ?? 0m) / 100m
                            : null,
                    _ => throw new DomainException($"Calculation type '{f.CalcType}' cannot be used in a salary structure.")
                };
                if (value is null) continue;

                result[rule.ComponentId] = value.Value;
                pending.Remove(rule);
                progressed = true;
            }

            if (!progressed)
                throw new DomainException(
                    "These components are a percentage of a component that is missing from the salary: " +
                    string.Join(", ", pending.Select(p => components.TryGetValue(p.ComponentId, out var c) ? c.Code : p.ComponentId.ToString())));
        }

        var remainderRules = segment.Rules.Where(r => r.Formula.CalcType == CalcType.Remainder).ToList();
        if (remainderRules.Count > 1)
            throw new DomainException("Only one component can take the remainder of the gross.");

        if (remainderRules.Count == 1)
        {
            var remainder = remainderRules[0];
            if (components.TryGetValue(remainder.ComponentId, out var rc) && rc.Type != ComponentType.Earning)
                throw new DomainException("The remainder component must be an earning.");

            var otherEarnings = result
                .Where(kv => components.TryGetValue(kv.Key, out var c) && c.Type == ComponentType.Earning)
                .Sum(kv => kv.Value);

            var rest = segment.PeriodGross - otherEarnings;
            if (rest < 0)
                throw new DomainException($"Salary components exceed the gross by {Money.Round(-rest)}.");

            result[remainder.ComponentId] = rest;
        }

        return result;
    }

    private static ComponentInfo Component(PayslipCalculationInput input, Guid id)
        => input.Components.TryGetValue(id, out var c)
            ? c
            : throw new DomainException($"Pay component {id} was not found.");

    private static PayslipLineData Line(
        ComponentInfo c, decimal amount, LineSource source,
        Guid? sourceReference = null, decimal? quantity = null, decimal? rate = null)
        => new(c.Id, c.Code, c.Name, c.Type, amount, quantity, rate,
               c.Type == ComponentType.Earning && c.IsTaxable, source, sourceReference, c.SortOrder);
}
