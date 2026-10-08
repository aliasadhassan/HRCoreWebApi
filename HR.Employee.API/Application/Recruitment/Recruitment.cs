namespace HR.Employee.API.Application.Recruitment;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Application.Employees;
using HR.Employee.API.Application.Employees.Commands;
using HR.Employee.API.Application.Lifecycle;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Recruitment;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Recruitment page: requisitions/jobs, candidates (talent pool), pipeline (applications), interviews, headcount.
// HR: employees.view dekhna, employees.edit chalana (approve, offer), employees.create hire (employee banta hai).
// Hiring manager (job.HiringManagerEmployeeId) apni jobs ki pipeline chalata hai; interviewer sirf apne interviews + feedback.

#region DTOs

public sealed record RecruitmentSummaryDto(
    int OpenJobs, int OpenPositions, int PendingApprovals, int DraftRequisitions, int ActiveApplications, int InInterview,
    int OffersPending, int HiredLast30Days, decimal? AverageDaysToHire, int InterviewsToday, int MyUpcomingInterviews, int MyFeedbackDue,
    bool IsHr, bool CanManage, bool CanHire, bool CanRequest, Guid? MyEmployeeId);

public sealed record JobListItemDto(
    Guid Id, string Code, string Title, Guid DepartmentId, string DepartmentName, Guid? DesignationId, string? DesignationTitle,
    Guid? LocationId, string? LocationName, Guid? HiringManagerEmployeeId, string? HiringManagerName,
    EmploymentType EmploymentType, short Openings, int Hired, int InPipeline, JobReason Reason, JobStatus Status,
    DateOnly? TargetStartDate, decimal? SalaryMin, decimal? SalaryMax, DateTime CreatedAt, DateTime? SubmittedAt, DateTime? OpenedAt,
    DateTime? ClosedAt, string? ReviewNote, bool IsMine);

public sealed record JobDto(
    JobListItemDto Job, string? Description, string? Requirements, Guid? ReplacesEmployeeId, string? ReplacesName,
    string? RequestedByName, DateTime? ApprovedAt, string? CloseNote, IReadOnlyList<int> StageCounts,
    bool CanEdit, bool CanSubmit, bool CanApprove, bool CanManage, bool CanDelete, bool CanAddCandidates, bool CanWorkPipeline, uint RowVersion);

public sealed record CandidateListItemDto(
    Guid Id, string FirstName, string LastName, string Email, string? Phone, string? City, string? CurrentCompany, string? CurrentTitle,
    decimal? ExperienceYears, CandidateSource Source, string? ReferredByName, int Applications, int ActiveApplications,
    string? LatestJobTitle, ApplicationStage? LatestStage, DateTime CreatedAt);

public sealed record ApplicationListItemDto(
    Guid Id, Guid JobId, string JobCode, string JobTitle, JobStatus JobStatus, Guid CandidateId, string CandidateName, string CandidateEmail,
    string? CurrentTitle, string? CurrentCompany, CandidateSource Source, ApplicationStage Stage, DateTime AppliedAt, DateTime StageChangedAt,
    byte? Rating, OfferStatus? OfferStatus, DateTime? NextInterviewAt, int InterviewsDone, decimal? InterviewScore, Guid? HiredEmployeeId);

public sealed record CandidateDto(
    Guid Id, string FirstName, string LastName, string Email, string? Phone, string? City, string? CurrentCompany, string? CurrentTitle,
    decimal? ExperienceYears, CandidateSource Source, Guid? ReferredByEmployeeId, string? ReferredByName, string? ResumeUrl, string? LinkedInUrl,
    string? Notes, DateTime CreatedAt, IReadOnlyList<ApplicationListItemDto> Applications, bool CanEdit, uint RowVersion);

public sealed record ApplicationEventDto(Guid Id, ApplicationEventKind Kind, ApplicationStage? FromStage, ApplicationStage ToStage, string? Note, string? ByName, DateTime At);

public sealed record InterviewDto(
    Guid Id, Guid ApplicationId, Guid CandidateId, string CandidateName, string? CandidateTitle, string? ResumeUrl, Guid JobId, string JobTitle,
    ApplicationStage Stage, string Title, DateTime ScheduledAt, short DurationMinutes, InterviewMode Mode, string? LocationOrLink,
    Guid InterviewerEmployeeId, string InterviewerName, InterviewStatus Status, byte? Rating, InterviewRecommendation? Recommendation,
    string? Feedback, DateTime? FeedbackAt, bool IsMine, bool CanEdit, bool CanFeedback, uint RowVersion);

public sealed record ApplicationDto(
    ApplicationListItemDto Application, string? Phone, string? City, decimal? ExperienceYears, string? ResumeUrl, string? LinkedInUrl,
    string? CandidateNotes, string DepartmentName, string? HiringManagerName, Guid? JobDesignationId, Guid? JobLocationId, Guid JobDepartmentId,
    Guid? JobHiringManagerId, EmploymentType JobEmploymentType, string? RejectReason,
    decimal? OfferSalary, DateOnly? OfferStartDate, DateOnly? OfferExpiresOn, DateTime? OfferedAt, DateTime? HiredAt, string? HiredEmployeeCode,
    IReadOnlyList<ApplicationEventDto> Events, IReadOnlyList<InterviewDto> Interviews,
    bool CanWork, bool CanOffer, bool CanHire, uint RowVersion);

public sealed record HeadcountRowDto(
    Guid DepartmentId, string DepartmentName, int Employees, int OnNotice, int OpenPositions, int PendingRequisitions,
    int InPipeline, int HiredLast90Days);

public sealed record RecruitmentOptionDto(Guid Id, string Name, string? Extra);

public sealed record RecruitmentLookupsDto(
    IReadOnlyList<RecruitmentOptionDto> Departments, IReadOnlyList<RecruitmentOptionDto> Designations,
    IReadOnlyList<RecruitmentOptionDto> Locations, IReadOnlyList<RecruitmentOptionDto> People);

public sealed record HireResultDto(Guid EmployeeId, string EmployeeCode, Guid? OnboardingCaseId, string? OnboardingError, bool JobFilled);

#endregion

#region Requests

public sealed record GetRecruitmentSummaryQuery : IRequest<RecruitmentSummaryDto>;
public sealed record GetRecruitmentLookupsQuery : IRequest<RecruitmentLookupsDto>;
public sealed record GetJobsQuery(JobStatus? Status, Guid? DepartmentId, string? Search) : IRequest<IReadOnlyList<JobListItemDto>>;
public sealed record GetJobQuery(Guid Id) : IRequest<JobDto>;
public sealed record GetCandidatesQuery(CandidateSource? Source, string? Search) : IRequest<IReadOnlyList<CandidateListItemDto>>;
public sealed record GetCandidateQuery(Guid Id) : IRequest<CandidateDto>;
public sealed record GetApplicationsQuery(Guid? JobId, ApplicationStage? Stage, bool? ActiveOnly, string? Search) : IRequest<IReadOnlyList<ApplicationListItemDto>>;
public sealed record GetApplicationQuery(Guid Id) : IRequest<ApplicationDto>;
public sealed record GetInterviewsQuery(bool Mine, InterviewStatus? Status, DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<InterviewDto>>;
public sealed record GetHeadcountQuery : IRequest<IReadOnlyList<HeadcountRowDto>>;

public sealed record SaveJobRequest(
    string Title, Guid DepartmentId, Guid? DesignationId, Guid? LocationId, Guid? HiringManagerEmployeeId,
    EmploymentType EmploymentType, short Openings, JobReason Reason, Guid? ReplacesEmployeeId, DateOnly? TargetStartDate,
    decimal? SalaryMin, decimal? SalaryMax, string? Description, string? Requirements);
public sealed record CreateJobCommand(SaveJobRequest Data) : IRequest<Guid>;
public sealed record UpdateJobCommand(Guid Id, SaveJobRequest Data) : IRequest;
public sealed record DeleteJobCommand(Guid Id) : IRequest;
public sealed record SubmitJobCommand(Guid Id) : IRequest;
public sealed record ApproveJobCommand(Guid Id) : IRequest;
public sealed record SendBackJobCommand(Guid Id, string Note) : IRequest;
public sealed record HoldJobCommand(Guid Id) : IRequest;
public sealed record ResumeJobCommand(Guid Id) : IRequest;
public sealed record CloseJobRequest(bool Filled, string? Note);
public sealed record CloseJobCommand(Guid Id, CloseJobRequest Data) : IRequest;
public sealed record ReopenJobCommand(Guid Id) : IRequest;

/// <summary>JobId diya ho to candidate us job par seedha apply bhi ho jata hai.</summary>
public sealed record SaveCandidateRequest(
    string FirstName, string LastName, string Email, string? Phone, string? City, string? CurrentCompany, string? CurrentTitle,
    decimal? ExperienceYears, CandidateSource Source, Guid? ReferredByEmployeeId, string? ResumeUrl, string? LinkedInUrl, string? Notes,
    Guid? JobId);
public sealed record CreateCandidateCommand(SaveCandidateRequest Data) : IRequest<Guid>;
public sealed record UpdateCandidateCommand(Guid Id, SaveCandidateRequest Data) : IRequest;
public sealed record DeleteCandidateCommand(Guid Id) : IRequest;

public sealed record CreateApplicationRequest(Guid JobId, Guid CandidateId);
public sealed record CreateApplicationCommand(CreateApplicationRequest Data) : IRequest<Guid>;
public sealed record MoveApplicationRequest(ApplicationStage Stage, string? Note);
public sealed record MoveApplicationCommand(Guid Id, MoveApplicationRequest Data) : IRequest;
public sealed record AddApplicationNoteCommand(Guid Id, string Note) : IRequest;
public sealed record RateApplicationCommand(Guid Id, byte? Rating) : IRequest;
public sealed record MakeOfferRequest(decimal? Salary, DateOnly? StartDate, DateOnly? ExpiresOn, string? Note);
public sealed record MakeOfferCommand(Guid Id, MakeOfferRequest Data) : IRequest;
public sealed record OfferResponseRequest(bool Accepted, string? Note);
public sealed record OfferResponseCommand(Guid Id, OfferResponseRequest Data) : IRequest;

/// <summary>
/// Hire: ExistingEmployeeId diya ho to us employee se jodo, warna naya employee banao (khali fields job se aate hain).
/// StartOnboarding = onboarding checklist bhi shuru (template khali = default).
/// </summary>
public sealed record HireRequest(
    Guid? ExistingEmployeeId, string? EmployeeCode, string? WorkEmail, Guid? LocationId, Guid? DepartmentId, Guid? DesignationId,
    Guid? ManagerId, EmploymentType? EmploymentType, DateOnly JoiningDate, DateOnly? ProbationEndDate,
    bool StartOnboarding, Guid? OnboardingTemplateId);
public sealed record HireCommand(Guid Id, HireRequest Data) : IRequest<HireResultDto>;

public sealed record SaveInterviewRequest(string Title, DateTime ScheduledAt, short DurationMinutes, InterviewMode Mode, string? LocationOrLink, Guid InterviewerEmployeeId);
public sealed record ScheduleInterviewCommand(Guid ApplicationId, SaveInterviewRequest Data) : IRequest<Guid>;
public sealed record UpdateInterviewCommand(Guid Id, SaveInterviewRequest Data) : IRequest;
public sealed record CancelInterviewCommand(Guid Id) : IRequest;
public sealed record NoShowInterviewCommand(Guid Id) : IRequest;
public sealed record InterviewFeedbackRequest(byte Rating, InterviewRecommendation Recommendation, string? Feedback);
public sealed record InterviewFeedbackCommand(Guid Id, InterviewFeedbackRequest Data) : IRequest;

#endregion

#region Validators

public sealed class SaveJobRequestValidator : AbstractValidator<SaveJobRequest>
{
    public SaveJobRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Openings).InclusiveBetween((short)1, (short)500);
        RuleFor(x => x.SalaryMin).GreaterThanOrEqualTo(0).When(x => x.SalaryMin is not null);
        RuleFor(x => x.SalaryMax).GreaterThanOrEqualTo(x => x.SalaryMin ?? 0).When(x => x.SalaryMax is not null)
            .WithMessage("Salary max cannot be less than salary min.");
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Requirements).MaximumLength(4000);
    }
}

public sealed class CreateJobValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveJobRequestValidator());
}

public sealed class UpdateJobValidator : AbstractValidator<UpdateJobCommand>
{
    public UpdateJobValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveJobRequestValidator());
}

public sealed class SendBackJobValidator : AbstractValidator<SendBackJobCommand>
{
    public SendBackJobValidator() => RuleFor(x => x.Note).NotEmpty().WithMessage("Add a note for the requester.").MaximumLength(500);
}

public sealed class CloseJobValidator : AbstractValidator<CloseJobCommand>
{
    public CloseJobValidator() => RuleFor(x => x.Data.Note).MaximumLength(500);
}

public sealed class SaveCandidateRequestValidator : AbstractValidator<SaveCandidateRequest>
{
    public SaveCandidateRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.CurrentCompany).MaximumLength(150);
        RuleFor(x => x.CurrentTitle).MaximumLength(150);
        RuleFor(x => x.ExperienceYears).InclusiveBetween(0, 60).When(x => x.ExperienceYears is not null);
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.ResumeUrl).MaximumLength(1000);
        RuleFor(x => x.LinkedInUrl).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CreateCandidateValidator : AbstractValidator<CreateCandidateCommand>
{
    public CreateCandidateValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveCandidateRequestValidator());
}

public sealed class UpdateCandidateValidator : AbstractValidator<UpdateCandidateCommand>
{
    public UpdateCandidateValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveCandidateRequestValidator());
}

public sealed class CreateApplicationValidator : AbstractValidator<CreateApplicationCommand>
{
    public CreateApplicationValidator()
    {
        RuleFor(x => x.Data.JobId).NotEmpty();
        RuleFor(x => x.Data.CandidateId).NotEmpty();
    }
}

public sealed class MoveApplicationValidator : AbstractValidator<MoveApplicationCommand>
{
    public MoveApplicationValidator()
    {
        RuleFor(x => x.Data.Stage).IsInEnum();
        RuleFor(x => x.Data.Note).MaximumLength(1000);
        RuleFor(x => x.Data.Note).NotEmpty().When(x => x.Data.Stage == ApplicationStage.Rejected)
            .WithMessage("Add a reason for rejecting the candidate.");
    }
}

public sealed class AddApplicationNoteValidator : AbstractValidator<AddApplicationNoteCommand>
{
    public AddApplicationNoteValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(1000);
}

public sealed class RateApplicationValidator : AbstractValidator<RateApplicationCommand>
{
    public RateApplicationValidator() => RuleFor(x => x.Rating).InclusiveBetween((byte)1, (byte)5).When(x => x.Rating is not null);
}

public sealed class MakeOfferValidator : AbstractValidator<MakeOfferCommand>
{
    public MakeOfferValidator()
    {
        RuleFor(x => x.Data.Salary).GreaterThanOrEqualTo(0).When(x => x.Data.Salary is not null);
        RuleFor(x => x.Data.Note).MaximumLength(1000);
    }
}

public sealed class OfferResponseValidator : AbstractValidator<OfferResponseCommand>
{
    public OfferResponseValidator() => RuleFor(x => x.Data.Note).MaximumLength(1000);
}

public sealed class HireValidator : AbstractValidator<HireCommand>
{
    public HireValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.EmployeeCode).MaximumLength(20);
        RuleFor(x => x.Data.WorkEmail).NotEmpty().EmailAddress().MaximumLength(256).When(x => x.Data.ExistingEmployeeId is null);
        RuleFor(x => x.Data.EmploymentType).IsInEnum().When(x => x.Data.EmploymentType is not null);
        RuleFor(x => x.Data.ProbationEndDate).GreaterThan(x => x.Data.JoiningDate).When(x => x.Data.ProbationEndDate is not null)
            .WithMessage("Probation end date must be after the joining date.");
    }
}

public sealed class SaveInterviewRequestValidator : AbstractValidator<SaveInterviewRequest>
{
    public SaveInterviewRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DurationMinutes).InclusiveBetween((short)5, (short)480);
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.LocationOrLink).MaximumLength(500);
        RuleFor(x => x.InterviewerEmployeeId).NotEmpty();
    }
}

public sealed class ScheduleInterviewValidator : AbstractValidator<ScheduleInterviewCommand>
{
    public ScheduleInterviewValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveInterviewRequestValidator());
}

public sealed class UpdateInterviewValidator : AbstractValidator<UpdateInterviewCommand>
{
    public UpdateInterviewValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveInterviewRequestValidator());
}

public sealed class InterviewFeedbackValidator : AbstractValidator<InterviewFeedbackCommand>
{
    public InterviewFeedbackValidator()
    {
        RuleFor(x => x.Data.Rating).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.Data.Recommendation).IsInEnum();
        RuleFor(x => x.Data.Feedback).MaximumLength(4000);
    }
}

#endregion

#region Access

public sealed class RecruitmentAccess(IAppDbContext db, ICurrentUser currentUser)
{
    private Guid? me;
    private bool loaded;
    private bool? hasReports;

    public bool IsHr => currentUser.HasPermission(Permissions.EmployeesView);
    public bool CanManage => currentUser.HasPermission(Permissions.EmployeesEdit);
    public bool CanHire => CanManage && currentUser.HasPermission(Permissions.EmployeesCreate);
    public Guid? UserId => currentUser.UserId;

    public void EnsureCanManage()
    {
        if (!CanManage)
            throw new UnauthorizedAccessException("You do not have permission to manage recruitment.");
    }

    public void EnsureHr()
    {
        if (!IsHr)
            throw new UnauthorizedAccessException("You do not have permission to see this.");
    }

    public async Task<Guid?> MeAsync(CancellationToken ct)
    {
        if (loaded)
            return me;
        loaded = true;
        if (currentUser.UserId is { } userId)
        {
            var id = await db.Employees.AsNoTracking().Where(e => e.UserId == userId).Select(e => e.Id).FirstOrDefaultAsync(ct);
            me = id == Guid.Empty ? null : id;
        }
        return me;
    }

    /// <summary>Requisition HR ke ilawa sirf wo bana sakta hai jis ki team ho (manager).</summary>
    public async Task<bool> CanRequestAsync(CancellationToken ct)
    {
        if (CanManage)
            return true;
        if (await MeAsync(ct) is not { } my)
            return false;
        hasReports ??= await db.Employees.AnyAsync(e => e.ManagerId == my && e.EmploymentStatus != EmploymentStatus.Exited, ct);
        return hasReports.Value;
    }

    /// <summary>HR ya job ka hiring manager / requester.</summary>
    public async Task<bool> CanSeeJobAsync(JobOpening job, CancellationToken ct)
        => IsHr || (job.RequestedByUserId is not null && job.RequestedByUserId == currentUser.UserId) || await IsHiringManagerAsync(job, ct);

    public async Task<bool> IsHiringManagerAsync(JobOpening job, CancellationToken ct)
        => job.HiringManagerEmployeeId is { } hm && hm == await MeAsync(ct);

    /// <summary>Pipeline chalana (stage, note, interview): HR manage ya hiring manager.</summary>
    public async Task<bool> CanWorkPipelineAsync(JobOpening job, CancellationToken ct)
        => CanManage || await IsHiringManagerAsync(job, ct);

    public async Task EnsureCanWorkPipelineAsync(JobOpening job, CancellationToken ct)
    {
        if (!await CanWorkPipelineAsync(job, ct))
            throw new UnauthorizedAccessException("Only HR or the hiring manager can work on this job's candidates.");
    }
}

#endregion

#region Queries

public sealed class RecruitmentQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetRecruitmentSummaryQuery, RecruitmentSummaryDto>,
    IRequestHandler<GetRecruitmentLookupsQuery, RecruitmentLookupsDto>,
    IRequestHandler<GetJobsQuery, IReadOnlyList<JobListItemDto>>,
    IRequestHandler<GetJobQuery, JobDto>,
    IRequestHandler<GetCandidatesQuery, IReadOnlyList<CandidateListItemDto>>,
    IRequestHandler<GetCandidateQuery, CandidateDto>,
    IRequestHandler<GetApplicationsQuery, IReadOnlyList<ApplicationListItemDto>>,
    IRequestHandler<GetApplicationQuery, ApplicationDto>,
    IRequestHandler<GetInterviewsQuery, IReadOnlyList<InterviewDto>>,
    IRequestHandler<GetHeadcountQuery, IReadOnlyList<HeadcountRowDto>>
{
    private readonly RecruitmentAccess access = new(db, currentUser);

    public async Task<RecruitmentSummaryDto> Handle(GetRecruitmentSummaryQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var now = DateTime.UtcNow;

        var jobs = await VisibleJobs(me).Select(x => new { x.j.Status, x.j.Openings, x.Hired }).ToListAsync(ct);
        var apps = await VisibleApplications(me)
            .Select(x => new { x.a.Stage, x.a.OfferStatus, x.a.AppliedAt, x.a.HiredAt })
            .ToListAsync(ct);
        var since30 = now.AddDays(-30);
        var hires = apps.Where(a => a.HiredAt != null && a.HiredAt >= now.AddDays(-365)).ToList();

        var dayStart = now.Date;
        var dayEnd = dayStart.AddDays(1);
        var interviewsToday = await VisibleInterviews(me, false)
            .CountAsync(x => x.i.Status == InterviewStatus.Scheduled && x.i.ScheduledAt >= dayStart && x.i.ScheduledAt < dayEnd, ct);

        int myUpcoming = 0, myDue = 0;
        if (me is { } my)
        {
            myUpcoming = await db.Interviews.CountAsync(i => i.InterviewerEmployeeId == my && i.Status == InterviewStatus.Scheduled && i.ScheduledAt >= now, ct);
            myDue = await db.Interviews.CountAsync(i => i.InterviewerEmployeeId == my && i.Status == InterviewStatus.Scheduled && i.ScheduledAt < now, ct);
        }

        var active = jobs.Where(j => j.Status is JobStatus.Open or JobStatus.OnHold).ToList();
        return new RecruitmentSummaryDto(
            jobs.Count(j => j.Status == JobStatus.Open),
            active.Sum(j => Math.Max(0, j.Openings - j.Hired)),
            jobs.Count(j => j.Status == JobStatus.Submitted),
            jobs.Count(j => j.Status == JobStatus.Draft),
            apps.Count(a => a.Stage <= ApplicationStage.Offer),
            apps.Count(a => a.Stage == ApplicationStage.Interview),
            apps.Count(a => a.Stage == ApplicationStage.Offer && a.OfferStatus == OfferStatus.Pending),
            apps.Count(a => a.HiredAt >= since30),
            hires.Count == 0 ? null : Math.Round((decimal)hires.Average(h => (h.HiredAt!.Value - h.AppliedAt).TotalDays), 1),
            interviewsToday, myUpcoming, myDue,
            access.IsHr, access.CanManage, access.CanHire, await access.CanRequestAsync(ct), me);
    }

    public async Task<RecruitmentLookupsDto> Handle(GetRecruitmentLookupsQuery q, CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name)
            .Select(d => new RecruitmentOptionDto(d.Id, d.Name, d.Code)).ToListAsync(ct);
        var designations = await db.Designations.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Title)
            .Select(d => new RecruitmentOptionDto(d.Id, d.Title, null)).ToListAsync(ct);
        var locations = await db.Locations.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.Name)
            .Select(l => new RecruitmentOptionDto(l.Id, l.Name, l.City)).ToListAsync(ct);
        var people = await db.Employees.AsNoTracking().Where(e => e.EmploymentStatus != EmploymentStatus.Exited)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName).Take(3000)
            .Select(e => new RecruitmentOptionDto(e.Id, e.FirstName + " " + e.LastName, e.Department.Name)).ToListAsync(ct);
        return new RecruitmentLookupsDto(departments, designations, locations, people);
    }

    public async Task<IReadOnlyList<JobListItemDto>> Handle(GetJobsQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var rows = VisibleJobs(me);
        if (q.Status is { } status) rows = rows.Where(x => x.j.Status == status);
        if (q.DepartmentId is { } departmentId) rows = rows.Where(x => x.j.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            rows = rows.Where(x => EF.Functions.ILike(x.j.Title, term) || EF.Functions.ILike(x.j.Code, term) || EF.Functions.ILike(x.DepartmentName, term));
        }

        return await rows
            .OrderBy(x => x.j.Status == JobStatus.Submitted ? 0 : x.j.Status == JobStatus.Open ? 1 : x.j.Status == JobStatus.Draft ? 2
                        : x.j.Status == JobStatus.OnHold ? 3 : 4)
            .ThenByDescending(x => x.j.CreatedAt)
            .Take(1000)
            .Select(ToJobItem(me, currentUser.UserId))
            .ToListAsync(ct);
    }

    public async Task<JobDto> Handle(GetJobQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var job = await db.JobOpenings.AsNoTracking().FirstOrDefaultAsync(j => j.Id == q.Id, ct) ?? throw new NotFoundException("Job", q.Id);
        if (!await access.CanSeeJobAsync(job, ct))
            throw new UnauthorizedAccessException("You cannot see this job.");

        var item = await JobRows(db.JobOpenings.AsNoTracking().Where(j => j.Id == q.Id)).Select(ToJobItem(me, currentUser.UserId)).FirstAsync(ct);
        var stages = await db.JobApplications.AsNoTracking().Where(a => a.JobOpeningId == q.Id)
            .GroupBy(a => a.Stage).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var stageCounts = Enum.GetValues<ApplicationStage>().Select(s => stages.Where(x => x.Key == s).Sum(x => x.Count)).ToList();

        var replaces = job.ReplacesEmployeeId is { } rid
            ? await db.Employees.AsNoTracking().Where(e => e.Id == rid).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync(ct) : null;
        var requestedBy = job.RequestedByUserId is { } uid
            ? await db.Employees.AsNoTracking().Where(e => e.UserId == uid).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync(ct) : null;

        var isRequester = job.RequestedByUserId is not null && job.RequestedByUserId == currentUser.UserId;
        var canWork = await access.CanWorkPipelineAsync(job, ct);
        var hasApps = stageCounts.Sum() > 0;
        return new JobDto(item, job.Description, job.Requirements, job.ReplacesEmployeeId, replaces, requestedBy, job.ApprovedAt, job.CloseNote,
            stageCounts,
            CanEdit: !job.IsClosed && (access.CanManage || (isRequester && job.Status == JobStatus.Draft)),
            CanSubmit: job.Status == JobStatus.Draft && !access.CanManage && isRequester,
            CanApprove: access.CanManage && job.Status is JobStatus.Draft or JobStatus.Submitted,
            CanManage: access.CanManage,
            CanDelete: !hasApps && (access.CanManage ? job.Status is JobStatus.Draft or JobStatus.Submitted or JobStatus.Cancelled
                                                      : isRequester && job.Status == JobStatus.Draft),
            CanAddCandidates: job.Status == JobStatus.Open && access.CanManage,
            CanWorkPipeline: canWork && job.Status is JobStatus.Open or JobStatus.OnHold,
            job.RowVersion);
    }

    public async Task<IReadOnlyList<CandidateListItemDto>> Handle(GetCandidatesQuery q, CancellationToken ct)
    {
        access.EnsureHr();
        var rows = db.Candidates.AsNoTracking();
        if (q.Source is { } source) rows = rows.Where(c => c.Source == source);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            rows = rows.Where(c => EF.Functions.ILike(c.FirstName + " " + c.LastName, term) || EF.Functions.ILike(c.Email, term)
                                   || (c.CurrentTitle != null && EF.Functions.ILike(c.CurrentTitle, term))
                                   || (c.CurrentCompany != null && EF.Functions.ILike(c.CurrentCompany, term)));
        }

        return await rows.OrderByDescending(c => c.CreatedAt).Take(1000)
            .Select(c => new CandidateListItemDto(
                c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.City, c.CurrentCompany, c.CurrentTitle, c.ExperienceYears, c.Source,
                c.ReferredByEmployeeId == null ? null : db.Employees.Where(e => e.Id == c.ReferredByEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
                db.JobApplications.Count(a => a.CandidateId == c.Id),
                db.JobApplications.Count(a => a.CandidateId == c.Id && a.Stage <= ApplicationStage.Offer),
                db.JobApplications.Where(a => a.CandidateId == c.Id).OrderByDescending(a => a.AppliedAt)
                    .Join(db.JobOpenings, a => a.JobOpeningId, j => j.Id, (a, j) => j.Title).FirstOrDefault(),
                db.JobApplications.Where(a => a.CandidateId == c.Id).OrderByDescending(a => a.AppliedAt).Select(a => (ApplicationStage?)a.Stage).FirstOrDefault(),
                c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<CandidateDto> Handle(GetCandidateQuery q, CancellationToken ct)
    {
        access.EnsureHr();
        var c = await db.Candidates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Candidate", q.Id);
        var referredBy = c.ReferredByEmployeeId is { } rid
            ? await db.Employees.AsNoTracking().Where(e => e.Id == rid).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync(ct) : null;
        var apps = await ApplicationItems(ApplicationRows().Where(x => x.a.CandidateId == c.Id).OrderByDescending(x => x.a.AppliedAt)).ToListAsync(ct);

        return new CandidateDto(c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.City, c.CurrentCompany, c.CurrentTitle, c.ExperienceYears,
            c.Source, c.ReferredByEmployeeId, referredBy, c.ResumeUrl, c.LinkedInUrl, c.Notes, c.CreatedAt, apps, access.CanManage, c.RowVersion);
    }

    public async Task<IReadOnlyList<ApplicationListItemDto>> Handle(GetApplicationsQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var rows = VisibleApplications(me);
        if (q.JobId is { } jobId) rows = rows.Where(x => x.a.JobOpeningId == jobId);
        if (q.Stage is { } stage) rows = rows.Where(x => x.a.Stage == stage);
        if (q.ActiveOnly == true) rows = rows.Where(x => x.a.Stage <= ApplicationStage.Offer);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            rows = rows.Where(x => EF.Functions.ILike(x.c.FirstName + " " + x.c.LastName, term) || EF.Functions.ILike(x.c.Email, term)
                                   || EF.Functions.ILike(x.j.Title, term));
        }
        return await ApplicationItems(rows.OrderBy(x => x.a.Stage).ThenByDescending(x => x.a.StageChangedAt).Take(2000)).ToListAsync(ct);
    }

    public async Task<ApplicationDto> Handle(GetApplicationQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var x = await ApplicationRows().Where(r => r.a.Id == q.Id).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Application", q.Id);
        if (!access.IsHr && !await access.IsHiringManagerAsync(x.j, ct))
            throw new UnauthorizedAccessException("You cannot see this application.");

        var item = await ApplicationItems(ApplicationRows().Where(r => r.a.Id == q.Id)).FirstAsync(ct);
        var dept = await db.Departments.AsNoTracking().Where(d => d.Id == x.j.DepartmentId).Select(d => d.Name).FirstAsync(ct);
        var hm = x.j.HiringManagerEmployeeId is { } hmId
            ? await db.Employees.AsNoTracking().Where(e => e.Id == hmId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync(ct) : null;
        var hiredCode = x.a.HiredEmployeeId is { } hid
            ? await db.Employees.AsNoTracking().Where(e => e.Id == hid).Select(e => e.EmployeeCode).FirstOrDefaultAsync(ct) : null;

        var events = await (from a in db.JobApplications.AsNoTracking()
                            where a.Id == q.Id
                            from e in a.Events
                            orderby e.At descending
                            select new ApplicationEventDto(e.Id, e.Kind, e.FromStage, e.ToStage, e.Note,
                                e.ByUserId == null ? null : db.Employees.Where(p => p.UserId == e.ByUserId).Select(p => p.FirstName + " " + p.LastName).FirstOrDefault(),
                                e.At))
            .Take(300).ToListAsync(ct);

        var canWork = await access.CanWorkPipelineAsync(x.j, ct);
        var interviews = await InterviewItems(InterviewRows().Where(r => r.i.JobApplicationId == q.Id).OrderBy(r => r.i.ScheduledAt), me, canWork).ToListAsync(ct);

        var jobActive = x.j.Status is JobStatus.Open or JobStatus.OnHold;
        return new ApplicationDto(item, x.c.Phone, x.c.City, x.c.ExperienceYears, x.c.ResumeUrl, x.c.LinkedInUrl, x.c.Notes, dept, hm,
            x.j.DesignationId, x.j.LocationId, x.j.DepartmentId, x.j.HiringManagerEmployeeId, x.j.EmploymentType, x.a.RejectReason,
            x.a.OfferSalary, x.a.OfferStartDate, x.a.OfferExpiresOn, x.a.OfferedAt, x.a.HiredAt, hiredCode,
            events, interviews,
            CanWork: canWork && jobActive && x.a.Stage != ApplicationStage.Hired,
            CanOffer: access.CanManage && jobActive && x.a.Stage <= ApplicationStage.Offer,
            CanHire: access.CanHire && jobActive && x.a.Stage == ApplicationStage.Offer && x.a.OfferStatus != OfferStatus.Declined,
            x.a.RowVersion);
    }

    public async Task<IReadOnlyList<InterviewDto>> Handle(GetInterviewsQuery q, CancellationToken ct)
    {
        var me = await access.MeAsync(ct);
        var rows = VisibleInterviews(me, q.Mine);
        if (q.Status is { } status) rows = rows.Where(x => x.i.Status == status);
        if (q.From is { } from)
        {
            var f = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            rows = rows.Where(x => x.i.ScheduledAt >= f);
        }
        if (q.To is { } to)
        {
            var t = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            rows = rows.Where(x => x.i.ScheduledAt < t);
        }

        var list = await InterviewItems(rows.OrderBy(x => x.i.Status == InterviewStatus.Scheduled ? 0 : 1).ThenBy(x => x.i.ScheduledAt).Take(1000), me, false)
            .ToListAsync(ct);
        if (!access.CanManage && me is not null)
        {
            // Hiring manager apni jobs ke interviews badal sakta hai
            var jobIds = list.Select(i => i.JobId).Distinct().ToList();
            var mine = (await db.JobOpenings.AsNoTracking().Where(j => jobIds.Contains(j.Id) && j.HiringManagerEmployeeId == me).Select(j => j.Id).ToListAsync(ct)).ToHashSet();
            list = list.Select(i => i with { CanEdit = mine.Contains(i.JobId) && i.Status == InterviewStatus.Scheduled }).ToList();
        }
        return list;
    }

    public async Task<IReadOnlyList<HeadcountRowDto>> Handle(GetHeadcountQuery q, CancellationToken ct)
    {
        access.EnsureHr();
        var since = DateTime.UtcNow.AddDays(-90);

        var employees = await db.Employees.AsNoTracking().Where(e => e.EmploymentStatus != EmploymentStatus.Exited)
            .GroupBy(e => e.DepartmentId)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count(), OnNotice = g.Count(e => e.EmploymentStatus == EmploymentStatus.OnNotice) })
            .ToListAsync(ct);
        var jobs = await JobRows(db.JobOpenings.AsNoTracking())
            .Select(x => new { x.j.DepartmentId, x.j.Status, x.j.Openings, x.Hired, x.InPipeline }).ToListAsync(ct);
        var hires = await (from a in db.JobApplications.AsNoTracking()
                           join j in db.JobOpenings on a.JobOpeningId equals j.Id
                           where a.HiredAt >= since
                           group a by j.DepartmentId into g
                           select new { DepartmentId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).Select(d => new { d.Id, d.Name, d.IsActive }).ToListAsync(ct);

        return departments
            .Select(d =>
            {
                var emp = employees.FirstOrDefault(e => e.DepartmentId == d.Id);
                var dj = jobs.Where(j => j.DepartmentId == d.Id).ToList();
                var active = dj.Where(j => j.Status is JobStatus.Open or JobStatus.OnHold).ToList();
                return new HeadcountRowDto(d.Id, d.Name, emp?.Count ?? 0, emp?.OnNotice ?? 0,
                    active.Sum(j => Math.Max(0, j.Openings - j.Hired)),
                    dj.Where(j => j.Status is JobStatus.Draft or JobStatus.Submitted).Sum(j => j.Openings),
                    active.Sum(j => j.InPipeline),
                    hires.FirstOrDefault(h => h.DepartmentId == d.Id)?.Count ?? 0);
            })
            .Where(r => r.Employees + r.OpenPositions + r.PendingRequisitions + r.InPipeline + r.HiredLast90Days > 0
                        || departments.First(x => x.Id == r.DepartmentId).IsActive)
            .ToList();
    }

    // ---- Rows ----

    /// <summary>Init properties (constructor record nahi) — EF baad ke Where/OrderBy mein in members ko SQL bana sake.</summary>
    internal sealed class JobRow
    {
        public JobOpening j { get; init; } = default!;
        public string DepartmentName { get; init; } = default!;
        public string? DesignationTitle { get; init; }
        public string? LocationName { get; init; }
        public string? HiringManagerName { get; init; }
        public int Hired { get; init; }
        public int InPipeline { get; init; }
    }

    private IQueryable<JobRow> JobRows(IQueryable<JobOpening> jobs)
        => from j in jobs
           join d in db.Departments on j.DepartmentId equals d.Id
           join ds in db.Designations on j.DesignationId equals ds.Id into dss
           from ds in dss.DefaultIfEmpty()
           join l in db.Locations on j.LocationId equals l.Id into ls
           from l in ls.DefaultIfEmpty()
           join hm in db.Employees on j.HiringManagerEmployeeId equals hm.Id into hms
           from hm in hms.DefaultIfEmpty()
           select new JobRow
           {
               j = j, DepartmentName = d.Name, DesignationTitle = ds == null ? null : ds.Title, LocationName = l == null ? null : l.Name,
               HiringManagerName = hm == null ? null : hm.FirstName + " " + hm.LastName,
               Hired = db.JobApplications.Count(a => a.JobOpeningId == j.Id && a.Stage == ApplicationStage.Hired),
               InPipeline = db.JobApplications.Count(a => a.JobOpeningId == j.Id && a.Stage <= ApplicationStage.Offer)
           };

    /// <summary>HR: sab. Warna: jin ka main hiring manager hoon ya jo maine maangi.</summary>
    private IQueryable<JobRow> VisibleJobs(Guid? me)
    {
        var rows = JobRows(db.JobOpenings.AsNoTracking());
        if (access.IsHr)
            return rows;
        var userId = currentUser.UserId;
        return rows.Where(x => (me != null && x.j.HiringManagerEmployeeId == me) || (userId != null && x.j.RequestedByUserId == userId));
    }

    private static System.Linq.Expressions.Expression<Func<JobRow, JobListItemDto>> ToJobItem(Guid? me, Guid? userId)
        => x => new JobListItemDto(
            x.j.Id, x.j.Code, x.j.Title, x.j.DepartmentId, x.DepartmentName, x.j.DesignationId, x.DesignationTitle,
            x.j.LocationId, x.LocationName, x.j.HiringManagerEmployeeId, x.HiringManagerName,
            x.j.EmploymentType, x.j.Openings, x.Hired, x.InPipeline, x.j.Reason, x.j.Status,
            x.j.TargetStartDate, x.j.SalaryMin, x.j.SalaryMax, x.j.CreatedAt, x.j.SubmittedAt, x.j.OpenedAt, x.j.ClosedAt, x.j.ReviewNote,
            (me != null && x.j.HiringManagerEmployeeId == me) || (userId != null && x.j.RequestedByUserId == userId));

    internal sealed class ApplicationRow
    {
        public JobApplication a { get; init; } = default!;
        public JobOpening j { get; init; } = default!;
        public Candidate c { get; init; } = default!;
    }

    private IQueryable<ApplicationRow> ApplicationRows()
        => from a in db.JobApplications.AsNoTracking()
           join j in db.JobOpenings on a.JobOpeningId equals j.Id
           join c in db.Candidates on a.CandidateId equals c.Id
           select new ApplicationRow { a = a, j = j, c = c };

    private IQueryable<ApplicationRow> VisibleApplications(Guid? me)
    {
        var rows = ApplicationRows();
        if (access.IsHr)
            return rows;
        return me is { } my ? rows.Where(x => x.j.HiringManagerEmployeeId == my) : rows.Where(x => false);
    }

    private IQueryable<ApplicationListItemDto> ApplicationItems(IQueryable<ApplicationRow> rows)
    {
        var now = DateTime.UtcNow;
        return rows.Select(x => new ApplicationListItemDto(
            x.a.Id, x.j.Id, x.j.Code, x.j.Title, x.j.Status, x.c.Id, x.c.FirstName + " " + x.c.LastName, x.c.Email,
            x.c.CurrentTitle, x.c.CurrentCompany, x.c.Source, x.a.Stage, x.a.AppliedAt, x.a.StageChangedAt, x.a.Rating, x.a.OfferStatus,
            db.Interviews.Where(i => i.JobApplicationId == x.a.Id && i.Status == InterviewStatus.Scheduled && i.ScheduledAt >= now)
                .Min(i => (DateTime?)i.ScheduledAt),
            db.Interviews.Count(i => i.JobApplicationId == x.a.Id && i.Status == InterviewStatus.Completed),
            db.Interviews.Where(i => i.JobApplicationId == x.a.Id && i.Rating != null).Average(i => (decimal?)i.Rating),
            x.a.HiredEmployeeId));
    }

    internal sealed class InterviewRow
    {
        public Interview i { get; init; } = default!;
        public JobApplication a { get; init; } = default!;
        public JobOpening j { get; init; } = default!;
        public Candidate c { get; init; } = default!;
        public string InterviewerName { get; init; } = default!;
    }

    private IQueryable<InterviewRow> InterviewRows()
        => from i in db.Interviews.AsNoTracking()
           join a in db.JobApplications on i.JobApplicationId equals a.Id
           join j in db.JobOpenings on a.JobOpeningId equals j.Id
           join c in db.Candidates on a.CandidateId equals c.Id
           join e in db.Employees on i.InterviewerEmployeeId equals e.Id
           select new InterviewRow { i = i, a = a, j = j, c = c, InterviewerName = e.FirstName + " " + e.LastName };

    /// <summary>Mine = jahan main interviewer hoon. Warna HR: sab; hiring manager: apni jobs; baqi: apne.</summary>
    private IQueryable<InterviewRow> VisibleInterviews(Guid? me, bool mine)
    {
        var rows = InterviewRows();
        if (mine)
            return me is { } m ? rows.Where(x => x.i.InterviewerEmployeeId == m) : rows.Where(x => false);
        if (access.IsHr)
            return rows;
        return me is { } my ? rows.Where(x => x.i.InterviewerEmployeeId == my || x.j.HiringManagerEmployeeId == my) : rows.Where(x => false);
    }

    /// <summary>Feedback sirf HR, hiring manager aur khud interviewer ko (baqi panel ek doosre se mutasir na ho).</summary>
    private IQueryable<InterviewDto> InterviewItems(IQueryable<InterviewRow> rows, Guid? me, bool canWork)
    {
        var isHr = access.IsHr;
        var canManage = access.CanManage;
        return rows.Select(x => new InterviewDto(
            x.i.Id, x.a.Id, x.c.Id, x.c.FirstName + " " + x.c.LastName, x.c.CurrentTitle, x.c.ResumeUrl, x.j.Id, x.j.Title, x.a.Stage,
            x.i.Title, x.i.ScheduledAt, x.i.DurationMinutes, x.i.Mode, x.i.LocationOrLink, x.i.InterviewerEmployeeId, x.InterviewerName,
            x.i.Status,
            isHr || x.i.InterviewerEmployeeId == me || x.j.HiringManagerEmployeeId == me ? x.i.Rating : null,
            isHr || x.i.InterviewerEmployeeId == me || x.j.HiringManagerEmployeeId == me ? x.i.Recommendation : null,
            isHr || x.i.InterviewerEmployeeId == me || x.j.HiringManagerEmployeeId == me ? x.i.Feedback : null,
            x.i.FeedbackAt,
            x.i.InterviewerEmployeeId == me,
            (canManage || canWork) && x.i.Status == InterviewStatus.Scheduled,
            x.i.InterviewerEmployeeId == me && (x.i.Status == InterviewStatus.Scheduled || x.i.Status == InterviewStatus.Completed)
                && x.a.Stage != ApplicationStage.Hired && x.a.Stage != ApplicationStage.Rejected && x.a.Stage != ApplicationStage.Withdrawn,
            x.i.RowVersion));
    }
}

#endregion

#region Job commands

public sealed class JobCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateJobCommand, Guid>,
    IRequestHandler<UpdateJobCommand>,
    IRequestHandler<DeleteJobCommand>,
    IRequestHandler<SubmitJobCommand>,
    IRequestHandler<ApproveJobCommand>,
    IRequestHandler<SendBackJobCommand>,
    IRequestHandler<HoldJobCommand>,
    IRequestHandler<ResumeJobCommand>,
    IRequestHandler<CloseJobCommand>,
    IRequestHandler<ReopenJobCommand>
{
    private readonly RecruitmentAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateJobCommand c, CancellationToken ct)
    {
        if (!await access.CanRequestAsync(ct))
            throw new UnauthorizedAccessException("Only HR and managers can raise a requisition.");
        var details = await DetailsAsync(c.Data, null, ct);

        var job = JobOpening.Create(currentUser.RequireTenantId(), await NextCodeAsync(ct), details, currentUser.UserId);
        db.JobOpenings.Add(job);
        await SaveAsync(ct);
        return job.Id;
    }

    public async Task Handle(UpdateJobCommand c, CancellationToken ct)
    {
        var job = await JobAsync(c.Id, ct);
        if (!access.CanManage && !(IsRequester(job) && job.Status == JobStatus.Draft))
            throw new UnauthorizedAccessException("You can only change your own draft requisitions.");

        var hired = await db.JobApplications.CountAsync(a => a.JobOpeningId == job.Id && a.Stage == ApplicationStage.Hired, ct);
        if (c.Data.Openings < hired)
            throw new ConflictException($"Openings cannot be fewer than the {hired} already hired.");

        job.Update(await DetailsAsync(c.Data, job, ct));
        await SaveAsync(ct);
    }

    /// <summary>Candidates lag gaye hon to delete nahi — Cancel karo (history bachao).</summary>
    public async Task Handle(DeleteJobCommand c, CancellationToken ct)
    {
        var job = await JobAsync(c.Id, ct);
        var allowed = access.CanManage
            ? job.Status is JobStatus.Draft or JobStatus.Submitted or JobStatus.Cancelled
            : IsRequester(job) && job.Status == JobStatus.Draft;
        if (!allowed)
            throw new ConflictException(access.CanManage ? "An approved job cannot be deleted. Cancel it instead." : "You can only delete your own draft requisitions.");
        if (await db.JobApplications.AnyAsync(a => a.JobOpeningId == job.Id, ct))
            throw new ConflictException("This job has candidates. Cancel it instead of deleting.");

        db.JobOpenings.Remove(job);
        await SaveAsync(ct);
    }

    public async Task Handle(SubmitJobCommand c, CancellationToken ct)
    {
        var job = await JobAsync(c.Id, ct);
        if (!access.CanManage && !IsRequester(job))
            throw new UnauthorizedAccessException("Only the requester can submit this requisition.");
        job.Submit(DateTime.UtcNow);
        await SaveAsync(ct);
    }

    public async Task Handle(ApproveJobCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var job = await JobAsync(c.Id, ct);
        job.Approve(currentUser.UserId, DateTime.UtcNow);
        await SaveAsync(ct);
    }

    public async Task Handle(SendBackJobCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var job = await JobAsync(c.Id, ct);
        job.SendBack(c.Note);
        await SaveAsync(ct);
    }

    public async Task Handle(HoldJobCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var job = await JobAsync(c.Id, ct);
        job.Hold();
        await SaveAsync(ct);
    }

    public async Task Handle(ResumeJobCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var job = await JobAsync(c.Id, ct);
        job.Resume();
        await SaveAsync(ct);
    }

    /// <summary>Filled ke liye kam az kam ek hire chahiye. Band karne par baqi active candidates "Rejected" nahi hote — HR khud bataye.</summary>
    public async Task Handle(CloseJobCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var job = await JobAsync(c.Id, ct);
        if (c.Data.Filled && !await db.JobApplications.AnyAsync(a => a.JobOpeningId == job.Id && a.Stage == ApplicationStage.Hired, ct))
            throw new ConflictException("No one has been hired for this job yet. Cancel it instead.");
        job.Close(c.Data.Filled, c.Data.Note, DateTime.UtcNow);
        await SaveAsync(ct);
    }

    public async Task Handle(ReopenJobCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var job = await JobAsync(c.Id, ct);
        job.Reopen();
        await SaveAsync(ct);
    }

    private bool IsRequester(JobOpening job) => job.RequestedByUserId is not null && job.RequestedByUserId == currentUser.UserId;

    /// <summary>References check. Manager (HR nahi) ki requisition ka hiring manager wo khud.</summary>
    private async Task<JobDetails> DetailsAsync(SaveJobRequest d, JobOpening? existing, CancellationToken ct)
    {
        var hiringManager = d.HiringManagerEmployeeId is { } h && h != Guid.Empty ? h : (Guid?)null;
        if (!access.CanManage)
            hiringManager = existing?.HiringManagerEmployeeId ?? await access.MeAsync(ct);

        var currentDepartment = existing?.DepartmentId;
        if (!await db.Departments.AnyAsync(x => x.Id == d.DepartmentId && (x.IsActive || x.Id == currentDepartment), ct))
            throw new NotFoundException("Department", d.DepartmentId);
        if (d.DesignationId is { } ds && ds != Guid.Empty && ds != existing?.DesignationId && !await db.Designations.AnyAsync(x => x.Id == ds && x.IsActive, ct))
            throw new NotFoundException("Designation", ds);
        if (d.LocationId is { } l && l != Guid.Empty && l != existing?.LocationId && !await db.Locations.AnyAsync(x => x.Id == l && x.IsActive, ct))
            throw new NotFoundException("Location", l);
        if (hiringManager is { } hm && hm != existing?.HiringManagerEmployeeId
            && !await db.Employees.AnyAsync(e => e.Id == hm && e.EmploymentStatus != EmploymentStatus.Exited, ct))
            throw new NotFoundException("Hiring manager", hm);
        if (d.Reason == JobReason.Replacement && d.ReplacesEmployeeId is { } r && r != Guid.Empty && !await db.Employees.AnyAsync(e => e.Id == r, ct))
            throw new NotFoundException("Employee", r);

        return new JobDetails(d.Title, d.DepartmentId, d.DesignationId, d.LocationId, hiringManager, d.EmploymentType, d.Openings, d.Reason,
            d.ReplacesEmployeeId, d.TargetStartDate, d.SalaryMin, d.SalaryMax, d.Description, d.Requirements);
    }

    /// <summary>REQ-0001, REQ-0002 ... (deleted bhi gino taake code dobara na mile).</summary>
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var tenantId = currentUser.RequireTenantId();
        var codes = await db.JobOpenings.IgnoreQueryFilters().AsNoTracking()
            .Where(j => j.TenantId == tenantId && j.Code.StartsWith(JobOpening.CodePrefix))
            .Select(j => j.Code).ToListAsync(ct);
        var max = codes.Select(t => int.TryParse(t[JobOpening.CodePrefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{JobOpening.CodePrefix}{max + 1:D4}";
    }

    private async Task<JobOpening> JobAsync(Guid id, CancellationToken ct)
    {
        var job = await db.JobOpenings.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Job", id);
        if (!await access.CanSeeJobAsync(job, ct))
            throw new NotFoundException("Job", id);
        return job;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_JobOpenings_TenantId_Code") == true)
        {
            throw new ConflictException("Someone created a job at the same moment. Try again.");
        }
    }
}

#endregion

#region Candidate + application commands

public sealed class CandidateCommandHandlers(IAppDbContext db, ICurrentUser currentUser, ISender sender) :
    IRequestHandler<CreateCandidateCommand, Guid>,
    IRequestHandler<UpdateCandidateCommand>,
    IRequestHandler<DeleteCandidateCommand>,
    IRequestHandler<CreateApplicationCommand, Guid>,
    IRequestHandler<MoveApplicationCommand>,
    IRequestHandler<AddApplicationNoteCommand>,
    IRequestHandler<RateApplicationCommand>,
    IRequestHandler<MakeOfferCommand>,
    IRequestHandler<OfferResponseCommand>,
    IRequestHandler<HireCommand, HireResultDto>
{
    private readonly RecruitmentAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateCandidateCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var d = c.Data;
        await EnsureEmailIsFreeAsync(d.Email, null, ct);
        await EnsureReferrerAsync(d, null, ct);

        JobOpening? job = null;
        if (d.JobId is { } jobId && jobId != Guid.Empty)
        {
            job = await db.JobOpenings.FirstOrDefaultAsync(j => j.Id == jobId, ct) ?? throw new NotFoundException("Job", jobId);
            job.EnsureAcceptsCandidates();
        }

        var tenantId = currentUser.RequireTenantId();
        var candidate = Candidate.Create(tenantId, ToDetails(d));
        db.Candidates.Add(candidate);
        // Id EF Add par hi bana deta hai, is liye application isi SaveChanges mein ja sakti hai
        if (job is not null)
            db.JobApplications.Add(JobApplication.Create(tenantId, job, candidate.Id, currentUser.UserId, DateTime.UtcNow));
        await SaveAsync(ct);
        return candidate.Id;
    }

    public async Task Handle(UpdateCandidateCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var candidate = await db.Candidates.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Candidate", c.Id);
        await EnsureEmailIsFreeAsync(c.Data.Email, candidate.Id, ct);
        await EnsureReferrerAsync(c.Data, candidate.ReferredByEmployeeId, ct);
        candidate.Update(ToDetails(c.Data));
        await SaveAsync(ct);
    }

    public async Task Handle(DeleteCandidateCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var candidate = await db.Candidates.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Candidate", c.Id);
        if (await db.JobApplications.AnyAsync(a => a.CandidateId == candidate.Id, ct))
            throw new ConflictException("This candidate has applications. They stay in the talent pool for the record.");
        db.Candidates.Remove(candidate);
        await SaveAsync(ct);
    }

    public async Task<Guid> Handle(CreateApplicationCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var job = await db.JobOpenings.FirstOrDefaultAsync(j => j.Id == c.Data.JobId, ct) ?? throw new NotFoundException("Job", c.Data.JobId);
        if (!await db.Candidates.AnyAsync(x => x.Id == c.Data.CandidateId, ct))
            throw new NotFoundException("Candidate", c.Data.CandidateId);
        if (await db.JobApplications.AnyAsync(a => a.JobOpeningId == job.Id && a.CandidateId == c.Data.CandidateId, ct))
            throw new ConflictException("This candidate is already in this job's pipeline.");

        var app = JobApplication.Create(currentUser.RequireTenantId(), job, c.Data.CandidateId, currentUser.UserId, DateTime.UtcNow);
        db.JobApplications.Add(app);
        await SaveAsync(ct);
        return app.Id;
    }

    public async Task Handle(MoveApplicationCommand c, CancellationToken ct)
    {
        var (app, job) = await ApplicationAsync(c.Id, ct);
        await access.EnsureCanWorkPipelineAsync(job, ct);
        job.EnsureActive();
        app.MoveTo(c.Data.Stage, c.Data.Note, currentUser.UserId, DateTime.UtcNow);
        await CancelOpenInterviewsAsync(app, ct);
        await SaveAsync(ct);
    }

    public async Task Handle(AddApplicationNoteCommand c, CancellationToken ct)
    {
        var (app, job) = await ApplicationAsync(c.Id, ct);
        await access.EnsureCanWorkPipelineAsync(job, ct);
        app.AddNote(c.Note, currentUser.UserId, DateTime.UtcNow);
        await SaveAsync(ct);
    }

    public async Task Handle(RateApplicationCommand c, CancellationToken ct)
    {
        var (app, job) = await ApplicationAsync(c.Id, ct);
        await access.EnsureCanWorkPipelineAsync(job, ct);
        app.Rate(c.Rating);
        await SaveAsync(ct);
    }

    public async Task Handle(MakeOfferCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var (app, job) = await ApplicationAsync(c.Id, ct);
        job.EnsureActive();
        var d = c.Data;
        app.MakeOffer(d.Salary, d.StartDate, d.ExpiresOn, d.Note, currentUser.UserId, DateTime.UtcNow);
        await SaveAsync(ct);
    }

    public async Task Handle(OfferResponseCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var (app, job) = await ApplicationAsync(c.Id, ct);
        job.EnsureActive();
        app.RecordOfferResponse(c.Data.Accepted, c.Data.Note, currentUser.UserId, DateTime.UtcNow);
        await CancelOpenInterviewsAsync(app, ct);
        await SaveAsync(ct);
    }

    /// <summary>
    /// Employee aur hire ek hi SaveChanges mein (dono ya koi nahi). Onboarding baad mein alag — wo fail ho to hire phir bhi ho chuka,
    /// jawab mein wajah aati hai aur HR Onboarding page se shuru kar sakta hai.
    /// </summary>
    public async Task<HireResultDto> Handle(HireCommand c, CancellationToken ct)
    {
        if (!access.CanHire)
            throw new UnauthorizedAccessException("You do not have permission to hire.");
        var (app, job) = await ApplicationAsync(c.Id, ct);
        job.EnsureActive();
        app.EnsureCanHire();
        var d = c.Data;
        var tenantId = currentUser.RequireTenantId();

        Employee employee;
        if (d.ExistingEmployeeId is { } existingId && existingId != Guid.Empty)
        {
            employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == existingId, ct) ?? throw new NotFoundException("Employee", existingId);
            if (employee.EmploymentStatus == EmploymentStatus.Exited)
                throw new ConflictException("This employee has exited. Create a new employee record instead.");
        }
        else
        {
            var candidate = await db.Candidates.AsNoTracking().FirstAsync(x => x.Id == app.CandidateId, ct);
            var locationId = d.LocationId ?? job.LocationId ?? throw new ConflictException("Select a location for the new employee.");
            var departmentId = d.DepartmentId ?? job.DepartmentId;
            var designationId = d.DesignationId ?? job.DesignationId ?? throw new ConflictException("Select a designation for the new employee.");
            var managerId = d.ManagerId ?? job.HiringManagerEmployeeId;
            await EmployeeReferenceChecks.EnsureJobReferencesAsync(db, locationId, departmentId, designationId, managerId, ct);

            var workEmail = d.WorkEmail!.Trim();
            if (await db.Employees.AnyAsync(e => e.WorkEmail == workEmail, ct))
                throw new ConflictException($"An employee with work email '{workEmail}' already exists.");
            var code = string.IsNullOrWhiteSpace(d.EmployeeCode)
                ? await CreateEmployeeHandler.GenerateCodeAsync(db, tenantId, ct)
                : d.EmployeeCode.Trim().ToUpperInvariant();
            if (await db.Employees.AnyAsync(e => e.EmployeeCode == code, ct))
                throw new ConflictException($"Employee code '{code}' is already in use.");

            employee = Employee.Create(tenantId, code, candidate.FirstName, null, candidate.LastName, workEmail,
                locationId, departmentId, designationId, managerId, d.EmploymentType ?? job.EmploymentType, d.JoiningDate, d.ProbationEndDate);
            db.Employees.Add(employee);
        }

        var now = DateTime.UtcNow;
        // Employee ka Id Add par ban jata hai (EF value generator)
        app.Hire(employee.Id, currentUser.UserId, now);
        await CancelOpenInterviewsAsync(app, ct);

        var hired = await db.JobApplications.CountAsync(a => a.JobOpeningId == job.Id && a.Stage == ApplicationStage.Hired && a.Id != app.Id, ct) + 1;
        var filled = hired >= job.Openings;
        if (filled)
            job.Close(true, "All positions filled.", now);

        await SaveAsync(ct);

        Guid? caseId = null;
        string? onboardingError = null;
        if (d.StartOnboarding)
        {
            try
            {
                caseId = await sender.Send(new StartOnboardingCommand(employee.Id, d.OnboardingTemplateId, $"Hired for {job.Code} {job.Title}."), ct);
            }
            catch (Exception ex) when (ex is DomainException or ConflictException or NotFoundException or UnauthorizedAccessException or ValidationException)
            {
                onboardingError = ex.Message;
            }
        }
        return new HireResultDto(employee.Id, employee.EmployeeCode, caseId, onboardingError, filled);
    }

    /// <summary>Pipeline se nikla (hired / rejected / withdrawn) to aage wale interviews ka koi matlab nahi.</summary>
    private async Task CancelOpenInterviewsAsync(JobApplication app, CancellationToken ct)
    {
        if (app.IsActive)
            return;
        var open = await db.Interviews.Where(i => i.JobApplicationId == app.Id && i.Status == InterviewStatus.Scheduled).ToListAsync(ct);
        foreach (var interview in open)
            interview.Cancel();
    }

    private async Task<(JobApplication, JobOpening)> ApplicationAsync(Guid id, CancellationToken ct)
    {
        var app = await db.JobApplications.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Application", id);
        var job = await db.JobOpenings.FirstAsync(j => j.Id == app.JobOpeningId, ct);
        if (!access.IsHr && !await access.IsHiringManagerAsync(job, ct))
            throw new NotFoundException("Application", id);
        return (app, job);
    }

    private async Task EnsureEmailIsFreeAsync(string email, Guid? exceptId, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var other = await db.Candidates.AsNoTracking().Where(x => x.Email == normalized && x.Id != exceptId)
            .Select(x => x.FirstName + " " + x.LastName).FirstOrDefaultAsync(ct);
        if (other is not null)
            throw new ConflictException($"{other} already uses {normalized}. Open that candidate instead.");
    }

    private async Task EnsureReferrerAsync(SaveCandidateRequest d, Guid? current, CancellationToken ct)
    {
        if (d.Source == CandidateSource.Referral && d.ReferredByEmployeeId is { } id && id != Guid.Empty && id != current
            && !await db.Employees.AnyAsync(e => e.Id == id, ct))
            throw new NotFoundException("Employee", id);
    }

    private static CandidateDetails ToDetails(SaveCandidateRequest d)
        => new(d.FirstName, d.LastName, d.Email, d.Phone, d.City, d.CurrentCompany, d.CurrentTitle, d.ExperienceYears, d.Source,
            d.ReferredByEmployeeId, d.ResumeUrl, d.LinkedInUrl, d.Notes);

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_Candidates_TenantId_Email") == true)
        {
            throw new ConflictException("A candidate with this email already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_JobApplications_Job_Candidate") == true)
        {
            throw new ConflictException("This candidate is already in this job's pipeline.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Employees_TenantId_EmployeeCode") == true)
        {
            throw new ConflictException("That employee code was just taken. Try again.");
        }
    }
}

#endregion

#region Interview commands

public sealed class InterviewCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<ScheduleInterviewCommand, Guid>,
    IRequestHandler<UpdateInterviewCommand>,
    IRequestHandler<CancelInterviewCommand>,
    IRequestHandler<NoShowInterviewCommand>,
    IRequestHandler<InterviewFeedbackCommand>
{
    private readonly RecruitmentAccess access = new(db, currentUser);

    /// <summary>Applied/Screening wala candidate interview lagte hi Interview stage mein chala jata hai.</summary>
    public async Task<Guid> Handle(ScheduleInterviewCommand c, CancellationToken ct)
    {
        var app = await db.JobApplications.FirstOrDefaultAsync(a => a.Id == c.ApplicationId, ct) ?? throw new NotFoundException("Application", c.ApplicationId);
        var job = await db.JobOpenings.FirstAsync(j => j.Id == app.JobOpeningId, ct);
        await access.EnsureCanWorkPipelineAsync(job, ct);
        job.EnsureActive();
        if (!app.IsActive)
            throw new ConflictException("This candidate is no longer in the pipeline.");
        await EnsureInterviewerAsync(c.Data.InterviewerEmployeeId, null, ct);

        var interview = Interview.Create(currentUser.RequireTenantId(), app.Id, ToDetails(c.Data));
        db.Interviews.Add(interview);
        if (app.Stage is ApplicationStage.Applied or ApplicationStage.Screening)
            app.MoveTo(ApplicationStage.Interview, null, currentUser.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return interview.Id;
    }

    public async Task Handle(UpdateInterviewCommand c, CancellationToken ct)
    {
        var (interview, _) = await ManageableAsync(c.Id, ct);
        await EnsureInterviewerAsync(c.Data.InterviewerEmployeeId, interview.InterviewerEmployeeId, ct);
        interview.Update(ToDetails(c.Data));
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelInterviewCommand c, CancellationToken ct)
    {
        var (interview, _) = await ManageableAsync(c.Id, ct);
        interview.Cancel();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(NoShowInterviewCommand c, CancellationToken ct)
    {
        var (interview, _) = await ManageableAsync(c.Id, ct);
        interview.MarkNoShow();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Sirf interviewer khud (HR bhi kisi aur ki taraf se nahi likhta).</summary>
    public async Task Handle(InterviewFeedbackCommand c, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == c.Id, ct) ?? throw new NotFoundException("Interview", c.Id);
        var me = await access.MeAsync(ct);
        if (me is null || interview.InterviewerEmployeeId != me)
            throw new UnauthorizedAccessException("Only the interviewer can give feedback.");
        var stage = await db.JobApplications.Where(a => a.Id == interview.JobApplicationId).Select(a => a.Stage).FirstAsync(ct);
        if (stage is ApplicationStage.Hired or ApplicationStage.Rejected or ApplicationStage.Withdrawn)
            throw new ConflictException("This candidate is no longer in the pipeline.");

        interview.SubmitFeedback(c.Data.Rating, c.Data.Recommendation, c.Data.Feedback, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<(Interview, JobOpening)> ManageableAsync(Guid id, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Interview", id);
        var job = await (from a in db.JobApplications
                         join j in db.JobOpenings on a.JobOpeningId equals j.Id
                         where a.Id == interview.JobApplicationId
                         select j).FirstAsync(ct);
        await access.EnsureCanWorkPipelineAsync(job, ct);
        return (interview, job);
    }

    private async Task EnsureInterviewerAsync(Guid employeeId, Guid? current, CancellationToken ct)
    {
        if (employeeId == current)
            return;
        var status = await db.Employees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => (EmploymentStatus?)e.EmploymentStatus).FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Interviewer", employeeId);
        if (status == EmploymentStatus.Exited)
            throw new ConflictException("The interviewer has exited.");
    }

    private static InterviewDetails ToDetails(SaveInterviewRequest d)
        => new(d.Title, d.ScheduledAt, d.DurationMinutes, d.Mode, d.LocationOrLink, d.InterviewerEmployeeId);
}

#endregion
