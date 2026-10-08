namespace HR.Employee.API.Domain.Recruitment;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;

/// <summary>
/// Job opening / requisition. Manager Draft banata hai aur Submit karta hai, HR Approve karke Open karta hai
/// (HR seedha Draft se bhi khol sakta hai). Open par hi naye candidates lagte hain; Filled/Cancelled band.
/// Code REQ-0001 system deta hai.
/// </summary>
public sealed class JobOpening : AuditableEntity
{
    public const string CodePrefix = "REQ-";

    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public Guid DepartmentId { get; private set; }
    public Guid? DesignationId { get; private set; }
    public Guid? LocationId { get; private set; }
    public Guid? HiringManagerEmployeeId { get; private set; }
    public EmploymentType EmploymentType { get; private set; }
    public short Openings { get; private set; }
    public JobReason Reason { get; private set; }
    public Guid? ReplacesEmployeeId { get; private set; }
    public DateOnly? TargetStartDate { get; private set; }
    public decimal? SalaryMin { get; private set; }
    public decimal? SalaryMax { get; private set; }
    public string? Description { get; private set; }
    public string? Requirements { get; private set; }

    public JobStatus Status { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTime? OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public string? CloseNote { get; private set; }

    private JobOpening() { }

    public static JobOpening Create(Guid tenantId, string code, JobDetails details, Guid? requestedByUserId)
    {
        var job = new JobOpening
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Code = Guard.Required(code, "Code", 20).ToUpperInvariant(),
            Status = JobStatus.Draft,
            RequestedByUserId = requestedByUserId
        };
        job.Apply(details);
        return job;
    }

    public bool IsClosed => Status is JobStatus.Filled or JobStatus.Cancelled;

    /// <summary>Band hone ke baad kuch nahi badalta (pehle Reopen).</summary>
    public void Update(JobDetails details)
    {
        if (IsClosed)
            throw new DomainException("This job is closed. Reopen it first.");
        Apply(details);
    }

    private void Apply(JobDetails d)
    {
        if (d.Openings is < 1 or > 500)
            throw new DomainException("Openings must be between 1 and 500.");
        if (d.EmploymentType is not (EmploymentType.FullTime or EmploymentType.PartTime or EmploymentType.Contract or EmploymentType.Intern))
            throw new DomainException("Select an employment type.");
        if (!Enum.IsDefined(d.Reason))
            throw new DomainException("Select a reason.");
        if (d.SalaryMin is < 0 || d.SalaryMax is < 0)
            throw new DomainException("Salary cannot be negative.");
        if (d.SalaryMin is { } min && d.SalaryMax is { } max && max < min)
            throw new DomainException("Salary max cannot be less than salary min.");

        Title = Guard.Required(d.Title, "Title", 150);
        DepartmentId = Guard.NotEmpty(d.DepartmentId, "Department");
        DesignationId = d.DesignationId == Guid.Empty ? null : d.DesignationId;
        LocationId = d.LocationId == Guid.Empty ? null : d.LocationId;
        HiringManagerEmployeeId = d.HiringManagerEmployeeId == Guid.Empty ? null : d.HiringManagerEmployeeId;
        EmploymentType = d.EmploymentType;
        Openings = d.Openings;
        Reason = d.Reason;
        ReplacesEmployeeId = d.Reason == JobReason.Replacement && d.ReplacesEmployeeId != Guid.Empty ? d.ReplacesEmployeeId : null;
        TargetStartDate = d.TargetStartDate;
        SalaryMin = d.SalaryMin;
        SalaryMax = d.SalaryMax;
        Description = Guard.Optional(d.Description, "Description", 4000);
        Requirements = Guard.Optional(d.Requirements, "Requirements", 4000);
    }

    public void Submit(DateTime now)
    {
        if (Status != JobStatus.Draft)
            throw new DomainException("Only a draft requisition can be submitted.");
        Status = JobStatus.Submitted;
        SubmittedAt = now;
        ReviewNote = null;
    }

    /// <summary>HR approval: Submitted (ya HR ka apna Draft) seedha Open.</summary>
    public void Approve(Guid? byUserId, DateTime now)
    {
        if (Status is not (JobStatus.Draft or JobStatus.Submitted))
            throw new DomainException("This job is already approved.");
        Status = JobStatus.Open;
        ApprovedAt = now;
        ApprovedByUserId = byUserId;
        OpenedAt = now;
        ReviewNote = null;
    }

    /// <summary>Wapis Draft mein, wajah ke saath — manager theek karke dobara bhej sakta hai.</summary>
    public void SendBack(string note)
    {
        if (Status != JobStatus.Submitted)
            throw new DomainException("Only a submitted requisition can be sent back.");
        Status = JobStatus.Draft;
        SubmittedAt = null;
        ReviewNote = Guard.Required(note, "Note", 500);
    }

    public void Hold()
    {
        if (Status != JobStatus.Open)
            throw new DomainException("Only an open job can be put on hold.");
        Status = JobStatus.OnHold;
    }

    public void Resume()
    {
        if (Status != JobStatus.OnHold)
            throw new DomainException("This job is not on hold.");
        Status = JobStatus.Open;
    }

    public void Close(bool filled, string? note, DateTime now)
    {
        if (IsClosed)
            throw new DomainException("This job is already closed.");
        if (filled && Status is not (JobStatus.Open or JobStatus.OnHold))
            throw new DomainException("Only an open job can be marked filled.");
        Status = filled ? JobStatus.Filled : JobStatus.Cancelled;
        ClosedAt = now;
        CloseNote = Guard.Optional(note, "Note", 500);
    }

    /// <summary>Filled/Cancelled ko dobara kholna. Jo kabhi approve nahi hua wo Draft mein jata hai.</summary>
    public void Reopen()
    {
        if (!IsClosed)
            throw new DomainException("This job is not closed.");
        Status = ApprovedAt is null ? JobStatus.Draft : JobStatus.Open;
        ClosedAt = null;
        CloseNote = null;
    }

    public void EnsureAcceptsCandidates()
    {
        if (Status != JobStatus.Open)
            throw new DomainException("Candidates can only be added to an open job.");
    }

    public void EnsureActive()
    {
        if (Status is not (JobStatus.Open or JobStatus.OnHold))
            throw new DomainException(IsClosed ? "This job is closed." : "This job has not been approved yet.");
    }
}

public sealed record JobDetails(
    string Title, Guid DepartmentId, Guid? DesignationId, Guid? LocationId, Guid? HiringManagerEmployeeId,
    EmploymentType EmploymentType, short Openings, JobReason Reason, Guid? ReplacesEmployeeId, DateOnly? TargetStartDate,
    decimal? SalaryMin, decimal? SalaryMax, string? Description, string? Requirements);
