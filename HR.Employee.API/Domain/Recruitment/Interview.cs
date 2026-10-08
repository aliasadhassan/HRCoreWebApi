namespace HR.Employee.API.Domain.Recruitment;

using HR.Employee.API.Domain.Common;

/// <summary>Application ka ek interview round. Interviewer feedback deta hai (rating 1-5 + recommendation).</summary>
public sealed class Interview : AuditableEntity
{
    public Guid JobApplicationId { get; private set; }
    public string Title { get; private set; } = default!;
    public DateTime ScheduledAt { get; private set; }
    public short DurationMinutes { get; private set; }
    public InterviewMode Mode { get; private set; }
    public string? LocationOrLink { get; private set; }
    public Guid InterviewerEmployeeId { get; private set; }
    public InterviewStatus Status { get; private set; }
    public byte? Rating { get; private set; }
    public InterviewRecommendation? Recommendation { get; private set; }
    public string? Feedback { get; private set; }
    public DateTime? FeedbackAt { get; private set; }

    private Interview() { }

    public static Interview Create(Guid tenantId, Guid applicationId, InterviewDetails details)
    {
        var interview = new Interview
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            JobApplicationId = Guard.NotEmpty(applicationId, "Application"),
            Status = InterviewStatus.Scheduled
        };
        interview.Apply(details);
        return interview;
    }

    public void Update(InterviewDetails details)
    {
        if (Status != InterviewStatus.Scheduled)
            throw new DomainException("Only a scheduled interview can be changed.");
        Apply(details);
    }

    private void Apply(InterviewDetails d)
    {
        if (d.DurationMinutes is < 5 or > 480)
            throw new DomainException("Duration must be between 5 and 480 minutes.");
        if (!Enum.IsDefined(d.Mode))
            throw new DomainException("Select how the interview happens.");

        Title = Guard.Required(d.Title, "Title", 100);
        ScheduledAt = DateTime.SpecifyKind(d.ScheduledAt.ToUniversalTime(), DateTimeKind.Utc);
        DurationMinutes = d.DurationMinutes;
        Mode = d.Mode;
        LocationOrLink = Guard.Optional(d.LocationOrLink, "Location or link", 500);
        InterviewerEmployeeId = Guard.NotEmpty(d.InterviewerEmployeeId, "Interviewer");
    }

    /// <summary>Feedback = interview ho gaya. Dobara bhejne se feedback update hota hai.</summary>
    public void SubmitFeedback(byte rating, InterviewRecommendation recommendation, string? feedback, DateTime now)
    {
        if (Status is InterviewStatus.Cancelled or InterviewStatus.NoShow)
            throw new DomainException("This interview did not take place.");
        if (rating is < 1 or > 5)
            throw new DomainException("Rating must be between 1 and 5.");
        if (!Enum.IsDefined(recommendation))
            throw new DomainException("Select a recommendation.");

        Rating = rating;
        Recommendation = recommendation;
        Feedback = Guard.Optional(feedback, "Feedback", 4000);
        FeedbackAt = now;
        Status = InterviewStatus.Completed;
    }

    public void Cancel()
    {
        if (Status != InterviewStatus.Scheduled)
            throw new DomainException("Only a scheduled interview can be cancelled.");
        Status = InterviewStatus.Cancelled;
    }

    public void MarkNoShow()
    {
        if (Status != InterviewStatus.Scheduled)
            throw new DomainException("Only a scheduled interview can be marked as a no-show.");
        Status = InterviewStatus.NoShow;
    }
}

public sealed record InterviewDetails(string Title, DateTime ScheduledAt, short DurationMinutes, InterviewMode Mode, string? LocationOrLink, Guid InterviewerEmployeeId);
