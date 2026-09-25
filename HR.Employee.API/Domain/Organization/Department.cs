namespace HR.Employee.API.Domain.Organization;

using HR.Employee.API.Domain.Common;

public sealed class Department : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid? ParentDepartmentId { get; private set; }
    public Guid? HeadEmployeeId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Department() { }

    public static Department Create(Guid tenantId, string name, string code, string? description, Guid? parentDepartmentId)
    {
        var department = new Department { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        department.Update(name, code, description, parentDepartmentId);
        return department;
    }

    public void Update(string name, string code, string? description, Guid? parentDepartmentId)
    {
        if (parentDepartmentId is not null && parentDepartmentId == Id && Id != Guid.Empty)
            throw new DomainException("A department cannot be its own parent.");

        Name = Guard.Required(name, "Name", 150);
        Code = Guard.Required(code, "Code", 20).ToUpperInvariant();
        Description = Guard.Optional(description, "Description", 500);
        ParentDepartmentId = parentDepartmentId;
    }

    public void AssignHead(Guid? employeeId) => HeadEmployeeId = employeeId;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
