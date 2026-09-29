namespace HR.Payroll.API.Tests.Calculation;

using HR.Payroll.API.Domain.Calculation;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Salaries;
using HR.Payroll.API.Domain.Tax;
using Xunit;

public class PayslipCalculatorTests
{
    static readonly Guid Tenant = Guid.NewGuid();
    static readonly Guid Basic = Guid.NewGuid(), Hra = Guid.NewGuid(), Special = Guid.NewGuid(), IncomeTax = Guid.NewGuid();

    static readonly Dictionary<Guid, ComponentInfo> Components = new()
    {
        [Basic]     = new(Basic, "BASIC", "Basic Salary", ComponentType.Earning, true, true, 1),
        [Hra]       = new(Hra, "HRA", "House Rent Allowance", ComponentType.Earning, true, true, 2),
        [Special]   = new(Special, "SPECIAL", "Special Allowance", ComponentType.Earning, true, true, 3),
        [IncomeTax] = new(IncomeTax, "INCOME_TAX", "Income Tax", ComponentType.Deduction, false, false, 900),
    };

    // "Standard PK": BASIC 60% of gross, HRA 40% of BASIC, SPECIAL = baqi
    static SalaryTemplate StandardPk()
    {
        var template = SalaryTemplate.Create(Tenant, "Standard PK", null);
        template.ReplaceLines(new[]
        {
            (Basic,   CalcType.PercentOfGross,     (decimal?)null, (decimal?)60m, (Guid?)null),
            (Hra,     CalcType.PercentOfComponent, null,           40m,           Basic),
            (Special, CalcType.Remainder,          null,           null,          null),
        });
        return template;
    }

    static TaxRegime PkFy2026()
    {
        var regime = TaxRegime.Create(null, "PK", "PK Salaried FY2026-27", 7, TaxCalcMethod.Annualized, new DateOnly(2026, 7, 1), null);
        regime.AddSlab(0, 600_000, 0, 0);
        regime.AddSlab(600_000, 1_200_000, 0, 5);
        regime.AddSlab(1_200_000, 2_200_000, 30_000, 15);
        regime.AddSlab(2_200_000, null, 180_000, 25);
        return regime;
    }

    static EmployeeSalary Salary(decimal monthly)
        => EmployeeSalary.Create(Tenant, Guid.NewGuid(), Guid.NewGuid(), null, "PKR",
            SalaryBasis.Monthly, monthly, new DateOnly(2026, 10, 1), SalaryChangeReason.Joining, null);

    static PayslipCalculationResult Run(EmployeeSalary salary, decimal payableDays,
        decimal ytdTaxable = 0, decimal ytdTax = 0, int remaining = 9, IReadOnlyList<AdHocLine>? adHoc = null)
    {
        var periodGross = SalaryMath.PeriodGross(salary.SalaryBasis, salary.BasisAmount, PayFrequency.Monthly);
        var rules = SalaryRules.Build(StandardPk().Lines, salary.Overrides);

        return PayslipCalculator.Calculate(new PayslipCalculationInput(
            PeriodDays: 31,
            Segments: new[] { new SalarySegment(periodGross, rules, payableDays) },
            Components: Components,
            AdHocLines: adHoc ?? Array.Empty<AdHocLine>(),
            Tax: new TaxInput(PkFy2026(), ytdTaxable, ytdTax, remaining),
            IncomeTaxComponentId: IncomeTax,
            RoundingDecimals: 2));
    }

    static decimal Line(PayslipCalculationResult r, string code) => r.Lines.Single(l => l.ComponentCode == code).Amount;

    [Fact]
    public void AliRaza_October2026_FullMonth()
    {
        var r = Run(Salary(150_000), payableDays: 31);

        Assert.Equal(90_000m, Line(r, "BASIC"));
        Assert.Equal(36_000m, Line(r, "HRA"));
        Assert.Equal(24_000m, Line(r, "SPECIAL"));
        Assert.Equal(150_000m, r.Total(ComponentType.Earning));
        Assert.Equal(5_833.33m, Line(r, "INCOME_TAX"));
        Assert.Equal(144_166.67m, r.NetPay);
    }

    [Fact]
    public void MidMonthJoiner_GrossIsExact_AndTaxProjectsFullSalary()
    {
        var r = Run(Salary(150_000), payableDays: 16);

        Assert.Equal(77_419.35m, r.Total(ComponentType.Earning));   // 150,000 × 16/31, paisa-paisa sahi
        Assert.Equal(12_387.09m, Line(r, "SPECIAL"));               // rounding ka farq yahan
        Assert.Equal(4_623.66m, Line(r, "INCOME_TAX"));
        Assert.Equal(72_795.69m, r.NetPay);
    }

    [Fact]
    public void AliRaza_January2027_HraOverride_RemainderAdjusts()
    {
        var salary = Salary(210_000);
        salary.SetOverride(Hra, CalcType.Fixed, 50_000m, null, null);

        var r = Run(salary, payableDays: 31, remaining: 6);

        Assert.Equal(126_000m, Line(r, "BASIC"));
        Assert.Equal(50_000m, Line(r, "HRA"));
        Assert.Equal(34_000m, Line(r, "SPECIAL"));
        Assert.Equal(LineSource.Override, r.Lines.Single(l => l.ComponentCode == "HRA").Source);
    }

    [Fact]
    public void ExcludedComponent_GoesToRemainder()
    {
        var salary = Salary(150_000);
        salary.ExcludeComponent(Hra);

        var r = Run(salary, payableDays: 31);

        Assert.DoesNotContain(r.Lines, l => l.ComponentCode == "HRA");
        Assert.Equal(60_000m, Line(r, "SPECIAL"));
    }

    [Fact]
    public void ComponentsExceedingGross_Throw()
    {
        var salary = Salary(150_000);
        salary.SetOverride(Hra, CalcType.Fixed, 200_000m, null, null);

        Assert.Throws<DomainException>(() => Run(salary, payableDays: 31));
    }

    [Fact]
    public void TaxAlreadyPaid_NoTaxLine()
    {
        var r = Run(Salary(150_000), payableDays: 31, ytdTaxable: 450_000, ytdTax: 60_000, remaining: 6);

        Assert.DoesNotContain(r.Lines, l => l.ComponentCode == "INCOME_TAX");
        Assert.Equal(150_000m, r.NetPay);
    }

    [Fact]
    public void Remainder_CannotBeSetPerEmployee()
    {
        Assert.Throws<DomainException>(() => Salary(150_000).SetOverride(Hra, CalcType.Remainder, null, null, null));
    }

    [Fact]
    public void Template_AllowsOnlyOneRemainder()
    {
        var template = SalaryTemplate.Create(Tenant, "Bad", null);
        Assert.Throws<DomainException>(() => template.ReplaceLines(new[]
        {
            (Basic,   CalcType.Remainder, (decimal?)null, (decimal?)null, (Guid?)null),
            (Special, CalcType.Remainder, null,           null,           null),
        }));
    }
}
