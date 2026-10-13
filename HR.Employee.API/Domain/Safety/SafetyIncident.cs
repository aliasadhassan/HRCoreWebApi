namespace HR.Employee.API.Domain.Safety;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Health &amp; safety incident: injury, near miss, hazard... Code HS-0001.
/// Koi bhi employee report kar sakta hai (Anonymous = team ko reporter ka naam nahi dikhta).
/// Band karne se pehle: koi khula action na ho, root cause likha ho, aur reportable ho to authority ko bataya gaya ho.
/// </summary>
public sealed class SafetyIncident : AuditableEntity
{
    public const string CodePrefix = "HS-";
    public const short MaxLostDays = 999;

    private readonly List<IncidentEntry> entries = new();

    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public IncidentType Type { get; private set; }
    public SafetySeverity Severity { get; private set; }
    public IncidentStatus Status { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public Guid? LocationId { get; private set; }
    public string? Area { get; private set; }
    public Guid? DepartmentId { get; private set; }

    public Guid? ReporterEmployeeId { get; private set; }
    public bool Anonymous { get; private set; }

    /// <summary>Jisay chot lagi / bimar hua. Employee na ho to naam (visitor, contractor).</summary>
    public Guid? AffectedEmployeeId { get; private set; }
    public string? AffectedName { get; private set; }
    public IncidentTreatment? Treatment { get; private set; }
    public string? InjuryNature { get; private set; }
    public string? BodyPart { get; private set; }
    public short LostDays { get; private set; }
    public string? Witnesses { get; private set; }
    public string? ImmediateAction { get; private set; }

    public Guid? InvestigatorEmployeeId { get; private set; }
    public RootCauseCategory? RootCauseCategory { get; private set; }
    public string? RootCause { get; private set; }

    /// <summary>Qanoonan authority (labour dept, EHS regulator) ko batana zaroori hai.</summary>
    public bool IsReportable { get; private set; }
    public DateOnly? AuthorityNotifiedOn { get; private set; }
    public string? AuthorityReference { get; private set; }

    public DateTime? ClosedAt { get; private set; }
    public string? ClosureNote { get; private set; }
    public DateTime LastActivityAt { get; private set; }

    public IReadOnlyCollection<IncidentEntry> Entries => entries.AsReadOnly();

    public bool IsClosed => Status == IncidentStatus.Closed;

    /// <summary>Lost-time incident: kaam se chhutti hui (LTI).</summary>
    public bool IsLostTime => LostDays > 0 || Treatment == IncidentTreatment.Fatal;

    private SafetyIncident() { }

    public static SafetyIncident Create(
        Guid tenantId, string code, IncidentDetails details, Guid? reporterEmployeeId, bool anonymous, Guid? byUserId, DateTime now)
    {
        if (anonymous && reporterEmployeeId is null)
            throw new DomainException("Only a named reporter can ask to stay anonymous.");
        var i = new SafetyIncident
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Code = Guard.Required(code, "Code", 20).ToUpperInvariant(),
            Status = IncidentStatus.Reported,
            ReporterEmployeeId = reporterEmployeeId,
            Anonymous = anonymous,
            LastActivityAt = now
        };
        i.Apply(details, now);
        i.Log(IncidentEntryKind.Created, null, byUserId, now);
        return i;
    }

    public void Update(IncidentDetails details, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        Apply(details, now);
        Log(IncidentEntryKind.Updated, null, byUserId, now);
    }

    private void Apply(IncidentDetails d, DateTime now)
    {
        if (!Enum.IsDefined(d.Type))
            throw new DomainException("Select a valid incident type.");
        if (!Enum.IsDefined(d.Severity))
            throw new DomainException("Select a valid severity.");
        if (d.Treatment is { } t && !Enum.IsDefined(t))
            throw new DomainException("Select a valid treatment.");
        if (d.OccurredAt.Kind == DateTimeKind.Local)
            throw new DomainException("Time must be sent in UTC.");
        var occurred = DateTime.SpecifyKind(d.OccurredAt, DateTimeKind.Utc);
        if (occurred > now.AddMinutes(5))
            throw new DomainException("The incident time cannot be in the future.");
        if (occurred < now.AddYears(-5))
            throw new DomainException("The incident time is too far in the past.");
        if (d.LostDays is < 0 or > MaxLostDays)
            throw new DomainException($"Lost days must be between 0 and {MaxLostDays}.");
        if (d.AffectedEmployeeId is not null && !string.IsNullOrWhiteSpace(d.AffectedName))
            throw new DomainException("Pick an employee or type a name for the person affected, not both.");
        var affected = d.AffectedEmployeeId is not null || !string.IsNullOrWhiteSpace(d.AffectedName);
        if (d.Type is IncidentType.Injury or IncidentType.Illness && !affected)
            throw new DomainException("Say who was hurt or became ill.");
        if (!affected && (d.Treatment is not null && d.Treatment != IncidentTreatment.None || d.LostDays > 0))
            throw new DomainException("Treatment and lost days need a person who was affected.");

        Title = Guard.Required(d.Title, "Title", 200);
        Description = Guard.Optional(d.Description, "Description", 4000);
        Type = d.Type;
        Severity = d.Severity;
        OccurredAt = occurred;
        LocationId = d.LocationId == Guid.Empty ? null : d.LocationId;
        Area = Guard.Optional(d.Area, "Area", 150);
        DepartmentId = d.DepartmentId == Guid.Empty ? null : d.DepartmentId;
        AffectedEmployeeId = d.AffectedEmployeeId == Guid.Empty ? null : d.AffectedEmployeeId;
        AffectedName = AffectedEmployeeId is null ? Guard.Optional(d.AffectedName, "Person affected", 120) : null;
        Treatment = d.Treatment;
        InjuryNature = Guard.Optional(d.InjuryNature, "Nature of injury", 120);
        BodyPart = Guard.Optional(d.BodyPart, "Body part", 120);
        LostDays = d.LostDays;
        Witnesses = Guard.Optional(d.Witnesses, "Witnesses", 500);
        ImmediateAction = Guard.Optional(d.ImmediateAction, "Immediate action", 2000);
        // Maut ya hospital = hamesha reportable aur critical/high
        IsReportable = d.IsReportable || d.Treatment is IncidentTreatment.Hospitalised or IncidentTreatment.Fatal;
        if (d.Treatment == IncidentTreatment.Fatal)
            Severity = SafetySeverity.Critical;
    }

    public void MoveTo(IncidentStatus status, string? note, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (status is IncidentStatus.Closed or IncidentStatus.Reported || !Enum.IsDefined(status))
            throw new DomainException(status == IncidentStatus.Closed ? "Use Close to finish an incident." : "Pick Investigating or Action pending.");
        if (status == Status)
            throw new DomainException("The incident is already in this status.");
        Status = status;
        Log(IncidentEntryKind.StatusChanged, Join(status.ToString(), Guard.Optional(note, "Note", 1000)), byUserId, now);
    }

    public void AssignInvestigator(Guid? employeeId, string? name, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        var id = employeeId == Guid.Empty ? null : employeeId;
        if (id == InvestigatorEmployeeId)
            return;
        if (id is not null && id == ReporterEmployeeId && Anonymous)
            throw new DomainException("The anonymous reporter cannot investigate their own report.");
        InvestigatorEmployeeId = id;
        if (id is not null && Status == IncidentStatus.Reported)
            Status = IncidentStatus.Investigating;
        Log(IncidentEntryKind.InvestigatorChanged, name, byUserId, now);
    }

    public void SetRootCause(RootCauseCategory? category, string? rootCause, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (category is { } c && !Enum.IsDefined(c))
            throw new DomainException("Select a valid root cause.");
        RootCauseCategory = category;
        RootCause = Guard.Optional(rootCause, "Root cause", 4000);
        Log(IncidentEntryKind.Updated, "Root cause", byUserId, now);
    }

    public void NotifyAuthority(DateOnly notifiedOn, string? reference, Guid? byUserId, DateOnly today, DateTime now)
    {
        EnsureOpen();
        if (notifiedOn > today)
            throw new DomainException("The date the authority was told cannot be in the future.");
        if (notifiedOn < DateOnly.FromDateTime(OccurredAt))
            throw new DomainException("The authority cannot be told before the incident happened.");
        IsReportable = true;
        AuthorityNotifiedOn = notifiedOn;
        AuthorityReference = Guard.Optional(reference, "Reference", 100);
        Log(IncidentEntryKind.AuthorityNotified, Join(notifiedOn.ToString("yyyy-MM-dd"), AuthorityReference), byUserId, now);
    }

    public void AddNote(string text, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        Log(IncidentEntryKind.Note, Guard.Required(text, "Note", 4000), byUserId, now);
    }

    internal void LogAction(IncidentEntryKind kind, string title, Guid? byUserId, DateTime now)
        => Log(kind, title, byUserId, now);

    /// <summary>openActions = is incident ke Open/InProgress actions (handler ginta hai).</summary>
    public void Close(string closureNote, int openActions, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (openActions > 0)
            throw new DomainException($"Finish or cancel the {openActions} open action(s) first.");
        if (RootCauseCategory is null || string.IsNullOrWhiteSpace(RootCause))
            throw new DomainException("Record the root cause before closing.");
        if (IsReportable && AuthorityNotifiedOn is null)
            throw new DomainException("This incident is reportable. Record when the authority was told before closing.");
        ClosureNote = Guard.Required(closureNote, "Closing note", 2000);
        Status = IncidentStatus.Closed;
        ClosedAt = now;
        Log(IncidentEntryKind.Closed, ClosureNote, byUserId, now);
    }

    public void Reopen(string reason, Guid? byUserId, DateTime now)
    {
        if (!IsClosed)
            throw new DomainException("Only a closed incident can be reopened.");
        var why = Guard.Required(reason, "Reason", 500);
        Status = IncidentStatus.Investigating;
        ClosedAt = null;
        ClosureNote = null;
        Log(IncidentEntryKind.Reopened, why, byUserId, now);
    }

    private void EnsureOpen()
    {
        if (IsClosed)
            throw new DomainException("This incident is closed. Reopen it to make changes.");
    }

    private DateTime? lastLoggedAt;

    private void Log(IncidentEntryKind kind, string? text, Guid? byUserId, DateTime now)
    {
        var at = lastLoggedAt is { } last && now <= last ? last.AddMilliseconds(1) : now;
        lastLoggedAt = at;
        entries.Add(new IncidentEntry(Id, kind, text, byUserId, at));
        LastActivityAt = at;
    }

    private static string? Join(string? a, string? b)
        => string.IsNullOrWhiteSpace(b) ? a : string.IsNullOrWhiteSpace(a) ? b : $"{a} · {b}";
}

public sealed record IncidentDetails(
    string Title, string? Description, IncidentType Type, SafetySeverity Severity, DateTime OccurredAt,
    Guid? LocationId, string? Area, Guid? DepartmentId,
    Guid? AffectedEmployeeId, string? AffectedName, IncidentTreatment? Treatment, string? InjuryNature, string? BodyPart,
    short LostDays, string? Witnesses, string? ImmediateAction, bool IsReportable);

/// <summary>Incident ki timeline line.</summary>
public sealed class IncidentEntry : TenantChildEntity
{
    public Guid IncidentId { get; private set; }
    public IncidentEntryKind Kind { get; private set; }
    public string? Text { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTime At { get; private set; }

    private IncidentEntry() { }

    internal IncidentEntry(Guid incidentId, IncidentEntryKind kind, string? text, Guid? byUserId, DateTime at)
    {
        IncidentId = incidentId;
        Kind = kind;
        Text = text;
        ByUserId = byUserId;
        At = at;
    }
}
