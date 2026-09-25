namespace HR.Employee.API.Domain.Organization;

using HR.Employee.API.Domain.Common;

/// <summary>Office/branch. Country, timezone aur work week yahin se aate hain.</summary>
public sealed class Location : AuditableEntity
{
    public const byte MondayToFriday = 31;   // bitmask: Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64

    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string CountryCode { get; private set; } = default!;
    public string? City { get; private set; }
    public string? AddressLine { get; private set; }
    public string TimeZone { get; private set; } = default!;
    public byte WorkWeekDays { get; private set; }
    public bool IsHeadOffice { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Location() { }

    public static Location Create(
        Guid tenantId, string name, string code, string countryCode, string timeZone,
        string? city, string? addressLine, byte workWeekDays, bool isHeadOffice)
    {
        var location = new Location { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        location.Update(name, code, countryCode, timeZone, city, addressLine, workWeekDays, isHeadOffice);
        return location;
    }

    public void Update(
        string name, string code, string countryCode, string timeZone,
        string? city, string? addressLine, byte workWeekDays, bool isHeadOffice)
    {
        if (workWeekDays is 0 or > 127)
            throw new DomainException("Work week must include at least one valid day.");

        Name = Guard.Required(name, "Name", 150);
        Code = Guard.Required(code, "Code", 20).ToUpperInvariant();
        CountryCode = Guard.CountryCode(countryCode);
        TimeZone = Guard.Required(timeZone, "Time zone", 64);
        City = Guard.Optional(city, "City", 100);
        AddressLine = Guard.Optional(addressLine, "Address", 300);
        WorkWeekDays = workWeekDays;
        IsHeadOffice = isHeadOffice;
    }

    public void SetHeadOffice(bool value) => IsHeadOffice = value;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    /// <summary>Leave ke din ginne ke liye: kya ye din is location pe working day hai?</summary>
    public bool IsWorkingDay(DateOnly date)
    {
        var bit = date.DayOfWeek == DayOfWeek.Sunday ? 64 : 1 << ((int)date.DayOfWeek - 1);
        return (WorkWeekDays & bit) != 0;
    }
}
