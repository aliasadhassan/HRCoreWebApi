namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Learning;
using HR.Employee.API.Domain.Learning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Learning &amp; growth page: courses (catalogue + enrollments), skills aur career. Har employee apni learning/skills/career goal;
/// manager apne reports; employees.edit catalogue, skill library aur role skills chalata hai (LearningAccess). Gateway /learning/{everything}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/learning")]
public sealed class LearningController(ISender mediator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<LearningSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetLearningSummaryQuery(), ct));

    [HttpGet("lookups")]
    public async Task<ActionResult<LearningLookupsDto>> Lookups(CancellationToken ct)
        => Ok(await mediator.Send(new GetLearningLookupsQuery(), ct));

    // ---- Courses ----

    [HttpGet("courses")]
    public async Task<ActionResult<IReadOnlyList<CourseListItemDto>>> Courses(
        [FromQuery] CourseCategory? category = null, [FromQuery] string? search = null, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetCoursesQuery(category, search, includeInactive), ct));

    [HttpGet("courses/{id:guid}")]
    public async Task<ActionResult<CourseDetailDto>> Course(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetCourseQuery(id), ct));

    [HttpPost("courses")]
    public async Task<IActionResult> CreateCourse([FromBody] SaveCourseRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateCourseCommand(body), ct);
        return Created($"api/learning/courses/{id}", new { id });
    }

    [HttpPut("courses/{id:guid}")]
    public Task<IActionResult> UpdateCourse(Guid id, [FromBody] SaveCourseRequest body, CancellationToken ct)
        => Run(new UpdateCourseCommand(id, body), ct);

    [HttpDelete("courses/{id:guid}")]
    public Task<IActionResult> DeleteCourse(Guid id, CancellationToken ct)
        => Run(new DeleteCourseCommand(id), ct);

    [HttpPost("courses/{id:guid}/enrol")]
    public async Task<IActionResult> Enrol(Guid id, CancellationToken ct)
        => Ok(new { id = await mediator.Send(new EnrolInCourseCommand(id), ct) });

    [HttpPost("courses/{id:guid}/assign")]
    public async Task<ActionResult<AssignResultDto>> Assign(Guid id, [FromBody] AssignCourseRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new AssignCourseCommand(id, body), ct));

    // ---- Enrollments ----

    [HttpGet("enrollments")]
    public async Task<ActionResult<IReadOnlyList<EnrollmentDto>>> Enrollments(
        [FromQuery] string? scope = null, [FromQuery] EnrollmentStatus? status = null, [FromQuery] bool overdue = false,
        [FromQuery] Guid? courseId = null, [FromQuery] Guid? employeeId = null, [FromQuery] Guid? departmentId = null,
        [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetEnrollmentsQuery(scope, status, overdue, courseId, employeeId, departmentId, search), ct));

    [HttpPut("enrollments/{id:guid}/progress")]
    public Task<IActionResult> Progress(Guid id, [FromBody] ProgressBody body, CancellationToken ct)
        => Run(new SetEnrollmentProgressCommand(id, body.Progress), ct);

    [HttpPost("enrollments/{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, [FromBody] CompleteEnrollmentRequest body, CancellationToken ct)
        => Run(new CompleteEnrollmentCommand(id, body), ct);

    [HttpPost("enrollments/{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => Run(new CancelEnrollmentCommand(id), ct);

    [HttpPut("enrollments/{id:guid}/due")]
    public Task<IActionResult> Due(Guid id, [FromBody] DueBody body, CancellationToken ct)
        => Run(new ChangeEnrollmentDueCommand(id, body.DueOn), ct);

    // ---- Skills ----

    [HttpGet("skills")]
    public async Task<ActionResult<IReadOnlyList<SkillDto>>> Skills([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetSkillsQuery(includeInactive), ct));

    [HttpPost("skills")]
    public async Task<IActionResult> CreateSkill([FromBody] SaveSkillRequest body, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateSkillCommand(body), ct);
        return Created($"api/learning/skills/{id}", new { id });
    }

    [HttpPut("skills/{id:guid}")]
    public Task<IActionResult> UpdateSkill(Guid id, [FromBody] SaveSkillRequest body, CancellationToken ct)
        => Run(new UpdateSkillCommand(id, body), ct);

    [HttpDelete("skills/{id:guid}")]
    public Task<IActionResult> DeleteSkill(Guid id, CancellationToken ct)
        => Run(new DeleteSkillCommand(id), ct);

    [HttpPost("skills/starter")]
    public async Task<IActionResult> StarterSkills(CancellationToken ct)
        => Ok(new { added = await mediator.Send(new AddStarterSkillsCommand(), ct) });

    [HttpGet("skills/matrix")]
    public async Task<ActionResult<SkillMatrixDto>> Matrix(
        [FromQuery] Guid? departmentId = null, [FromQuery] Guid? skillId = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetSkillMatrixQuery(departmentId, skillId, search), ct));

    /// <summary>Mera growth profile (skills, role gaps, career goal).</summary>
    [HttpGet("people/me")]
    public async Task<ActionResult<PersonGrowthDto>> Me(CancellationToken ct)
        => Ok(await mediator.Send(new GetPersonGrowthQuery(null), ct));

    [HttpGet("people/{employeeId:guid}")]
    public async Task<ActionResult<PersonGrowthDto>> Person(Guid employeeId, CancellationToken ct)
        => Ok(await mediator.Send(new GetPersonGrowthQuery(employeeId), ct));

    [HttpPut("people/{employeeId:guid}/skills/{skillId:guid}")]
    public Task<IActionResult> Rate(Guid employeeId, Guid skillId, [FromBody] LevelBody body, CancellationToken ct)
        => Run(new RateSkillCommand(employeeId, skillId, body.Level), ct);

    [HttpDelete("people/{employeeId:guid}/skills/{skillId:guid}")]
    public Task<IActionResult> RemoveSkill(Guid employeeId, Guid skillId, CancellationToken ct)
        => Run(new RemoveSkillCommand(employeeId, skillId), ct);

    [HttpPut("people/{employeeId:guid}/goal")]
    public Task<IActionResult> SetGoal(Guid employeeId, [FromBody] SetCareerGoalRequest body, CancellationToken ct)
        => Run(new SetCareerGoalCommand(employeeId, body), ct);

    [HttpPost("people/{employeeId:guid}/goal/achieved")]
    public Task<IActionResult> Achieved(Guid employeeId, CancellationToken ct)
        => Run(new AchieveCareerGoalCommand(employeeId), ct);

    [HttpDelete("people/{employeeId:guid}/goal")]
    public Task<IActionResult> ClearGoal(Guid employeeId, CancellationToken ct)
        => Run(new ClearCareerGoalCommand(employeeId), ct);

    // ---- Career ----

    [HttpGet("career/roles")]
    public async Task<ActionResult<IReadOnlyList<CareerRoleDto>>> Roles(CancellationToken ct)
        => Ok(await mediator.Send(new GetCareerRolesQuery(), ct));

    [HttpPut("career/roles/{designationId:guid}")]
    public Task<IActionResult> SaveRole(Guid designationId, [FromBody] RoleSkillsBody body, CancellationToken ct)
        => Run(new SaveRoleSkillsCommand(designationId, body.Skills), ct);

    [HttpGet("career/goals")]
    public async Task<ActionResult<IReadOnlyList<CareerGoalRowDto>>> Goals(
        [FromQuery] Guid? departmentId = null, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetCareerGoalsQuery(departmentId, search), ct));

    private async Task<IActionResult> Run(IRequest command, CancellationToken ct)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }
}

public sealed record ProgressBody(byte Progress);
public sealed record DueBody(DateOnly? DueOn);
public sealed record LevelBody(byte Level);
public sealed record RoleSkillsBody(IReadOnlyList<RoleSkillItem> Skills);
