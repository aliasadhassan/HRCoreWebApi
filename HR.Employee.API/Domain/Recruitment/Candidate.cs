namespace HR.Employee.API.Domain.Recruitment;

using HR.Employee.API.Domain.Common;

/// <summary>Candidate (talent pool). Ek candidate kai jobs par apply ho sakta hai. Email tenant mein unique.</summary>
public sealed class Candidate : AuditableEntity
{
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string? City { get; private set; }
    public string? CurrentCompany { get; private set; }
    public string? CurrentTitle { get; private set; }
    public decimal? ExperienceYears { get; private set; }
    public CandidateSource Source { get; private set; }
    public Guid? ReferredByEmployeeId { get; private set; }
    public string? ResumeUrl { get; private set; }
    public string? LinkedInUrl { get; private set; }
    public string? Notes { get; private set; }

    private Candidate() { }

    public static Candidate Create(Guid tenantId, CandidateDetails details)
    {
        var candidate = new Candidate { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        candidate.Update(details);
        return candidate;
    }

    public string FullName => $"{FirstName} {LastName}";

    public void Update(CandidateDetails d)
    {
        if (!Enum.IsDefined(d.Source))
            throw new DomainException("Select a source.");
        if (d.ExperienceYears is < 0 or > 60)
            throw new DomainException("Experience must be between 0 and 60 years.");

        FirstName = Guard.Required(d.FirstName, "First name", 100);
        LastName = Guard.Required(d.LastName, "Last name", 100);
        Email = Guard.Email(d.Email).ToLowerInvariant();
        Phone = Guard.Optional(d.Phone, "Phone", 30);
        City = Guard.Optional(d.City, "City", 100);
        CurrentCompany = Guard.Optional(d.CurrentCompany, "Current company", 150);
        CurrentTitle = Guard.Optional(d.CurrentTitle, "Current title", 150);
        ExperienceYears = d.ExperienceYears;
        Source = d.Source;
        ReferredByEmployeeId = d.Source == CandidateSource.Referral && d.ReferredByEmployeeId != Guid.Empty ? d.ReferredByEmployeeId : null;
        ResumeUrl = Link(d.ResumeUrl, "Resume link", 1000);
        LinkedInUrl = Link(d.LinkedInUrl, "LinkedIn link", 500);
        Notes = Guard.Optional(d.Notes, "Notes", 2000);
    }

    /// <summary>Sirf http(s) links — javascript: jaise links UI mein click na hon.</summary>
    private static string? Link(string? value, string field, int max)
    {
        var link = Guard.Optional(value, field, max);
        if (link is null)
            return null;
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new DomainException($"{field} must start with https://");
        return link;
    }
}

public sealed record CandidateDetails(
    string FirstName, string LastName, string Email, string? Phone, string? City, string? CurrentCompany, string? CurrentTitle,
    decimal? ExperienceYears, CandidateSource Source, Guid? ReferredByEmployeeId, string? ResumeUrl, string? LinkedInUrl, string? Notes);
