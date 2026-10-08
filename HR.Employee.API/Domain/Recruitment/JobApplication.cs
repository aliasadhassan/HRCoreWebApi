namespace HR.Employee.API.Domain.Recruitment;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Candidate x job. Stages: Applied → Screening → Interview → Offer → Hired (ya Rejected / Withdrawn).
/// Offer aur Hire ke apne methods hain; har badlaav ApplicationEvents mein history chhodta hai.
/// </summary>
public sealed class JobApplication : AuditableEntity
{
    private readonly List<ApplicationEvent> _events = new();

    public Guid JobOpeningId { get; private set; }
    public Guid CandidateId { get; private set; }
    public ApplicationStage Stage { get; private set; }
    public DateTime AppliedAt { get; private set; }
    public DateTime StageChangedAt { get; private set; }
    public byte? Rating { get; private set; }
    public string? RejectReason { get; private set; }

    public decimal? OfferSalary { get; private set; }
    public DateOnly? OfferStartDate { get; private set; }
    public DateOnly? OfferExpiresOn { get; private set; }
    public OfferStatus? OfferStatus { get; private set; }
    public DateTime? OfferedAt { get; private set; }

    public DateTime? HiredAt { get; private set; }
    public Guid? HiredEmployeeId { get; private set; }

    public IReadOnlyCollection<ApplicationEvent> Events => _events.AsReadOnly();

    private JobApplication() { }

    public static JobApplication Create(Guid tenantId, JobOpening job, Guid candidateId, Guid? byUserId, DateTime now)
    {
        job.EnsureAcceptsCandidates();
        var app = new JobApplication
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            JobOpeningId = job.Id,
            CandidateId = Guard.NotEmpty(candidateId, "Candidate"),
            Stage = ApplicationStage.Applied,
            AppliedAt = now,
            StageChangedAt = now
        };
        app.Log(ApplicationEventKind.Applied, null, null, byUserId, now);
        return app;
    }

    public bool IsActive => Stage is ApplicationStage.Applied or ApplicationStage.Screening or ApplicationStage.Interview or ApplicationStage.Offer;

    /// <summary>Pipeline mein aage/peeche ya Reject/Withdraw. Offer aur Hired yahan se nahi (MakeOffer / Hire).</summary>
    public void MoveTo(ApplicationStage stage, string? note, Guid? byUserId, DateTime now)
    {
        if (!Enum.IsDefined(stage))
            throw new DomainException("Select a valid stage.");
        if (Stage == ApplicationStage.Hired)
            throw new DomainException("This candidate has already been hired.");
        if (stage == Stage)
            throw new DomainException("The candidate is already in this stage.");
        if (stage == ApplicationStage.Offer)
            throw new DomainException("Use Make offer to move a candidate to the offer stage.");
        if (stage == ApplicationStage.Hired)
            throw new DomainException("Use Hire to complete a hire.");

        var text = Guard.Optional(note, "Note", 1000);
        if (stage == ApplicationStage.Rejected && text is null)
            throw new DomainException("Add a reason for rejecting the candidate.");

        var from = Stage;
        Stage = stage;
        StageChangedAt = now;
        RejectReason = stage == ApplicationStage.Rejected ? text : null;
        // Offer stage chhodne par pending offer ka koi matlab nahi
        if (from == ApplicationStage.Offer && OfferStatus == Recruitment.OfferStatus.Pending)
            OfferStatus = null;
        Log(ApplicationEventKind.Stage, from, text, byUserId, now);
    }

    public void MakeOffer(decimal? salary, DateOnly? startDate, DateOnly? expiresOn, string? note, Guid? byUserId, DateTime now)
    {
        if (Stage is not (ApplicationStage.Screening or ApplicationStage.Interview or ApplicationStage.Offer or ApplicationStage.Applied))
            throw new DomainException("An offer can only be made to a candidate in the pipeline.");
        if (salary is < 0)
            throw new DomainException("Salary cannot be negative.");
        if (expiresOn is { } exp && exp < DateOnly.FromDateTime(now))
            throw new DomainException("The offer cannot expire in the past.");

        var from = Stage;
        OfferSalary = salary;
        OfferStartDate = startDate;
        OfferExpiresOn = expiresOn;
        OfferStatus = Recruitment.OfferStatus.Pending;
        OfferedAt = now;
        Stage = ApplicationStage.Offer;
        StageChangedAt = now;
        RejectReason = null;
        Log(ApplicationEventKind.Offer, from, Guard.Optional(note, "Note", 1000), byUserId, now);
    }

    /// <summary>Candidate ka jawab. Inkaar = Withdrawn.</summary>
    public void RecordOfferResponse(bool accepted, string? note, Guid? byUserId, DateTime now)
    {
        if (Stage != ApplicationStage.Offer || OfferStatus != Recruitment.OfferStatus.Pending)
            throw new DomainException("There is no pending offer.");

        OfferStatus = accepted ? Recruitment.OfferStatus.Accepted : Recruitment.OfferStatus.Declined;
        var text = Guard.Optional(note, "Note", 1000);
        if (!accepted)
        {
            Stage = ApplicationStage.Withdrawn;
            StageChangedAt = now;
        }
        Log(ApplicationEventKind.OfferResponse, ApplicationStage.Offer, text, byUserId, now);
    }

    /// <summary>Employee banane se pehle jaanch (Hire bhi yahi dobara karta hai).</summary>
    public void EnsureCanHire()
    {
        if (Stage == ApplicationStage.Hired)
            throw new DomainException("This candidate has already been hired.");
        if (Stage != ApplicationStage.Offer)
            throw new DomainException("Make an offer before hiring the candidate.");
        if (OfferStatus == Recruitment.OfferStatus.Declined)
            throw new DomainException("The candidate declined the offer.");
    }

    public void Hire(Guid employeeId, Guid? byUserId, DateTime now)
    {
        EnsureCanHire();

        OfferStatus = Recruitment.OfferStatus.Accepted;
        Stage = ApplicationStage.Hired;
        StageChangedAt = now;
        HiredAt = now;
        HiredEmployeeId = Guard.NotEmpty(employeeId, "Employee");
        Log(ApplicationEventKind.Hired, ApplicationStage.Offer, null, byUserId, now);
    }

    public void Rate(byte? rating)
    {
        if (rating is < 1 or > 5)
            throw new DomainException("Rating must be between 1 and 5.");
        Rating = rating;
    }

    public void AddNote(string note, Guid? byUserId, DateTime now)
        => Log(ApplicationEventKind.Note, Stage, Guard.Required(note, "Note", 1000), byUserId, now);

    private void Log(ApplicationEventKind kind, ApplicationStage? from, string? note, Guid? byUserId, DateTime now)
        => _events.Add(new ApplicationEvent(Id, kind, from, Stage, note, byUserId, now));
}

/// <summary>Application ki history (append-only).</summary>
public sealed class ApplicationEvent : TenantChildEntity
{
    public Guid JobApplicationId { get; private set; }
    public ApplicationEventKind Kind { get; private set; }
    public ApplicationStage? FromStage { get; private set; }
    public ApplicationStage ToStage { get; private set; }
    public string? Note { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTime At { get; private set; }

    private ApplicationEvent() { }

    internal ApplicationEvent(Guid applicationId, ApplicationEventKind kind, ApplicationStage? from, ApplicationStage to, string? note, Guid? byUserId, DateTime at)
    {
        JobApplicationId = applicationId;
        Kind = kind;
        FromStage = from;
        ToStage = to;
        Note = note;
        ByUserId = byUserId;
        At = at;
    }
}
