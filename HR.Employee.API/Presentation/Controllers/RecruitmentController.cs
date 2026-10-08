namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Recruitment;
using HR.Employee.API.Domain.Recruitment;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Recruitment page: requisitions/jobs, candidates, pipeline, interviews, headcount.
/// Hiring managers aur interviewers ke paas koi khaas permission nahi — jaanch handler mein (RecruitmentAccess).
/// Gateway /recruitment/{everything}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/recruitment")]
public sealed class RecruitmentController(ISender mediator) : ControllerBase
{
    // ---- Overview ----

    [HttpGet("summary")]
    public async Task<ActionResult<RecruitmentSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetRecruitmentSummaryQuery(), ct));

    [HttpGet("lookups")]
    public async Task<ActionResult<RecruitmentLookupsDto>> Lookups(CancellationToken ct)
        => Ok(await mediator.Send(new GetRecruitmentLookupsQuery(), ct));

    [HttpGet("headcount")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<IReadOnlyList<HeadcountRowDto>>> Headcount(CancellationToken ct)
        => Ok(await mediator.Send(new GetHeadcountQuery(), ct));

    // ---- Jobs / requisitions ----

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<JobListItemDto>>> Jobs(
        [FromQuery] JobStatus? status, [FromQuery] Guid? departmentId, [FromQuery] string? search, CancellationToken ct)
        => Ok(await mediator.Send(new GetJobsQuery(status, departmentId, search), ct));

    [HttpGet("jobs/{id:guid}")]
    public async Task<ActionResult<JobDto>> Job(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetJobQuery(id), ct));

    [HttpPost("jobs")]
    public async Task<IActionResult> CreateJob([FromBody] SaveJobRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateJobCommand(body), ct);
        return Created($"api/recruitment/jobs/{id}", new { id });
    }

    [HttpPut("jobs/{id:guid}")]
    public async Task<IActionResult> UpdateJob(Guid id, [FromBody] SaveJobRequest body, CancellationToken ct)
    {
        await mediator.Send(new UpdateJobCommand(id, body), ct);
        return NoContent();
    }

    [HttpDelete("jobs/{id:guid}")]
    public async Task<IActionResult> DeleteJob(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteJobCommand(id), ct);
        return NoContent();
    }

    [HttpPost("jobs/{id:guid}/submit")]
    public async Task<IActionResult> SubmitJob(Guid id, CancellationToken ct)
    {
        await mediator.Send(new SubmitJobCommand(id), ct);
        return NoContent();
    }

    [HttpPost("jobs/{id:guid}/approve")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> ApproveJob(Guid id, CancellationToken ct)
    {
        await mediator.Send(new ApproveJobCommand(id), ct);
        return NoContent();
    }

    [HttpPost("jobs/{id:guid}/send-back")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> SendBackJob(Guid id, [FromBody] RecruitmentNoteBody body, CancellationToken ct)
    {
        await mediator.Send(new SendBackJobCommand(id, body.Note ?? ""), ct);
        return NoContent();
    }

    [HttpPost("jobs/{id:guid}/hold")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> HoldJob(Guid id, CancellationToken ct)
    {
        await mediator.Send(new HoldJobCommand(id), ct);
        return NoContent();
    }

    [HttpPost("jobs/{id:guid}/resume")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> ResumeJob(Guid id, CancellationToken ct)
    {
        await mediator.Send(new ResumeJobCommand(id), ct);
        return NoContent();
    }

    [HttpPost("jobs/{id:guid}/close")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> CloseJob(Guid id, [FromBody] CloseJobRequest body, CancellationToken ct)
    {
        await mediator.Send(new CloseJobCommand(id, body), ct);
        return NoContent();
    }

    [HttpPost("jobs/{id:guid}/reopen")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> ReopenJob(Guid id, CancellationToken ct)
    {
        await mediator.Send(new ReopenJobCommand(id), ct);
        return NoContent();
    }

    // ---- Candidates ----

    [HttpGet("candidates")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<IReadOnlyList<CandidateListItemDto>>> Candidates(
        [FromQuery] CandidateSource? source, [FromQuery] string? search, CancellationToken ct)
        => Ok(await mediator.Send(new GetCandidatesQuery(source, search), ct));

    [HttpGet("candidates/{id:guid}")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<CandidateDto>> Candidate(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetCandidateQuery(id), ct));

    [HttpPost("candidates")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> CreateCandidate([FromBody] SaveCandidateRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateCandidateCommand(body), ct);
        return Created($"api/recruitment/candidates/{id}", new { id });
    }

    [HttpPut("candidates/{id:guid}")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> UpdateCandidate(Guid id, [FromBody] SaveCandidateRequest body, CancellationToken ct)
    {
        await mediator.Send(new UpdateCandidateCommand(id, body), ct);
        return NoContent();
    }

    [HttpDelete("candidates/{id:guid}")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> DeleteCandidate(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteCandidateCommand(id), ct);
        return NoContent();
    }

    // ---- Applications (pipeline) ----

    [HttpGet("applications")]
    public async Task<ActionResult<IReadOnlyList<ApplicationListItemDto>>> Applications(
        [FromQuery] Guid? jobId, [FromQuery] ApplicationStage? stage, [FromQuery] bool? active, [FromQuery] string? search, CancellationToken ct)
        => Ok(await mediator.Send(new GetApplicationsQuery(jobId, stage, active, search), ct));

    [HttpGet("applications/{id:guid}")]
    public async Task<ActionResult<ApplicationDto>> Application(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetApplicationQuery(id), ct));

    [HttpPost("applications")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> CreateApplication([FromBody] CreateApplicationRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateApplicationCommand(body), ct);
        return Created($"api/recruitment/applications/{id}", new { id });
    }

    [HttpPost("applications/{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, [FromBody] MoveApplicationRequest body, CancellationToken ct)
    {
        await mediator.Send(new MoveApplicationCommand(id, body), ct);
        return NoContent();
    }

    [HttpPost("applications/{id:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] RecruitmentNoteBody body, CancellationToken ct)
    {
        await mediator.Send(new AddApplicationNoteCommand(id, body.Note ?? ""), ct);
        return NoContent();
    }

    [HttpPut("applications/{id:guid}/rating")]
    public async Task<IActionResult> Rate(Guid id, [FromBody] RateApplicationBody body, CancellationToken ct)
    {
        await mediator.Send(new RateApplicationCommand(id, body.Rating), ct);
        return NoContent();
    }

    [HttpPost("applications/{id:guid}/offer")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Offer(Guid id, [FromBody] MakeOfferRequest body, CancellationToken ct)
    {
        await mediator.Send(new MakeOfferCommand(id, body), ct);
        return NoContent();
    }

    [HttpPost("applications/{id:guid}/offer-response")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> OfferResponse(Guid id, [FromBody] OfferResponseRequest body, CancellationToken ct)
    {
        await mediator.Send(new OfferResponseCommand(id, body), ct);
        return NoContent();
    }

    [HttpPost("applications/{id:guid}/hire")]
    [HasPermission(Permissions.EmployeesCreate)]
    public async Task<ActionResult<HireResultDto>> Hire(Guid id, [FromBody] HireRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new HireCommand(id, body), ct));

    [HttpPost("applications/{id:guid}/interviews")]
    public async Task<IActionResult> Schedule(Guid id, [FromBody] SaveInterviewRequest body, CancellationToken ct)
    {
        var interviewId = await mediator.Send(new ScheduleInterviewCommand(id, body), ct);
        return Created($"api/recruitment/interviews/{interviewId}", new { id = interviewId });
    }

    // ---- Interviews ----

    [HttpGet("interviews")]
    public async Task<ActionResult<IReadOnlyList<InterviewDto>>> Interviews(
        [FromQuery] bool? mine, [FromQuery] InterviewStatus? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await mediator.Send(new GetInterviewsQuery(mine == true, status, from, to), ct));

    [HttpPut("interviews/{id:guid}")]
    public async Task<IActionResult> UpdateInterview(Guid id, [FromBody] SaveInterviewRequest body, CancellationToken ct)
    {
        await mediator.Send(new UpdateInterviewCommand(id, body), ct);
        return NoContent();
    }

    [HttpPost("interviews/{id:guid}/cancel")]
    public async Task<IActionResult> CancelInterview(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelInterviewCommand(id), ct);
        return NoContent();
    }

    [HttpPost("interviews/{id:guid}/no-show")]
    public async Task<IActionResult> NoShow(Guid id, CancellationToken ct)
    {
        await mediator.Send(new NoShowInterviewCommand(id), ct);
        return NoContent();
    }

    [HttpPost("interviews/{id:guid}/feedback")]
    public async Task<IActionResult> Feedback(Guid id, [FromBody] InterviewFeedbackRequest body, CancellationToken ct)
    {
        await mediator.Send(new InterviewFeedbackCommand(id, body), ct);
        return NoContent();
    }
}

public sealed record RecruitmentNoteBody(string? Note);
public sealed record RateApplicationBody(byte? Rating);
