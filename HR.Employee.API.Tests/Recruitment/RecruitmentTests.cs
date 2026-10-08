namespace HR.Employee.API.Tests.Recruitment;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Recruitment;
using Xunit;

public class RecruitmentTests
{
    static readonly Guid Tenant = Guid.NewGuid(), Department = Guid.NewGuid(), Candidate = Guid.NewGuid(), User = Guid.NewGuid();
    static readonly DateTime Now = new(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);

    static JobDetails Details(short openings = 1, decimal? min = null, decimal? max = null)
        => new("Backend engineer", Department, null, null, null, EmploymentType.FullTime, openings, JobReason.NewPosition, null, null, min, max, null, null);

    static JobOpening OpenJob()
    {
        var job = JobOpening.Create(Tenant, "req-0001", Details(), User);
        job.Approve(User, Now);
        return job;
    }

    [Fact]
    public void Requisition_goes_draft_submitted_open()
    {
        var job = JobOpening.Create(Tenant, "req-0001", Details(), User);
        Assert.Equal("REQ-0001", job.Code);
        Assert.Throws<DomainException>(() => job.EnsureAcceptsCandidates());

        job.Submit(Now);
        job.SendBack("Add salary band");
        Assert.Equal(JobStatus.Draft, job.Status);
        Assert.Equal("Add salary band", job.ReviewNote);

        job.Submit(Now);
        job.Approve(User, Now);
        Assert.Equal(JobStatus.Open, job.Status);
        Assert.Null(job.ReviewNote);
        job.EnsureAcceptsCandidates();
    }

    [Fact]
    public void Salary_band_and_openings_are_checked()
    {
        Assert.Throws<DomainException>(() => JobOpening.Create(Tenant, "R", Details(min: 10, max: 5), User));
        Assert.Throws<DomainException>(() => JobOpening.Create(Tenant, "R", Details(openings: 0), User));
    }

    [Fact]
    public void Closed_job_cannot_change_until_reopened()
    {
        var job = OpenJob();
        job.Close(false, "Budget cut", Now);
        Assert.Equal(JobStatus.Cancelled, job.Status);
        Assert.Throws<DomainException>(() => job.Update(Details()));

        job.Reopen();
        Assert.Equal(JobStatus.Open, job.Status);
        job.Update(Details(openings: 3));
        Assert.Equal(3, job.Openings);
    }

    [Fact]
    public void Offer_and_hire_go_through_their_own_steps()
    {
        var app = JobApplication.Create(Tenant, OpenJob(), Candidate, User, Now);
        Assert.Throws<DomainException>(() => app.MoveTo(ApplicationStage.Offer, null, User, Now));
        Assert.Throws<DomainException>(() => app.MoveTo(ApplicationStage.Hired, null, User, Now));
        Assert.Throws<DomainException>(() => app.Hire(Guid.NewGuid(), User, Now));

        app.MoveTo(ApplicationStage.Interview, null, User, Now);
        app.MakeOffer(120000, null, DateOnly.FromDateTime(Now).AddDays(7), null, User, Now);
        Assert.Equal(OfferStatus.Pending, app.OfferStatus);

        app.Hire(Guid.NewGuid(), User, Now);
        Assert.Equal(ApplicationStage.Hired, app.Stage);
        Assert.Equal(OfferStatus.Accepted, app.OfferStatus);
        Assert.Throws<DomainException>(() => app.MoveTo(ApplicationStage.Rejected, "late", User, Now));
        Assert.Equal(4, app.Events.Count);
    }

    [Fact]
    public void Declined_offer_withdraws_the_candidate()
    {
        var app = JobApplication.Create(Tenant, OpenJob(), Candidate, User, Now);
        app.MakeOffer(null, null, null, null, User, Now);
        app.RecordOfferResponse(false, "Took another offer", User, Now);
        Assert.Equal(ApplicationStage.Withdrawn, app.Stage);
        Assert.Throws<DomainException>(() => app.EnsureCanHire());
    }

    [Fact]
    public void Rejecting_needs_a_reason()
    {
        var app = JobApplication.Create(Tenant, OpenJob(), Candidate, User, Now);
        Assert.Throws<DomainException>(() => app.MoveTo(ApplicationStage.Rejected, " ", User, Now));
        app.MoveTo(ApplicationStage.Rejected, "Not enough experience", User, Now);
        Assert.Equal("Not enough experience", app.RejectReason);

        app.MoveTo(ApplicationStage.Screening, null, User, Now);
        Assert.Null(app.RejectReason);
    }

    [Fact]
    public void Candidate_links_must_be_http()
    {
        CandidateDetails D(string? resume) => new("Ayesha", "Malik", "Ayesha@Mail.com", null, null, null, null, null,
            CandidateSource.Referral, null, resume, null, null);
        Assert.Throws<DomainException>(() => HR.Employee.API.Domain.Recruitment.Candidate.Create(Tenant, D("javascript:alert(1)")));
        var c = HR.Employee.API.Domain.Recruitment.Candidate.Create(Tenant, D("https://cv.example/a.pdf"));
        Assert.Equal("ayesha@mail.com", c.Email);
    }

    [Fact]
    public void Feedback_completes_an_interview_but_not_a_cancelled_one()
    {
        var details = new InterviewDetails("Technical", Now, 60, InterviewMode.Video, null, Guid.NewGuid());
        var interview = Interview.Create(Tenant, Guid.NewGuid(), details);
        interview.SubmitFeedback(4, InterviewRecommendation.Yes, "Solid", Now);
        Assert.Equal(InterviewStatus.Completed, interview.Status);
        Assert.Throws<DomainException>(() => interview.Update(details));

        var other = Interview.Create(Tenant, Guid.NewGuid(), details);
        other.Cancel();
        Assert.Throws<DomainException>(() => other.SubmitFeedback(3, InterviewRecommendation.No, null, Now));
    }
}
