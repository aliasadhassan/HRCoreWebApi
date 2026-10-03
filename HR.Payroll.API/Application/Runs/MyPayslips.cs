namespace HR.Payroll.API.Application.Runs;

using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

/// <summary>Employee self-service: sirf apni, sirf approved/paid runs ki payslips.</summary>
public sealed record MyPayslipDto(
    Guid Id, Guid PayrollRunId, string PayslipNumber, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly? PayDate,
    string CurrencyCode, decimal GrossEarnings, decimal TotalDeductions, decimal NetPay, RunStatus RunStatus);

public sealed record GetMyPayslipsQuery : IRequest<IReadOnlyList<MyPayslipDto>>;
public sealed record GetMyPayslipQuery(Guid PayslipId) : IRequest<PayslipDto>;

public sealed class MyPayslipHandlers(IAppDbContext db, ICurrentUser currentUser, ISender mediator) :
    IRequestHandler<GetMyPayslipsQuery, IReadOnlyList<MyPayslipDto>>,
    IRequestHandler<GetMyPayslipQuery, PayslipDto>
{
    private static readonly RunStatus[] Visible = [RunStatus.Approved, RunStatus.Paid];

    public async Task<IReadOnlyList<MyPayslipDto>> Handle(GetMyPayslipsQuery request, CancellationToken ct)
    {
        var ids = await MyEmployeeIdsAsync(ct);
        return await (
            from p in db.Payslips.AsNoTracking()
            join r in db.PayrollRuns.AsNoTracking() on p.PayrollRunId equals r.Id
            join pp in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals pp.Id into periods
            from pp in periods.DefaultIfEmpty()
            where ids.Contains(p.EmployeeId) && Visible.Contains(r.Status)
            orderby p.PeriodStart descending
            select new MyPayslipDto(p.Id, p.PayrollRunId, p.PayslipNumber, p.PeriodStart, p.PeriodEnd,
                pp == null ? (DateOnly?)null : pp.PayDate, p.CurrencyCode, p.GrossEarnings, p.TotalDeductions, p.NetPay, r.Status))
            .ToListAsync(ct);
    }

    public async Task<PayslipDto> Handle(GetMyPayslipQuery request, CancellationToken ct)
    {
        var ids = await MyEmployeeIdsAsync(ct);
        var mine = await (
            from p in db.Payslips
            join r in db.PayrollRuns on p.PayrollRunId equals r.Id
            where p.Id == request.PayslipId && ids.Contains(p.EmployeeId) && Visible.Contains(r.Status)
            select p.Id).AnyAsync(ct);
        if (!mine)
            throw new NotFoundException("Payslip", request.PayslipId);
        return await mediator.Send(new GetPayslipByIdQuery(request.PayslipId), ct);
    }

    /// <summary>Payroll ke paas UserId nahi — login email = work email se pehchaan.</summary>
    private async Task<List<Guid>> MyEmployeeIdsAsync(CancellationToken ct)
    {
        var email = currentUser.Email?.Trim().ToLower();
        if (string.IsNullOrEmpty(email)) return [];
        return await db.PayrollEmployees.AsNoTracking()
            .Where(e => e.WorkEmail.ToLower() == email)
            .Select(e => e.Id).ToListAsync(ct);
    }
}
