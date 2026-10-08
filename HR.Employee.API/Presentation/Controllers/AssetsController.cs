namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Assets;
using HR.Employee.API.Domain.Assets;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Assets page: inventory, assignments, returns, history (HR) + my assets (har koi).
/// Gateway /assets/{everything} — isliye list bhi "items" sub-path par (khali path route nahi hota).
/// Category ka access handler mein (employees.edit ya settings.manage).
/// </summary>
[ApiController]
[Authorize]
[Route("api/assets")]
public sealed class AssetsController(ISender mediator) : ControllerBase
{
    // ---- Overview ----

    [HttpGet("summary")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<AssetSummaryDto>> Summary(CancellationToken ct)
        => Ok(await mediator.Send(new GetAssetSummaryQuery(), ct));

    [HttpGet("items")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<IReadOnlyList<AssetListItemDto>>> List(
        [FromQuery] AssetStatus? status, [FromQuery] Guid? categoryId, [FromQuery] string? search, CancellationToken ct)
        => Ok(await mediator.Send(new GetAssetsQuery(status, categoryId, search), ct));

    [HttpGet("items/{id:guid}")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<AssetDto>> Get(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetAssetQuery(id), ct));

    [HttpGet("next-tag")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<ActionResult<object>> NextTag(CancellationToken ct)
        => Ok(new { tag = await mediator.Send(new GetNextAssetTagQuery(), ct) });

    [HttpGet("assignments")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<IReadOnlyList<AssetAssignmentDto>>> Assignments(
        [FromQuery] AssignmentState state = AssignmentState.Open, [FromQuery] Guid? employeeId = null, [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new GetAssetAssignmentsQuery(state, employeeId, search), ct));

    [HttpGet("history")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<IReadOnlyList<AssetEventDto>>> History(
        [FromQuery] Guid? assetId, [FromQuery] AssetEventType? type, [FromQuery] string? search,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await mediator.Send(new GetAssetHistoryQuery(assetId, type, search, from, to), ct));

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<MyAssetDto>>> Mine(CancellationToken ct)
        => Ok(await mediator.Send(new GetMyAssetsQuery(), ct));

    [HttpPost("my/{assignmentId:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid assignmentId, CancellationToken ct)
    {
        await mediator.Send(new AcknowledgeAssetCommand(assignmentId), ct);
        return NoContent();
    }

    // ---- Register ----

    [HttpPost("items")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Create([FromBody] SaveAssetRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateAssetCommand(request), ct);
        return Created($"api/assets/items/{id}", new { id });
    }

    [HttpPut("items/{id:guid}")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveAssetRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateAssetCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("items/{id:guid}")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteAssetCommand(id), ct);
        return NoContent();
    }

    [HttpPost("items/{id:guid}/assign")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignAssetRequest request, CancellationToken ct)
    {
        var assignmentId = await mediator.Send(new AssignAssetCommand(id, request), ct);
        return Ok(new { id = assignmentId });
    }

    [HttpPost("items/{id:guid}/return")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnAssetRequest request, CancellationToken ct)
    {
        await mediator.Send(new ReturnAssetCommand(id, request), ct);
        return NoContent();
    }

    [HttpPut("items/{id:guid}/due-back")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> DueBack(Guid id, [FromBody] DueBackRequest request, CancellationToken ct)
    {
        await mediator.Send(new ChangeDueBackCommand(id, request.DueBack), ct);
        return NoContent();
    }

    [HttpPost("items/{id:guid}/status")]
    [HasPermission(Permissions.EmployeesEdit)]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusRequest request, CancellationToken ct)
    {
        await mediator.Send(new ChangeAssetStatusCommand(id, request.Status, request.Note), ct);
        return NoContent();
    }

    // ---- Categories ----

    [HttpGet("categories")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<IReadOnlyList<AssetCategoryDto>>> Categories([FromQuery] bool includeInactive = true, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetAssetCategoriesQuery(includeInactive), ct));

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] SaveAssetCategoryRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new CreateAssetCategoryCommand(request), ct);
        return Created($"api/assets/categories/{id}", new { id });
    }

    [HttpPost("categories/starter")]
    public async Task<ActionResult<object>> CreateStarterCategories(CancellationToken ct)
        => Ok(new { created = await mediator.Send(new CreateStarterAssetCategoriesCommand(), ct) });

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] SaveAssetCategoryRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateAssetCategoryCommand(id, request), ct);
        return NoContent();
    }

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteAssetCategoryCommand(id), ct);
        return NoContent();
    }

    public sealed record DueBackRequest(DateOnly? DueBack);
    public sealed record StatusRequest(AssetStatus Status, string? Note);
}
