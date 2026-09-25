namespace HR.Employee.API.Domain.Organization;

using HR.Employee.API.Domain.Common;

public sealed class Designation : AuditableEntity
{
    public string Title { get; private set; } = default!;
    public byte? Level { get; private set; }          // 1 = entry ... 20 = C-level (org chart / payroll grades)
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Designation() { }

    public static Designation Create(Guid tenantId, string title, byte? level, string? description)
    {
        var designation = new Designation { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        designation.Update(title, level, description);
        return designation;
    }

    public void Update(string title, byte? level, string? description)
    {
        if (level is 0 or > 20)
            throw new DomainException("Level must be between 1 and 20.");

        Title = Guard.Required(title, "Title", 150);
        Level = level;
        Description = Guard.Optional(description, "Description", 500);
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
