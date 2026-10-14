namespace HR.Employee.API.Presentation.Controllers;

using HR.Employee.API.Application.Imports;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Bulk import (Excel): locations, departments, designations, employees.
///   GET  api/imports/{kind}/template        → .xlsx template (company ke codes dropdown mein)
///   POST api/imports/{kind}?dryRun=true      → sirf check, kuch save nahi
///   POST api/imports/{kind}                  → check + sab rows ek transaction mein; ek bhi ghalti = kuch save nahi
/// Jawab hamesha 200 + ImportResult (Succeeded false ho to Errors mein row-wise ghaltiyan).
/// </summary>
[ApiController]
[Authorize]
[HasPermission(Permissions.EmployeesCreate)]
[Route("api/imports")]
public sealed class ImportsController(ISender mediator) : ControllerBase
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("{kind}/template")]
    public async Task<IActionResult> Template(ImportKind kind, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind))
            return NotFound();

        var bytes = await mediator.Send(new GetImportTemplateQuery(kind), ct);
        return File(bytes, XlsxType, $"{kind.ToString().ToLowerInvariant()}-import-template.xlsx");
    }

    [HttpPost("{kind}")]
    [RequestSizeLimit(ImportWorkbook.MaxFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportWorkbook.MaxFileBytes + 64 * 1024)]
    public async Task<ActionResult<ImportResult>> Import(ImportKind kind, IFormFile file, [FromQuery] bool dryRun, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind))
            return NotFound();
        if (file is null || file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "No file", detail: "Choose an .xlsx file to upload.");
        if (file.Length > ImportWorkbook.MaxFileBytes)
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "File too large",
                           detail: "The file is too large. The limit is 10 MB.");

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return Ok(await mediator.Send(new RunImportCommand(kind, buffer.ToArray(), dryRun), ct));
    }
}
