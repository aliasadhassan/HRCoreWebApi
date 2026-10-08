namespace HR.Employee.API.Domain.Assets;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Company ki cheez (laptop, phone, card) aur us ki poori kahani. Ek waqt mein sirf ek khuli assignment;
/// assign/return/status badalne se Status aur AssetEvents (history) khud update hote hain.
/// </summary>
public sealed class Asset : AuditableEntity
{
    private readonly List<AssetAssignment> _assignments = new();
    private readonly List<AssetEvent> _events = new();

    public string AssetTag { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public Guid AssetCategoryId { get; private set; }
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public string? SerialNumber { get; private set; }
    public Guid? LocationId { get; private set; }
    public DateOnly? PurchaseDate { get; private set; }
    public decimal? PurchaseCost { get; private set; }
    public string? Vendor { get; private set; }
    public DateOnly? WarrantyUntil { get; private set; }
    public AssetCondition Condition { get; private set; }
    public AssetStatus Status { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Include mein sirf khuli assignment load karo (filtered include) — poori history ki zaroorat nahi.</summary>
    public IReadOnlyCollection<AssetAssignment> Assignments => _assignments.AsReadOnly();
    public IReadOnlyCollection<AssetEvent> Events => _events.AsReadOnly();

    public AssetAssignment? OpenAssignment => _assignments.FirstOrDefault(a => a.ReturnedOn == null);

    private Asset() { }

    public static Asset Create(Guid tenantId, string assetTag, AssetDetails details, Guid? byUserId, DateTime now)
    {
        var asset = new Asset
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            AssetTag = Tag(assetTag),
            Status = AssetStatus.Available
        };
        asset.Apply(details);
        asset.Log(AssetEventType.Created, null, byUserId, now, null);
        return asset;
    }

    public void Update(string assetTag, AssetDetails details, Guid? byUserId, DateTime now)
    {
        AssetTag = Tag(assetTag);
        Apply(details);
        Log(AssetEventType.Updated, null, byUserId, now, null);
    }

    private void Apply(AssetDetails d)
    {
        if (!Enum.IsDefined(d.Condition))
            throw new DomainException("Select the condition.");
        if (d.PurchaseCost is < 0)
            throw new DomainException("Purchase cost cannot be negative.");
        if (d.WarrantyUntil is { } w && d.PurchaseDate is { } p && w < p)
            throw new DomainException("Warranty end cannot be before the purchase date.");

        Name = Guard.Required(d.Name, "Name", 150);
        AssetCategoryId = Guard.NotEmpty(d.CategoryId, "Category");
        Brand = Guard.Optional(d.Brand, "Brand", 100);
        Model = Guard.Optional(d.Model, "Model", 100);
        SerialNumber = Guard.Optional(d.SerialNumber, "Serial number", 100);
        LocationId = d.LocationId == Guid.Empty ? null : d.LocationId;
        PurchaseDate = d.PurchaseDate;
        PurchaseCost = d.PurchaseCost;
        Vendor = Guard.Optional(d.Vendor, "Vendor", 150);
        WarrantyUntil = d.WarrantyUntil;
        Condition = d.Condition;
        Notes = Guard.Optional(d.Notes, "Notes", 1000);
    }

    public AssetAssignment Assign(Guid employeeId, DateOnly assignedOn, DateOnly? dueBack, string? note, Guid? byUserId, DateTime now)
    {
        if (Status != AssetStatus.Available)
            throw new DomainException(Status == AssetStatus.Assigned
                ? "This asset is already assigned. Record the return first."
                : "Only an available asset can be assigned.");
        if (OpenAssignment is not null)
            throw new DomainException("This asset is already assigned. Record the return first.");
        if (dueBack is { } d && d < assignedOn)
            throw new DomainException("Return date cannot be before the assigned date.");

        var assignment = new AssetAssignment(Id, Guard.NotEmpty(employeeId, "Employee"), assignedOn, dueBack, Condition,
            Guard.Optional(note, "Note", 500), byUserId);
        _assignments.Add(assignment);
        Status = AssetStatus.Assigned;
        Log(AssetEventType.Assigned, employeeId, byUserId, now, assignment.AssignNote);
        return assignment;
    }

    public void ChangeDueBack(DateOnly? dueBack)
    {
        var open = OpenAssignment ?? throw new DomainException("This asset is not assigned.");
        if (dueBack is { } d && d < open.AssignedOn)
            throw new DomainException("Return date cannot be before the assigned date.");
        open.DueBack = dueBack;
    }

    /// <summary>Wapsi: condition aur aage ka status (Available / InRepair / Lost — kho gaya to wapis nahi aaya, phir bhi assignment band).</summary>
    public void Return(DateOnly returnedOn, AssetCondition condition, AssetStatus nextStatus, string? note, Guid? byUserId, DateTime now)
    {
        var open = OpenAssignment ?? throw new DomainException("This asset is not assigned.");
        if (!Enum.IsDefined(condition))
            throw new DomainException("Select the condition.");
        if (nextStatus is not (AssetStatus.Available or AssetStatus.InRepair or AssetStatus.Lost))
            throw new DomainException("After a return the asset is available, in repair or lost.");
        if (returnedOn < open.AssignedOn)
            throw new DomainException("Return date cannot be before the assigned date.");
        if (nextStatus == AssetStatus.Lost && string.IsNullOrWhiteSpace(note))
            throw new DomainException("Add a note when an asset is lost.");

        open.Close(returnedOn, condition, Guard.Optional(note, "Note", 500), byUserId);
        Condition = condition;
        Status = nextStatus;
        Log(AssetEventType.Returned, open.EmployeeId, byUserId, now, open.ReturnNote);
    }

    /// <summary>Assigned ke ilawa status: repair, retire, lost — aur wapis available. Assigned asset pehle return hota hai.</summary>
    public void ChangeStatus(AssetStatus status, string? note, Guid? byUserId, DateTime now)
    {
        if (status == AssetStatus.Assigned || !Enum.IsDefined(status))
            throw new DomainException("Use assign to give an asset to an employee.");
        if (Status == AssetStatus.Assigned)
            throw new DomainException("Record the return before changing the status of an assigned asset.");
        if (status == Status)
            return;
        if (status is AssetStatus.Retired or AssetStatus.Lost && string.IsNullOrWhiteSpace(note))
            throw new DomainException("Add a note when retiring or losing an asset.");

        Status = status;
        Log(AssetEventType.StatusChanged, null, byUserId, now, Guard.Optional(note, "Note", 500));
    }

    public void Acknowledge(Guid assignmentId, Guid employeeId, Guid? byUserId, DateTime now)
    {
        var open = OpenAssignment;
        if (open is null || open.Id != assignmentId)
            throw new DomainException("This assignment is closed.");
        if (open.EmployeeId != employeeId)
            throw new DomainException("This asset is not assigned to you.");
        if (open.AcknowledgedAt is not null)
            return;

        open.AcknowledgedAt = now;
        Log(AssetEventType.Acknowledged, employeeId, byUserId, now, null);
    }

    private void Log(AssetEventType type, Guid? employeeId, Guid? byUserId, DateTime now, string? detail)
        => _events.Add(new AssetEvent(Id, type, Status, employeeId, byUserId, now, detail));

    private static string Tag(string? value)
    {
        var tag = Guard.Required(value, "Asset tag", 40).ToUpperInvariant();
        if (!tag.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '/' or '.'))
            throw new DomainException("Asset tag can use letters, digits, - _ / and . only.");
        return tag;
    }
}

public sealed record AssetDetails(
    string Name, Guid CategoryId, string? Brand, string? Model, string? SerialNumber, Guid? LocationId,
    DateOnly? PurchaseDate, decimal? PurchaseCost, string? Vendor, DateOnly? WarrantyUntil, AssetCondition Condition, string? Notes);

/// <summary>Kis employee ke paas kab se kab tak. ReturnedOn null = abhi us ke paas.</summary>
public sealed class AssetAssignment : TenantChildEntity
{
    public Guid AssetId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly AssignedOn { get; private set; }
    public DateOnly? DueBack { get; internal set; }
    public AssetCondition ConditionOut { get; private set; }
    public string? AssignNote { get; private set; }
    public Guid? AssignedByUserId { get; private set; }
    public DateTime? AcknowledgedAt { get; internal set; }
    public DateOnly? ReturnedOn { get; private set; }
    public AssetCondition? ConditionIn { get; private set; }
    public string? ReturnNote { get; private set; }
    public Guid? ReceivedByUserId { get; private set; }

    private AssetAssignment() { }

    internal AssetAssignment(Guid assetId, Guid employeeId, DateOnly assignedOn, DateOnly? dueBack, AssetCondition conditionOut, string? note, Guid? byUserId)
    {
        AssetId = assetId;
        EmployeeId = employeeId;
        AssignedOn = assignedOn;
        DueBack = dueBack;
        ConditionOut = conditionOut;
        AssignNote = note;
        AssignedByUserId = byUserId;
    }

    internal void Close(DateOnly returnedOn, AssetCondition conditionIn, string? note, Guid? byUserId)
    {
        ReturnedOn = returnedOn;
        ConditionIn = conditionIn;
        ReturnNote = note;
        ReceivedByUserId = byUserId;
    }
}

/// <summary>History tab: append-only log. Detail chhota sa text (note / status badla).</summary>
public sealed class AssetEvent : TenantChildEntity
{
    public Guid AssetId { get; private set; }
    public AssetEventType Type { get; private set; }
    /// <summary>Event ke baad asset ka status (UI translate karta hai).</summary>
    public AssetStatus Status { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTime At { get; private set; }
    public string? Detail { get; private set; }

    private AssetEvent() { }

    internal AssetEvent(Guid assetId, AssetEventType type, AssetStatus status, Guid? employeeId, Guid? byUserId, DateTime at, string? detail)
    {
        AssetId = assetId;
        Type = type;
        Status = status;
        EmployeeId = employeeId;
        ByUserId = byUserId;
        At = at;
        Detail = detail;
    }
}
