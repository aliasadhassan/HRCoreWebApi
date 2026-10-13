namespace HR.Payroll.API.Presentation.Controllers;

using HR.Payroll.API.Application.ReportBuilder;
using HR.Shared.Library.Reporting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public sealed record PayrollRunReportBody(string Source, ReportSpec Spec, DateOnly? From, DateOnly? To);
public sealed record LetterPayBody(IReadOnlyList<Guid> EmployeeIds);

/// <summary>
/// Report builder ke payroll sources aur salary certificate ke figures. Saved reports / templates Employee API par
/// (/report-builder). Gateway: /payroll/report-builder/{x}.
/// </summary>
[ApiController]
[Authorize]
[Route("api/payroll/report-builder")]
public sealed class PayrollReportBuilderController(ISender mediator) : ControllerBase
{
    [HttpGet("sources")]
    public async Task<ActionResult<IReadOnlyList<PayrollSourceDto>>> Sources(CancellationToken ct)
        => Ok(await mediator.Send(new GetPayrollSourcesQuery(), ct));

    [HttpPost("run")]
    public async Task<ActionResult<PayrollRunResultDto>> Run([FromBody] PayrollRunReportBody body, CancellationToken ct)
        => Ok(await mediator.Send(new RunPayrollReportQuery(body.Source, body.Spec ?? new ReportSpec(), body.From, body.To), ct));

    [HttpPost("letter-pay")]
    public async Task<ActionResult<IReadOnlyList<LetterPayDto>>> LetterPay([FromBody] LetterPayBody body, CancellationToken ct)
        => Ok(await mediator.Send(new GetLetterPayQuery(body.EmployeeIds ?? []), ct));
}
