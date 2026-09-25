namespace HR.Employee.API.Domain.Leaves;

using HR.Employee.API.Domain.Common;

/// <summary>LocationId null = tenant ki saari locations pe lagu.</summary>
public sealed class Holiday : AuditableEntity
{
    public Guid? LocationId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsOptional { get; private set; }

    private Holiday() { }

    public static Holiday Create(Guid tenantId, DateOnly date, string name, Guid? locationId, bool isOptional)
    {
        var holiday = new Holiday { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        holiday.Update(date, name, locationId, isOptional);
        return holiday;
    }

    public void Update(DateOnly date, string name, Guid? locationId, bool isOptional)
    {
        Date = date;
        Name = Guard.Required(name, "Name", 150);
        LocationId = locationId;
        IsOptional = isOptional;
    }
}
