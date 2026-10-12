namespace HR.Employee.API.Domain.Policies;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Company policy (code of conduct, leave policy...). Draft → Published → Archived (Restore wapas).
/// Body/DocumentUrl = working copy jo HR edit karta hai; Publish usay naye PolicyVersion mein snapshot karta hai
/// aur employees hamesha current version parhte hain. Audience = poori company ya ek department.
/// Acknowledgement: AckVersion = aakhri version jis par sab ko dobara "parh liya" kehna hai; chhoti
/// correction bina re-acknowledge ke publish ho sakti hai (purani acknowledgements chalti rehti hain).
/// </summary>
public sealed class Policy : AuditableEntity
{
    public const int BodyMaxLength = 20000;

    private readonly List<PolicyVersion> _versions = new();

    public string Title { get; private set; } = default!;
    public PolicyCategory Category { get; private set; }
    public string? Summary { get; private set; }
    public string? Body { get; private set; }
    public string? DocumentUrl { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? OwnerEmployeeId { get; private set; }
    public bool RequiresAcknowledgement { get; private set; }
    public short? AcknowledgeWithinDays { get; private set; }
    public short? ReviewEveryMonths { get; private set; }

    public PolicyStatus Status { get; private set; }
    public int CurrentVersion { get; private set; }
    public bool HasUnpublishedChanges { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateOnly? EffectiveOn { get; private set; }
    public int? AckVersion { get; private set; }
    public DateOnly? AckDueOn { get; private set; }
    public DateOnly? LastReviewedOn { get; private set; }
    public DateOnly? NextReviewOn { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    public IReadOnlyCollection<PolicyVersion> Versions => _versions.AsReadOnly();

    private Policy() { }

    public static Policy Create(Guid tenantId, PolicyDetails details)
    {
        var policy = new Policy { TenantId = Guard.NotEmpty(tenantId, "Tenant"), Status = PolicyStatus.Draft };
        policy.Update(details, DateOnly.MinValue);
        policy.HasUnpublishedChanges = false;
        return policy;
    }

    /// <summary>Settings foran lagti hain; body/document ki tabdeeli agle Publish tak working copy mein rehti hai.</summary>
    public void Update(PolicyDetails d, DateOnly today)
    {
        EnsureNotArchived();
        if (!Enum.IsDefined(d.Category))
            throw new DomainException("Select a valid category.");
        if (d.AcknowledgeWithinDays is < 1 or > 365)
            throw new DomainException("Acknowledge within must be between 1 and 365 days.");
        if (d.ReviewEveryMonths is < 1 or > 60)
            throw new DomainException("Review every must be between 1 and 60 months.");

        var body = Guard.Optional(d.Body, "Policy text", BodyMaxLength);
        var url = Guard.OptionalUrl(d.DocumentUrl, "Document link");
        HasUnpublishedChanges |= body != Body || url != DocumentUrl;

        Title = Guard.Required(d.Title, "Title", 150);
        Category = d.Category;
        Summary = Guard.Optional(d.Summary, "Summary", 500);
        Body = body;
        DocumentUrl = url;
        DepartmentId = NullIfEmpty(d.DepartmentId);
        OwnerEmployeeId = NullIfEmpty(d.OwnerEmployeeId);
        SetAcknowledgement(d.RequiresAcknowledgement, d.AcknowledgeWithinDays, today);
        SetReviewCycle(d.ReviewEveryMonths);
    }

    private void SetAcknowledgement(bool required, short? withinDays, DateOnly today)
    {
        RequiresAcknowledgement = required;
        AcknowledgeWithinDays = required ? withinDays : null;
        if (!required)
        {
            AckVersion = null;
            AckDueOn = null;
        }
        else if (AckVersion is null && CurrentVersion > 0)
        {
            // Pehle acknowledgement nahi chahiye thi, ab chahiye: current version se shuru
            AckVersion = CurrentVersion;
            AckDueOn = DueFrom(today);
        }
    }

    private void SetReviewCycle(short? months)
    {
        if (months == ReviewEveryMonths)
            return;
        ReviewEveryMonths = months;
        var from = LastReviewedOn;
        NextReviewOn = from is { } f && months is { } m ? f.AddMonths(m) : null;
    }

    private static Guid? NullIfEmpty(Guid? id) => id == Guid.Empty ? null : id;

    /// <summary>
    /// Working copy ko naya version banata hai. reacknowledge = sab ko dobara acknowledge karna hoga
    /// (pehla version hamesha). Pehli baar ke baad bina tabdeeli dobara publish nahi hota.
    /// </summary>
    public PolicyVersion Publish(string? changeSummary, bool reacknowledge, DateOnly? effectiveOn, Guid? byUserId, DateTime now)
    {
        EnsureNotArchived();
        if (Body is null && DocumentUrl is null)
            throw new DomainException("Add the policy text or a document link before publishing.");
        if (CurrentVersion > 0 && !HasUnpublishedChanges)
            throw new DomainException("Nothing has changed since the last published version.");
        var summary = Guard.Optional(changeSummary, "What changed", 500);
        if (CurrentVersion > 0 && summary is null)
            throw new DomainException("Say briefly what changed so employees know what to look for.");

        var today = DateOnly.FromDateTime(now);
        var first = CurrentVersion == 0;
        CurrentVersion++;
        Status = PolicyStatus.Published;
        HasUnpublishedChanges = false;
        PublishedAt = now;
        EffectiveOn = effectiveOn ?? today;
        if (RequiresAcknowledgement && (first || reacknowledge || AckVersion is null))
        {
            AckVersion = CurrentVersion;
            AckDueOn = DueFrom(today);
        }
        if (first || ReviewEveryMonths is not null)
        {
            LastReviewedOn = today;
            NextReviewOn = ReviewEveryMonths is { } m ? today.AddMonths(m) : null;
        }

        var version = new PolicyVersion(Id, CurrentVersion, Title, Body, DocumentUrl, summary, EffectiveOn.Value,
            AckVersion == CurrentVersion, byUserId, now);
        _versions.Add(version);
        return version;
    }

    /// <summary>HR ne policy dekh li, abhi theek hai: agli review date aage.</summary>
    public void MarkReviewed(DateOnly today)
    {
        if (Status != PolicyStatus.Published)
            throw new DomainException("Only a published policy can be marked as reviewed.");
        LastReviewedOn = today;
        NextReviewOn = ReviewEveryMonths is { } m ? today.AddMonths(m) : null;
    }

    public void Archive(DateTime now)
    {
        EnsureNotArchived();
        Status = PolicyStatus.Archived;
        ArchivedAt = now;
    }

    public void Restore()
    {
        if (Status != PolicyStatus.Archived)
            throw new DomainException("Only an archived policy can be restored.");
        Status = CurrentVersion > 0 ? PolicyStatus.Published : PolicyStatus.Draft;
        ArchivedAt = null;
    }

    public bool NeedsAcknowledgement => Status == PolicyStatus.Published && AckVersion is not null;

    public bool IsReviewDue(DateOnly today) => Status == PolicyStatus.Published && NextReviewOn is { } next && next <= today;

    private DateOnly? DueFrom(DateOnly today) => AcknowledgeWithinDays is { } days ? today.AddDays(days) : null;

    private void EnsureNotArchived()
    {
        if (Status == PolicyStatus.Archived)
            throw new DomainException("This policy is archived. Restore it first.");
    }
}

public sealed record PolicyDetails(
    string Title, PolicyCategory Category, string? Summary, string? Body, string? DocumentUrl, Guid? DepartmentId,
    Guid? OwnerEmployeeId, bool RequiresAcknowledgement, short? AcknowledgeWithinDays, short? ReviewEveryMonths);

/// <summary>Publish ka snapshot (append-only). Employees current version parhte hain; purane history ke liye.</summary>
public sealed class PolicyVersion : TenantChildEntity
{
    public Guid PolicyId { get; private set; }
    public int Number { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Body { get; private set; }
    public string? DocumentUrl { get; private set; }
    public string? ChangeSummary { get; private set; }
    public DateOnly EffectiveOn { get; private set; }
    public bool RequiresAcknowledgement { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTime PublishedAt { get; private set; }

    private PolicyVersion() { }

    internal PolicyVersion(Guid policyId, int number, string title, string? body, string? documentUrl, string? changeSummary,
        DateOnly effectiveOn, bool requiresAcknowledgement, Guid? byUserId, DateTime at)
    {
        PolicyId = policyId;
        Number = number;
        Title = title;
        Body = body;
        DocumentUrl = documentUrl;
        ChangeSummary = changeSummary;
        EffectiveOn = effectiveOn;
        RequiresAcknowledgement = requiresAcknowledgement;
        PublishedByUserId = byUserId;
        PublishedAt = at;
    }
}

/// <summary>Employee ne policy ka ek version parh kar maan liya (append-only, ek version par ek dafa).</summary>
public sealed class PolicyAcknowledgement : TenantChildEntity
{
    public Guid PolicyId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTime AcknowledgedAt { get; private set; }

    private PolicyAcknowledgement() { }

    public static PolicyAcknowledgement Create(Policy policy, Guid employeeId, Guid? byUserId, DateTime now)
    {
        if (policy.Status != PolicyStatus.Published)
            throw new DomainException("This policy is not published.");
        if (!policy.NeedsAcknowledgement)
            throw new DomainException("This policy does not need an acknowledgement.");
        return new PolicyAcknowledgement
        {
            PolicyId = policy.Id,
            VersionNumber = policy.CurrentVersion,
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            ByUserId = byUserId,
            AcknowledgedAt = now
        };
    }
}
