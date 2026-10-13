namespace HR.Employee.API.Domain.Safety;

using HR.Employee.API.Domain.Common;

/// <summary>Inspection checklist template (e.g. fire safety walk). Items order mein.</summary>
public sealed class SafetyChecklist : AuditableEntity
{
    public const int MaxItems = 60;

    private readonly List<ChecklistItem> items = new();

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<ChecklistItem> Items => items.AsReadOnly();

    private SafetyChecklist() { }

    public static SafetyChecklist Create(Guid tenantId, string name, string? description, IReadOnlyList<string> items)
    {
        var c = new SafetyChecklist { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        c.Update(name, description, true, items);
        return c;
    }

    /// <summary>Items poori list se badal jati hain (purani inspections apni copy rakhti hain).</summary>
    public void Update(string name, string? description, bool isActive, IReadOnlyList<string> newItems)
    {
        var texts = newItems.Select(t => Guard.Required(t, "Checklist item", 300)).ToList();
        if (texts.Count == 0)
            throw new DomainException("Add at least one checklist item.");
        if (texts.Count > MaxItems)
            throw new DomainException($"A checklist can have at most {MaxItems} items.");
        if (texts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != texts.Count)
            throw new DomainException("Checklist items must be different from each other.");
        Name = Guard.Required(name, "Name", 120);
        Description = Guard.Optional(description, "Description", 500);
        IsActive = isActive;
        items.Clear();
        for (var i = 0; i < texts.Count; i++)
            items.Add(new ChecklistItem(Id, (short)(i + 1), texts[i]));
    }

    public static IReadOnlyList<(string Name, string Description, string[] Items)> Samples { get; } =
    [
        ("Workplace safety walk", "Monthly walk-through of offices and work areas.",
        [
            "Walkways and exits are clear of obstructions",
            "Floors are dry, even and free of trip hazards",
            "Lighting works in all areas, including stairs",
            "Electrical cables and sockets are undamaged and not overloaded",
            "Heavy items are stored low and shelves are secure",
            "Chairs and desks are in good condition",
            "Hazardous substances are labelled and stored safely",
            "Safety signs and notices are visible and up to date",
            "Staff are using the required protective equipment",
            "Kitchen and washrooms are clean and hygienic"
        ]),
        ("Fire safety", "Quarterly check of fire equipment and escape routes.",
        [
            "Fire extinguishers are in place, charged and in date",
            "Fire alarm call points are unobstructed",
            "Fire alarm was tested this week",
            "Emergency exit doors open easily and are not locked",
            "Emergency lighting works",
            "Evacuation plan and assembly point are displayed",
            "Fire wardens are named and trained",
            "No combustible waste is stored near heat sources"
        ]),
        ("First aid", "Monthly check of first aid kits and arrangements.",
        [
            "First aid kits are stocked and nothing is out of date",
            "First aiders are named and their certificates are current",
            "First aid notices show who to contact",
            "Eye wash station is clean and working",
            "Accident book or incident reporting is available",
            "Defibrillator (if fitted) is charged and checked"
        ])
    ];
}

public sealed class ChecklistItem : TenantChildEntity
{
    public Guid ChecklistId { get; private set; }
    public short SortOrder { get; private set; }
    public string Text { get; private set; } = default!;

    private ChecklistItem() { }

    internal ChecklistItem(Guid checklistId, short sortOrder, string text)
    {
        ChecklistId = checklistId;
        SortOrder = sortOrder;
        Text = text;
    }
}

/// <summary>
/// Scheduled inspection. Checklist ki lines copy ho jati hain (template badle to purani inspection na badle).
/// RepeatDays = complete hote hi agli inspection itne din baad khud schedule (handler banata hai).
/// </summary>
public sealed class SafetyInspection : AuditableEntity
{
    public const string CodePrefix = "HI-";

    private readonly List<InspectionLine> lines = new();

    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public Guid? ChecklistId { get; private set; }
    public Guid? LocationId { get; private set; }
    public string? Area { get; private set; }
    public Guid InspectorEmployeeId { get; private set; }
    public DateOnly DueOn { get; private set; }
    public short? RepeatDays { get; private set; }
    public InspectionStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public decimal? ScorePercent { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyCollection<InspectionLine> Lines => lines.AsReadOnly();

    public bool IsOverdue(DateOnly today) => Status == InspectionStatus.Scheduled && DueOn < today;

    private SafetyInspection() { }

    public static SafetyInspection Schedule(
        Guid tenantId, string code, string title, SafetyChecklist checklist, InspectionPlan plan, DateOnly today, bool allowPastDue = false)
    {
        if (!checklist.IsActive)
            throw new DomainException("This checklist is switched off.");
        var i = new SafetyInspection
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Code = Guard.Required(code, "Code", 20).ToUpperInvariant(),
            ChecklistId = checklist.Id,
            Status = InspectionStatus.Scheduled
        };
        i.Title = Guard.Required(title, "Title", 200);
        i.Plan(plan, today, allowPastDue);
        foreach (var item in checklist.Items.OrderBy(x => x.SortOrder))
            i.lines.Add(new InspectionLine(i.Id, item.SortOrder, item.Text));
        return i;
    }

    public void Reschedule(string title, InspectionPlan plan, DateOnly today)
    {
        EnsureScheduled();
        Title = Guard.Required(title, "Title", 200);
        Plan(plan, today, allowPastDue: false);
    }

    private void Plan(InspectionPlan p, DateOnly today, bool allowPastDue)
    {
        if (!allowPastDue && p.DueOn < today)
            throw new DomainException("The due date cannot be in the past.");
        if (p.RepeatDays is < 7 or > 365)
            throw new DomainException("Repeat every 7 to 365 days.");
        InspectorEmployeeId = Guard.NotEmpty(p.InspectorEmployeeId, "Inspector");
        LocationId = p.LocationId == Guid.Empty ? null : p.LocationId;
        Area = Guard.Optional(p.Area, "Area", 150);
        DueOn = p.DueOn;
        RepeatDays = p.RepeatDays;
    }

    /// <summary>Har line ka natija. Score = pass / (pass + fail). Fail line par note zaroori.</summary>
    public void Complete(IReadOnlyDictionary<Guid, (InspectionResult Result, string? Note)> results, string? notes, DateTime now)
    {
        EnsureScheduled();
        foreach (var line in lines)
        {
            if (!results.TryGetValue(line.Id, out var r))
                throw new DomainException($"Answer every item. Missing: \"{line.Text}\".");
            if (!Enum.IsDefined(r.Result))
                throw new DomainException("Select pass, fail or not applicable.");
            var note = Guard.Optional(r.Note, "Item note", 500);
            if (r.Result == InspectionResult.Fail && note is null)
                throw new DomainException($"Say what is wrong for: \"{line.Text}\".");
            line.Record(r.Result, note);
        }
        if (results.Keys.Any(k => lines.All(l => l.Id != k)))
            throw new DomainException("One of the answers is for an item not on this inspection.");
        var pass = lines.Count(l => l.Result == InspectionResult.Pass);
        var fail = lines.Count(l => l.Result == InspectionResult.Fail);
        ScorePercent = pass + fail == 0 ? null : Math.Round(100m * pass / (pass + fail), 1);
        Notes = Guard.Optional(notes, "Notes", 2000);
        Status = InspectionStatus.Completed;
        CompletedAt = now;
    }

    public void Cancel(string? reason)
    {
        EnsureScheduled();
        Status = InspectionStatus.Cancelled;
        Notes = Guard.Optional(reason, "Reason", 2000);
    }

    private void EnsureScheduled()
    {
        if (Status != InspectionStatus.Scheduled)
            throw new DomainException("This inspection is already completed or cancelled.");
    }
}

public sealed record InspectionPlan(Guid InspectorEmployeeId, Guid? LocationId, string? Area, DateOnly DueOn, short? RepeatDays);

public sealed class InspectionLine : TenantChildEntity
{
    public Guid InspectionId { get; private set; }
    public short SortOrder { get; private set; }
    public string Text { get; private set; } = default!;
    public InspectionResult? Result { get; private set; }
    public string? Note { get; private set; }

    private InspectionLine() { }

    internal InspectionLine(Guid inspectionId, short sortOrder, string text)
    {
        InspectionId = inspectionId;
        SortOrder = sortOrder;
        Text = text;
    }

    internal void Record(InspectionResult result, string? note)
    {
        Result = result;
        Note = note;
    }
}
