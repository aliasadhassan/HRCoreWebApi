namespace HR.Employee.API.Domain.Policies;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Statutory kaam jo company ko waqt par karna hai (tax return, EOBI/social security contribution, labour licence renewal).
/// NextDueOn = agli due date; filing record karne par frequency ke hisaab se aage barhti hai (Once = filing ke baad mukammal).
/// DueDay mahine ka asal din yaad rakhta hai taake 31 Jan → 28 Feb → 31 Mar sahi chale.
/// </summary>
public sealed class ComplianceObligation : AuditableEntity
{
    private readonly List<ComplianceFiling> _filings = new();

    public string Title { get; private set; } = default!;
    public string? Authority { get; private set; }
    public ObligationCategory Category { get; private set; }
    public ObligationFrequency Frequency { get; private set; }
    public string? Description { get; private set; }
    public string? LinkUrl { get; private set; }
    public Guid? OwnerEmployeeId { get; private set; }
    public short RemindDaysBefore { get; private set; }
    public DateOnly? NextDueOn { get; private set; }
    public byte DueDay { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<ComplianceFiling> Filings => _filings.AsReadOnly();

    private ComplianceObligation() { }

    public static ComplianceObligation Create(Guid tenantId, ObligationDetails details)
    {
        var obligation = new ComplianceObligation { TenantId = Guard.NotEmpty(tenantId, "Tenant"), NextDueOn = DateOnly.MinValue };
        obligation.Update(details);
        return obligation;
    }

    /// <summary>NextDue sirf tab badalti hai jab kaam abhi baqi ho (mukammal Once obligation ki date nahi badalti).</summary>
    public void Update(ObligationDetails d)
    {
        if (!Enum.IsDefined(d.Category))
            throw new DomainException("Select a valid category.");
        if (!Enum.IsDefined(d.Frequency))
            throw new DomainException("Select how often this is due.");
        if (d.RemindDaysBefore is < 0 or > 90)
            throw new DomainException("Remind days must be between 0 and 90.");

        Title = Guard.Required(d.Title, "Title", 150);
        Authority = Guard.Optional(d.Authority, "Authority", 100);
        Category = d.Category;
        Description = Guard.Optional(d.Description, "Description", 1000);
        LinkUrl = Guard.OptionalUrl(d.LinkUrl, "Link");
        OwnerEmployeeId = d.OwnerEmployeeId == Guid.Empty ? null : d.OwnerEmployeeId;
        RemindDaysBefore = d.RemindDaysBefore;
        IsActive = d.IsActive;

        Frequency = d.Frequency;
        if (!IsCompleted)
            SetNextDue(d.NextDueOn ?? throw new DomainException("Set the next due date."));
        else if (d.Frequency != ObligationFrequency.Once && LastPeriodDueOn is { } last)
            NextDueOn = Advance(last); // ek dafa wala ab recurring: aakhri filing se agli date
    }

    public bool IsCompleted => NextDueOn is null;

    public DateOnly? LastPeriodDueOn => _filings.Count == 0 ? null : _filings.Max(f => f.PeriodDueOn);

    public ObligationState StateOn(DateOnly today)
    {
        if (NextDueOn is not { } due)
            return ObligationState.Completed;
        if (!IsActive)
            return ObligationState.Paused;
        if (due < today)
            return ObligationState.Overdue;
        return due <= today.AddDays(RemindDaysBefore) ? ObligationState.DueSoon : ObligationState.Upcoming;
    }

    /// <summary>Current due date ki filing. Aage ki date nahi chalti (filedOn ≤ aaj).</summary>
    public ComplianceFiling RecordFiling(DateOnly filedOn, string? reference, string? evidenceUrl, string? notes, Guid? byUserId, DateTime now)
    {
        if (NextDueOn is not { } due)
            throw new DomainException("This obligation is already complete.");
        if (!IsActive)
            throw new DomainException("This obligation is paused. Turn it back on first.");
        if (filedOn > DateOnly.FromDateTime(now))
            throw new DomainException("The filing date cannot be in the future.");

        var filing = new ComplianceFiling(Id, due, filedOn, Guard.Optional(reference, "Reference", 100),
            Guard.OptionalUrl(evidenceUrl, "Evidence link"), Guard.Optional(notes, "Notes", 1000), byUserId, now);
        _filings.Add(filing);
        NextDueOn = Frequency == ObligationFrequency.Once ? null : Advance(due);
        return filing;
    }

    /// <summary>Sirf aakhri filing wapas hoti hai; due date us period par laut aati hai.</summary>
    public void UndoFiling(ComplianceFiling filing)
    {
        if (!_filings.Contains(filing))
            throw new DomainException("Filing not found.");
        if (filing.PeriodDueOn != LastPeriodDueOn)
            throw new DomainException("Only the latest filing can be undone.");
        _filings.Remove(filing);
        NextDueOn = filing.PeriodDueOn;
    }

    /// <summary>Agli due date. Mahine ka din DueDay par wapas jata hai jahan mumkin ho.</summary>
    public DateOnly Advance(DateOnly from)
    {
        var months = Frequency switch
        {
            ObligationFrequency.Monthly => 1,
            ObligationFrequency.Quarterly => 3,
            ObligationFrequency.HalfYearly => 6,
            ObligationFrequency.Annually => 12,
            _ => 0
        };
        if (months == 0)
            return from;
        var next = from.AddMonths(months);
        var day = Math.Min(DueDay, (byte)DateTime.DaysInMonth(next.Year, next.Month));
        return new DateOnly(next.Year, next.Month, day);
    }

    private void SetNextDue(DateOnly next)
    {
        NextDueOn = next;
        DueDay = (byte)next.Day;
    }
}

public enum ObligationState : byte { Upcoming = 1, DueSoon = 2, Overdue = 3, Completed = 4, Paused = 5 }

public sealed record ObligationDetails(
    string Title, string? Authority, ObligationCategory Category, ObligationFrequency Frequency, string? Description,
    string? LinkUrl, Guid? OwnerEmployeeId, short RemindDaysBefore, DateOnly? NextDueOn, bool IsActive);

/// <summary>Ek due date ka kaam ho gaya: kab file kiya, reference (challan / receipt no.), saboot ka link.</summary>
public sealed class ComplianceFiling : TenantChildEntity
{
    public Guid ObligationId { get; private set; }
    public DateOnly PeriodDueOn { get; private set; }
    public DateOnly FiledOn { get; private set; }
    public string? Reference { get; private set; }
    public string? EvidenceUrl { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTime RecordedAt { get; private set; }

    private ComplianceFiling() { }

    internal ComplianceFiling(Guid obligationId, DateOnly periodDueOn, DateOnly filedOn, string? reference, string? evidenceUrl,
        string? notes, Guid? byUserId, DateTime at)
    {
        ObligationId = obligationId;
        PeriodDueOn = periodDueOn;
        FiledOn = filedOn;
        Reference = reference;
        EvidenceUrl = evidenceUrl;
        Notes = notes;
        ByUserId = byUserId;
        RecordedAt = at;
    }

    public bool IsLate => FiledOn > PeriodDueOn;
}
