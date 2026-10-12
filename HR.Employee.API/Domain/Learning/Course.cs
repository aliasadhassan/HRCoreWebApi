namespace HR.Employee.API.Domain.Learning;

using HR.Employee.API.Domain.Common;

public sealed record CourseDetails(
    string Title, CourseCategory Category, CourseFormat Format, string? Provider, string? Description, string? LinkUrl,
    decimal? DurationHours, Guid? SkillId, byte? SkillLevel, bool AllowSelfEnrol, short? CompleteWithinDays, bool IsActive);

/// <summary>
/// Catalogue ka course. SkillId + SkillLevel diye hon to course mukammal karne par employee ka us skill ka level
/// kam az kam SkillLevel ho jata hai. CompleteWithinDays = assign/enrol par default due date.
/// </summary>
public sealed class Course : AuditableEntity
{
    public const byte MaxLevel = 5;

    public string Title { get; private set; } = default!;
    public CourseCategory Category { get; private set; }
    public CourseFormat Format { get; private set; }
    public string? Provider { get; private set; }
    public string? Description { get; private set; }
    public string? LinkUrl { get; private set; }
    public decimal? DurationHours { get; private set; }
    public Guid? SkillId { get; private set; }
    public byte? SkillLevel { get; private set; }
    public bool AllowSelfEnrol { get; private set; }
    public short? CompleteWithinDays { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Course() { }

    public static Course Create(Guid tenantId, CourseDetails details)
    {
        var course = new Course { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        course.Update(details);
        return course;
    }

    public void Update(CourseDetails d)
    {
        if (!Enum.IsDefined(d.Category))
            throw new DomainException("Select a valid category.");
        if (!Enum.IsDefined(d.Format))
            throw new DomainException("Select a valid format.");
        if (d.DurationHours is { } h && (h <= 0 || h > 1000))
            throw new DomainException("Duration must be between 0.1 and 1000 hours.");
        if (d.CompleteWithinDays is < 1 or > 365)
            throw new DomainException("Complete within must be between 1 and 365 days.");

        var skillId = d.SkillId == Guid.Empty ? null : d.SkillId;
        if (skillId is null && d.SkillLevel is not null)
            throw new DomainException("Pick the skill this course builds.");
        if (d.SkillLevel is 0 or > MaxLevel)
            throw new DomainException("Skill level must be between 1 and 5.");

        Title = Guard.Required(d.Title, "Title", 150);
        Category = d.Category;
        Format = d.Format;
        Provider = Guard.Optional(d.Provider, "Provider", 100);
        Description = Guard.Optional(d.Description, "Description", 2000);
        LinkUrl = Guard.OptionalUrl(d.LinkUrl, "Link");
        DurationHours = d.DurationHours is { } hours ? Math.Round(hours, 1, MidpointRounding.AwayFromZero) : null;
        SkillId = skillId;
        SkillLevel = skillId is null ? null : d.SkillLevel;
        AllowSelfEnrol = d.AllowSelfEnrol;
        CompleteWithinDays = d.CompleteWithinDays;
        IsActive = d.IsActive;
    }

    public DateOnly? DefaultDueOn(DateOnly today) => CompleteWithinDays is { } days ? today.AddDays(days) : null;
}

/// <summary>
/// Ek employee ka ek course: HR/manager ne assign kiya ya khud enrol hua. Ek waqt mein ek hi khula (NotStarted/InProgress)
/// enrollment per course per employee (partial unique index). Complete/Cancel ke baad naya ban sakta hai (refresher).
/// </summary>
public sealed class CourseEnrollment : AuditableEntity
{
    public Guid CourseId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public EnrollmentSource Source { get; private set; }
    public EnrollmentStatus Status { get; private set; }
    public byte Progress { get; private set; }
    public DateOnly? DueOn { get; private set; }
    public Guid? AssignedByUserId { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateOnly? CompletedOn { get; private set; }
    public byte? Score { get; private set; }
    public byte? Rating { get; private set; }
    public string? Feedback { get; private set; }
    public string? CertificateUrl { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    private CourseEnrollment() { }

    public bool IsOpen => Status is EnrollmentStatus.NotStarted or EnrollmentStatus.InProgress;

    public bool IsOverdueOn(DateOnly today) => IsOpen && DueOn is { } due && due < today;

    public static CourseEnrollment Create(Course course, Guid employeeId, EnrollmentSource source, DateOnly? dueOn, Guid? byUserId, DateOnly today)
    {
        if (!course.IsActive)
            throw new DomainException("This course is not active.");
        if (!Enum.IsDefined(source))
            throw new DomainException("Select a valid source.");
        if (source == EnrollmentSource.Self && !course.AllowSelfEnrol)
            throw new DomainException("This course is assigned by HR or your manager. You cannot enrol yourself.");
        var due = dueOn ?? course.DefaultDueOn(today);
        if (due is { } d && d < today)
            throw new DomainException("Due date cannot be in the past.");

        return new CourseEnrollment
        {
            TenantId = course.TenantId,
            CourseId = course.Id,
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            Source = source,
            Status = EnrollmentStatus.NotStarted,
            DueOn = due,
            AssignedByUserId = source == EnrollmentSource.Assigned ? byUserId : null
        };
    }

    /// <summary>0-99. 100 ke liye Complete chalayein (completion date aur feedback ke sath).</summary>
    public void SetProgress(byte progress, DateTime now)
    {
        EnsureOpen();
        if (progress > 99)
            throw new DomainException("Progress must be between 0 and 99. Mark the course complete to finish it.");
        Progress = progress;
        if (progress > 0)
        {
            Status = EnrollmentStatus.InProgress;
            StartedAt ??= now;
        }
        else if (StartedAt is null)
        {
            Status = EnrollmentStatus.NotStarted;
        }
    }

    public void Complete(DateOnly completedOn, byte? score, byte? rating, string? feedback, string? certificateUrl, DateOnly today, DateTime now)
    {
        EnsureOpen();
        if (completedOn > today)
            throw new DomainException("Completion date cannot be in the future.");
        if (score is > 100)
            throw new DomainException("Score must be between 0 and 100.");
        if (rating is 0 or > 5)
            throw new DomainException("Rating must be between 1 and 5.");

        Feedback = Guard.Optional(feedback, "Feedback", 1000);
        CertificateUrl = Guard.OptionalUrl(certificateUrl, "Certificate link");
        Status = EnrollmentStatus.Completed;
        Progress = 100;
        StartedAt ??= now;
        CompletedOn = completedOn;
        Score = score;
        Rating = rating;
    }

    public void Cancel(DateTime now)
    {
        EnsureOpen();
        Status = EnrollmentStatus.Cancelled;
        CancelledAt = now;
    }

    public void ChangeDue(DateOnly? dueOn, DateOnly today)
    {
        EnsureOpen();
        if (dueOn is { } d && d < today)
            throw new DomainException("Due date cannot be in the past.");
        DueOn = dueOn;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new DomainException("This enrollment is already closed.");
    }
}
