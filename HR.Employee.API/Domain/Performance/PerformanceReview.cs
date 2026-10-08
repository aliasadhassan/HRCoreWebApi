namespace HR.Employee.API.Domain.Performance;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Ek employee ka ek cycle mein review. Self review sirf employee likhta hai; manager review reviewer (ya HR).
/// Employee ko manager wala hissa sirf Shared ke baad dikhta hai.
/// </summary>
public sealed class PerformanceReview : AuditableEntity
{
    public const byte MinRating = 1;
    public const byte MaxRating = 5;

    public Guid ReviewCycleId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid? ReviewerEmployeeId { get; private set; }
    public ReviewStatus Status { get; private set; }

    public byte? SelfRating { get; private set; }
    public string? SelfSummary { get; private set; }
    public DateTime? SelfSubmittedAt { get; private set; }

    public byte? ManagerRating { get; private set; }
    public string? ManagerSummary { get; private set; }
    public string? Strengths { get; private set; }
    public string? Improvements { get; private set; }
    public DateTime? ManagerSubmittedAt { get; private set; }
    public Guid? ManagerSubmittedByUserId { get; private set; }

    public string? EmployeeComment { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }

    private PerformanceReview() { }

    public static PerformanceReview Create(Guid tenantId, ReviewCycle cycle, Guid employeeId, Guid? reviewerEmployeeId)
    {
        if (reviewerEmployeeId == employeeId)
            reviewerEmployeeId = null;
        return new PerformanceReview
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            ReviewCycleId = cycle.Id,
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            ReviewerEmployeeId = reviewerEmployeeId,
            Status = cycle.FirstStage
        };
    }

    public bool HasStarted => SelfRating is not null || SelfSummary is not null || ManagerRating is not null || ManagerSummary is not null;

    public void SaveSelf(byte? rating, string? summary, bool submit, DateTime now)
    {
        if (Status != ReviewStatus.SelfReview)
            throw new DomainException("The self review has already been submitted.");
        CheckRating(rating);
        var text = Guard.Optional(summary, "Summary", 4000);
        if (submit && (rating is null || text is null))
            throw new DomainException("Add a rating and a summary before submitting.");

        SelfRating = rating;
        SelfSummary = text;
        if (submit)
        {
            SelfSubmittedAt = now;
            Status = ReviewStatus.ManagerReview;
        }
    }

    /// <summary>Manager draft kabhi bhi save kar sakta hai; submit tab jab self review ho chuka ho (ya cycle mein self review na ho).</summary>
    public void SaveManager(byte? rating, string? summary, string? strengths, string? improvements, bool submit, Guid? byUserId, DateTime now)
    {
        if (Status is ReviewStatus.Shared or ReviewStatus.Acknowledged)
            throw new DomainException("This review has already been shared. Reopen it to make changes.");
        CheckRating(rating);
        var text = Guard.Optional(summary, "Summary", 4000);
        if (submit)
        {
            if (Status == ReviewStatus.SelfReview)
                throw new DomainException("Wait for the self review before sharing the manager review.");
            if (rating is null || text is null)
                throw new DomainException("Add a rating and a summary before sharing.");
        }

        ManagerRating = rating;
        ManagerSummary = text;
        Strengths = Guard.Optional(strengths, "Strengths", 2000);
        Improvements = Guard.Optional(improvements, "Areas to improve", 2000);
        if (submit)
        {
            ManagerSubmittedAt = now;
            ManagerSubmittedByUserId = byUserId;
            Status = ReviewStatus.Shared;
        }
    }

    /// <summary>HR shared review ko wapis manager ke paas bhejta hai (acknowledgement bhi hat jata hai).</summary>
    public void Reopen()
    {
        if (Status is not (ReviewStatus.Shared or ReviewStatus.Acknowledged))
            throw new DomainException("Only a shared review can be reopened.");
        Status = ReviewStatus.ManagerReview;
        ManagerSubmittedAt = null;
        ManagerSubmittedByUserId = null;
        AcknowledgedAt = null;
        EmployeeComment = null;
    }

    /// <summary>Self review khulwana (employee ne galti se submit kiya) — sirf jab manager ne share nahi kiya.</summary>
    public void ReopenSelf(ReviewCycle cycle)
    {
        if (!cycle.IncludeSelfReview)
            throw new DomainException("This cycle has no self review.");
        if (Status != ReviewStatus.ManagerReview || SelfSubmittedAt is null)
            throw new DomainException("Only a submitted self review can be reopened before the manager shares the review.");
        Status = ReviewStatus.SelfReview;
        SelfSubmittedAt = null;
    }

    public void Acknowledge(string? comment, DateTime now)
    {
        if (Status == ReviewStatus.Acknowledged)
            return;
        if (Status != ReviewStatus.Shared)
            throw new DomainException("This review has not been shared with you yet.");
        EmployeeComment = Guard.Optional(comment, "Comment", 2000);
        AcknowledgedAt = now;
        Status = ReviewStatus.Acknowledged;
    }

    public void ChangeReviewer(Guid? reviewerEmployeeId)
    {
        if (reviewerEmployeeId == EmployeeId)
            throw new DomainException("An employee cannot review themselves.");
        if (Status is ReviewStatus.Shared or ReviewStatus.Acknowledged)
            throw new DomainException("This review has already been shared.");
        ReviewerEmployeeId = reviewerEmployeeId == Guid.Empty ? null : reviewerEmployeeId;
    }

    private static void CheckRating(byte? rating)
    {
        if (rating is { } r && (r < MinRating || r > MaxRating))
            throw new DomainException("Rating must be between 1 and 5.");
    }
}
