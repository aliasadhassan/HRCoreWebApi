namespace HR.Payroll.API.Application.Loans;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Inputs;
using HR.Payroll.API.Domain.Salaries;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Loans & advances page: My loans (employee), Loans, Loan requests, Repayments tabs + Loan policy.
// Katauti payroll run karta hai (PayrollRunCalculator + approve pe LoanRepayment); yahan sirf loan ka lifecycle.

#region DTOs

public sealed record LoanDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? DepartmentName,
    LoanType LoanType, string CurrencyCode, decimal PrincipalAmount, decimal InstallmentAmount, decimal OutstandingAmount,
    decimal RepaidAmount, int InstallmentsLeft, DateOnly StartDate, LoanStatus Status,
    Guid DeductionComponentId, string? DeductionComponentName, string? Remarks, DateTime CreatedAt);

public sealed record LoanRequestDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? DepartmentName,
    LoanType LoanType, string CurrencyCode, decimal RequestedAmount, short RequestedInstallments, decimal SuggestedInstallment,
    DateOnly PreferredStartDate, string Reason, LoanRequestStatus Status, decimal? ApprovedAmount, decimal? ApprovedInstallmentAmount,
    Guid? EmployeeLoanId, DateTime? DecidedAt, string? DecisionComment, DateTime CreatedAt, bool CanCancel);

public sealed record LoanRepaymentDto(
    Guid Id, Guid EmployeeLoanId, Guid EmployeeId, string EmployeeName, LoanType LoanType, Guid PayslipId, string PayslipNumber,
    DateOnly PeriodStart, DateOnly PeriodEnd, decimal Amount, string CurrencyCode, DateTime CreatedAt);

/// <summary>Employee ko form se pehle pata ho kitna maang sakta hai.</summary>
public sealed record LoanLimitDto(LoanType LoanType, bool Enabled, bool Eligible, string? NotEligibleReason, decimal? MaxAmount, short MaxInstallments);

public sealed record MyLoansDto(
    Guid? EmployeeId, string? CurrencyCode, decimal? MonthlyGross, IReadOnlyList<LoanLimitDto> Limits,
    IReadOnlyList<LoanDto> Loans, IReadOnlyList<LoanRequestDto> Requests);

public sealed record LoanPolicyDto(
    bool LoansEnabled, decimal? MaxLoanAmount, byte? MaxLoanSalaryMultiple, short MaxLoanInstallments, short MinServiceMonths,
    bool AdvancesEnabled, decimal MaxAdvancePercent, byte MaxAdvanceInstallments, bool AllowMultipleActive,
    Guid? LoanDeductionComponentId, Guid? AdvanceDeductionComponentId);

#endregion

#region Queries + commands

public sealed record GetMyLoansQuery : IRequest<MyLoansDto>;

public sealed record GetLoansQuery(LoanStatus? Status, LoanType? LoanType, Guid? EmployeeId) : IRequest<IReadOnlyList<LoanDto>>;
public sealed record GetLoanRequestsQuery(LoanRequestStatus? Status, Guid? EmployeeId) : IRequest<IReadOnlyList<LoanRequestDto>>;
public sealed record GetLoanRepaymentsQuery(Guid? LoanId, DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<LoanRepaymentDto>>;

public sealed record SubmitLoanRequestCommand(
    LoanType LoanType, decimal Amount, short Installments, DateOnly PreferredStartDate, string Reason) : IRequest<Guid>;

public sealed record CancelLoanRequestCommand(Guid Id) : IRequest;

/// <summary>Khaali fields = jo maanga woh (installment = raqam / installments, upar ki taraf).</summary>
public sealed record ApproveLoanRequestCommand(
    Guid Id, decimal? Amount, decimal? InstallmentAmount, DateOnly? StartDate, Guid? DeductionComponentId, string? Comment) : IRequest<Guid>;

public sealed record RejectLoanRequestCommand(Guid Id, string? Comment) : IRequest;

/// <summary>HR seedha loan de (request ke baghair).</summary>
public sealed record CreateLoanCommand(
    Guid EmployeeId, LoanType LoanType, decimal Amount, decimal InstallmentAmount, DateOnly StartDate,
    Guid? DeductionComponentId, string? Remarks) : IRequest<Guid>;

public enum LoanAction : byte { Pause = 1, Resume = 2, Cancel = 3 }

public sealed record ChangeLoanStatusCommand(Guid Id, LoanAction Action) : IRequest;
public sealed record ChangeLoanInstallmentCommand(Guid Id, decimal InstallmentAmount) : IRequest;

public sealed record GetLoanPolicyQuery : IRequest<LoanPolicyDto>;
public sealed record SaveLoanPolicyCommand(
    bool LoansEnabled, decimal? MaxLoanAmount, byte? MaxLoanSalaryMultiple, short MaxLoanInstallments, short MinServiceMonths,
    bool AdvancesEnabled, decimal MaxAdvancePercent, byte MaxAdvanceInstallments, bool AllowMultipleActive,
    Guid? LoanDeductionComponentId, Guid? AdvanceDeductionComponentId) : IRequest;

public sealed class SubmitLoanRequestValidator : AbstractValidator<SubmitLoanRequestCommand>
{
    public SubmitLoanRequestValidator()
    {
        RuleFor(x => x.LoanType).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Installments).InclusiveBetween((short)1, (short)120);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ApproveLoanRequestValidator : AbstractValidator<ApproveLoanRequestCommand>
{
    public ApproveLoanRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount is not null);
        RuleFor(x => x.InstallmentAmount).GreaterThan(0).When(x => x.InstallmentAmount is not null);
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}

public sealed class RejectLoanRequestValidator : AbstractValidator<RejectLoanRequestCommand>
{
    public RejectLoanRequestValidator()
        => RuleFor(x => x.Comment).NotEmpty().WithMessage("Please give a reason for rejecting.").MaximumLength(500);
}

public sealed class CreateLoanValidator : AbstractValidator<CreateLoanCommand>
{
    public CreateLoanValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.LoanType).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.InstallmentAmount).GreaterThan(0).LessThanOrEqualTo(x => x.Amount)
            .WithMessage("Installment must be more than zero and not more than the loan amount.");
        RuleFor(x => x.Remarks).MaximumLength(500);
    }
}

public sealed class ChangeLoanInstallmentValidator : AbstractValidator<ChangeLoanInstallmentCommand>
{
    public ChangeLoanInstallmentValidator() => RuleFor(x => x.InstallmentAmount).GreaterThan(0);
}

public sealed class ChangeLoanStatusValidator : AbstractValidator<ChangeLoanStatusCommand>
{
    public ChangeLoanStatusValidator() => RuleFor(x => x.Action).IsInEnum();
}

#endregion

public sealed class LoanHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetMyLoansQuery, MyLoansDto>,
    IRequestHandler<GetLoansQuery, IReadOnlyList<LoanDto>>,
    IRequestHandler<GetLoanRequestsQuery, IReadOnlyList<LoanRequestDto>>,
    IRequestHandler<GetLoanRepaymentsQuery, IReadOnlyList<LoanRepaymentDto>>,
    IRequestHandler<SubmitLoanRequestCommand, Guid>,
    IRequestHandler<CancelLoanRequestCommand>,
    IRequestHandler<ApproveLoanRequestCommand, Guid>,
    IRequestHandler<RejectLoanRequestCommand>,
    IRequestHandler<CreateLoanCommand, Guid>,
    IRequestHandler<ChangeLoanStatusCommand>,
    IRequestHandler<ChangeLoanInstallmentCommand>,
    IRequestHandler<GetLoanPolicyQuery, LoanPolicyDto>,
    IRequestHandler<SaveLoanPolicyCommand>
{
    private static readonly LoanStatus[] Open = [LoanStatus.Active, LoanStatus.Paused];
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Sab ke loans dekhna: payroll.view.all ya approve karne wala.</summary>
    private void EnsureCanViewAll()
    {
        if (!currentUser.HasPermission(Permissions.PayrollViewAll) && !currentUser.HasPermission(Permissions.PayrollApprove))
            throw new UnauthorizedAccessException("You do not have permission to see everyone's loans.");
    }

    // ───────────────────────── Employee ─────────────────────────

    public async Task<MyLoansDto> Handle(GetMyLoansQuery q, CancellationToken ct)
    {
        var me = await MyEmployeeAsync(ct);
        if (me is null)
            return new MyLoansDto(null, null, null, [], [], []);

        var policy = await PolicyAsync(ct);
        var salary = await CurrentSalaryAsync(me.Id, ct);
        var gross = salary is null ? (decimal?)null : SalaryMath.PeriodGross(salary.SalaryBasis, salary.BasisAmount, PayFrequency.Monthly);
        var hasOpen = await db.EmployeeLoans.AnyAsync(l => l.EmployeeId == me.Id && Open.Contains(l.Status), ct);

        var limits = new[] { LoanType.Loan, LoanType.SalaryAdvance }.Select(type =>
        {
            var reason = NotEligibleReason(policy, type, me.JoiningDate, gross, hasOpen);
            return new LoanLimitDto(type, policy.IsEnabled(type), reason is null, reason,
                                    gross is { } g ? policy.LimitFor(type, g) : null, policy.MaxInstallments(type));
        }).ToList();

        var loans = await ToLoanDtosAsync(db.EmployeeLoans.AsNoTracking().Where(l => l.EmployeeId == me.Id), ct);
        var requests = await ToRequestDtosAsync(db.LoanRequests.AsNoTracking().Where(r => r.EmployeeId == me.Id), me.Id, ct);
        return new MyLoansDto(me.Id, salary?.CurrencyCode, gross, limits, loans, requests);
    }

    public async Task<Guid> Handle(SubmitLoanRequestCommand c, CancellationToken ct)
    {
        var me = await MyEmployeeAsync(ct)
                 ?? throw new UnauthorizedAccessException("Your login is not linked to a payroll employee.");
        var policy = await PolicyAsync(ct);
        var salary = await CurrentSalaryAsync(me.Id, ct)
                     ?? throw new ConflictException("Your salary is not set up yet. Please contact HR.");
        var gross = SalaryMath.PeriodGross(salary.SalaryBasis, salary.BasisAmount, PayFrequency.Monthly);
        var hasOpen = await db.EmployeeLoans.AnyAsync(l => l.EmployeeId == me.Id && Open.Contains(l.Status), ct);

        if (NotEligibleReason(policy, c.LoanType, me.JoiningDate, gross, hasOpen) is { } reason)
            throw new DomainException(reason);
        if (c.Installments > policy.MaxInstallments(c.LoanType))
            throw new DomainException($"You can repay in at most {policy.MaxInstallments(c.LoanType)} installments.");
        if (policy.LimitFor(c.LoanType, gross) is { } max && c.Amount > max)
            throw new DomainException($"The most you can request is {salary.CurrencyCode} {max:N0}.");
        if (c.PreferredStartDate < new DateOnly(Today.Year, Today.Month, 1))
            throw new DomainException("Repayment cannot start in a past month.");
        if (await db.LoanRequests.AnyAsync(r => r.EmployeeId == me.Id && r.Status == LoanRequestStatus.Pending, ct))
            throw new ConflictException("You already have a pending request. Wait for a decision or cancel it first.");

        var request = LoanRequest.Submit(currentUser.RequireTenantId(), me.Id, c.LoanType, salary.CurrencyCode,
                                         c.Amount, c.Installments, c.PreferredStartDate, c.Reason);
        db.LoanRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request.Id;
    }

    public async Task Handle(CancelLoanRequestCommand c, CancellationToken ct)
    {
        var me = await MyEmployeeAsync(ct)
                 ?? throw new UnauthorizedAccessException("Your login is not linked to a payroll employee.");
        var request = await db.LoanRequests.FirstOrDefaultAsync(r => r.Id == c.Id, ct) ?? throw new NotFoundException("Loan request", c.Id);
        request.Cancel(me.Id);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────── HR: lists ─────────────────────────

    public async Task<IReadOnlyList<LoanDto>> Handle(GetLoansQuery q, CancellationToken ct)
    {
        EnsureCanViewAll();
        var loans = db.EmployeeLoans.AsNoTracking();
        if (q.Status is { } status) loans = loans.Where(l => l.Status == status);
        if (q.LoanType is { } type) loans = loans.Where(l => l.LoanType == type);
        if (q.EmployeeId is { } employeeId) loans = loans.Where(l => l.EmployeeId == employeeId);
        return await ToLoanDtosAsync(loans, ct);
    }

    public async Task<IReadOnlyList<LoanRequestDto>> Handle(GetLoanRequestsQuery q, CancellationToken ct)
    {
        EnsureCanViewAll();
        var requests = db.LoanRequests.AsNoTracking();
        if (q.Status is { } status) requests = requests.Where(r => r.Status == status);
        if (q.EmployeeId is { } employeeId) requests = requests.Where(r => r.EmployeeId == employeeId);
        return await ToRequestDtosAsync(requests, null, ct);
    }

    public async Task<IReadOnlyList<LoanRepaymentDto>> Handle(GetLoanRepaymentsQuery q, CancellationToken ct)
    {
        IQueryable<EmployeeLoan> loans = db.EmployeeLoans.AsNoTracking();
        if (q.LoanId is { } loanId)
        {
            // Apne loan ki qistein employee khud bhi dekh sake
            var me = await MyEmployeeAsync(ct);
            var owner = await db.EmployeeLoans.Where(l => l.Id == loanId).Select(l => (Guid?)l.EmployeeId).FirstOrDefaultAsync(ct)
                        ?? throw new NotFoundException("Loan", loanId);
            if (owner != me?.Id)
                EnsureCanViewAll();
            loans = loans.Where(l => l.Id == loanId);
        }
        else
        {
            EnsureCanViewAll();
        }

        var rows = await (
            from r in db.LoanRepayments.AsNoTracking()
            join l in loans on r.EmployeeLoanId equals l.Id
            join p in db.Payslips.AsNoTracking() on r.PayslipId equals p.Id
            where (q.From == null || p.PeriodEnd >= q.From) && (q.To == null || p.PeriodStart <= q.To)
            orderby p.PeriodStart descending, p.EmployeeName
            select new LoanRepaymentDto(r.Id, l.Id, l.EmployeeId, p.EmployeeName, l.LoanType, p.Id, p.PayslipNumber,
                                        p.PeriodStart, p.PeriodEnd, r.Amount, l.CurrencyCode, r.CreatedAt))
            .Take(1000)
            .ToListAsync(ct);
        return rows;
    }

    // ───────────────────────── HR: decisions + changes ─────────────────────────

    public async Task<Guid> Handle(ApproveLoanRequestCommand c, CancellationToken ct)
    {
        var approver = currentUser.UserId ?? throw new UnauthorizedAccessException("Approver could not be identified.");
        var request = await db.LoanRequests.FirstOrDefaultAsync(r => r.Id == c.Id, ct) ?? throw new NotFoundException("Loan request", c.Id);
        if (request.Status != LoanRequestStatus.Pending)
            throw new DomainException("This request is no longer pending.");

        var amount = c.Amount ?? request.RequestedAmount;
        var installment = c.InstallmentAmount ?? Math.Ceiling(amount / request.RequestedInstallments * 100m) / 100m;
        var loan = await CreateLoanAsync(request.EmployeeId, request.LoanType, request.CurrencyCode, amount, installment,
                                         c.StartDate ?? request.PreferredStartDate, c.DeductionComponentId,
                                         $"Request: {request.Reason}", ct);
        request.MarkApproved(approver, loan, c.Comment);
        await db.SaveChangesAsync(ct);
        return loan.Id;
    }

    public async Task Handle(RejectLoanRequestCommand c, CancellationToken ct)
    {
        var approver = currentUser.UserId ?? throw new UnauthorizedAccessException("Approver could not be identified.");
        var request = await db.LoanRequests.FirstOrDefaultAsync(r => r.Id == c.Id, ct) ?? throw new NotFoundException("Loan request", c.Id);
        request.Reject(approver, c.Comment!);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> Handle(CreateLoanCommand c, CancellationToken ct)
    {
        var salary = await CurrentSalaryAsync(c.EmployeeId, ct);
        var currency = salary?.CurrencyCode
                       ?? await db.PayrollSettings.Select(s => s.BaseCurrency).FirstOrDefaultAsync(ct)
                       ?? throw new ConflictException("Set the base currency in payroll settings first.");
        var loan = await CreateLoanAsync(c.EmployeeId, c.LoanType, currency, c.Amount, c.InstallmentAmount, c.StartDate,
                                         c.DeductionComponentId, c.Remarks, ct);
        await db.SaveChangesAsync(ct);
        return loan.Id;
    }

    public async Task Handle(ChangeLoanStatusCommand c, CancellationToken ct)
    {
        var loan = await db.EmployeeLoans.FirstOrDefaultAsync(l => l.Id == c.Id, ct) ?? throw new NotFoundException("Loan", c.Id);
        switch (c.Action)
        {
            case LoanAction.Pause: loan.Pause(); break;
            case LoanAction.Resume: loan.Resume(); break;
            default: loan.Cancel(); break;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ChangeLoanInstallmentCommand c, CancellationToken ct)
    {
        var loan = await db.EmployeeLoans.FirstOrDefaultAsync(l => l.Id == c.Id, ct) ?? throw new NotFoundException("Loan", c.Id);
        if (c.InstallmentAmount > loan.PrincipalAmount)
            throw new DomainException("Installment cannot be greater than the loan amount.");
        loan.ChangeInstallment(c.InstallmentAmount);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────── Policy ─────────────────────────

    public async Task<LoanPolicyDto> Handle(GetLoanPolicyQuery q, CancellationToken ct)
    {
        var p = await PolicyAsync(ct);
        return new LoanPolicyDto(p.LoansEnabled, p.MaxLoanAmount, p.MaxLoanSalaryMultiple, p.MaxLoanInstallments, p.MinServiceMonths,
                                 p.AdvancesEnabled, p.MaxAdvancePercent, p.MaxAdvanceInstallments, p.AllowMultipleActive,
                                 p.LoanDeductionComponentId, p.AdvanceDeductionComponentId);
    }

    public async Task Handle(SaveLoanPolicyCommand c, CancellationToken ct)
    {
        foreach (var componentId in new[] { c.LoanDeductionComponentId, c.AdvanceDeductionComponentId }.OfType<Guid>())
            await EnsureDeductionComponentAsync(componentId, ct);

        var policy = await db.LoanPolicies.FirstOrDefaultAsync(ct);
        if (policy is null)
        {
            policy = LoanPolicy.CreateDefault(currentUser.RequireTenantId());
            db.LoanPolicies.Add(policy);
        }
        policy.Update(c.LoansEnabled, c.MaxLoanAmount, c.MaxLoanSalaryMultiple, c.MaxLoanInstallments, c.MinServiceMonths,
                      c.AdvancesEnabled, c.MaxAdvancePercent, c.MaxAdvanceInstallments, c.AllowMultipleActive,
                      c.LoanDeductionComponentId, c.AdvanceDeductionComponentId);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────── helpers ─────────────────────────

    private async Task<EmployeeLoan> CreateLoanAsync(
        Guid employeeId, LoanType type, string currency, decimal amount, decimal installment, DateOnly startDate,
        Guid? componentId, string? remarks, CancellationToken ct)
    {
        var employee = await db.PayrollEmployees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
                       ?? throw new NotFoundException("Employee", employeeId);
        if (!employee.IsActive)
            throw new ConflictException("This employee has exited; a loan cannot be given.");

        var policy = await PolicyAsync(ct);
        if (!policy.AllowMultipleActive && await db.EmployeeLoans.AnyAsync(l => l.EmployeeId == employeeId && Open.Contains(l.Status), ct))
            throw new ConflictException($"{employee.FullName} already has an open loan. Close it first or allow multiple loans in the policy.");

        var component = componentId ?? policy.DeductionComponentFor(type)
                        ?? throw new ConflictException("Choose a deduction component, or set a default one in the loan policy.");
        await EnsureDeductionComponentAsync(component, ct);

        var loan = EmployeeLoan.Create(currentUser.RequireTenantId(), employeeId, type, currency, amount, installment, startDate,
                                       component, remarks is { Length: > 500 } ? remarks[..500] : remarks);
        db.EmployeeLoans.Add(loan);   // Id yahin ban jata hai (request ko link karne ke liye)
        return loan;
    }

    private async Task EnsureDeductionComponentAsync(Guid componentId, CancellationToken ct)
    {
        var component = await db.PayComponents.AsNoTracking().FirstOrDefaultAsync(p => p.Id == componentId, ct)
                        ?? throw new NotFoundException("Pay component", componentId);
        if (component.ComponentType != ComponentType.Deduction || !component.IsActive)
            throw new ConflictException($"{component.Name} must be an active deduction component.");
    }

    private static string? NotEligibleReason(LoanPolicy policy, LoanType type, DateOnly joiningDate, decimal? gross, bool hasOpenLoan)
    {
        var what = type == LoanType.Loan ? "Loans" : "Salary advances";
        if (!policy.IsEnabled(type))
            return $"{what} are not offered at the moment.";
        if (gross is null)
            return "Your salary is not set up yet. Please contact HR.";
        if (type == LoanType.Loan && joiningDate.AddMonths(policy.MinServiceMonths) > Today)
            return $"Loans are available after {policy.MinServiceMonths} months of service.";
        if (hasOpenLoan && !policy.AllowMultipleActive)
            return "You already have an open loan or advance.";
        return null;
    }

    private async Task<LoanPolicy> PolicyAsync(CancellationToken ct)
        => await db.LoanPolicies.AsNoTracking().FirstOrDefaultAsync(ct) ?? LoanPolicy.CreateDefault(currentUser.RequireTenantId());

    private Task<EmployeeSalary?> CurrentSalaryAsync(Guid employeeId, CancellationToken ct)
        => db.EmployeeSalaries.AsNoTracking()
            .Where(s => s.EmployeeId == employeeId && s.EffectiveFrom <= Today && (s.EffectiveTo == null || s.EffectiveTo >= Today))
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    /// <summary>Payroll ke paas UserId nahi — login email = work email se pehchaan (MyPayslips jaisa).</summary>
    private async Task<MyEmployee?> MyEmployeeAsync(CancellationToken ct)
    {
        var email = currentUser.Email?.Trim().ToLower();
        if (string.IsNullOrEmpty(email)) return null;
        return await db.PayrollEmployees.AsNoTracking()
            .Where(e => e.WorkEmail.ToLower() == email && e.IsActive)
            .Select(e => new MyEmployee(e.Id, e.JoiningDate))
            .FirstOrDefaultAsync(ct);
    }

    private sealed record MyEmployee(Guid Id, DateOnly JoiningDate);

    private async Task<IReadOnlyList<LoanDto>> ToLoanDtosAsync(IQueryable<EmployeeLoan> loans, CancellationToken ct)
    {
        var rows = await (
            from l in loans
            join e in db.PayrollEmployees.AsNoTracking() on l.EmployeeId equals e.Id
            join pc in db.PayComponents.AsNoTracking() on l.DeductionComponentId equals pc.Id into pcs
            from pc in pcs.DefaultIfEmpty()
            orderby l.Status, l.CreatedAt descending
            select new { l, e.FullName, e.EmployeeCode, e.DepartmentName, Component = pc == null ? null : pc.Name })
            .Take(1000)
            .ToListAsync(ct);

        return rows.Select(x => new LoanDto(
            x.l.Id, x.l.EmployeeId, x.FullName, x.EmployeeCode, x.DepartmentName, x.l.LoanType, x.l.CurrencyCode,
            x.l.PrincipalAmount, x.l.InstallmentAmount, x.l.OutstandingAmount, x.l.PrincipalAmount - x.l.OutstandingAmount,
            x.l.InstallmentAmount > 0 ? (int)Math.Ceiling(x.l.OutstandingAmount / x.l.InstallmentAmount) : 0,
            x.l.StartDate, x.l.Status, x.l.DeductionComponentId, x.Component, x.l.Remarks, x.l.CreatedAt)).ToList();
    }

    private async Task<IReadOnlyList<LoanRequestDto>> ToRequestDtosAsync(IQueryable<LoanRequest> requests, Guid? meId, CancellationToken ct)
    {
        var rows = await (
            from r in requests
            join e in db.PayrollEmployees.AsNoTracking() on r.EmployeeId equals e.Id
            orderby r.Status, r.CreatedAt descending
            select new { r, e.FullName, e.EmployeeCode, e.DepartmentName })
            .Take(1000)
            .ToListAsync(ct);

        return rows.Select(x => new LoanRequestDto(
            x.r.Id, x.r.EmployeeId, x.FullName, x.EmployeeCode, x.DepartmentName, x.r.LoanType, x.r.CurrencyCode,
            x.r.RequestedAmount, x.r.RequestedInstallments, Math.Ceiling(x.r.RequestedAmount / x.r.RequestedInstallments * 100m) / 100m,
            x.r.PreferredStartDate, x.r.Reason, x.r.Status, x.r.ApprovedAmount, x.r.ApprovedInstallmentAmount, x.r.EmployeeLoanId,
            x.r.DecidedAt, x.r.DecisionComment, x.r.CreatedAt,
            meId is not null && x.r.EmployeeId == meId && x.r.Status == LoanRequestStatus.Pending)).ToList();
    }
}
