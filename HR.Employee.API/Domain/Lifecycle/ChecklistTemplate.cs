namespace HR.Employee.API.Domain.Lifecycle;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Onboarding ya exit checklist ka template. Case shuru hote waqt is ke tasks copy hote hain
/// (baad mein template badle to chalte cases par asar nahi). Har kind ka ek default template.
/// </summary>
public sealed class ChecklistTemplate : AuditableEntity
{
    public const int MaxTasks = 60;

    private readonly List<ChecklistTemplateTask> _tasks = new();

    public LifecycleKind Kind { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<ChecklistTemplateTask> Tasks => _tasks.AsReadOnly();

    private ChecklistTemplate() { }

    public static ChecklistTemplate Create(Guid tenantId, LifecycleKind kind, string name, string? description)
    {
        if (!Enum.IsDefined(kind))
            throw new DomainException("Select onboarding or exit.");

        var template = new ChecklistTemplate { TenantId = Guard.NotEmpty(tenantId, "Tenant"), Kind = kind };
        template.Update(name, description);
        return template;
    }

    public void Update(string name, string? description)
    {
        Name = Guard.Required(name, "Name", 150);
        Description = Guard.Optional(description, "Description", 500);
    }

    public void SetDefault(bool isDefault)
    {
        if (isDefault && !IsActive)
            throw new DomainException("An inactive template cannot be the default.");
        IsDefault = isDefault;
    }

    public void Activate() => IsActive = true;

    public void Deactivate()
    {
        IsActive = false;
        IsDefault = false;
    }

    /// <summary>Poori task list replace (form ek saath save hota hai). Order list ke order se.</summary>
    public void ReplaceTasks(IEnumerable<TemplateTaskData> tasks)
    {
        var list = tasks.ToList();
        if (list.Count > MaxTasks)
            throw new DomainException($"A checklist can have at most {MaxTasks} tasks.");

        _tasks.Clear();
        short order = 0;
        foreach (var t in list)
            _tasks.Add(new ChecklistTemplateTask(t, ++order));
    }
}

public sealed record TemplateTaskData(string Title, string? Description, TaskOwner Owner, short DueOffsetDays, bool IsRequired);

/// <summary>DueOffsetDays: onboarding mein joining date se, exit mein last working day se (minus = pehle).</summary>
public sealed class ChecklistTemplateTask : TenantChildEntity
{
    public const short MaxOffset = 365;

    public Guid ChecklistTemplateId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public TaskOwner Owner { get; private set; }
    public short DueOffsetDays { get; private set; }
    public bool IsRequired { get; private set; }
    public short SortOrder { get; private set; }

    private ChecklistTemplateTask() { }

    internal ChecklistTemplateTask(TemplateTaskData data, short sortOrder)
    {
        if (!Enum.IsDefined(data.Owner))
            throw new DomainException("Select who owns each task.");
        if (data.DueOffsetDays is < -MaxOffset or > MaxOffset)
            throw new DomainException($"Due offset must be between -{MaxOffset} and {MaxOffset} days.");

        Title = Guard.Required(data.Title, "Task title", 200);
        Description = Guard.Optional(data.Description, "Task description", 1000);
        Owner = data.Owner;
        DueOffsetDays = data.DueOffsetDays;
        IsRequired = data.IsRequired;
        SortOrder = sortOrder;
    }
}
