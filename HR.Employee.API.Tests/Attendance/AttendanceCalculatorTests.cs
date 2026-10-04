namespace HR.Employee.API.Tests.Attendance;

using HR.Employee.API.Application.Attendance;
using HR.Employee.API.Domain.Attendance;
using Xunit;

public class AttendanceCalculatorTests
{
    static readonly Guid Tenant = Guid.NewGuid(), Employee = Guid.NewGuid();
    static readonly TimeZoneInfo Karachi = AttendanceEngine.FindTimeZone("Asia/Karachi");   // UTC+5
    static readonly DateOnly Day = new(2026, 10, 5);

    // 09:00–18:00, 60 min break → 480 net; 15 min grace in, 10 min grace out
    static Shift DayShift() => Shift.Create(Tenant, "General", "GEN", null, new(9, 0), new(18, 0), 60, 15, 10, false);

    static AttendancePolicy Policy(bool overtime = true)
    {
        var policy = AttendancePolicy.Create(Tenant, "Default", null);
        policy.SetDayRules(360, 240, null);   // 6h full day, 4h half day
        policy.SetOvertime(overtime, 30, 240, true, 1.5m, 2m, 2m);
        return policy;
    }

    static AttendanceDay Open(Shift? shift, DayType type = DayType.Workday)
    {
        var (start, end) = shift is null ? (null, null) : AttendanceEngine.ToUtc(Day, shift, Karachi);
        return AttendanceDay.Open(Tenant, Employee, Day, type, null, start, end);
    }

    static DateTime Local(int hour, int minute = 0, int addDays = 0)
        => TimeZoneInfo.ConvertTimeToUtc(Day.AddDays(addDays).ToDateTime(new TimeOnly(hour, minute)), Karachi);

    static void Punch(AttendanceDay day, DateTime at)
        => day.AddPunch(at, PunchDirection.Unknown, PunchSource.Biometric, null, null, null, null, null);

    [Fact]
    public void Full_day_on_time_is_present_without_late_or_overtime()
    {
        var shift = DayShift();
        var day = Open(shift);
        Punch(day, Local(9, 5));
        Punch(day, Local(18, 2));

        var t = AttendanceCalculator.Calculate(day, shift, Policy());

        Assert.Equal(AttendanceStatus.Present, t.Status);
        Assert.Equal(477, t.WorkedMinutes);        // 537 span - 60 break
        Assert.Equal(0, t.LateMinutes);            // 5 min, grace 15
        Assert.Equal(0, t.OvertimeMinutes);
    }

    [Fact]
    public void Late_beyond_grace_counts_full_minutes()
    {
        var shift = DayShift();
        var day = Open(shift);
        Punch(day, Local(9, 40));
        Punch(day, Local(18, 0));

        var t = AttendanceCalculator.Calculate(day, shift, Policy());

        Assert.Equal(40, t.LateMinutes);
        Assert.Equal(440, t.WorkedMinutes);
        Assert.Equal(AttendanceStatus.Present, t.Status);   // late alag ginta hai, din phir bhi full
    }

    [Fact]
    public void Short_day_becomes_half_day_and_very_short_is_absent()
    {
        var shift = DayShift();
        var half = Open(shift);
        Punch(half, Local(9, 0));
        Punch(half, Local(14, 0));                 // 300 - 60 = 240 → half day
        Assert.Equal(AttendanceStatus.HalfDay, AttendanceCalculator.Calculate(half, shift, Policy()).Status);

        var absent = Open(shift);
        Punch(absent, Local(9, 0));
        Punch(absent, Local(10, 30));              // 90 min, break not deducted (span < 2x break)
        var t = AttendanceCalculator.Calculate(absent, shift, Policy());
        Assert.Equal(90, t.WorkedMinutes);
        Assert.Equal(AttendanceStatus.Absent, t.Status);
    }

    [Fact]
    public void Single_punch_is_incomplete()
    {
        var shift = DayShift();
        var day = Open(shift);
        Punch(day, Local(9, 30));

        var t = AttendanceCalculator.Calculate(day, shift, Policy());

        Assert.Equal(AttendanceStatus.Incomplete, t.Status);
        Assert.Equal(30, t.LateMinutes);           // grace 15 se upar → poore 30
    }

    [Fact]
    public void Overtime_needs_minimum_and_is_capped()
    {
        var shift = DayShift();

        var small = Open(shift);
        Punch(small, Local(9, 0));
        Punch(small, Local(18, 20));               // 20 extra < 30 min minimum
        Assert.Equal(0, AttendanceCalculator.Calculate(small, shift, Policy()).OvertimeMinutes);

        var big = Open(shift);
        Punch(big, Local(9, 0));
        Punch(big, Local(23, 30));                 // 270 extra → capped at 240
        Assert.Equal(240, AttendanceCalculator.Calculate(big, shift, Policy()).OvertimeMinutes);

        var disabled = Open(shift);
        Punch(disabled, Local(9, 0));
        Punch(disabled, Local(20, 0));
        Assert.Equal(0, AttendanceCalculator.Calculate(disabled, shift, Policy(overtime: false)).OvertimeMinutes);
    }

    [Fact]
    public void Work_on_weekly_off_is_all_overtime()
    {
        var day = Open(null, DayType.WeeklyOff);
        Punch(day, Local(10, 0));
        Punch(day, Local(13, 0));

        var t = AttendanceCalculator.Calculate(day, null, Policy());

        Assert.Equal(AttendanceStatus.Present, t.Status);
        Assert.Equal(180, t.OvertimeMinutes);
        Assert.Equal(0, t.LateMinutes);
    }

    [Fact]
    public void No_punch_status_follows_day_type()
    {
        Assert.Equal(AttendanceStatus.Absent, AttendanceCalculator.Calculate(Open(DayShift()), DayShift(), Policy()).Status);
        Assert.Equal(AttendanceStatus.Holiday, AttendanceCalculator.Calculate(Open(null, DayType.Holiday), null, Policy()).Status);
        Assert.Equal(AttendanceStatus.WeeklyOff, AttendanceCalculator.Calculate(Open(null, DayType.WeeklyOff), null, Policy()).Status);

        var onLeave = Open(DayShift());
        onLeave.LinkLeave(Guid.NewGuid(), AttendanceStatus.OnLeave);
        Assert.Equal(AttendanceStatus.OnLeave, AttendanceCalculator.Calculate(onLeave, DayShift(), Policy()).Status);
    }

    [Fact]
    public void Night_shift_crosses_midnight_in_utc_schedule()
    {
        var night = Shift.Create(Tenant, "Night", "NGT", null, new(22, 0), new(6, 0), 30, 10, 0, false);
        Assert.True(night.CrossesMidnight);
        Assert.Equal(450, night.NetMinutes);

        var (start, end) = AttendanceEngine.ToUtc(Day, night, Karachi);
        Assert.Equal(new DateTime(2026, 10, 5, 17, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 10, 6, 1, 0, 0, DateTimeKind.Utc), end);

        var day = Open(night);
        Punch(day, Local(22, 20));
        Punch(day, Local(6, 0, addDays: 1));
        var t = AttendanceCalculator.Calculate(day, night, Policy());
        Assert.Equal(20, t.LateMinutes);           // grace se upar ho to poore minutes
        Assert.Equal(430, t.WorkedMinutes);        // 460 span - 30 break
    }

    [Fact]
    public void Duplicate_and_ignored_punches_do_not_change_totals()
    {
        var shift = DayShift();
        var day = Open(shift);
        Punch(day, Local(9, 0));
        Punch(day, Local(9, 0));                   // biometric re-sync
        Punch(day, Local(18, 0));
        Punch(day, Local(23, 0));                  // galti
        Assert.Equal(3, day.Punches.Count);

        day.IgnorePunch(day.Punches.Single(p => p.PunchedAt == Local(23, 0)).Id, "Wrong card");
        Assert.Equal(Local(18, 0), day.LastOut);
    }

    [Fact]
    public void Geofence_accepts_inside_and_rejects_outside()
    {
        var policy = Policy();
        policy.SetClockIn(ClockInMethods.Web, true, 31.520370m, 74.358749m, 200);   // Lahore

        Assert.True(policy.IsInsideGeofence(31.521000m, 74.359000m));                // ~75 m
        Assert.False(policy.IsInsideGeofence(31.540000m, 74.358749m));               // ~2 km
        Assert.False(policy.IsInsideGeofence(null, null));
        Assert.False(policy.Allows(PunchSource.Mobile));
    }

    [Fact]
    public void Day_bitmask_matches_location_work_week()
    {
        Assert.Equal(1, AttendanceEngine.DayBit(new DateOnly(2026, 10, 5)));   // Monday
        Assert.Equal(16, AttendanceEngine.DayBit(new DateOnly(2026, 10, 9)));  // Friday
        Assert.Equal(64, AttendanceEngine.DayBit(new DateOnly(2026, 10, 11))); // Sunday
    }
}
