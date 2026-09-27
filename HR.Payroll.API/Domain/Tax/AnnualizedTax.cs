namespace HR.Payroll.API.Domain.Tax;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Domain service: "Annualized" method (PK jaisa).
///   Saal ki expected income = (ab tak ki income + opening) + (is period ki income × bache hue periods)
///   Saal ka tax − ab tak kata hua tax = bache hue periods mein barabar baanto
/// Increment ya bonus aaye to agle periods khud adjust ho jate hain.
/// </summary>
public static class AnnualizedTax
{
    public static decimal ForPeriod(
        TaxRegime regime,
        decimal ytdTaxableIncome,        // is tax saal ki pichhli payslips + opening balance
        decimal ytdTaxPaid,
        decimal currentPeriodTaxable,
        int remainingPeriodsIncludingCurrent,
        int decimals = 2)
    {
        if (regime.CalcMethod == TaxCalcMethod.None || currentPeriodTaxable <= 0)
            return 0m;
        if (remainingPeriodsIncludingCurrent < 1)
            throw new DomainException("Remaining periods must be at least one.");

        if (regime.CalcMethod == TaxCalcMethod.PerPeriodFlat)
            return regime.CalculateAnnualTax(currentPeriodTaxable, decimals);

        var projectedAnnual = ytdTaxableIncome + currentPeriodTaxable * remainingPeriodsIncludingCurrent;
        var annualTax = regime.CalculateAnnualTax(projectedAnnual, decimals);
        var remainingTax = Math.Max(0m, annualTax - ytdTaxPaid);

        return Money.Round(remainingTax / remainingPeriodsIncludingCurrent, decimals);
    }
}
