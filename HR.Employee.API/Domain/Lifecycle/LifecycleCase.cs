namespace HR.Employee.API.Domain.Lifecycle;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Ek employee ka onboarding ya exit case: tasks ki checklist + (exit mein) exit details.
/// AnchorDate = onboarding mein joining date, exit mein last working day. Task due dates isi se.
/// Ek employee ka ek kind ka sirf ek InProgress case (unique index).
/// </summary>
public sealed class LifecycleCase : AuditableEntity
{
    public const int MaxTasks = 100;

    private readonly List<LifecycleTask> _tasks = new();

    public Guid EmployeeId { get; private set; }
    public LifecycleKind Kind { get; private set; }
    public Guid? ChecklistTemplateId { get; private set; }
    public CaseStatus Status { get; private set; } = CaseStatus.InProgress;
    public DateOnly AnchorDate { get; private set; }

    // Exit details (Kind = Exit)
    public ExitType? ExitType { get; private set; }
    public DateOnly? NoticeDate { get; private set; }
    public string? Reason { get; private set; }
    public bool? EligibleForRehire { get; private set; }
    public string? InterviewNotes { get; private set; }

    public string? Notes { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public Guid? ClosedByUserId { get; private set; }

    public IReadOnlyCollection<LifecycleTask> Tasks => _tasks.AsReadOnly();

    private LifecycleCase() { }

    public static LifecycleCase StartOnboarding(Guid tenantId, Guid employeeId, DateOnly joiningDate, ChecklistTemplate? template)
    {
        var c = new LifecycleCase
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            Kind = LifecycleKind.Onboarding,
            AnchorDate = joiningDate
        };
        c.CopyTemplate(template);
        return c;
    }

    public static LifecycleCase StartExit(
        Guid tenantId, Guid employeeId, ExitType exitType, DateOnly noticeDate, DateOnly lastWorkingDay,
        string reason, ChecklistTemplate? template)
    {
        var c = new LifecycleCase
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            Kind = LifecycleKind.Exit
        };
        c.SetExitDetails(exitType, noticeDate, lastWorkingDay, reason, null, null);
        c.CopyTemplate(template);
        return c;
    }

    public bool IsOpen => Status == CaseStatus.InProgress;

    /// <summary>Last working day badle to khule tasks ki due dates bhi utni shift.</summary>
    public void SetExitDetails(
        ExitType exitType, DateOnly noticeDate, DateOnly lastWorkingDay, string reason, bool? eligibleForRehire, string? interviewNotes)
    {
        EnsureOpen();
        if (Kind != LifecycleKind.Exit)
            throw new DomainException("Exit details only apply to exit cases.");
        if (!Enum.IsDefined(exitType))
            throw new DomainException("Select an exit type.");
        if (lastWorkingDay < noticeDate)
            throw new DomainException("Last working day cannot be before the notice date.");

        var shift = AnchorDate == default ? 0 : lastWorkingDay.DayNumber - AnchorDate.DayNumber;
        if (shift != 0)
            foreach (var task in _tasks.Where(t => t.Status == LifecycleTaskStatus.Open))
                task.ShiftDue(shift);

        ExitType = exitType;
        NoticeDate = noticeDate;
        AnchorDate = lastWorkingDay;
        Reason = Guard.Required(reason, "Exit reason", 500);
        EligibleForRehire = eligibleForRehire;
        InterviewNotes = Guard.Optional(interviewNotes, "Exit interview notes", 4000);
    }

    public void SetNotes(string? notes)
    {
        EnsureOpen();
        Notes = Guard.Optional(notes, "Notes", 2000);
    }

    // ─────────────────────────────── Tasks ────────────────────────────────
    public LifecycleTask AddTask(string title, string? description, TaskOwner owner, Guid? assigneeEmployeeId, DateOnly? dueDate, bool isRequired)
    {
        EnsureOpen();
        if (_tasks.Count >= MaxTasks)
            throw new DomainException($"A checklist can have at most {MaxTasks} tasks.");

        var order = (short)(_tasks.Count == 0 ? 1 : _tasks.Max(t => t.SortOrder) + 1);
        var task = new LifecycleTask(title, description, owner, assigneeEmployeeId, dueDate, isRequired, order);
        _tasks.Add(task);
        return task;
    }

    public void UpdateTask(Guid taskId, string title, string? description, TaskOwner owner, Guid? assigneeEmployeeId, DateOnly? dueDate, bool isRequired)
    {
        EnsureOpen();
        FindTask(taskId).Update(title, description, owner, assigneeEmployeeId, dueDate, isRequired);
    }

    public void RemoveTask(Guid taskId)
    {
        EnsureOpen();
        var task = FindTask(taskId);
        if (task.Status == LifecycleTaskStatus.Done)
            throw new DomainException("A completed task cannot be removed. Reopen it first.");
        _tasks.Remove(task);
    }

    public void CompleteTask(Guid taskId, Guid? userId, string? note, DateTime now)
    {
        EnsureOpen();
        FindTask(taskId).Close(LifecycleTaskStatus.Done, userId, note, now);
    }

    public void SkipTask(Guid taskId, Guid? userId, string? note, DateTime now)
    {
        EnsureOpen();
        var task = FindTask(taskId);
        if (task.IsRequired && string.IsNullOrWhiteSpace(note))
            throw new DomainException("Add a note to explain why a required task is skipped.");
        task.Close(LifecycleTaskStatus.Skipped, userId, note, now);
    }

    public void ReopenTask(Guid taskId)
    {
        EnsureOpen();
        FindTask(taskId).Reopen();
    }

    // ─────────────────────────────── Close ────────────────────────────────
    /// <summary>Sab required tasks Done/Skipped hon tab. Exit case: employee ka asal Exit() handler karta hai.</summary>
    public void Complete(Guid? userId, DateTime now)
    {
        EnsureOpen();
        var pending = _tasks.Count(t => t.IsRequired && t.Status == LifecycleTaskStatus.Open);
        if (pending > 0)
            throw new DomainException(pending == 1
                ? "1 required task is still open."
                : $"{pending} required tasks are still open.");

        Status = CaseStatus.Completed;
        ClosedAt = now;
        ClosedByUserId = userId;
    }

    public void Cancel(Guid? userId, DateTime now)
    {
        EnsureOpen();
        Status = CaseStatus.Cancelled;
        ClosedAt = now;
        ClosedByUserId = userId;
    }

    private void CopyTemplate(ChecklistTemplate? template)
    {
        if (template is null)
            return;
        if (template.Kind != Kind)
            throw new DomainException("This template is for a different kind of checklist.");
        if (!template.IsActive)
            throw new DomainException("This template is inactive.");

        ChecklistTemplateId = template.Id;
        foreach (var t in template.Tasks.OrderBy(t => t.SortOrder))
            _tasks.Add(new LifecycleTask(t.Title, t.Description, t.Owner, null, AnchorDate.AddDays(t.DueOffsetDays), t.IsRequired, t.SortOrder));
    }

    private LifecycleTask FindTask(Guid taskId)
        => _tasks.FirstOrDefault(t => t.Id == taskId) ?? throw new DomainException("Task not found.");

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new DomainException("This checklist is closed and can no longer be changed.");
    }
}

public sealed class LifecycleTask : TenantChildEntity
{
    public Guid LifecycleCaseId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public TaskOwner Owner { get; private set; }
    public Guid? AssigneeEmployeeId { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public bool IsRequired { get; private set; }
    public short SortOrder { get; private set; }
    public LifecycleTaskStatus Status { get; private set; } = LifecycleTaskStatus.Open;
    public string? Note { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? CompletedByUserId { get; private set; }

    private LifecycleTask() { }

    internal LifecycleTask(string title, string? description, TaskOwner owner, Guid? assigneeEmployeeId, DateOnly? dueDate, bool isRequired, short sortOrder)
    {
        Update(title, description, owner, assigneeEmployeeId, dueDate, isRequired);
        SortOrder = sortOrder;
    }

    internal void Update(string title, string? description, TaskOwner owner, Guid? assigneeEmployeeId, DateOnly? dueDate, bool isRequired)
    {
        if (!Enum.IsDefined(owner))
            throw new DomainException("Select who owns the task.");

        Title = Guard.Required(title, "Task title", 200);
        Description = Guard.Optional(description, "Task description", 1000);
        Owner = owner;
        AssigneeEmployeeId = assigneeEmployeeId == Guid.Empty ? null : assigneeEmployeeId;
        DueDate = dueDate;
        IsRequired = isRequired;
    }

    internal void ShiftDue(int days)
    {
        if (DueDate is { } due)
            DueDate = due.AddDays(days);
    }

    internal void Close(LifecycleTaskStatus status, Guid? userId, string? note, DateTime now)
    {
        if (Status != LifecycleTaskStatus.Open)
            throw new DomainException("This task is already closed.");

        Status = status;
        Note = Guard.Optional(note, "Note", 1000);
        CompletedAt = now;
        CompletedByUserId = userId;
    }

    internal void Reopen()
    {
        if (Status == LifecycleTaskStatus.Open)
            return;

        Status = LifecycleTaskStatus.Open;
        CompletedAt = null;
        CompletedByUserId = null;
    }
}
