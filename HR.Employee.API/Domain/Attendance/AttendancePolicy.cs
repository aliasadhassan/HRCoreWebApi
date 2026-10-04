namespace HR.Employee.API.Domain.Attendance;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Leaves;

/// <summary>
/// Location-wise attendance rules (Attendance setup → Policies tab). LocationId null = tenant default.
/// Ek location ki sirf ek active policy. Overtime rules bhi yahin hain — alag page nahi chahiye.
/// </summary>
public sealed class AttendancePolicy : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public Guid? LocationId { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Din ka status: kam se kam itne minutes = full day / half day (is se kam = absent)
    public short FullDayMinutes { get; private set; } = 360;
    public short HalfDayMinutes { get; private set; } = 240;
    public byte? LatesPerHalfDay { get; private set; }                // e.g. 3 late = 1 half day cut; null = koi cut nahi

    // Clock-in
    public ClockInMethods AllowedMethods { get; private set; } = ClockInMethods.Web | ClockInMethods.Mobile | ClockInMethods.Biometric;
    public bool RequireGeofence { get; private set; }
    public decimal? GeoLatitude { get; private set; }
    public decimal? GeoLongitude { get; private set; }
    public short? GeoRadiusMeters { get; private set; }

    // Requests (correction / WFH / on duty / overtime)
    public ApproverType RequestApprover { get; private set; } = ApproverType.LineManager;
    public byte CorrectionWindowDays { get; private set; } = 7;      // kitne din purani attendance theek karwa sakte hain
    public byte? MaxCorrectionsPerMonth { get; private set; }

    // Overtime
    public bool OvertimeEnabled { get; private set; }
    public short OvertimeMinMinutes { get; private set; } = 30;      // is se kam extra time overtime nahi
    public short? OvertimeMaxMinutesPerDay { get; private set; }
    public bool OvertimeRequiresApproval { get; private set; } = true;
    public decimal OvertimeRateWorkday { get; private set; } = 1.5m;
    public decimal OvertimeRateWeeklyOff { get; private set; } = 2m;
    public decimal OvertimeRateHoliday { get; private set; } = 2m;

    private AttendancePolicy() { }

    public static AttendancePolicy Create(Guid tenantId, string name, Guid? locationId)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Name = Guard.Required(name, "Name", 150),
            LocationId = locationId
        };

    public void Rename(string name) => Name = Guard.Required(name, "Name", 150);
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void SetDayRules(short fullDayMinutes, short halfDayMinutes, byte? latesPerHalfDay)
    {
        if (fullDayMinutes is <= 0 or > 1440)
            throw new DomainException("Full day minutes must be between 1 and 1440.");
        if (halfDayMinutes <= 0 || halfDayMinutes >= fullDayMinutes)
            throw new DomainException("Half day minutes must be less than full day minutes.");
        if (latesPerHalfDay == 0)
            throw new DomainException("Lates per half day must be at least 1.");

        FullDayMinutes = fullDayMinutes;
        HalfDayMinutes = halfDayMinutes;
        LatesPerHalfDay = latesPerHalfDay;
    }

    public void SetClockIn(ClockInMethods allowedMethods, bool requireGeofence, decimal? latitude, decimal? longitude, short? radiusMeters)
    {
        if (allowedMethods == 0)
            throw new DomainException("At least one clock-in method is required.");
        if (requireGeofence && (latitude is null || longitude is null || radiusMeters is null or <= 0))
            throw new DomainException("Geofence needs a latitude, longitude and radius.");
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new DomainException("Coordinates are not valid.");

        AllowedMethods = allowedMethods;
        RequireGeofence = requireGeofence;
        GeoLatitude = latitude;
        GeoLongitude = longitude;
        GeoRadiusMeters = radiusMeters;
    }

    public void SetRequestRules(ApproverType approver, byte correctionWindowDays, byte? maxCorrectionsPerMonth)
    {
        if (correctionWindowDays is 0 or > 90)
            throw new DomainException("Correction window must be between 1 and 90 days.");

        RequestApprover = approver;
        CorrectionWindowDays = correctionWindowDays;
        MaxCorrectionsPerMonth = maxCorrectionsPerMonth;
    }

    public void SetOvertime(
        bool enabled, short minMinutes, short? maxMinutesPerDay, bool requiresApproval,
        decimal rateWorkday, decimal rateWeeklyOff, decimal rateHoliday)
    {
        if (minMinutes < 0)
            throw new DomainException("Minimum overtime cannot be negative.");
        if (maxMinutesPerDay is <= 0)
            throw new DomainException("Maximum overtime per day must be greater than zero.");
        if (rateWorkday < 1 || rateWeeklyOff < 1 || rateHoliday < 1 || rateWorkday > 5 || rateWeeklyOff > 5 || rateHoliday > 5)
            throw new DomainException("Overtime rates must be between 1x and 5x.");

        OvertimeEnabled = enabled;
        OvertimeMinMinutes = minMinutes;
        OvertimeMaxMinutesPerDay = maxMinutesPerDay;
        OvertimeRequiresApproval = requiresApproval;
        OvertimeRateWorkday = rateWorkday;
        OvertimeRateWeeklyOff = rateWeeklyOff;
        OvertimeRateHoliday = rateHoliday;
    }

    /// <summary>Policy mein methods allowed hain ya nahi (Web/Mobile/Biometric).</summary>
    public bool Allows(PunchSource source) => source switch
    {
        PunchSource.Web => AllowedMethods.HasFlag(ClockInMethods.Web),
        PunchSource.Mobile => AllowedMethods.HasFlag(ClockInMethods.Mobile),
        PunchSource.Biometric => AllowedMethods.HasFlag(ClockInMethods.Biometric),
        _ => true
    };

    /// <summary>Haversine — office ke radius ke andar? Geofence band ho to hamesha true.</summary>
    public bool IsInsideGeofence(decimal? latitude, decimal? longitude)
    {
        if (!RequireGeofence)
            return true;
        if (latitude is null || longitude is null)
            return false;

        const double earthRadiusMeters = 6_371_000;
        static double Rad(double deg) => deg * Math.PI / 180;

        var dLat = Rad((double)(latitude.Value - GeoLatitude!.Value));
        var dLon = Rad((double)(longitude.Value - GeoLongitude!.Value));
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad((double)GeoLatitude.Value)) * Math.Cos(Rad((double)latitude.Value)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var distance = 2 * earthRadiusMeters * Math.Asin(Math.Sqrt(a));
        return distance <= GeoRadiusMeters!.Value;
    }

    public decimal OvertimeRateFor(DayType dayType) => dayType switch
    {
        DayType.WeeklyOff => OvertimeRateWeeklyOff,
        DayType.Holiday => OvertimeRateHoliday,
        _ => OvertimeRateWorkday
    };
}

/// <summary>Biometric machine (Attendance setup → Devices tab). Punches isi serial se import hote hain.</summary>
public sealed class AttendanceDevice : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string SerialNumber { get; private set; } = default!;
    public string? Vendor { get; private set; }                 // ZKTeco, Hikvision ...
    public Guid LocationId { get; private set; }
    public string? ApiKeyHash { get; private set; }             // device/agent push ke liye; asal key kabhi store nahi
    public DateTime? LastSyncedAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    private AttendanceDevice() { }

    public static AttendanceDevice Create(Guid tenantId, string name, string serialNumber, string? vendor, Guid locationId)
    {
        var device = new AttendanceDevice { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        device.Update(name, serialNumber, vendor, locationId);
        return device;
    }

    public void Update(string name, string serialNumber, string? vendor, Guid locationId)
    {
        Name = Guard.Required(name, "Name", 100);
        SerialNumber = Guard.Required(serialNumber, "Serial number", 50).ToUpperInvariant();
        Vendor = Guard.Optional(vendor, "Vendor", 50);
        LocationId = Guard.NotEmpty(locationId, "Location");
    }

    public void SetApiKeyHash(string? hash) => ApiKeyHash = Guard.Optional(hash, "API key", 128);
    public void MarkSynced(DateTime at) => LastSyncedAt = at;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
