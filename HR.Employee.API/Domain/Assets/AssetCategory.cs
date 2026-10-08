namespace HR.Employee.API.Domain.Assets;

using HR.Employee.API.Domain.Common;

/// <summary>Asset ki qism (Laptop, Phone, ID card...). Icon = Material Symbols naam, UI ke liye.</summary>
public sealed class AssetCategory : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public bool IsActive { get; private set; } = true;

    private AssetCategory() { }

    public static AssetCategory Create(Guid tenantId, string name, string? description, string? icon)
    {
        var category = new AssetCategory { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        category.Update(name, description, icon, true);
        return category;
    }

    public void Update(string name, string? description, string? icon, bool isActive)
    {
        Name = Guard.Required(name, "Name", 100);
        Description = Guard.Optional(description, "Description", 300);
        Icon = Guard.Optional(icon, "Icon", 40);
        if (Icon is not null && !Icon.All(ch => char.IsAsciiLetterLower(ch) || char.IsAsciiDigit(ch) || ch == '_'))
            throw new DomainException("Icon must be a Material Symbols name (lowercase letters, digits, underscore).");
        IsActive = isActive;
    }
}
