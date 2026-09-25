namespace HR.Employee.API.Domain.Leaves;

using HR.Employee.API.Domain.Common;

/// <summary>Leave KYA hai (Annual, Sick...). KITNI milti hai woh LeavePolicy batati hai.</summary>
public sealed class LeaveType : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Color { get; private set; }
    public bool IsPaid { get; private set; } = true;
    public bool RequiresAttachment { get; private set; }
    public bool AllowHalfDay { get; private set; } = true;
    public bool AllowNegativeBalance { get; private set; }
    public short SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private LeaveType() { }

    public static LeaveType Create(
        Guid tenantId, string name, string code, string? color, bool isPaid,
        bool requiresAttachment, bool allowHalfDay, bool allowNegativeBalance, short sortOrder)
    {
        var type = new LeaveType { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        type.Update(name, code, color, isPaid, requiresAttachment, allowHalfDay, allowNegativeBalance, sortOrder);
        return type;
    }

    public void Update(
        string name, string code, string? color, bool isPaid,
        bool requiresAttachment, bool allowHalfDay, bool allowNegativeBalance, short sortOrder)
    {
        color = Guard.Optional(color, "Color", 7);
        if (color is not null && (color.Length != 7 || color[0] != '#'))
            throw new DomainException("Color must be a hex value like #4F6F52.");

        Name = Guard.Required(name, "Name", 100);
        Code = Guard.Required(code, "Code", 20).ToUpperInvariant();
        Color = color;
        IsPaid = isPaid;
        RequiresAttachment = requiresAttachment;
        AllowHalfDay = allowHalfDay;
        AllowNegativeBalance = allowNegativeBalance;
        SortOrder = sortOrder;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
