namespace HR.Employee.API.Domain.Attendance;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Aggregate root: ek employee ka ek din (Attendance → Timesheet tab ki row).
/// Shift/schedule SNAPSHOT hai — baad mein shift badle to purani attendance nahi badalti.
/// Totals (worked/late/early/overtime) application ka calculator punches + policy se set karta hai.
/// </summary>
public sealed class AttendanceDay : AuditableEntity
{
    private readonly List<AttendancePunch> _punches = new();

    public Guid EmployeeId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public DayType DayType { get; private set; }
    public Guid? ShiftId { get; private set; }
    public DateTime? ScheduledStart { get; private set; }     // UTC
    public DateTime? ScheduledEnd { get; private set; }       // UTC (raat ki shift mein agla din)

    public DateTime? FirstIn { get; private set; }
    public DateTime? LastOut { get; private set; }
    public short WorkedMinutes { get; private set; }
    public short LateMinutes { get; private set; }
    public short EarlyLeaveMinutes { get; private set; }
    public short OvertimeMinutes { get; private set; }         // calculated; payroll sirf approved wala leta hai
    public AttendanceStatus Status { get; private set; }

    public Guid? LeaveRequestId { get; private set; }          // OnLeave / HalfDay leave
    public bool IsManuallyEdited { get; private set; }
    public string? Remarks { get; private set; }

    public IReadOnlyCollection<AttendancePunch> Punches => _punches.AsReadOnly();

    private AttendanceDay() { }

    public static AttendanceDay Open(
        Guid tenantId, Guid employeeId, DateOnly workDate, DayType dayType,
        Guid? shiftId, DateTime? scheduledStart, DateTime? scheduledEnd)
    {
        if (scheduledStart is not null && scheduledEnd is not null && scheduledEnd <= scheduledStart)
            throw new DomainException("Scheduled end must be after the scheduled start.");

        return new AttendanceDay
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            WorkDate = workDate,
            DayType = dayType,
            ShiftId = shiftId,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            Status = dayType switch
            {
                DayType.Holiday => AttendanceStatus.Holiday,
                DayType.WeeklyOff => AttendanceStatus.WeeklyOff,
                _ => AttendanceStatus.Absent
            }
        };
    }

    /// <summary>Roster/holiday badla ho to schedule dobara set (manual edit wale din nahi chhede jate).</summary>
    public void Reschedule(DayType dayType, Guid? shiftId, DateTime? scheduledStart, DateTime? scheduledEnd)
    {
        if (scheduledStart is not null && scheduledEnd is not null && scheduledEnd <= scheduledStart)
            throw new DomainException("Scheduled end must be after the scheduled start.");

        DayType = dayType;
        ShiftId = shiftId;
        ScheduledStart = scheduledStart;
        ScheduledEnd = scheduledEnd;
    }

    public void Apply(AttendanceTotals totals)
    {
        if (IsManuallyEdited)
            return;   // HR ne haath se theek kiya — calculator overwrite na kare
        ApplyTotals(totals.WorkedMinutes, totals.LateMinutes, totals.EarlyLeaveMinutes, totals.OvertimeMinutes, totals.Status);
    }

    public AttendancePunch AddPunch(
        DateTime punchedAt, PunchDirection direction, PunchSource source,
        Guid? deviceId, decimal? latitude, decimal? longitude, string? ipAddress, string? note)
    {
        // Biometric dobara same record bheje to duplicate nahi
        var existing = _punches.FirstOrDefault(p => p.PunchedAt == punchedAt);
        if (existing is not null)
            return existing;

        var punch = new AttendancePunch(punchedAt, direction, source, deviceId, latitude, longitude, ipAddress, note);
        _punches.Add(punch);

        RefreshInOut();
        return punch;
    }

    /// <summary>Galat punch (ghalti se double tap, dusre ka card) — delete nahi, ignore.</summary>
    public void IgnorePunch(Guid punchId, string reason)
    {
        var punch = _punches.FirstOrDefault(p => p.Id == punchId)
                    ?? throw new DomainException("Punch not found.");
        punch.Ignore(reason);
        RefreshInOut();
    }

    /// <summary>Pehla punch = in, aakhri = out (sirf ek punch ho to out abhi nahi).</summary>
    private void RefreshInOut()
    {
        var times = _punches.Where(p => !p.IsIgnored).Select(p => p.PunchedAt).Order().ToList();
        FirstIn = times.Count > 0 ? times[0] : null;
        LastOut = times.Count > 1 ? times[^1] : null;
    }

    /// <summary>Calculator ka nateeja. Manual edit (HR) ho to IsManuallyEdited = true.</summary>
    public void ApplyTotals(
        short workedMinutes, short lateMinutes, short earlyLeaveMinutes, short overtimeMinutes,
        AttendanceStatus status, bool manual = false, string? remarks = null)
    {
        if (workedMinutes < 0 || lateMinutes < 0 || earlyLeaveMinutes < 0 || overtimeMinutes < 0)
            throw new DomainException("Attendance totals cannot be negative.");

        WorkedMinutes = workedMinutes;
        LateMinutes = lateMinutes;
        EarlyLeaveMinutes = earlyLeaveMinutes;
        OvertimeMinutes = overtimeMinutes;
        Status = status;
        if (manual)
            IsManuallyEdited = true;
        if (remarks is not null)
            Remarks = Guard.Optional(remarks, "Remarks", 500);
    }

    /// <summary>Leave approve hui (ya cancel → null).</summary>
    public void LinkLeave(Guid? leaveRequestId, AttendanceStatus status)
    {
        LeaveRequestId = leaveRequestId;
        Status = status;
    }
}

/// <summary>Raw clock event. Kabhi delete nahi — galat punch IsIgnored hota hai (audit trail).</summary>
public sealed class AttendancePunch : Entity
{
    public Guid AttendanceDayId { get; private set; }
    public DateTime PunchedAt { get; private set; }            // UTC
    public PunchDirection Direction { get; private set; }
    public PunchSource Source { get; private set; }
    public Guid? DeviceId { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string? IpAddress { get; private set; }
    public bool IsIgnored { get; private set; }
    public string? Note { get; private set; }
    public DateTime RecordedAt { get; private set; }

    private AttendancePunch() { }

    internal AttendancePunch(
        DateTime punchedAt, PunchDirection direction, PunchSource source,
        Guid? deviceId, decimal? latitude, decimal? longitude, string? ipAddress, string? note)
    {
        Id = Guid.NewGuid();   // save se pehle bhi IgnorePunch(id) chal sake (EF naye child ko phir bhi Added hi maanta hai)
        PunchedAt = punchedAt;
        Direction = direction;
        Source = source;
        DeviceId = deviceId;
        Latitude = latitude;
        Longitude = longitude;
        IpAddress = Guard.Optional(ipAddress, "IP address", 45);
        Note = Guard.Optional(note, "Note", 200);
        RecordedAt = DateTime.UtcNow;
    }

    internal void Ignore(string reason)
    {
        IsIgnored = true;
        Note = Guard.Required(reason, "Reason", 200);
    }
}
