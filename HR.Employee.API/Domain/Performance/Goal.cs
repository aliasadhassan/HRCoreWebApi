namespace HR.Employee.API.Domain.Performance;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Employee ka goal. Cycle optional (cycle se juda ho to review mein dikhta hai). Progress 0-100;
/// har check-in GoalCheckIns mein history chhodta hai.
/// </summary>
public sealed class Goal : AuditableEntity
{
    private readonly List<GoalCheckIn> _checkIns = new();

    public Guid EmployeeId { get; private set; }
    public Guid? ReviewCycleId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public byte? Weight { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public byte Progress { get; private set; }
    public GoalStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public IReadOnlyCollection<GoalCheckIn> CheckIns => _checkIns.AsReadOnly();

    private Goal() { }

    public static Goal Create(Guid tenantId, Guid employeeId, GoalDetails details)
    {
        var goal = new Goal
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            Status = GoalStatus.NotStarted
        };
        goal.Apply(details);
        return goal;
    }

    public void Update(GoalDetails details)
    {
        EnsureOpen();
        Apply(details);
    }

    private void Apply(GoalDetails d)
    {
        if (d.Weight is > 100)
            throw new DomainException("Weight must be between 0 and 100.");
        if (d.StartDate is { } s && d.DueDate < s)
            throw new DomainException("Due date cannot be before the start date.");

        Title = Guard.Required(d.Title, "Title", 200);
        Description = Guard.Optional(d.Description, "Description", 2000);
        ReviewCycleId = d.ReviewCycleId == Guid.Empty ? null : d.ReviewCycleId;
        Weight = d.Weight;
        StartDate = d.StartDate;
        DueDate = d.DueDate;
    }

    /// <summary>Progress update. 100% = Completed (aur Completed = 100%).</summary>
    public GoalCheckIn CheckIn(byte progress, GoalStatus status, string? note, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (progress > 100)
            throw new DomainException("Progress must be between 0 and 100.");
        if (status == GoalStatus.Cancelled || !Enum.IsDefined(status))
            throw new DomainException("Select a valid status.");
        if (status == GoalStatus.Completed || progress == 100)
        {
            status = GoalStatus.Completed;
            progress = 100;
        }
        else if (status == GoalStatus.NotStarted && progress > 0)
            status = GoalStatus.OnTrack;

        Progress = progress;
        Status = status;
        CompletedAt = status == GoalStatus.Completed ? now : null;

        var checkIn = new GoalCheckIn(Id, progress, status, Guard.Optional(note, "Note", 1000), byUserId, now);
        _checkIns.Add(checkIn);
        return checkIn;
    }

    public void Cancel(string? note, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        Status = GoalStatus.Cancelled;
        _checkIns.Add(new GoalCheckIn(Id, Progress, GoalStatus.Cancelled, Guard.Optional(note, "Note", 1000), byUserId, now));
    }

    /// <summary>Completed/Cancelled goal ko wapis kholna.</summary>
    public void Reopen(Guid? byUserId, DateTime now)
    {
        if (Status is not (GoalStatus.Completed or GoalStatus.Cancelled))
            throw new DomainException("This goal is already open.");
        Status = Progress == 0 ? GoalStatus.NotStarted : GoalStatus.OnTrack;
        CompletedAt = null;
        _checkIns.Add(new GoalCheckIn(Id, Progress, Status, null, byUserId, now));
    }

    private void EnsureOpen()
    {
        if (Status is GoalStatus.Completed or GoalStatus.Cancelled)
            throw new DomainException("This goal is closed. Reopen it first.");
    }
}

public sealed record GoalDetails(string Title, string? Description, Guid? ReviewCycleId, byte? Weight, DateOnly? StartDate, DateOnly DueDate);

/// <summary>Goal ki progress history (append-only).</summary>
public sealed class GoalCheckIn : TenantChildEntity
{
    public Guid GoalId { get; private set; }
    public byte Progress { get; private set; }
    public GoalStatus Status { get; private set; }
    public string? Note { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTime At { get; private set; }

    private GoalCheckIn() { }

    internal GoalCheckIn(Guid goalId, byte progress, GoalStatus status, string? note, Guid? byUserId, DateTime at)
    {
        GoalId = goalId;
        Progress = progress;
        Status = status;
        Note = note;
        ByUserId = byUserId;
        At = at;
    }
}
