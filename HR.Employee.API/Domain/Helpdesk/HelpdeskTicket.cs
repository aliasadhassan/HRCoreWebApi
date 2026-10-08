namespace HR.Employee.API.Domain.Helpdesk;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Employee ki request / sawal. Flow: (PendingApproval →) Open → InProgress ⇄ WaitingOnEmployee → Resolved → Closed.
/// Approval wali category mein manager pehle haan/na karta hai (Rejected band). Employee Resolved ko Close (confirm)
/// ya Reopen kar sakta hai; Open/PendingApproval/WaitingOnEmployee mein Cancel. Code HD-0001.
/// Har badlaav TicketActivities mein; InternalNote sirf agents dekhte hain.
/// </summary>
public sealed class HelpdeskTicket : AuditableEntity
{
    public const string CodePrefix = "HD-";

    private readonly List<TicketActivity> _activities = new();

    public string Code { get; private set; } = default!;
    public Guid EmployeeId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Subject { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Link { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public bool IsConfidential { get; private set; }
    public Guid? RaisedByUserId { get; private set; }

    public Guid? ApproverEmployeeId { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionNote { get; private set; }

    public Guid? AssigneeEmployeeId { get; private set; }
    public DateTime? OpenedAt { get; private set; }
    public DateTime? DueAt { get; private set; }
    public DateTime? FirstResponseAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? Resolution { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public byte? SatisfactionRating { get; private set; }
    public DateTime LastActivityAt { get; private set; }

    public IReadOnlyCollection<TicketActivity> Activities => _activities.AsReadOnly();

    private HelpdeskTicket() { }

    /// <summary>approverEmployeeId null = approval nahi chahiye (ya manager nahi), seedha Open.</summary>
    public static HelpdeskTicket Create(
        Guid tenantId, string code, Guid employeeId, HelpdeskCategory category, string subject, string? description, string? link,
        TicketPriority priority, Guid? approverEmployeeId, Guid? byUserId, DateTime now)
    {
        if (!category.IsActive)
            throw new DomainException("This category is no longer available.");
        var ticket = new HelpdeskTicket
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Code = Guard.Required(code, "Code", 20).ToUpperInvariant(),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            IsConfidential = category.IsConfidential,
            RaisedByUserId = byUserId,
            AssigneeEmployeeId = category.DefaultAssigneeEmployeeId,
            LastActivityAt = now
        };
        ticket.SetCategory(category);
        ticket.Edit(subject, description, link, priority);

        if (category.NeedsManagerApproval && approverEmployeeId is { } approver && approver != employeeId)
        {
            ticket.Status = TicketStatus.PendingApproval;
            ticket.ApproverEmployeeId = approver;
        }
        else
            ticket.Open(category, now);

        ticket.Log(TicketActivityKind.Created, null, null, byUserId, now);
        return ticket;
    }

    public bool IsFinal => Status is TicketStatus.Closed or TicketStatus.Rejected or TicketStatus.Cancelled;
    public bool IsActive => Status is TicketStatus.Open or TicketStatus.InProgress or TicketStatus.WaitingOnEmployee;
    public bool IsOverdue(DateTime now) => IsActive && DueAt is { } due && due < now;

    /// <summary>Subject/description/link/priority. Band ticket nahi badalta.</summary>
    public void Edit(string subject, string? description, string? link, TicketPriority priority)
    {
        EnsureNotFinal();
        if (!Enum.IsDefined(priority))
            throw new DomainException("Select a valid priority.");
        Subject = Guard.Required(subject, "Subject", 200);
        Description = Guard.Optional(description, "Description", 4000);
        Link = Guard.Optional(link, "Link", 1000);
        if (Link is not null && !(Uri.TryCreate(Link, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
            throw new DomainException("Link must be a full http(s) address.");
        Priority = priority;
    }

    /// <summary>Agent category badle. SLA dobara Open time se ginte hain; confidential flag category ke saath.</summary>
    public void ChangeCategory(HelpdeskCategory category, Guid? byUserId, DateTime now)
    {
        EnsureNotFinal();
        if (category.Id == CategoryId)
            return;
        SetCategory(category);
        IsConfidential = category.IsConfidential;
        if (OpenedAt is { } opened)
            DueAt = category.ResolutionHours is { } h ? opened.AddHours(h) : null;
        Log(TicketActivityKind.Status, Status, $"Category: {category.Name}", byUserId, now);
    }

    public void Approve(string? note, Guid? byUserId, HelpdeskCategory category, DateTime now)
    {
        if (Status != TicketStatus.PendingApproval)
            throw new DomainException("This request is not waiting for approval.");
        DecidedAt = now;
        DecisionNote = Guard.Optional(note, "Note", 1000);
        Open(category, now);
        Log(TicketActivityKind.Approved, TicketStatus.PendingApproval, DecisionNote, byUserId, now);
    }

    public void Reject(string note, Guid? byUserId, DateTime now)
    {
        if (Status != TicketStatus.PendingApproval)
            throw new DomainException("This request is not waiting for approval.");
        DecidedAt = now;
        DecisionNote = Guard.Required(note, "Reason", 1000);
        Status = TicketStatus.Rejected;
        ClosedAt = now;
        Log(TicketActivityKind.Rejected, TicketStatus.PendingApproval, DecisionNote, byUserId, now);
    }

    public void Assign(Guid? assigneeEmployeeId, string? assigneeName, Guid? byUserId, DateTime now)
    {
        EnsureNotFinal();
        if (Status == TicketStatus.PendingApproval)
            throw new DomainException("Wait for the manager's approval before assigning this request.");
        var to = assigneeEmployeeId == Guid.Empty ? null : assigneeEmployeeId;
        if (to == AssigneeEmployeeId)
            return;
        AssigneeEmployeeId = to;
        Log(TicketActivityKind.Assigned, Status, assigneeName, byUserId, now);
    }

    /// <summary>Agent ka status badalna: InProgress, WaitingOnEmployee (sawal ke saath), Resolved (jawab ke saath).</summary>
    public void SetStatus(TicketStatus status, string? note, Guid? byUserId, DateTime now)
    {
        EnsureNotFinal();
        if (Status == TicketStatus.PendingApproval)
            throw new DomainException("Wait for the manager's approval first.");
        if (status == Status)
            throw new DomainException("The request already has this status.");
        var text = Guard.Optional(note, "Note", 2000);
        switch (status)
        {
            case TicketStatus.InProgress:
                break;
            case TicketStatus.WaitingOnEmployee:
                if (text is null)
                    throw new DomainException("Tell the employee what you need from them.");
                break;
            case TicketStatus.Resolved:
                if (text is null)
                    throw new DomainException("Add a short resolution so the employee knows what was done.");
                Resolution = text;
                ResolvedAt = now;
                break;
            case TicketStatus.Closed:
                ClosedAt = now;
                break;
            default:
                throw new DomainException("This status cannot be set here.");
        }
        var from = Status;
        Status = status;
        if (status != TicketStatus.Resolved && status != TicketStatus.Closed)
        {
            ResolvedAt = null;
            Resolution = null;
        }
        Responded(now);
        Log(TicketActivityKind.Status, from, text, byUserId, now);
    }

    /// <summary>Comment. Employee jawab de to WaitingOnEmployee wapas InProgress.</summary>
    public void Comment(string body, bool internalNote, bool byAgent, Guid? byUserId, DateTime now)
    {
        EnsureNotFinal();
        var text = Guard.Required(body, "Comment", 4000);
        if (internalNote && !byAgent)
            throw new DomainException("Only the helpdesk team can add internal notes.");
        if (byAgent && !internalNote)
            Responded(now);
        Log(internalNote ? TicketActivityKind.InternalNote : TicketActivityKind.Comment, null, text, byUserId, now);
        if (!byAgent && Status == TicketStatus.WaitingOnEmployee)
        {
            Status = TicketStatus.InProgress;
            Log(TicketActivityKind.Status, TicketStatus.WaitingOnEmployee, null, byUserId, now);
        }
    }

    /// <summary>Employee: hal se khush, band karo (rating optional).</summary>
    public void Confirm(byte? rating, Guid? byUserId, DateTime now)
    {
        if (Status != TicketStatus.Resolved)
            throw new DomainException("Only a resolved request can be closed.");
        SetRating(rating);
        Status = TicketStatus.Closed;
        ClosedAt = now;
        Log(TicketActivityKind.Status, TicketStatus.Resolved, null, byUserId, now);
        if (rating is not null)
            Log(TicketActivityKind.Rated, null, rating.ToString(), byUserId, now);
    }

    public void Rate(byte rating, Guid? byUserId, DateTime now)
    {
        if (Status is not (TicketStatus.Resolved or TicketStatus.Closed))
            throw new DomainException("You can rate a request once it is resolved.");
        SetRating(rating);
        Log(TicketActivityKind.Rated, null, rating.ToString(), byUserId, now);
    }

    /// <summary>Employee (ya agent): hal kaafi nahi, dobara kholo. Close ke baad bhi 30 din tak.</summary>
    public void Reopen(string reason, Guid? byUserId, DateTime now)
    {
        if (Status is not (TicketStatus.Resolved or TicketStatus.Closed))
            throw new DomainException("Only a resolved or closed request can be reopened.");
        if (Status == TicketStatus.Closed && ClosedAt is { } closed && closed.AddDays(30) < now)
            throw new DomainException("This request was closed more than 30 days ago. Raise a new one instead.");
        var text = Guard.Required(reason, "Reason", 2000);
        var from = Status;
        Status = TicketStatus.InProgress;
        ResolvedAt = null;
        Resolution = null;
        ClosedAt = null;
        SatisfactionRating = null;
        Log(TicketActivityKind.Status, from, text, byUserId, now);
    }

    public void Cancel(string? reason, Guid? byUserId, DateTime now)
    {
        if (Status is not (TicketStatus.PendingApproval or TicketStatus.Open or TicketStatus.WaitingOnEmployee))
            throw new DomainException("This request can no longer be cancelled.");
        var from = Status;
        Status = TicketStatus.Cancelled;
        ClosedAt = now;
        Log(TicketActivityKind.Status, from, Guard.Optional(reason, "Reason", 1000), byUserId, now);
    }

    private void Open(HelpdeskCategory category, DateTime now)
    {
        Status = TicketStatus.Open;
        OpenedAt = now;
        DueAt = category.ResolutionHours is { } h ? now.AddHours(h) : null;
    }

    private void SetCategory(HelpdeskCategory category)
        => CategoryId = Guard.NotEmpty(category.Id, "Category");

    private void SetRating(byte? rating)
    {
        if (rating is < 1 or > 5)
            throw new DomainException("Rating must be between 1 and 5.");
        SatisfactionRating = rating;
    }

    private void Responded(DateTime now) => FirstResponseAt ??= now;

    private void EnsureNotFinal()
    {
        if (IsFinal)
            throw new DomainException("This request is closed.");
    }

    private void Log(TicketActivityKind kind, TicketStatus? from, string? note, Guid? byUserId, DateTime now)
    {
        LastActivityAt = now;
        _activities.Add(new TicketActivity(Id, kind, from, Status, note, byUserId, now));
    }
}

/// <summary>Ticket ki history + comments (append-only). InternalNote employee ko nahi dikhta.</summary>
public sealed class TicketActivity : TenantChildEntity
{
    public Guid TicketId { get; private set; }
    public TicketActivityKind Kind { get; private set; }
    public TicketStatus? FromStatus { get; private set; }
    public TicketStatus ToStatus { get; private set; }
    public string? Note { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTime At { get; private set; }

    private TicketActivity() { }

    internal TicketActivity(Guid ticketId, TicketActivityKind kind, TicketStatus? from, TicketStatus to, string? note, Guid? byUserId, DateTime at)
    {
        TicketId = ticketId;
        Kind = kind;
        FromStatus = from;
        ToStatus = to;
        Note = note;
        ByUserId = byUserId;
        At = at;
    }
}
