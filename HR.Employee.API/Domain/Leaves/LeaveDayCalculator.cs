namespace HR.Employee.API.Domain.Leaves;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Organization;

/// <summary>Domain service: location ke work week + holidays nikaal kar leave ke din.</summary>
public static class LeaveDayCalculator
{
    private const int MaxRangeDays = 366;

    public static decimal CountWorkingDays(DateOnly start, DateOnly end, bool isHalfDay, Location location, IReadOnlySet<DateOnly> holidays)
    {
        if (end < start)
            throw new DomainException("End date cannot be before the start date.");
        if (end.DayNumber - start.DayNumber > MaxRangeDays)
            throw new DomainException("A single leave request cannot span more than a year.");

        if (isHalfDay)
            return IsWorking(start, location, holidays) ? 0.5m : 0m;

        decimal days = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (IsWorking(date, location, holidays))
                days++;
        }
        return days;
    }

    private static bool IsWorking(DateOnly date, Location location, IReadOnlySet<DateOnly> holidays)
        => location.IsWorkingDay(date) && !holidays.Contains(date);
}
