namespace HR.Employee.API.Domain.Relations;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Employee relations case: complaint, grievance, harassment, disciplinary... Code ER-0001.
/// Stage company ki apni list se (CaseStage), logic phase par. Restricted case sirf members dekhte hain.
/// Members: Owner (ek hi), Investigators, Viewers. Parties: complainant, respondent, witness (respondent kabhi member nahi ban sakta).
/// Har badlaav timeline (CaseEntry) mein; Message = reporter bhi dekhta hai, Note = sirf team.
/// </summary>
public sealed class ErCase : AuditableEntity
{
    public const string CodePrefix = "ER-";
    public const byte MaxEscalation = 3;
    public const int MaxMembers = 20;
    public const int MaxParties = 30;
    public const int MaxEvidence = 50;

    private readonly List<CaseMember> members = new();
    private readonly List<CaseParty> parties = new();
    private readonly List<CaseEntry> entries = new();
    private readonly List<CaseEvidence> evidence = new();

    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public CaseType Type { get; private set; }
    public CaseSeverity Severity { get; private set; }
    public CaseConfidentiality Confidentiality { get; private set; }
    public CaseSource Source { get; private set; }
    public Guid StageId { get; private set; }
    public CasePhase Phase { get; private set; }
    public DateOnly ReportedOn { get; private set; }
    public DateOnly? IncidentOn { get; private set; }
    public string? Location { get; private set; }

    /// <summary>Jis ne case uthaya (self-service). ReporterHidden = investigators ko naam nahi dikhta.</summary>
    public Guid? ReporterEmployeeId { get; private set; }
    public bool ReporterHidden { get; private set; }

    public Guid? OwnerEmployeeId { get; private set; }
    public DateOnly? DueOn { get; private set; }
    public byte EscalationLevel { get; private set; }
    public DateTime? EscalatedAt { get; private set; }

    public CaseOutcome? Outcome { get; private set; }
    public CaseAction? ActionTaken { get; private set; }
    public string? ResolutionSummary { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public short ReopenCount { get; private set; }
    public DateTime LastActivityAt { get; private set; }

    public IReadOnlyCollection<CaseMember> Members => members.AsReadOnly();
    public IReadOnlyCollection<CaseParty> Parties => parties.AsReadOnly();
    public IReadOnlyCollection<CaseEntry> Entries => entries.AsReadOnly();
    public IReadOnlyCollection<CaseEvidence> Evidence => evidence.AsReadOnly();

    public bool IsClosed => Phase == CasePhase.Closed;
    public bool IsOverdue(DateOnly today) => !IsClosed && DueOn is { } due && due < today;

    private ErCase() { }

    public static ErCase Create(
        Guid tenantId, string code, CaseDetails details, CaseSource source, CaseStage firstStage, DateOnly reportedOn,
        Guid? reporterEmployeeId, bool reporterHidden, Guid? byUserId, DateOnly today, DateTime now)
    {
        if (!Enum.IsDefined(source))
            throw new DomainException("Select where the case came from.");
        if (firstStage.Phase == CasePhase.Closed || !firstStage.IsActive)
            throw new DomainException("A new case cannot start in a closed or inactive stage.");
        if (reportedOn > today)
            throw new DomainException("Reported date cannot be in the future.");
        if (reporterHidden && reporterEmployeeId is null)
            throw new DomainException("Only a named reporter can ask to stay hidden.");

        var c = new ErCase
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Code = Guard.Required(code, "Code", 20).ToUpperInvariant(),
            Source = source,
            ReportedOn = reportedOn,
            ReporterEmployeeId = reporterEmployeeId,
            ReporterHidden = reporterHidden,
            LastActivityAt = now
        };
        c.Apply(details, today);
        c.EnterStage(firstStage, today);
        c.Log(CaseEntryKind.Created, null, false, byUserId, now);
        return c;
    }

    /// <summary>Title, type, severity, confidentiality, incident. Band case nahi badalta.</summary>
    public void Update(CaseDetails details, Guid? byUserId, DateOnly today, DateTime now)
    {
        EnsureOpen();
        Apply(details, today);
        Log(CaseEntryKind.Updated, null, false, byUserId, now);
    }

    private void Apply(CaseDetails d, DateOnly today)
    {
        if (!Enum.IsDefined(d.Type))
            throw new DomainException("Select a valid case type.");
        if (!Enum.IsDefined(d.Severity))
            throw new DomainException("Select a valid severity.");
        if (!Enum.IsDefined(d.Confidentiality))
            throw new DomainException("Select a valid confidentiality level.");
        if (d.Type is CaseType.Harassment or CaseType.Whistleblowing && d.Confidentiality != CaseConfidentiality.Restricted)
            throw new DomainException("Harassment and whistleblowing cases must be restricted.");
        if (d.IncidentOn is { } incident && incident > today)
            throw new DomainException("Incident date cannot be in the future.");
        Title = Guard.Required(d.Title, "Title", 200);
        Description = Guard.Optional(d.Description, "Description", 4000);
        Type = d.Type;
        Severity = d.Severity;
        Confidentiality = d.Confidentiality;
        IncidentOn = d.IncidentOn;
        Location = Guard.Optional(d.Location, "Location", 200);
    }

    /// <summary>Kisi bhi khule stage par le jao (aagay ya peeche). Band karne ke liye Close.</summary>
    public void MoveTo(CaseStage stage, string? note, Guid? byUserId, DateOnly today, DateTime now)
    {
        EnsureOpen();
        if (stage.Phase == CasePhase.Closed)
            throw new DomainException("Use Close to finish a case. It needs an outcome.");
        if (!stage.IsActive)
            throw new DomainException("This stage is no longer in use.");
        if (stage.Id == StageId)
            throw new DomainException("The case is already in this stage.");
        EnterStage(stage, today);
        Log(CaseEntryKind.StageChanged, Join(stage.Name, note), false, byUserId, now, stage.Id);
    }

    public void Close(CaseStage stage, CaseOutcome outcome, CaseAction action, string summary, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (stage.Phase != CasePhase.Closed)
            throw new DomainException("Pick a closed stage to close the case.");
        if (!Enum.IsDefined(outcome))
            throw new DomainException("Select the outcome.");
        if (!Enum.IsDefined(action))
            throw new DomainException("Select the action taken.");
        if (OwnerEmployeeId is null)
            throw new DomainException("Assign an owner before closing the case.");
        ResolutionSummary = Guard.Required(summary, "Summary", 4000);
        Outcome = outcome;
        ActionTaken = action;
        StageId = stage.Id;
        Phase = CasePhase.Closed;
        DueOn = null;
        ClosedAt = now;
        Log(CaseEntryKind.Closed, stage.Name, true, byUserId, now, stage.Id);
    }

    public void Reopen(CaseStage stage, string reason, Guid? byUserId, DateOnly today, DateTime now)
    {
        if (!IsClosed)
            throw new DomainException("Only a closed case can be reopened.");
        if (stage.Phase is CasePhase.Closed || !stage.IsActive)
            throw new DomainException("Pick an active open stage to reopen the case in.");
        var why = Guard.Required(reason, "Reason", 1000);
        Outcome = null;
        ActionTaken = null;
        ResolutionSummary = null;
        ClosedAt = null;
        ReopenCount++;
        EnterStage(stage, today);
        Log(CaseEntryKind.Reopened, Join(stage.Name, why), true, byUserId, now, stage.Id);
    }

    public void SetDeadline(DateOnly? dueOn, string? reason, Guid? byUserId, DateOnly today, DateTime now)
    {
        EnsureOpen();
        if (dueOn is { } due && due < today)
            throw new DomainException("The deadline cannot be in the past.");
        if (dueOn == DueOn)
            return;
        DueOn = dueOn;
        Log(CaseEntryKind.DeadlineChanged, Join(dueOn?.ToString("yyyy-MM-dd") ?? "-", Guard.Optional(reason, "Reason", 500)), false, byUserId, now);
    }

    /// <summary>Senior tak le jao (level 1-3). toName = jisko escalate kiya (member ban jata hai).</summary>
    public void Escalate(string reason, string? toName, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (EscalationLevel >= MaxEscalation)
            throw new DomainException($"This case is already at the highest escalation level ({MaxEscalation}).");
        var why = Guard.Required(reason, "Reason", 1000);
        EscalationLevel++;
        EscalatedAt = now;
        Log(CaseEntryKind.Escalated, Join(toName, why), false, byUserId, now);
    }

    public void AddMember(Guid employeeId, string name, CaseMemberRole role, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (!Enum.IsDefined(role) || role == CaseMemberRole.Owner)
            throw new DomainException("Add people as investigator or viewer. Use Change owner for the owner.");
        EnsureNoConflict(employeeId);
        var existing = members.FirstOrDefault(m => m.EmployeeId == employeeId);
        if (existing is not null)
        {
            if (existing.Role == CaseMemberRole.Owner)
                throw new DomainException("This person owns the case.");
            if (existing.Role == role)
                throw new DomainException("This person is already on the case.");
            existing.SetRole(role);
        }
        else
        {
            if (members.Count >= MaxMembers)
                throw new DomainException($"A case can have at most {MaxMembers} people.");
            members.Add(new CaseMember(Id, employeeId, role, now));
        }
        Log(CaseEntryKind.MemberAdded, $"{name} · {role}", false, byUserId, now);
    }

    public void RemoveMember(Guid employeeId, string name, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        var member = members.FirstOrDefault(m => m.EmployeeId == employeeId) ?? throw new DomainException("This person is not on the case.");
        if (member.Role == CaseMemberRole.Owner)
            throw new DomainException("The owner cannot be removed. Change the owner first.");
        members.Remove(member);
        Log(CaseEntryKind.MemberRemoved, name, false, byUserId, now);
    }

    /// <summary>Naya owner. Purana owner investigator ban jata hai (agar tha).</summary>
    public void ChangeOwner(Guid employeeId, string name, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (OwnerEmployeeId == employeeId)
            throw new DomainException("This person already owns the case.");
        EnsureNoConflict(employeeId);
        foreach (var old in members.Where(m => m.Role == CaseMemberRole.Owner))
            old.SetRole(CaseMemberRole.Investigator);
        var member = members.FirstOrDefault(m => m.EmployeeId == employeeId);
        if (member is null)
        {
            if (members.Count >= MaxMembers)
                throw new DomainException($"A case can have at most {MaxMembers} people.");
            members.Add(new CaseMember(Id, employeeId, CaseMemberRole.Owner, now));
        }
        else
            member.SetRole(CaseMemberRole.Owner);
        OwnerEmployeeId = employeeId;
        Log(CaseEntryKind.OwnerChanged, name, false, byUserId, now);
    }

    /// <summary>Admin ka emergency access (wajah ke saath). Viewer ban jata hai; timeline aur access log dono mein.</summary>
    public void BreakGlass(Guid employeeId, string name, string reason, Guid? byUserId, DateTime now)
    {
        var why = Guard.Required(reason, "Reason", 1000);
        if (members.Any(m => m.EmployeeId == employeeId))
            throw new DomainException("You are already on this case.");
        EnsureNoConflict(employeeId);
        members.Add(new CaseMember(Id, employeeId, CaseMemberRole.Viewer, now));
        Log(CaseEntryKind.BreakGlass, $"{name} · {why}", false, byUserId, now);
    }

    public CaseParty AddParty(Guid? employeeId, string? externalName, string displayName, CasePartyRole role, string? note, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (!Enum.IsDefined(role))
            throw new DomainException("Select the person's role in the case.");
        if (parties.Count >= MaxParties)
            throw new DomainException($"A case can have at most {MaxParties} people involved.");
        if (employeeId is { } id)
        {
            if (members.Any(m => m.EmployeeId == id))
                throw new DomainException("This person works on the case, so they cannot also be involved in it. Remove them from the team first.");
            if (parties.Any(p => p.EmployeeId == id))
                throw new DomainException("This person is already listed on the case.");
        }
        var party = new CaseParty(Id, employeeId, employeeId is null ? Guard.Required(externalName, "Name", 150) : null, role,
            Guard.Optional(note, "Note", 500), now);
        parties.Add(party);
        Log(CaseEntryKind.PartyAdded, $"{displayName} · {role}", false, byUserId, now);
        return party;
    }

    public void RemoveParty(Guid partyId, string displayName, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        var party = parties.FirstOrDefault(p => p.Id == partyId) ?? throw new DomainException("This person is not listed on the case.");
        parties.Remove(party);
        Log(CaseEntryKind.PartyRemoved, displayName, false, byUserId, now);
    }

    /// <summary>shared = reporter bhi dekhta hai (Message), warna sirf team (Note).</summary>
    public void AddNote(string text, bool shared, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (shared && ReporterEmployeeId is null)
            throw new DomainException("This case has no reporter to message.");
        Log(shared ? CaseEntryKind.Message : CaseEntryKind.Note, Guard.Required(text, "Note", 4000), shared, byUserId, now);
    }

    public void AddReporterMessage(string text, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        Log(CaseEntryKind.ReporterMessage, Guard.Required(text, "Message", 4000), true, byUserId, now);
    }

    public CaseEvidence AddEvidence(string title, string url, string? note, bool restricted, bool fromReporter, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        if (evidence.Count >= MaxEvidence)
            throw new DomainException($"A case can have at most {MaxEvidence} evidence items.");
        var item = new CaseEvidence(Id, Guard.Required(title, "Title", 200), Guard.OptionalUrl(url, "Link") ?? throw new DomainException("Link is required."),
            Guard.Optional(note, "Note", 500), restricted, fromReporter, byUserId, now);
        evidence.Add(item);
        Log(CaseEntryKind.EvidenceAdded, item.Title, fromReporter, byUserId, now);
        return item;
    }

    public void RemoveEvidence(Guid evidenceId, Guid? byUserId, DateTime now)
    {
        EnsureOpen();
        var item = evidence.FirstOrDefault(e => e.Id == evidenceId) ?? throw new DomainException("This evidence is not on the case.");
        evidence.Remove(item);
        Log(CaseEntryKind.EvidenceRemoved, item.Title, false, byUserId, now);
    }

    public CaseMemberRole? RoleOf(Guid? employeeId)
        => employeeId is { } id ? members.FirstOrDefault(m => m.EmployeeId == id)?.Role : null;

    /// <summary>Reporter, respondent ya koi bhi involved shakhs case par kaam nahi kar sakta.</summary>
    private void EnsureNoConflict(Guid employeeId)
    {
        if (employeeId == ReporterEmployeeId)
            throw new DomainException("The person who raised the case cannot work on it.");
        if (parties.Any(p => p.EmployeeId == employeeId))
            throw new DomainException("This person is involved in the case, so they cannot work on it.");
    }

    private void EnterStage(CaseStage stage, DateOnly today)
    {
        StageId = stage.Id;
        Phase = stage.Phase;
        if (stage.TargetDays is { } days)
            DueOn = today.AddDays(days);
    }

    private void EnsureOpen()
    {
        if (IsClosed)
            throw new DomainException("This case is closed. Reopen it to make changes.");
    }

    /// <summary>Ek hi request ki kai lines ka waqt 1 ms aagay (DB ms tak rakhta hai), taake timeline ki tarteeb pakki rahe.</summary>
    private DateTime? lastLoggedAt;

    private void Log(CaseEntryKind kind, string? text, bool shared, Guid? byUserId, DateTime now, Guid? stageId = null)
    {
        var at = lastLoggedAt is { } last && now <= last ? last.AddMilliseconds(1) : now;
        lastLoggedAt = at;
        entries.Add(new CaseEntry(Id, kind, text, shared, stageId, byUserId, at));
        LastActivityAt = at;
    }

    private static string? Join(string? a, string? b)
        => string.IsNullOrWhiteSpace(b) ? a : string.IsNullOrWhiteSpace(a) ? b : $"{a} · {b}";
}

public sealed record CaseDetails(
    string Title, string? Description, CaseType Type, CaseSeverity Severity, CaseConfidentiality Confidentiality, DateOnly? IncidentOn, string? Location)
{
    /// <summary>Self-service ke default: harassment/discrimination/whistleblowing restricted aur high.</summary>
    public static (CaseSeverity Severity, CaseConfidentiality Confidentiality) DefaultsFor(CaseType type) => type switch
    {
        CaseType.Harassment or CaseType.Whistleblowing => (CaseSeverity.High, CaseConfidentiality.Restricted),
        CaseType.Discrimination => (CaseSeverity.High, CaseConfidentiality.Restricted),
        _ => (CaseSeverity.Medium, CaseConfidentiality.Standard)
    };
}

public sealed class CaseMember : TenantChildEntity
{
    public Guid CaseId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public CaseMemberRole Role { get; private set; }
    public DateTime AddedAt { get; private set; }

    private CaseMember() { }

    internal CaseMember(Guid caseId, Guid employeeId, CaseMemberRole role, DateTime at)
    {
        CaseId = caseId;
        EmployeeId = employeeId;
        Role = role;
        AddedAt = at;
    }

    internal void SetRole(CaseMemberRole role) => Role = role;
}

/// <summary>Case mein involved shakhs. EmployeeId null = bahar ka (naam likha hua).</summary>
public sealed class CaseParty : TenantChildEntity
{
    public Guid CaseId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public string? ExternalName { get; private set; }
    public CasePartyRole Role { get; private set; }
    public string? Note { get; private set; }
    public DateTime AddedAt { get; private set; }

    private CaseParty() { }

    internal CaseParty(Guid caseId, Guid? employeeId, string? externalName, CasePartyRole role, string? note, DateTime at)
    {
        CaseId = caseId;
        EmployeeId = employeeId;
        ExternalName = externalName;
        Role = role;
        Note = note;
        AddedAt = at;
    }
}

/// <summary>Timeline ki ek line. Shared = reporter bhi dekhta hai.</summary>
public sealed class CaseEntry : TenantChildEntity
{
    public Guid CaseId { get; private set; }
    public CaseEntryKind Kind { get; private set; }
    public string? Text { get; private set; }
    public bool Shared { get; private set; }
    public Guid? StageId { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTime At { get; private set; }

    private CaseEntry() { }

    internal CaseEntry(Guid caseId, CaseEntryKind kind, string? text, bool shared, Guid? stageId, Guid? byUserId, DateTime at)
    {
        CaseId = caseId;
        Kind = kind;
        Text = text;
        Shared = shared;
        StageId = stageId;
        ByUserId = byUserId;
        At = at;
    }
}

/// <summary>Evidence ka link (file upload nahi). Restricted = sirf owner aur investigators.</summary>
public sealed class CaseEvidence : TenantChildEntity
{
    public Guid CaseId { get; private set; }
    public string Title { get; private set; } = default!;
    public string Url { get; private set; } = default!;
    public string? Note { get; private set; }
    public bool IsRestricted { get; private set; }
    public bool FromReporter { get; private set; }
    public Guid? AddedByUserId { get; private set; }
    public DateTime AddedAt { get; private set; }

    private CaseEvidence() { }

    internal CaseEvidence(Guid caseId, string title, string url, string? note, bool restricted, bool fromReporter, Guid? byUserId, DateTime at)
    {
        CaseId = caseId;
        Title = title;
        Url = url;
        Note = note;
        IsRestricted = restricted;
        FromReporter = fromReporter;
        AddedByUserId = byUserId;
        AddedAt = at;
    }
}

/// <summary>Kis ne kab case khola. Sirf likha jata hai (app role ke paas UPDATE/DELETE nahi).</summary>
public sealed class CaseAccessLog : TenantChildEntity
{
    public Guid CaseId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public string? UserName { get; private set; }
    public CaseAccessAction Action { get; private set; }
    public string? Note { get; private set; }
    public DateTime At { get; private set; }

    private CaseAccessLog() { }

    public CaseAccessLog(Guid caseId, Guid? userId, Guid? employeeId, string? userName, CaseAccessAction action, string? note, DateTime at)
    {
        CaseId = caseId;
        UserId = userId;
        EmployeeId = employeeId;
        UserName = userName is { Length: > 150 } ? userName[..150] : userName;
        Action = action;
        Note = note is { Length: > 1000 } ? note[..1000] : note;
        At = at;
    }
}
