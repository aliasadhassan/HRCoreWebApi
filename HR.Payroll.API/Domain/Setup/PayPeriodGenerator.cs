namespace HR.Payroll.API.Domain.Setup;

using HR.Payroll.API.Domain.Common;

/// <summary>Domain service: pay group ke agle periods banata hai (e.g. 3 mahine advance).</summary>
public static class PayPeriodGenerator
{
    public static IReadOnlyList<PayPeriod> GenerateNext(PayGroup group, PayPeriod? lastPeriod, int count)
    {
        if (count is < 1 or > 60)
            throw new DomainException("You can generate between 1 and 60 periods at a time.");

        var periods = new List<PayPeriod>(count);
        var previous = lastPeriod;

        for (var i = 0; i < count; i++)
        {
            var start = previous is null ? group.AnchorDate : previous.PeriodEnd.AddDays(1);
            var end = PeriodEnd(group.PayFrequency, start);
            var number = PeriodNumber(group.PayFrequency, start, previous);

            var period = PayPeriod.Create(group.TenantId, group.Id, start, end, end.AddDays(group.PayDayOffset), number);
            periods.Add(period);
            previous = period;
        }

        return periods;
    }

    private static DateOnly PeriodEnd(PayFrequency frequency, DateOnly start) => frequency switch
    {
        PayFrequency.Monthly => new DateOnly(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month)),
        PayFrequency.SemiMonthly => start.Day <= 15
            ? new DateOnly(start.Year, start.Month, 15)
            : new DateOnly(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month)),
        PayFrequency.BiWeekly => start.AddDays(13),
        PayFrequency.Weekly => start.AddDays(6),
        _ => throw new DomainException("Unknown pay frequency.")
    };

    private static byte PeriodNumber(PayFrequency frequency, DateOnly start, PayPeriod? previous) => frequency switch
    {
        PayFrequency.Monthly => (byte)start.Month,
        PayFrequency.SemiMonthly => (byte)((start.Month - 1) * 2 + (start.Day <= 15 ? 1 : 2)),
        // Weekly/bi-weekly: naye saal mein 1 se dobara
        _ => previous is null || previous.PeriodStart.Year != start.Year ? (byte)1 : (byte)(previous.PeriodNumber + 1)
    };
}
