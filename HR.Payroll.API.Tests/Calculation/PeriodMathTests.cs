namespace HR.Payroll.API.Tests.Calculation;

using HR.Payroll.API.Domain.Calculation;
using HR.Payroll.API.Domain.Common;
using Xunit;

public class PeriodMathTests
{
    static readonly DateOnly OctStart = new(2026, 10, 1), OctEnd = new(2026, 10, 31);
    static readonly (DateOnly, decimal)[] NoLeave = Array.Empty<(DateOnly, decimal)>();

    [Fact]
    public void TaxYear_October_Has9MonthsLeft()
        => Assert.Equal(9, TaxYearPeriods.RemainingIncludingCurrent(PayFrequency.Monthly, new DateOnly(2026, 7, 1), OctStart));

    [Fact]
    public void TaxYear_June_IsLastMonth()
        => Assert.Equal(1, TaxYearPeriods.RemainingIncludingCurrent(PayFrequency.Monthly, new DateOnly(2026, 7, 1), new DateOnly(2027, 6, 1)));

    [Fact]
    public void FullMonth_CalendarDays()
        => Assert.Equal(31m, ProrationDays.PayableDays(ProrationMethod.CalendarDays, OctStart, OctEnd, OctStart, OctEnd, NoLeave));

    [Fact]
    public void JoinerOn16th_Gets16Days()
        => Assert.Equal(16m, ProrationDays.PayableDays(ProrationMethod.CalendarDays, OctStart, OctEnd, new DateOnly(2026, 10, 16), OctEnd, NoLeave));

    [Fact]
    public void UnpaidLeave_IsDeducted_HalfDaysToo()
        => Assert.Equal(29.5m, ProrationDays.PayableDays(ProrationMethod.CalendarDays, OctStart, OctEnd, OctStart, OctEnd,
            new[] { (new DateOnly(2026, 10, 5), 1m), (new DateOnly(2026, 10, 6), 0.5m) }));

    [Fact]
    public void Fixed30_FullMonthOf31Days_Is30()
        => Assert.Equal(30m, ProrationDays.PayableDays(ProrationMethod.Fixed30, OctStart, OctEnd, OctStart, OctEnd, NoLeave));

    [Fact]
    public void WorkingDays_IgnoresWeekendLeave()
    {
        // Oct 2026: 22 weekdays. 3 Oct = Saturday, 5 Oct = Monday
        var leave = new[] { (new DateOnly(2026, 10, 3), 1m), (new DateOnly(2026, 10, 5), 1m) };
        Assert.Equal(22m, ProrationDays.PeriodDays(ProrationMethod.WorkingDays, OctStart, OctEnd));
        Assert.Equal(21m, ProrationDays.PayableDays(ProrationMethod.WorkingDays, OctStart, OctEnd, OctStart, OctEnd, leave));
    }
}
