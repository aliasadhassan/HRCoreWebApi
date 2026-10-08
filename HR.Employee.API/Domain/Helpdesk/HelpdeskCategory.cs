namespace HR.Employee.API.Domain.Helpdesk;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Request ki qism (Salary certificate, Payroll question, IT access...). HR settings mein banata hai.
/// NeedsManagerApproval = pehle employee ka manager haan kare. Confidential = sirf HR agents + assignee dekhte hain
/// (manager approval nahi ho sakti). ResolutionHours = SLA, ticket Open hone se gina jata hai.
/// </summary>
public sealed class HelpdeskCategory : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public bool NeedsManagerApproval { get; private set; }
    public bool IsConfidential { get; private set; }
    public short? ResolutionHours { get; private set; }
    public Guid? DefaultAssigneeEmployeeId { get; private set; }
    public short SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private HelpdeskCategory() { }

    public static HelpdeskCategory Create(Guid tenantId, CategoryDetails details)
    {
        var category = new HelpdeskCategory { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        category.Update(details);
        return category;
    }

    public void Update(CategoryDetails d)
    {
        Name = Guard.Required(d.Name, "Name", 100);
        Description = Guard.Optional(d.Description, "Description", 300);
        Icon = Guard.Optional(d.Icon, "Icon", 40);
        if (Icon is not null && !Icon.All(ch => char.IsAsciiLetterLower(ch) || char.IsAsciiDigit(ch) || ch == '_'))
            throw new DomainException("Icon must be a Material Symbols name (lowercase letters, digits, underscore).");
        if (d.IsConfidential && d.NeedsManagerApproval)
            throw new DomainException("A confidential category cannot need manager approval.");
        if (d.ResolutionHours is < 1 or > 2000)
            throw new DomainException("Resolution time must be between 1 and 2000 hours.");
        if (d.SortOrder is < 0 or > 999)
            throw new DomainException("Sort order must be between 0 and 999.");
        NeedsManagerApproval = d.NeedsManagerApproval;
        IsConfidential = d.IsConfidential;
        ResolutionHours = d.ResolutionHours;
        DefaultAssigneeEmployeeId = d.DefaultAssigneeEmployeeId == Guid.Empty ? null : d.DefaultAssigneeEmployeeId;
        SortOrder = d.SortOrder;
        IsActive = d.IsActive;
    }
}

public sealed record CategoryDetails(
    string Name, string? Description, string? Icon, bool NeedsManagerApproval, bool IsConfidential,
    short? ResolutionHours, Guid? DefaultAssigneeEmployeeId, short SortOrder, bool IsActive);
