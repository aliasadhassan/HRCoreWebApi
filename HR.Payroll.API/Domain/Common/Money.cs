namespace HR.Payroll.API.Domain.Common;

/// <summary>Payroll mein rounding hamesha ek hi tareeqe se — warna totals paisa-paisa ghalat.</summary>
public static class Money
{
    public static decimal Round(decimal value, int decimals = 2)
        => Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    public static decimal Percent(decimal baseAmount, decimal percent, int decimals = 2)
        => Round(baseAmount * percent / 100m, decimals);
}
