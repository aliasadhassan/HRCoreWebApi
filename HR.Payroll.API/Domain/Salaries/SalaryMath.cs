namespace HR.Payroll.API.Domain.Salaries;

using HR.Payroll.API.Domain.Common;

public static class SalaryMath
{
    /// <summary>
    /// Ek period ki full (bina proration) gross. Annual ÷ periods; Monthly × 12 ÷ periods.
    /// Hourly ka period amount hours pe depend karta hai — engine PayrollInputs se nikalta hai.
    /// </summary>
    public static decimal PeriodGross(SalaryBasis basis, decimal amount, PayFrequency frequency, int decimals = 2)
    {
        var periods = frequency.PeriodsPerYear();
        return basis switch
        {
            SalaryBasis.Annual => Money.Round(amount / periods, decimals),
            SalaryBasis.Monthly => Money.Round(amount * 12m / periods, decimals),
            SalaryBasis.Hourly => throw new DomainException("Hourly salaries are calculated from worked hours."),
            _ => throw new DomainException("Unknown salary basis.")
        };
    }

    public static decimal AnnualEquivalent(SalaryBasis basis, decimal amount) => basis switch
    {
        SalaryBasis.Annual => amount,
        SalaryBasis.Monthly => amount * 12m,
        SalaryBasis.Hourly => throw new DomainException("An hourly rate has no fixed annual equivalent."),
        _ => throw new DomainException("Unknown salary basis.")
    };
}
