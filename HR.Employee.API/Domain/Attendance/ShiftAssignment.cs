namespace HR.Employee.API.Domain.Attendance;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Employee ki default shift ek date range ke liye (Attendance → Roster tab).
/// EffectiveTo null = abhi tak chal rahi. Ek employee ki ranges overlap na hon — application check karti hai.
/// WeeklyOffDays null = location ka work week (Location.WorkWeekDays) hi chalega.
/// </summary>
public sealed class ShiftAssignment : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid ShiftId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public byte? WeeklyOffDays { get; private set; }          // bitmask Mon=1 .. Sun=64 (OFF din)

    private ShiftAssignment() { }

    public static ShiftAssignment Create(
        Guid tenantId, Guid employeeId, Guid shiftId, DateOnly effectiveFrom, DateOnly? effectiveTo, byte? weeklyOffDays)
    {
        var assignment = new ShiftAssignment
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee")
        };
        assignment.Update(shiftId, effectiveFrom, effectiveTo, weeklyOffDays);
        return assignment;
    }

    public void Update(Guid shiftId, DateOnly effectiveFrom, DateOnly? effectiveTo, byte? weeklyOffDays)
    {
        if (effectiveTo < effectiveFrom)
            throw new DomainException("End date cannot be before the start date.");
        if (weeklyOffDays > 127)
            throw new DomainException("Weekly off days are not valid.");

        ShiftId = Guard.NotEmpty(shiftId, "Shift");
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        WeeklyOffDays = weeklyOffDays;
    }

    /// <summary>Nayi assignment shuru hone par purani ko ek din pehle band karo.</summary>
    public void EndOn(DateOnly lastDay)
    {
        if (lastDay < EffectiveFrom)
            throw new DomainException("End date cannot be before the start date.");
        EffectiveTo = lastDay;
    }

    public bool Covers(DateOnly date) => date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);
}

/// <summary>
/// Kisi ek din ka override (shift swap, extra off, special duty). ShiftId null = us din off.
/// Roster calendar: pehle RosterEntry, warna ShiftAssignment.
/// </summary>
public sealed class RosterEntry : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public Guid? ShiftId { get; private set; }
    public string? Note { get; private set; }

    public bool IsOff => ShiftId is null;

    private RosterEntry() { }

    public static RosterEntry Create(Guid tenantId, Guid employeeId, DateOnly workDate, Guid? shiftId, string? note)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            WorkDate = workDate,
            ShiftId = shiftId,
            Note = Guard.Optional(note, "Note", 200)
        };

    public void Change(Guid? shiftId, string? note)
    {
        ShiftId = shiftId;
        Note = Guard.Optional(note, "Note", 200);
    }
}
