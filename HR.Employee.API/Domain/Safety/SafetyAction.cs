namespace HR.Employee.API.Domain.Safety;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Corrective / preventive action (CAPA). Incident ya inspection ki fail line se, ya akela.
/// Assignee: Open → InProgress → Done. Safety team: Done → Verified (ya wapas Open), Cancel.
/// </summary>
public sealed class SafetyAction : AuditableEntity
{
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid? IncidentId { get; private set; }
    public Guid? InspectionId { get; private set; }
    public Guid AssigneeEmployeeId { get; private set; }
    public SafetySeverity Priority { get; private set; }
    public DateOnly DueOn { get; private set; }
    public ActionStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? CompletionNote { get; private set; }
    public DateTime? VerifiedAt { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }

    public bool IsOpen => Status is ActionStatus.Open or ActionStatus.InProgress;
    public bool IsOverdue(DateOnly today) => IsOpen && DueOn < today;

    private SafetyAction() { }

    public static SafetyAction Create(
        Guid tenantId, ActionDetails details, Guid? incidentId, Guid? inspectionId, DateOnly today)
    {
        var a = new SafetyAction
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            IncidentId = incidentId == Guid.Empty ? null : incidentId,
            InspectionId = inspectionId == Guid.Empty ? null : inspectionId,
            Status = ActionStatus.Open
        };
        if (a.IncidentId is not null && a.InspectionId is not null)
            throw new DomainException("An action belongs to an incident or an inspection, not both.");
        a.Apply(details, today, isNew: true);
        return a;
    }

    public void Update(ActionDetails details, DateOnly today)
    {
        if (!IsOpen)
            throw new DomainException("Only an open action can be changed.");
        Apply(details, today, isNew: false);
    }

    private void Apply(ActionDetails d, DateOnly today, bool isNew)
    {
        if (!Enum.IsDefined(d.Priority))
            throw new DomainException("Select a valid priority.");
        if (isNew && d.DueOn < today)
            throw new DomainException("The due date cannot be in the past.");
        if (d.DueOn > today.AddYears(2))
            throw new DomainException("The due date is too far ahead.");
        Title = Guard.Required(d.Title, "Title", 200);
        Description = Guard.Optional(d.Description, "Description", 2000);
        AssigneeEmployeeId = Guard.NotEmpty(d.AssigneeEmployeeId, "Assignee");
        Priority = d.Priority;
        DueOn = d.DueOn;
    }

    public void Start()
    {
        if (Status != ActionStatus.Open)
            throw new DomainException("Only an open action can be started.");
        Status = ActionStatus.InProgress;
    }

    public void Complete(string note, DateTime now)
    {
        if (!IsOpen)
            throw new DomainException("This action is not open.");
        CompletionNote = Guard.Required(note, "What was done", 2000);
        Status = ActionStatus.Done;
        CompletedAt = now;
    }

    /// <summary>Safety team check karti hai. accepted = false → wapas Open (note ke saath).</summary>
    public void Verify(bool accepted, Guid? byUserId, DateTime now)
    {
        if (Status != ActionStatus.Done)
            throw new DomainException("Only a done action can be verified.");
        if (accepted)
        {
            Status = ActionStatus.Verified;
            VerifiedAt = now;
            VerifiedByUserId = byUserId;
        }
        else
        {
            Status = ActionStatus.Open;
            CompletedAt = null;
        }
    }

    public void Cancel(string? reason)
    {
        if (!IsOpen)
            throw new DomainException("Only an open action can be cancelled.");
        Status = ActionStatus.Cancelled;
        CompletionNote = Guard.Optional(reason, "Reason", 2000);
    }
}

public sealed record ActionDetails(string Title, string? Description, Guid AssigneeEmployeeId, SafetySeverity Priority, DateOnly DueOn);
