namespace HR.Payroll.API.Domain.Tax;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Domain service: "Annualized" method (PK jaisa).
///   Saal ki expected income = (ab tak ki income + opening) + is period ki income + (poori period salary × baqi periods)
///   Saal ka tax − ab tak kata hua tax = bache hue periods mein barabar baanto
/// Mid-month joiner ki is period ki income kam hoti hai, lekin agle mahine poori salary aayegi —
/// is liye projection "fullPeriodTaxable" se hota hai, prorated amount se nahi.
/// Increment ya bonus aaye to agle periods khud adjust ho jate hain.
/// </summary>
public static class AnnualizedTax
{
    public static decimal ForPeriod(
        TaxRegime regime,
        decimal ytdTaxableIncome,        // is tax saal ki pichhli payslips + opening balance
        decimal ytdTaxPaid,
        decimal currentPeriodTaxable,    // is period ki asal taxable income (proration ke baad)
        decimal fullPeriodTaxable,       // poori period ki taxable salary (bina proration, bina one-time inputs)
        int remainingPeriodsIncludingCurrent,
        int decimals = 2)
    {
        if (regime.CalcMethod == TaxCalcMethod.None || currentPeriodTaxable <= 0)
            return 0m;
        if (remainingPeriodsIncludingCurrent < 1)
            throw new DomainException("Remaining periods must be at least one.");

        if (regime.CalcMethod == TaxCalcMethod.PerPeriodFlat)
            return regime.CalculateAnnualTax(currentPeriodTaxable, decimals);

        var projectedAnnual = ytdTaxableIncome
                            + currentPeriodTaxable
                            + fullPeriodTaxable * (remainingPeriodsIncludingCurrent - 1);

        var annualTax = regime.CalculateAnnualTax(projectedAnnual, decimals);
        var remainingTax = Math.Max(0m, annualTax - ytdTaxPaid);

        return Money.Round(remainingTax / remainingPeriodsIncludingCurrent, decimals);
    }
}
