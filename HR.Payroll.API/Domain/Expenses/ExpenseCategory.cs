namespace HR.Payroll.API.Domain.Expenses;

using HR.Payroll.API.Domain.Common;

/// <summary>Kharche ki qism (Travel, Meals, Fuel...). Har tenant apni list; pehli baar defaults bante hain.</summary>
public sealed class ExpenseCategory : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal? MaxPerClaim { get; private set; }     // ek claim mein is qism ki kul had (null = koi had nahi)
    public bool ReceiptRequired { get; private set; }
    public bool IsActive { get; private set; } = true;
    public short SortOrder { get; private set; }

    private ExpenseCategory() { }

    public static ExpenseCategory Create(
        Guid tenantId, string code, string name, string? description, decimal? maxPerClaim, bool receiptRequired, short sortOrder)
    {
        var category = new ExpenseCategory { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        category.Update(code, name, description, maxPerClaim, receiptRequired, true, sortOrder);
        return category;
    }

    public void Update(string code, string name, string? description, decimal? maxPerClaim, bool receiptRequired, bool isActive, short sortOrder)
    {
        if (maxPerClaim is <= 0)
            throw new DomainException("Limit per claim must be greater than zero.");

        Code = Guard.Required(code, "Code", 20).ToUpperInvariant();
        Name = Guard.Required(name, "Name", 100);
        Description = Guard.Optional(description, "Description", 300);
        MaxPerClaim = maxPerClaim;
        ReceiptRequired = receiptRequired;
        IsActive = isActive;
        SortOrder = sortOrder;
    }

    public static readonly (string Code, string Name, bool Receipt)[] Defaults =
    [
        ("TRAVEL", "Travel and transport", true),
        ("LODGING", "Hotel and lodging", true),
        ("MEALS", "Meals", false),
        ("FUEL", "Fuel and mileage", false),
        ("PHONE", "Phone and internet", true),
        ("SUPPLIES", "Office supplies", true),
        ("OTHER", "Other", true)
    ];
}
