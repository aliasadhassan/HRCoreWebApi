namespace HR.Employee.API.Domain.Attendance;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Shift definition (Attendance setup → Shifts tab). EndTime &lt;= StartTime = raat ki shift (agle din khatam).
/// Flexible shift: start/end sirf reference, sirf poore ghante (net minutes) dekhe jate hain.
/// </summary>
public sealed class Shift : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Color { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public short BreakMinutes { get; private set; }          // unpaid break, net hours se minus
    public byte GraceInMinutes { get; private set; }         // itni der tak late nahi
    public byte GraceOutMinutes { get; private set; }        // itna pehle jana early-leave nahi
    public bool IsFlexible { get; private set; }
    public bool IsActive { get; private set; } = true;

    public bool CrossesMidnight => EndTime <= StartTime;

    /// <summary>Break ke baad kaam ke minutes (full day ki requirement).</summary>
    public int NetMinutes
    {
        get
        {
            var span = (int)(EndTime - StartTime).TotalMinutes;   // TimeOnly minus midnight wrap khud handle karta hai
            return span - BreakMinutes;
        }
    }

    private Shift() { }

    public static Shift Create(
        Guid tenantId, string name, string code, string? color, TimeOnly startTime, TimeOnly endTime,
        short breakMinutes, byte graceInMinutes, byte graceOutMinutes, bool isFlexible)
    {
        var shift = new Shift { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        shift.Update(name, code, color, startTime, endTime, breakMinutes, graceInMinutes, graceOutMinutes, isFlexible);
        return shift;
    }

    public void Update(
        string name, string code, string? color, TimeOnly startTime, TimeOnly endTime,
        short breakMinutes, byte graceInMinutes, byte graceOutMinutes, bool isFlexible)
    {
        if (startTime == endTime)
            throw new DomainException("Shift start and end time cannot be the same.");
        if (breakMinutes < 0)
            throw new DomainException("Break cannot be negative.");

        Name = Guard.Required(name, "Name", 100);
        Code = Guard.Required(code, "Code", 20).ToUpperInvariant();
        Color = Guard.Optional(color, "Color", 7);
        StartTime = startTime;
        EndTime = endTime;
        BreakMinutes = breakMinutes;
        GraceInMinutes = graceInMinutes;
        GraceOutMinutes = graceOutMinutes;
        IsFlexible = isFlexible;

        if (NetMinutes <= 0)
            throw new DomainException("Break is longer than the shift.");
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
