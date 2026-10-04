namespace HR.Employee.API.Domain.Attendance;

public sealed record AttendanceTotals(
    short WorkedMinutes, short LateMinutes, short EarlyLeaveMinutes, short OvertimeMinutes, AttendanceStatus Status);

/// <summary>
/// Punches + shift + policy → din ke totals. Pure function (DB nahi), isliye unit test hota hai.
/// Biometric aksar direction nahi bhejta, isliye pehla punch = in, aakhri = out maana jata hai.
/// </summary>
public static class AttendanceCalculator
{
    public static AttendanceTotals Calculate(AttendanceDay day, Shift? shift, AttendancePolicy policy, bool onHalfDayLeave = false)
    {
        var hasIn = day.FirstIn is not null;
        var hasOut = day.LastOut is not null;

        if (!hasIn)
            return new(0, 0, 0, 0, NoPunchStatus(day, onHalfDayLeave));

        if (!hasOut)
            return new(0, Late(day, shift), 0, 0, AttendanceStatus.Incomplete);

        var span = (int)(day.LastOut!.Value - day.FirstIn!.Value).TotalMinutes;
        var breakMinutes = shift?.BreakMinutes ?? 0;
        // Break tabhi minus jab banda break se zyada der ruka ho (2 ghante ka chakkar = break nahi)
        var worked = span > breakMinutes * 2 ? span - breakMinutes : span;

        var late = Late(day, shift);
        var early = Early(day, shift);
        var overtime = Overtime(day.DayType, worked, shift, policy);

        // Full/half day ki hadd policy se (HR tay karta hai); late hona alag gina jata hai (LatesPerHalfDay)
        var status = day.DayType != DayType.Workday
            ? AttendanceStatus.Present                         // off/holiday pe aaya = present (poora overtime)
            : worked >= policy.FullDayMinutes || onHalfDayLeave && worked >= policy.HalfDayMinutes
                ? AttendanceStatus.Present
                : worked >= policy.HalfDayMinutes ? AttendanceStatus.HalfDay : AttendanceStatus.Absent;

        return new(Clamp(worked), late, early, overtime, status);
    }

    private static AttendanceStatus NoPunchStatus(AttendanceDay day, bool onHalfDayLeave) => day.DayType switch
    {
        DayType.Holiday => AttendanceStatus.Holiday,
        DayType.WeeklyOff => AttendanceStatus.WeeklyOff,
        _ when day.LeaveRequestId is not null && !onHalfDayLeave => AttendanceStatus.OnLeave,
        _ => AttendanceStatus.Absent
    };

    private static short Late(AttendanceDay day, Shift? shift)
    {
        if (shift is null || shift.IsFlexible || day.DayType != DayType.Workday || day.ScheduledStart is null || day.FirstIn is null)
            return 0;
        var minutes = (int)(day.FirstIn.Value - day.ScheduledStart.Value).TotalMinutes;
        return minutes > shift.GraceInMinutes ? Clamp(minutes) : (short)0;
    }

    private static short Early(AttendanceDay day, Shift? shift)
    {
        if (shift is null || shift.IsFlexible || day.DayType != DayType.Workday || day.ScheduledEnd is null || day.LastOut is null)
            return 0;
        var minutes = (int)(day.ScheduledEnd.Value - day.LastOut.Value).TotalMinutes;
        return minutes > shift.GraceOutMinutes ? Clamp(minutes) : (short)0;
    }

    private static short Overtime(DayType dayType, int worked, Shift? shift, AttendancePolicy policy)
    {
        if (!policy.OvertimeEnabled)
            return 0;

        var extra = dayType == DayType.Workday ? worked - (shift?.NetMinutes ?? policy.FullDayMinutes) : worked;
        if (extra < policy.OvertimeMinMinutes || extra <= 0)
            return 0;
        if (policy.OvertimeMaxMinutesPerDay is { } max && extra > max)
            extra = max;
        return Clamp(extra);
    }

    private static short Clamp(int minutes) => (short)Math.Clamp(minutes, 0, short.MaxValue);
}
