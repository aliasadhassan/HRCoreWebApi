namespace HR.Payroll.API.Domain.Calculation;

using HR.Payroll.API.Domain.Common;

/// <summary>Proration ke din: CalendarDays, WorkingDays (Mon–Fri) ya Fixed30.</summary>
public static class ProrationDays
{
    public static decimal PeriodDays(ProrationMethod method, DateOnly periodStart, DateOnly periodEnd) => method switch
    {
        ProrationMethod.CalendarDays => CalendarDays(periodStart, periodEnd),
        ProrationMethod.WorkingDays => WorkingDays(periodStart, periodEnd),
        ProrationMethod.Fixed30 => 30m,
        _ => throw new DomainException("Unknown proration method.")
    };

    /// <summary>
    /// Segment (from–to) ke payable din, unpaid leave minus.
    /// Poora period cover ho to hamesha PeriodDays — Fixed30 mein 31 din ka mahina bhi poora 30.
    /// </summary>
    public static decimal PayableDays(
        ProrationMethod method, DateOnly periodStart, DateOnly periodEnd, DateOnly from, DateOnly to,
        IEnumerable<(DateOnly Date, decimal Fraction)> unpaidLeave)
    {
        if (to < from)
            return 0m;

        var periodDays = PeriodDays(method, periodStart, periodEnd);
        var days = from == periodStart && to == periodEnd
            ? periodDays
            : method switch
            {
                ProrationMethod.WorkingDays => WorkingDays(from, to),
                _ => Math.Min(CalendarDays(from, to), periodDays)
            };

        var unpaid = unpaidLeave
            .Where(u => u.Date >= from && u.Date <= to)
            .Where(u => method != ProrationMethod.WorkingDays || IsWeekday(u.Date))
            .Sum(u => u.Fraction);

        return Math.Max(0m, days - unpaid);
    }

    private static decimal CalendarDays(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber + 1;

    private static decimal WorkingDays(DateOnly from, DateOnly to)
    {
        var count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
            if (IsWeekday(d)) count++;
        return count;
    }

    private static bool IsWeekday(DateOnly d) => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}

/// <summary>Tax saal mein is period samet kitne periods baqi hain (annualized tax ke liye).</summary>
public static class TaxYearPeriods
{
    public static int RemainingIncludingCurrent(PayFrequency frequency, DateOnly taxYearStart, DateOnly periodStart)
    {
        if (periodStart < taxYearStart)
            throw new DomainException("Period starts before the tax year.");

        var months = (periodStart.Year - taxYearStart.Year) * 12 + periodStart.Month - taxYearStart.Month;
        var days = periodStart.DayNumber - taxYearStart.DayNumber;

        var elapsed = frequency switch
        {
            PayFrequency.Monthly => months,
            PayFrequency.SemiMonthly => months * 2 + (periodStart.Day > 15 ? 1 : 0),
            PayFrequency.BiWeekly => days / 14,
            PayFrequency.Weekly => days / 7,
            _ => throw new DomainException("Unknown pay frequency.")
        };

        return Math.Max(1, frequency.PeriodsPerYear() - elapsed);
    }
}
