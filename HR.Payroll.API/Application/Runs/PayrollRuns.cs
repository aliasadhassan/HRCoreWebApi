namespace HR.Payroll.API.Application.Runs;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Runs;
using MediatR;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

// ─────────────────────────── DTOs ───────────────────────────
public sealed record PayrollRunDto(
    Guid Id, Guid PayGroupId, string PayGroupName, Guid PayPeriodId, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly PayDate,
    RunType RunType, RunStatus Status, string CurrencyCode,
    int TotalEmployees, int ProcessedEmployees, decimal TotalGross, decimal TotalDeductions, decimal TotalNet, decimal TotalEmployerCost,
    DateTime? CalculatedAt, DateTime? ApprovedAt, string? FailureReason);

public sealed record PayslipListItemDto(
    Guid Id, Guid EmployeeId, string PayslipNumber, string EmployeeCode, string EmployeeName, string? DepartmentName,
    decimal PayableDays, decimal GrossEarnings, decimal TotalDeductions, decimal TaxAmount, decimal NetPay,
    PayslipStatus Status, string? HoldReason);

public sealed record PayslipLineDto(
    string ComponentCode, string ComponentName, ComponentType ComponentType, decimal Amount,
    decimal? Quantity, decimal? Rate, bool IsTaxable, LineSource Source);

public sealed record PayslipDto(
    Guid Id, Guid PayrollRunId, string PayslipNumber, string EmployeeCode, string EmployeeName,
    string? DepartmentName, string? DesignationTitle, string? BankAccountMasked, string CurrencyCode,
    DateOnly PeriodStart, DateOnly PeriodEnd, decimal PeriodDays, decimal PayableDays, decimal UnpaidLeaveDays,
    decimal GrossEarnings, decimal TotalDeductions, decimal TaxAmount, decimal NetPay, decimal EmployerContributions,
    decimal TaxableIncome, PayslipStatus Status, string? HoldReason, DateTime CalculatedAt,
    IReadOnlyList<PayslipLineDto> Lines);

// ───────────────────────── Requests ─────────────────────────
public sealed record CreatePayrollRunCommand(Guid PayGroupId, Guid PayPeriodId, RunType RunType = RunType.Regular) : IRequest<Guid>;
public sealed record CalculatePayrollRunCommand(Guid RunId) : IRequest<PayrollRunDto>;
public sealed record ApprovePayrollRunCommand(Guid RunId) : IRequest<PayrollRunDto>;
public sealed record CancelPayrollRunCommand(Guid RunId) : IRequest;

public sealed record GetPayrollRunsQuery(Guid? PayGroupId) : IRequest<IReadOnlyList<PayrollRunDto>>;
public sealed record GetPayrollRunByIdQuery(Guid RunId) : IRequest<PayrollRunDto>;
public sealed record GetRunPayslipsQuery(Guid RunId) : IRequest<IReadOnlyList<PayslipListItemDto>>;
public sealed record GetPayslipByIdQuery(Guid PayslipId) : IRequest<PayslipDto>;

public sealed class CreatePayrollRunValidator : AbstractValidator<CreatePayrollRunCommand>
{
    public CreatePayrollRunValidator()
    {
        RuleFor(x => x.PayGroupId).NotEmpty();
        RuleFor(x => x.PayPeriodId).NotEmpty();
        RuleFor(x => x.RunType).IsInEnum()
            .Equal(RunType.Regular).WithMessage("Only regular runs are supported for now.");
    }
}

// ───────────────────────── Handlers ─────────────────────────
public sealed class PayrollRunHandlers(IAppDbContext db, ICurrentUser currentUser, PayrollRunCalculator calculator) :
    IRequestHandler<CreatePayrollRunCommand, Guid>,
    IRequestHandler<CalculatePayrollRunCommand, PayrollRunDto>,
    IRequestHandler<ApprovePayrollRunCommand, PayrollRunDto>,
    IRequestHandler<CancelPayrollRunCommand>,
    IRequestHandler<GetPayrollRunsQuery, IReadOnlyList<PayrollRunDto>>,
    IRequestHandler<GetPayrollRunByIdQuery, PayrollRunDto>,
    IRequestHandler<GetRunPayslipsQuery, IReadOnlyList<PayslipListItemDto>>,
    IRequestHandler<GetPayslipByIdQuery, PayslipDto>
{
    public async Task<Guid> Handle(CreatePayrollRunCommand request, CancellationToken ct)
    {
        var group = await db.PayGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.PayGroupId, ct)
                    ?? throw new NotFoundException("Pay group", request.PayGroupId);
        if (!group.IsActive)
            throw new ConflictException("This pay group is inactive.");

        var period = await db.PayPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PayPeriodId, ct)
                     ?? throw new NotFoundException("Pay period", request.PayPeriodId);
        if (period.PayGroupId != group.Id)
            throw new ConflictException("This period does not belong to the selected pay group.");
        period.EnsureOpen();

        // DB unique index (UX_Runs_RegularPerPeriod) bhi rokta hai; ye check saaf message ke liye
        var exists = await db.PayrollRuns.AnyAsync(r => r.PayPeriodId == period.Id
                                                        && r.RunType == RunType.Regular
                                                        && r.Status != RunStatus.Cancelled, ct);
        if (exists)
            throw new ConflictException("A regular payroll run already exists for this period.");

        var run = PayrollRun.Create(currentUser.RequireTenantId(), group.Id, period.Id, request.RunType, group.CurrencyCode);
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run.Id;
    }

    /// <summary>
    /// Abhi synchronous (chhote tenants ke liye kaafi). Bade tenants ke liye yahi calculator
    /// ek MassTransit consumer se chalega aur API 202 degi — logic badle baghair.
    /// </summary>
    public async Task<PayrollRunDto> Handle(CalculatePayrollRunCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.RunId, ct)
                  ?? throw new NotFoundException("Payroll run", request.RunId);
        if (!run.CanRecalculate)
            throw new ConflictException($"A run in '{run.Status}' status cannot be calculated.");

        var outcome = await calculator.CalculateAsync(run, ct);
        outcome.Period.EnsureOpen();

        run.BeginProcessing(outcome.Calculations.Count + outcome.Issues.Count);

        if (outcome.Issues.Count > 0)
        {
            var reason = string.Join(Environment.NewLine, outcome.Issues);
            run.Fail(reason.Length > 1000 ? reason[..997] + "..." : reason);
            await db.SaveChangesAsync(ct);
            return await Handle(new GetPayrollRunByIdQuery(run.Id), ct);
        }

        var existing = await db.Payslips.Include(p => p.Lines)
            .Where(p => p.PayrollRunId == run.Id)
            .ToListAsync(ct);
        var byEmployee = existing.ToDictionary(p => p.EmployeeId);
        var tenantId = currentUser.RequireTenantId();
        var period = outcome.Period;
        var slips = new List<Payslip>();

        foreach (var calc in outcome.Calculations)
        {
            var employee = calc.Employee;
            if (!byEmployee.TryGetValue(employee.Id, out var slip))
            {
                var snapshot = new PayslipSnapshot(
                    employee.EmployeeCode, employee.FullName, employee.DepartmentName, employee.DesignationTitle,
                    BankAccountMasked: null, run.CurrencyCode, period.PeriodStart, period.PeriodEnd);

                slip = Payslip.Create(tenantId, run.Id, employee.Id,
                    PayslipNumber(outcome.Settings.PayslipNumberPrefix, period.PeriodEnd, employee.EmployeeCode, run), snapshot);
                db.Payslips.Add(slip);
            }

            var r = calc.Result;
            slip.ApplyCalculation(r.Lines, r.PeriodDays, r.PayableDays, calc.UnpaidLeaveDays, r.TaxableIncome);
            slips.Add(slip);
        }

        // Recalculation: jo employee ab is run mein nahi (pay group badla), uski purani payslip hatao
        var calculatedIds = outcome.Calculations.Select(c => c.Employee.Id).ToHashSet();
        foreach (var stale in existing.Where(p => !calculatedIds.Contains(p.EmployeeId)))
            db.Payslips.Remove(stale);

        run.CompleteCalculation(
            slips.Sum(s => s.GrossEarnings),
            slips.Sum(s => s.TotalDeductions),
            slips.Sum(s => s.NetPay),
            slips.Sum(s => s.GrossEarnings + s.EmployerContributions));

        await db.SaveChangesAsync(ct);
        return await Handle(new GetPayrollRunByIdQuery(run.Id), ct);
    }

    /// <summary>
    /// Approve = paisa final. Loan installments YAHAN katte hain, calculation pe nahi —
    /// is liye recalculation mein kuch reverse nahi karna padta. Period lock event handler karta hai.
    /// </summary>
    public async Task<PayrollRunDto> Handle(ApprovePayrollRunCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.RunId, ct)
                  ?? throw new NotFoundException("Payroll run", request.RunId);

        var approver = currentUser.UserId ?? throw new UnauthorizedAccessException("Approver could not be identified.");
        run.Approve(approver);

        var loanLines = await db.Payslips.AsNoTracking()
            .Where(p => p.PayrollRunId == run.Id && p.Status != PayslipStatus.OnHold)
            .SelectMany(p => p.Lines
                .Where(l => l.Source == LineSource.Loan && l.SourceReference != null)
                .Select(l => new { PayslipId = p.Id, LoanId = l.SourceReference!.Value, l.Amount }))
            .ToListAsync(ct);

        if (loanLines.Count > 0)
        {
            var loanIds = loanLines.Select(l => l.LoanId).Distinct().ToList();
            var loans = await db.EmployeeLoans.Where(l => loanIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
            var tenantId = currentUser.RequireTenantId();

            foreach (var line in loanLines)
            {
                if (!loans.TryGetValue(line.LoanId, out var loan))
                    throw new ConflictException("A loan on this run no longer exists. Recalculate the run first.");

                loan.ApplyRepayment(line.Amount);
                db.LoanRepayments.Add(LoanRepayment.Create(tenantId, loan.Id, line.PayslipId, line.Amount));
            }
        }

        await db.SaveChangesAsync(ct);
        return await Handle(new GetPayrollRunByIdQuery(run.Id), ct);
    }

    public async Task Handle(CancelPayrollRunCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.RunId, ct)
                  ?? throw new NotFoundException("Payroll run", request.RunId);
        run.Cancel();
        await db.SaveChangesAsync(ct);
    }

    // ───────────── Queries ─────────────

    public async Task<IReadOnlyList<PayrollRunDto>> Handle(GetPayrollRunsQuery request, CancellationToken ct)
    => await RunsQuery(r => request.PayGroupId == null || r.PayGroupId == request.PayGroupId)
        .Take(100)
        .ToListAsync(ct);

    public async Task<PayrollRunDto> Handle(GetPayrollRunByIdQuery request, CancellationToken ct)
        => await RunsQuery(r => r.Id == request.RunId).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Payroll run", request.RunId);

    public async Task<IReadOnlyList<PayslipListItemDto>> Handle(GetRunPayslipsQuery request, CancellationToken ct)
    {
        if (!await db.PayrollRuns.AnyAsync(r => r.Id == request.RunId, ct))
            throw new NotFoundException("Payroll run", request.RunId);

        return await db.Payslips.AsNoTracking()
            .Where(p => p.PayrollRunId == request.RunId)
            .OrderBy(p => p.EmployeeCode)
            .Select(p => new PayslipListItemDto(
                p.Id, p.EmployeeId, p.PayslipNumber, p.EmployeeCode, p.EmployeeName, p.DepartmentName,
                p.PayableDays, p.GrossEarnings, p.TotalDeductions, p.TaxAmount, p.NetPay, p.Status, p.HoldReason))
            .ToListAsync(ct);
    }

    public async Task<PayslipDto> Handle(GetPayslipByIdQuery request, CancellationToken ct)
    {
        var p = await db.Payslips.AsNoTracking().Include(x => x.Lines)
                    .FirstOrDefaultAsync(x => x.Id == request.PayslipId, ct)
                ?? throw new NotFoundException("Payslip", request.PayslipId);

        var lines = p.Lines
            .OrderBy(l => l.ComponentType).ThenBy(l => l.SortOrder)
            .Select(l => new PayslipLineDto(l.ComponentCode, l.ComponentName, l.ComponentType, l.Amount,
                                            l.Quantity, l.Rate, l.IsTaxable, l.Source))
            .ToList();

        return new PayslipDto(
            p.Id, p.PayrollRunId, p.PayslipNumber, p.EmployeeCode, p.EmployeeName, p.DepartmentName, p.DesignationTitle,
            p.BankAccountMasked, p.CurrencyCode, p.PeriodStart, p.PeriodEnd, p.PeriodDays, p.PayableDays, p.UnpaidLeaveDays,
            p.GrossEarnings, p.TotalDeductions, p.TaxAmount, p.NetPay, p.EmployerContributions, p.TaxableIncome,
            p.Status, p.HoldReason, p.CalculatedAt, lines);
    }

    /// <summary>Filter aur ordering entity pe, DTO banne se PEHLE — warna EF SQL nahi bana sakta.</summary>
    private IQueryable<PayrollRunDto> RunsQuery(Expression<Func<PayrollRun, bool>> filter)
        => from r in db.PayrollRuns.AsNoTracking().Where(filter)
           join g in db.PayGroups.AsNoTracking() on r.PayGroupId equals g.Id
           join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
           orderby p.PeriodStart descending
           select new PayrollRunDto(
               r.Id, r.PayGroupId, g.Name, r.PayPeriodId, p.PeriodStart, p.PeriodEnd, p.PayDate,
               r.RunType, r.Status, r.CurrencyCode,
               r.TotalEmployees, r.ProcessedEmployees, r.TotalGross, r.TotalDeductions, r.TotalNet, r.TotalEmployerCost,
               r.CalculatedAt, r.ApprovedAt, r.FailureReason);

    /// <summary>PS-202610-EMP001. Regular run ka number stable hai (recalculation pe wahi rehta hai).</summary>
    private static string PayslipNumber(string prefix, DateOnly periodEnd, string employeeCode, PayrollRun run)
        => run.RunType == RunType.Regular
            ? $"{prefix}-{periodEnd:yyyyMM}-{employeeCode}"
            : $"{prefix}-{periodEnd:yyyyMM}-{employeeCode}-{run.Id.ToString("N")[..4].ToUpperInvariant()}";
}

/// <summary>Regular run approve → period lock: us period ke inputs/leave ab payroll ko nahi badal sakte.</summary>
public sealed class LockPeriodOnRunApproved(IAppDbContext db) : INotificationHandler<PayrollRunApprovedDomainEvent>
{
    public async Task Handle(PayrollRunApprovedDomainEvent notification, CancellationToken ct)
    {
        if (notification.Run.RunType != RunType.Regular)
            return;

        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == notification.Run.PayPeriodId, ct);
        period?.Lock();
    }
}
