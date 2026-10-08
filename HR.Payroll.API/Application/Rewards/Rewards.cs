namespace HR.Payroll.API.Application.Rewards;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Application.Salaries;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Inputs;
using HR.Payroll.API.Domain.Rewards;
using HR.Payroll.API.Domain.Salaries;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Benefits & rewards page: My benefits (employee), Plans, Enrolments, Increments, Bonuses.
// Dekhna: payroll.view.all / payroll.run / payroll.approve. Tajweez + enrolment: payroll.run.
// Increment aur bonus approve: payroll.approve. Plans: settings.manage.
// Increment approve = nayi EmployeeSalary (purani band). Bonus approve = PayrollInput (Source = Bonus) ya Direct.
// Benefit ka employee hissa run calculator khud kaat-ta hai (LineSource.Benefit).

#region DTOs

public sealed record RewardsSummaryDto(
    int ActivePlans, int CoveredEmployees, decimal MonthlyEmployerCost, decimal MonthlyEmployeeCost, string? CurrencyCode,
    int PendingEnrolments, int PendingRevisions, int PendingBonuses, int RevisionsAppliedThisYear, decimal? AverageIncreaseThisYear,
    decimal BonusesThisYear, int BonusesThisYearCount,
    bool CanView, bool CanManage, bool CanApprove, bool CanConfigure, Guid? MyEmployeeId);

public sealed record BenefitPlanDto(
    Guid Id, string Name, BenefitType BenefitType, string? Provider, string? Description, string CurrencyCode,
    decimal EmployerMonthlyCost, decimal EmployeeMonthlyCost, decimal DependentMonthlyCost, short MaxDependents,
    Guid? DeductionComponentId, string? DeductionComponentName, bool OpenForRequests, bool IsActive, short SortOrder,
    int Enrolled, int Requested, decimal MonthlyEmployerTotal, uint RowVersion);

public sealed record EnrolmentDto(
    Guid Id, Guid BenefitPlanId, string PlanName, BenefitType BenefitType, Guid EmployeeId, string EmployeeName, string EmployeeCode,
    string? DepartmentName, EnrolmentStatus Status, short Dependents, DateOnly? StartDate, DateOnly? EndDate,
    decimal EmployerMonthlyCost, decimal EmployeeMonthlyCost, string CurrencyCode, bool DeductedInPayroll,
    string? EmployeeNote, string? DecisionNote, DateTime? DecidedAt, DateTime CreatedAt, bool CanCancel);

public sealed record SalaryRevisionDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? DepartmentName, string? DesignationTitle,
    SalaryChangeReason Reason, string CurrencyCode, SalaryBasis SalaryBasis, decimal CurrentAmount, decimal ProposedAmount,
    decimal ChangePercent, DateOnly EffectiveFrom, string? NewTitle, string? Justification, SalaryRevisionStatus Status,
    DateTime? DecidedAt, string? DecisionNote, DateTime CreatedAt);

public sealed record BonusDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? DepartmentName, BonusType BonusType, string Title,
    string CurrencyCode, decimal Amount, Guid PayComponentId, string PayComponentName, Guid? BatchId, string? Reason,
    BonusStatus Status, PayoutMethod? PayoutMethod, DateOnly? PayPeriodStart, DateOnly? PayPeriodEnd, DateTime? PaidAt,
    DateTime? DecidedAt, string? DecisionNote, DateTime CreatedAt);

/// <summary>Increment / bonus batch ke liye: har active employee ki current salary.</summary>
public sealed record SalaryLineDto(
    Guid EmployeeId, string EmployeeName, string EmployeeCode, Guid DepartmentId, string? DepartmentName, string? DesignationTitle,
    DateOnly JoiningDate, string? CurrencyCode, SalaryBasis? SalaryBasis, decimal? CurrentAmount, DateOnly? CurrentFrom,
    decimal? MonthlyEquivalent, DateOnly? LastChangeOn, decimal? LastChangePercent, bool HasPendingRevision);

public sealed record RewardOptionDto(Guid Id, string Name, string? Extra);

public sealed record RewardsLookupsDto(
    IReadOnlyList<RewardOptionDto> Employees, IReadOnlyList<RewardOptionDto> Departments,
    IReadOnlyList<RewardOptionDto> EarningComponents, IReadOnlyList<RewardOptionDto> DeductionComponents, string? BaseCurrency);

public sealed record MyRewardsDto(
    Guid? EmployeeId, string? CurrencyCode, IReadOnlyList<BenefitPlanDto> Plans, IReadOnlyList<EnrolmentDto> Enrolments,
    IReadOnlyList<BonusDto> Bonuses, IReadOnlyList<SalaryRevisionDto> Revisions);

public sealed record BatchResultDto(int Created, IReadOnlyList<string> Skipped);

#endregion

#region Queries + commands

public sealed record GetRewardsSummaryQuery : IRequest<RewardsSummaryDto>;
public sealed record GetRewardsLookupsQuery : IRequest<RewardsLookupsDto>;
public sealed record GetMyRewardsQuery : IRequest<MyRewardsDto>;

public sealed record GetBenefitPlansQuery : IRequest<IReadOnlyList<BenefitPlanDto>>;
public sealed record SaveBenefitPlanCommand(
    Guid? Id, string Name, BenefitType BenefitType, string? Provider, string? Description, string? CurrencyCode,
    decimal EmployerMonthlyCost, decimal EmployeeMonthlyCost, decimal DependentMonthlyCost, short MaxDependents,
    Guid? DeductionComponentId, bool OpenForRequests, bool IsActive, short SortOrder) : IRequest<Guid>;
public sealed record DeleteBenefitPlanCommand(Guid Id) : IRequest;

public sealed record GetEnrolmentsQuery(EnrolmentStatus? Status, Guid? PlanId, Guid? EmployeeId) : IRequest<IReadOnlyList<EnrolmentDto>>;
public sealed record RequestEnrolmentCommand(Guid PlanId, short Dependents, string? Note) : IRequest<Guid>;
public sealed record CancelEnrolmentRequestCommand(Guid Id) : IRequest;
public sealed record EnrolEmployeeCommand(Guid PlanId, Guid EmployeeId, short Dependents, DateOnly StartDate, string? Note) : IRequest<Guid>;
public sealed record ApproveEnrolmentCommand(Guid Id, DateOnly StartDate, short? Dependents, string? Note) : IRequest;
public sealed record RejectEnrolmentCommand(Guid Id, string Note) : IRequest;
public sealed record EndEnrolmentCommand(Guid Id, DateOnly EndDate, string? Note) : IRequest;
public sealed record ChangeEnrolmentDependentsCommand(Guid Id, short Dependents) : IRequest;

public sealed record GetSalaryLinesQuery : IRequest<IReadOnlyList<SalaryLineDto>>;
public sealed record GetSalaryRevisionsQuery(SalaryRevisionStatus? Status, Guid? EmployeeId) : IRequest<IReadOnlyList<SalaryRevisionDto>>;
public sealed record ProposeSalaryRevisionCommand(
    Guid EmployeeId, SalaryChangeReason Reason, decimal ProposedAmount, DateOnly EffectiveFrom, string? NewTitle, string? Justification)
    : IRequest<Guid>;
public sealed record ProposeSalaryRevisionsBatchCommand(
    IReadOnlyList<Guid> EmployeeIds, decimal Percent, DateOnly EffectiveFrom, SalaryChangeReason Reason, string? Justification)
    : IRequest<BatchResultDto>;
public sealed record UpdateSalaryRevisionCommand(
    Guid Id, SalaryChangeReason Reason, decimal ProposedAmount, DateOnly EffectiveFrom, string? NewTitle, string? Justification) : IRequest;
public sealed record ApproveSalaryRevisionsCommand(IReadOnlyList<Guid> Ids, string? Note) : IRequest<BatchResultDto>;
public sealed record RejectSalaryRevisionCommand(Guid Id, string Note) : IRequest;
public sealed record CancelSalaryRevisionCommand(Guid Id) : IRequest;

public enum BonusBasis : byte { Fixed = 1, PercentOfMonthlySalary = 2 }

public sealed record GetBonusesQuery(BonusStatus? Status, BonusType? Type, Guid? EmployeeId) : IRequest<IReadOnlyList<BonusDto>>;
public sealed record ProposeBonusCommand(
    Guid EmployeeId, BonusType BonusType, string Title, decimal Amount, Guid PayComponentId, string? Reason) : IRequest<Guid>;
public sealed record ProposeBonusBatchCommand(
    IReadOnlyList<Guid> EmployeeIds, BonusType BonusType, string Title, BonusBasis Basis, decimal Value, Guid PayComponentId, string? Reason)
    : IRequest<BatchResultDto>;
public sealed record UpdateBonusCommand(Guid Id, BonusType BonusType, string Title, decimal Amount, Guid PayComponentId, string? Reason) : IRequest;
/// <summary>Payout khaali = Payroll (agla khula period).</summary>
public sealed record ApproveBonusesCommand(IReadOnlyList<Guid> Ids, PayoutMethod? Payout, string? Note) : IRequest<BatchResultDto>;
public sealed record RejectBonusCommand(Guid Id, string Note) : IRequest;
public sealed record CancelBonusCommand(Guid Id) : IRequest;
public sealed record MarkBonusPaidCommand(Guid Id) : IRequest;

public sealed class SaveBenefitPlanValidator : AbstractValidator<SaveBenefitPlanCommand>
{
    public SaveBenefitPlanValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BenefitType).IsInEnum();
        RuleFor(x => x.Provider).MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CurrencyCode).Length(3).When(x => !string.IsNullOrEmpty(x.CurrencyCode));
        RuleFor(x => x.EmployerMonthlyCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EmployeeMonthlyCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DependentMonthlyCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxDependents).InclusiveBetween((short)0, (short)10);
        RuleFor(x => x.SortOrder).InclusiveBetween((short)0, (short)999);
    }
}

public sealed class RequestEnrolmentValidator : AbstractValidator<RequestEnrolmentCommand>
{
    public RequestEnrolmentValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Dependents).InclusiveBetween((short)0, (short)10);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class EnrolEmployeeValidator : AbstractValidator<EnrolEmployeeCommand>
{
    public EnrolEmployeeValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Dependents).InclusiveBetween((short)0, (short)10);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class RejectEnrolmentValidator : AbstractValidator<RejectEnrolmentCommand>
{
    public RejectEnrolmentValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(500);
}

public sealed class ProposeSalaryRevisionValidator : AbstractValidator<ProposeSalaryRevisionCommand>
{
    public ProposeSalaryRevisionValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.ProposedAmount).GreaterThan(0);
        RuleFor(x => x.NewTitle).MaximumLength(150);
        RuleFor(x => x.Justification).MaximumLength(1000);
    }
}

public sealed class ProposeSalaryRevisionsBatchValidator : AbstractValidator<ProposeSalaryRevisionsBatchCommand>
{
    public ProposeSalaryRevisionsBatchValidator()
    {
        RuleFor(x => x.EmployeeIds).NotEmpty().Must(x => x.Count <= 1000).WithMessage("Pick at most 1000 employees at a time.");
        RuleFor(x => x.Percent).GreaterThan(0).LessThanOrEqualTo(100);
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Justification).MaximumLength(1000);
    }
}

public sealed class RejectSalaryRevisionValidator : AbstractValidator<RejectSalaryRevisionCommand>
{
    public RejectSalaryRevisionValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(500);
}

public sealed class ProposeBonusValidator : AbstractValidator<ProposeBonusCommand>
{
    public ProposeBonusValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.BonusType).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PayComponentId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class ProposeBonusBatchValidator : AbstractValidator<ProposeBonusBatchCommand>
{
    public ProposeBonusBatchValidator()
    {
        RuleFor(x => x.EmployeeIds).NotEmpty().Must(x => x.Count <= 1000).WithMessage("Pick at most 1000 employees at a time.");
        RuleFor(x => x.BonusType).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Basis).IsInEnum();
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(1000).When(x => x.Basis == BonusBasis.PercentOfMonthlySalary)
            .WithMessage("A bonus can be at most 1000% of the monthly salary.");
        RuleFor(x => x.PayComponentId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class RejectBonusValidator : AbstractValidator<RejectBonusCommand>
{
    public RejectBonusValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(500);
}

#endregion

public sealed class RewardHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetRewardsSummaryQuery, RewardsSummaryDto>,
    IRequestHandler<GetRewardsLookupsQuery, RewardsLookupsDto>,
    IRequestHandler<GetMyRewardsQuery, MyRewardsDto>,
    IRequestHandler<GetBenefitPlansQuery, IReadOnlyList<BenefitPlanDto>>,
    IRequestHandler<SaveBenefitPlanCommand, Guid>,
    IRequestHandler<DeleteBenefitPlanCommand>,
    IRequestHandler<GetEnrolmentsQuery, IReadOnlyList<EnrolmentDto>>,
    IRequestHandler<RequestEnrolmentCommand, Guid>,
    IRequestHandler<CancelEnrolmentRequestCommand>,
    IRequestHandler<EnrolEmployeeCommand, Guid>,
    IRequestHandler<ApproveEnrolmentCommand>,
    IRequestHandler<RejectEnrolmentCommand>,
    IRequestHandler<EndEnrolmentCommand>,
    IRequestHandler<ChangeEnrolmentDependentsCommand>,
    IRequestHandler<GetSalaryLinesQuery, IReadOnlyList<SalaryLineDto>>,
    IRequestHandler<GetSalaryRevisionsQuery, IReadOnlyList<SalaryRevisionDto>>,
    IRequestHandler<ProposeSalaryRevisionCommand, Guid>,
    IRequestHandler<ProposeSalaryRevisionsBatchCommand, BatchResultDto>,
    IRequestHandler<UpdateSalaryRevisionCommand>,
    IRequestHandler<ApproveSalaryRevisionsCommand, BatchResultDto>,
    IRequestHandler<RejectSalaryRevisionCommand>,
    IRequestHandler<CancelSalaryRevisionCommand>,
    IRequestHandler<GetBonusesQuery, IReadOnlyList<BonusDto>>,
    IRequestHandler<ProposeBonusCommand, Guid>,
    IRequestHandler<ProposeBonusBatchCommand, BatchResultDto>,
    IRequestHandler<UpdateBonusCommand>,
    IRequestHandler<ApproveBonusesCommand, BatchResultDto>,
    IRequestHandler<RejectBonusCommand>,
    IRequestHandler<CancelBonusCommand>,
    IRequestHandler<MarkBonusPaidCommand>
{
    private static readonly RunStatus[] ClosedRun = [RunStatus.Approved, RunStatus.Paid];
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private bool CanView => currentUser.HasPermission(Permissions.PayrollViewAll) || currentUser.HasPermission(Permissions.PayrollRun)
                            || currentUser.HasPermission(Permissions.PayrollApprove);
    private bool CanManage => currentUser.HasPermission(Permissions.PayrollRun);
    private bool CanApprove => currentUser.HasPermission(Permissions.PayrollApprove);
    private bool CanConfigure => currentUser.HasPermission(Permissions.SettingsManage);

    private void EnsureView()
    {
        if (!CanView) throw new UnauthorizedAccessException("You do not have permission to see everyone's benefits and rewards.");
    }

    private void EnsureManage()
    {
        if (!CanManage) throw new UnauthorizedAccessException("You need the payroll run permission to do this.");
    }

    private void EnsureApprove()
    {
        if (!CanApprove) throw new UnauthorizedAccessException("You need the payroll approve permission to do this.");
    }

    private void EnsureConfigure()
    {
        if (!CanConfigure) throw new UnauthorizedAccessException("You need the settings permission to change benefit plans.");
    }

    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedAccessException("User could not be identified.");

    // ───────────────────────── Summary + lookups ─────────────────────────

    public async Task<RewardsSummaryDto> Handle(GetRewardsSummaryQuery q, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct);
        var currency = await db.PayrollSettings.Select(s => s.BaseCurrency).FirstOrDefaultAsync(ct);
        if (!CanView)
            return new RewardsSummaryDto(0, 0, 0, 0, currency, 0, 0, 0, 0, null, 0, 0, false, false, false, CanConfigure, me);

        var today = Today;
        var yearStart = new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var active = db.BenefitEnrolments.AsNoTracking().Where(e => e.Status == EnrolmentStatus.Active);
        var applied = await db.SalaryRevisions.AsNoTracking()
            .Where(r => r.Status == SalaryRevisionStatus.Applied && r.DecidedAt >= yearStart)
            .Select(r => new { r.CurrentAmount, r.ProposedAmount })
            .ToListAsync(ct);
        var bonuses = await db.BonusAwards.AsNoTracking()
            .Where(b => (b.Status == BonusStatus.Approved || b.Status == BonusStatus.Paid) && b.DecidedAt >= yearStart)
            .Select(b => b.Amount)
            .ToListAsync(ct);

        return new RewardsSummaryDto(
            await db.BenefitPlans.CountAsync(p => p.IsActive, ct),
            await active.Select(e => e.EmployeeId).Distinct().CountAsync(ct),
            await active.SumAsync(e => (decimal?)e.EmployerMonthlyCost, ct) ?? 0,
            await active.SumAsync(e => (decimal?)e.EmployeeMonthlyCost, ct) ?? 0,
            currency,
            await db.BenefitEnrolments.CountAsync(e => e.Status == EnrolmentStatus.Requested, ct),
            await db.SalaryRevisions.CountAsync(r => r.Status == SalaryRevisionStatus.Pending, ct),
            await db.BonusAwards.CountAsync(b => b.Status == BonusStatus.Pending, ct),
            applied.Count,
            applied.Count == 0 ? null : Math.Round(applied.Average(r => (r.ProposedAmount - r.CurrentAmount) / r.CurrentAmount * 100), 1),
            bonuses.Sum(), bonuses.Count,
            true, CanManage, CanApprove, CanConfigure, me);
    }

    public async Task<RewardsLookupsDto> Handle(GetRewardsLookupsQuery q, CancellationToken ct)
    {
        EnsureView();
        var employees = await db.PayrollEmployees.AsNoTracking().Where(e => e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new { e.Id, e.FullName, e.EmployeeCode, e.DepartmentId, e.DepartmentName })
            .ToListAsync(ct);
        var components = await db.PayComponents.AsNoTracking()
            .Where(c => c.IsActive && c.SystemCode == null)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Code, c.ComponentType })
            .ToListAsync(ct);

        return new RewardsLookupsDto(
            employees.Select(e => new RewardOptionDto(e.Id, e.FullName, e.EmployeeCode)).ToList(),
            employees.GroupBy(e => e.DepartmentId)
                     .Select(g => new RewardOptionDto(g.Key, g.First().DepartmentName ?? "—", null))
                     .OrderBy(d => d.Name).ToList(),
            components.Where(c => c.ComponentType == ComponentType.Earning).Select(c => new RewardOptionDto(c.Id, c.Name, c.Code)).ToList(),
            components.Where(c => c.ComponentType == ComponentType.Deduction).Select(c => new RewardOptionDto(c.Id, c.Name, c.Code)).ToList(),
            await db.PayrollSettings.Select(s => s.BaseCurrency).FirstOrDefaultAsync(ct));
    }

    // ───────────────────────── Employee ─────────────────────────

    public async Task<MyRewardsDto> Handle(GetMyRewardsQuery q, CancellationToken ct)
    {
        var plans = (await PlanDtosAsync(ct)).Where(p => p.IsActive).ToList();
        if (await MyEmployeeIdAsync(ct) is not { } me)
            return new MyRewardsDto(null, null, plans.Where(p => p.OpenForRequests).ToList(), [], [], []);

        var enrolments = await EnrolmentDtosAsync(db.BenefitEnrolments.AsNoTracking().Where(e => e.EmployeeId == me), me, ct);
        var mine = enrolments.Where(e => e.Status is EnrolmentStatus.Active or EnrolmentStatus.Requested).Select(e => e.BenefitPlanId).ToHashSet();
        // Employee ko sirf faisla shuda bonus dikhta hai — pending tajweez HR ki andar ki baat hai
        var bonuses = await BonusDtosAsync(db.BonusAwards.AsNoTracking()
            .Where(b => b.EmployeeId == me && (b.Status == BonusStatus.Approved || b.Status == BonusStatus.Paid)), ct);
        var revisions = await RevisionDtosAsync(db.SalaryRevisions.AsNoTracking()
            .Where(r => r.EmployeeId == me && r.Status == SalaryRevisionStatus.Applied), ct);

        return new MyRewardsDto(me, await CurrencyAsync(me, ct),
            plans.Where(p => p.OpenForRequests || mine.Contains(p.Id)).ToList(), enrolments, bonuses, revisions);
    }

    public async Task<Guid> Handle(RequestEnrolmentCommand c, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct) ?? throw new UnauthorizedAccessException("Your login is not linked to a payroll employee.");
        var plan = await db.BenefitPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == c.PlanId, ct) ?? throw new NotFoundException("Benefit plan", c.PlanId);
        await EnsureNotLiveAsync(plan, me, ct);
        var enrolment = BenefitEnrolment.Request(currentUser.RequireTenantId(), plan, me, c.Dependents, c.Note);
        db.BenefitEnrolments.Add(enrolment);
        await db.SaveChangesAsync(ct);
        return enrolment.Id;
    }

    public async Task Handle(CancelEnrolmentRequestCommand c, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct) ?? throw new UnauthorizedAccessException("Your login is not linked to a payroll employee.");
        var enrolment = await db.BenefitEnrolments.FirstOrDefaultAsync(e => e.Id == c.Id, ct) ?? throw new NotFoundException("Enrolment", c.Id);
        enrolment.Cancel(me);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────── Plans ─────────────────────────

    public async Task<IReadOnlyList<BenefitPlanDto>> Handle(GetBenefitPlansQuery q, CancellationToken ct)
    {
        if (!CanView && !CanConfigure) EnsureView();
        return await PlanDtosAsync(ct);
    }

    public async Task<Guid> Handle(SaveBenefitPlanCommand c, CancellationToken ct)
    {
        EnsureConfigure();
        var name = c.Name.Trim();
        if (await db.BenefitPlans.AnyAsync(p => p.Id != c.Id && p.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException($"A plan called \"{name}\" already exists.");
        if (c.DeductionComponentId is { } componentId)
            await EnsureComponentAsync(componentId, ComponentType.Deduction, ct);

        var currency = c.CurrencyCode ?? await db.PayrollSettings.Select(s => s.BaseCurrency).FirstOrDefaultAsync(ct)
                       ?? throw new ConflictException("Set up payroll first so the plan has a currency.");
        var data = new BenefitPlanData(name, c.BenefitType, c.Provider, c.Description, currency, c.EmployerMonthlyCost,
            c.EmployeeMonthlyCost, c.DependentMonthlyCost, c.MaxDependents, c.DeductionComponentId, c.OpenForRequests, c.IsActive, c.SortOrder);

        BenefitPlan plan;
        if (c.Id is { } id)
        {
            plan = await db.BenefitPlans.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Benefit plan", id);
            var maxUsed = await db.BenefitEnrolments.Where(e => e.BenefitPlanId == id && (e.Status == EnrolmentStatus.Requested || e.Status == EnrolmentStatus.Active))
                                  .MaxAsync(e => (short?)e.Dependents, ct) ?? 0;
            if (c.MaxDependents < maxUsed)
                throw new ConflictException($"Someone on this plan already covers {maxUsed} dependents. Allow at least that many.");
            plan.Update(data);
        }
        else
        {
            plan = BenefitPlan.Create(currentUser.RequireTenantId(), data);
            db.BenefitPlans.Add(plan);
        }
        await db.SaveChangesAsync(ct);
        return plan.Id;
    }

    public async Task Handle(DeleteBenefitPlanCommand c, CancellationToken ct)
    {
        EnsureConfigure();
        var plan = await db.BenefitPlans.FirstOrDefaultAsync(p => p.Id == c.Id, ct) ?? throw new NotFoundException("Benefit plan", c.Id);
        if (await db.BenefitEnrolments.AnyAsync(e => e.BenefitPlanId == plan.Id, ct))
            throw new ConflictException($"{plan.Name} has enrolment history. Mark it inactive instead of deleting it.");
        db.BenefitPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────── Enrolments ─────────────────────────

    public async Task<IReadOnlyList<EnrolmentDto>> Handle(GetEnrolmentsQuery q, CancellationToken ct)
    {
        EnsureView();
        var query = db.BenefitEnrolments.AsNoTracking();
        if (q.Status is { } status) query = query.Where(e => e.Status == status);
        if (q.PlanId is { } planId) query = query.Where(e => e.BenefitPlanId == planId);
        if (q.EmployeeId is { } employeeId) query = query.Where(e => e.EmployeeId == employeeId);
        return await EnrolmentDtosAsync(query, null, ct);
    }

    public async Task<Guid> Handle(EnrolEmployeeCommand c, CancellationToken ct)
    {
        EnsureManage();
        var plan = await db.BenefitPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == c.PlanId, ct) ?? throw new NotFoundException("Benefit plan", c.PlanId);
        var employee = await ActiveEmployeeAsync(c.EmployeeId, ct);
        if (c.StartDate < employee.JoiningDate)
            throw new ConflictException("Coverage cannot start before the joining date.");
        await EnsureNotLiveAsync(plan, employee.Id, ct);
        var enrolment = BenefitEnrolment.Enrol(currentUser.RequireTenantId(), plan, employee.Id, c.Dependents, c.StartDate, UserId, c.Note);
        db.BenefitEnrolments.Add(enrolment);
        await db.SaveChangesAsync(ct);
        return enrolment.Id;
    }

    public async Task Handle(ApproveEnrolmentCommand c, CancellationToken ct)
    {
        EnsureManage();
        var enrolment = await db.BenefitEnrolments.FirstOrDefaultAsync(e => e.Id == c.Id, ct) ?? throw new NotFoundException("Enrolment", c.Id);
        var plan = await db.BenefitPlans.AsNoTracking().FirstAsync(p => p.Id == enrolment.BenefitPlanId, ct);
        if (!plan.IsActive)
            throw new ConflictException($"{plan.Name} is no longer offered. Reject the request instead.");
        var employee = await ActiveEmployeeAsync(enrolment.EmployeeId, ct);
        if (c.StartDate < employee.JoiningDate)
            throw new ConflictException("Coverage cannot start before the joining date.");
        enrolment.Approve(plan, c.StartDate, c.Dependents ?? enrolment.Dependents, UserId, c.Note);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RejectEnrolmentCommand c, CancellationToken ct)
    {
        EnsureManage();
        var enrolment = await db.BenefitEnrolments.FirstOrDefaultAsync(e => e.Id == c.Id, ct) ?? throw new NotFoundException("Enrolment", c.Id);
        enrolment.Reject(UserId, c.Note);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(EndEnrolmentCommand c, CancellationToken ct)
    {
        EnsureManage();
        var enrolment = await db.BenefitEnrolments.FirstOrDefaultAsync(e => e.Id == c.Id, ct) ?? throw new NotFoundException("Enrolment", c.Id);
        enrolment.End(c.EndDate, UserId, c.Note);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ChangeEnrolmentDependentsCommand c, CancellationToken ct)
    {
        EnsureManage();
        var enrolment = await db.BenefitEnrolments.FirstOrDefaultAsync(e => e.Id == c.Id, ct) ?? throw new NotFoundException("Enrolment", c.Id);
        var plan = await db.BenefitPlans.AsNoTracking().FirstAsync(p => p.Id == enrolment.BenefitPlanId, ct);
        enrolment.ChangeDependents(plan, c.Dependents);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────── Increments ─────────────────────────

    public async Task<IReadOnlyList<SalaryLineDto>> Handle(GetSalaryLinesQuery q, CancellationToken ct)
    {
        EnsureView();
        var employees = await db.PayrollEmployees.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(ct);
        var current = await db.EmployeeSalaries.AsNoTracking().Where(s => s.EffectiveTo == null)
            .Select(s => new { s.EmployeeId, s.CurrencyCode, s.SalaryBasis, s.BasisAmount, s.EffectiveFrom })
            .ToListAsync(ct);
        var currentByEmployee = current.GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.First());
        var last = await db.SalaryRevisions.AsNoTracking().Where(r => r.Status == SalaryRevisionStatus.Applied)
            .Select(r => new { r.EmployeeId, r.EffectiveFrom, r.CurrentAmount, r.ProposedAmount })
            .ToListAsync(ct);
        var lastByEmployee = last.GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.EffectiveFrom).First());
        var pending = (await db.SalaryRevisions.AsNoTracking().Where(r => r.Status == SalaryRevisionStatus.Pending)
            .Select(r => r.EmployeeId).ToListAsync(ct)).ToHashSet();

        return employees.Select(e =>
        {
            var s = currentByEmployee.GetValueOrDefault(e.Id);
            var l = lastByEmployee.GetValueOrDefault(e.Id);
            return new SalaryLineDto(
                e.Id, e.FullName, e.EmployeeCode, e.DepartmentId, e.DepartmentName, e.DesignationTitle, e.JoiningDate,
                s?.CurrencyCode, s?.SalaryBasis, s?.BasisAmount, s?.EffectiveFrom, s is null ? null : MonthlyOf(s.SalaryBasis, s.BasisAmount),
                l?.EffectiveFrom ?? s?.EffectiveFrom,
                l is null ? null : Math.Round((l.ProposedAmount - l.CurrentAmount) / l.CurrentAmount * 100, 1),
                pending.Contains(e.Id));
        }).ToList();
    }

    public async Task<IReadOnlyList<SalaryRevisionDto>> Handle(GetSalaryRevisionsQuery q, CancellationToken ct)
    {
        EnsureView();
        var query = db.SalaryRevisions.AsNoTracking();
        if (q.Status is { } status) query = query.Where(r => r.Status == status);
        if (q.EmployeeId is { } employeeId) query = query.Where(r => r.EmployeeId == employeeId);
        return await RevisionDtosAsync(query, ct);
    }

    public async Task<Guid> Handle(ProposeSalaryRevisionCommand c, CancellationToken ct)
    {
        EnsureManage();
        var employee = await ActiveEmployeeAsync(c.EmployeeId, ct);
        var current = await CurrentSalaryAsync(employee.Id, ct);
        await EnsureNoPendingRevisionAsync(employee.Id, employee.FullName, ct);
        var revision = SalaryRevision.Propose(currentUser.RequireTenantId(), employee.Id, c.Reason, current.Id, current.CurrencyCode,
            current.SalaryBasis, current.BasisAmount, current.EffectiveFrom, c.ProposedAmount, c.EffectiveFrom, c.NewTitle, c.Justification);
        db.SalaryRevisions.Add(revision);
        await db.SaveChangesAsync(ct);
        return revision.Id;
    }

    public async Task<BatchResultDto> Handle(ProposeSalaryRevisionsBatchCommand c, CancellationToken ct)
    {
        EnsureManage();
        var ids = c.EmployeeIds.Distinct().ToList();
        var employees = await db.PayrollEmployees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var salaries = (await db.EmployeeSalaries.AsNoTracking().Where(s => ids.Contains(s.EmployeeId) && s.EffectiveTo == null).ToListAsync(ct))
            .GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.First());
        var pending = (await db.SalaryRevisions.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.Status == SalaryRevisionStatus.Pending).Select(r => r.EmployeeId).ToListAsync(ct)).ToHashSet();

        var tenantId = currentUser.RequireTenantId();
        var skipped = new List<string>();
        var created = 0;
        foreach (var id in ids)
        {
            if (!employees.TryGetValue(id, out var e) || !e.IsActive) { skipped.Add($"{e?.FullName ?? id.ToString()}: not an active employee."); continue; }
            if (pending.Contains(id)) { skipped.Add($"{e.FullName}: already has a pending change."); continue; }
            if (!salaries.TryGetValue(id, out var s)) { skipped.Add($"{e.FullName}: no current salary."); continue; }
            try
            {
                var amount = RoundSalary(s.BasisAmount * (1 + c.Percent / 100m), s.SalaryBasis);
                db.SalaryRevisions.Add(SalaryRevision.Propose(tenantId, id, c.Reason, s.Id, s.CurrencyCode, s.SalaryBasis, s.BasisAmount,
                    s.EffectiveFrom, amount, c.EffectiveFrom, null, c.Justification));
                created++;
            }
            catch (DomainException ex)
            {
                skipped.Add($"{e.FullName}: {ex.Message}");
            }
        }
        await db.SaveChangesAsync(ct);
        return new BatchResultDto(created, skipped);
    }

    public async Task Handle(UpdateSalaryRevisionCommand c, CancellationToken ct)
    {
        EnsureManage();
        var revision = await db.SalaryRevisions.FirstOrDefaultAsync(r => r.Id == c.Id, ct) ?? throw new NotFoundException("Salary change", c.Id);
        var currentFrom = await db.EmployeeSalaries.Where(s => s.Id == revision.CurrentSalaryId).Select(s => s.EffectiveFrom).FirstAsync(ct);
        revision.Edit(c.Reason, c.ProposedAmount, c.EffectiveFrom, currentFrom, c.NewTitle, c.Justification);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Har approve apni transaction mein: purani salary band, nayi (template, grade, overrides wahi) + revision Applied.
    /// Ek fail ho to baaqi chalti rahti hain; wajah Skipped mein.
    /// </summary>
    public async Task<BatchResultDto> Handle(ApproveSalaryRevisionsCommand c, CancellationToken ct)
    {
        EnsureApprove();
        if (c.Ids.Count == 0 || c.Ids.Count > 1000)
            throw new DomainException("Pick between 1 and 1000 salary changes.");
        var userId = UserId;
        var skipped = new List<string>();
        var applied = 0;
        foreach (var id in c.Ids.Distinct())
        {
            var revision = await db.SalaryRevisions.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (revision is null) { skipped.Add($"{id}: not found."); continue; }
            var name = await db.PayrollEmployees.Where(e => e.Id == revision.EmployeeId).Select(e => e.FullName).FirstOrDefaultAsync(ct) ?? "—";
            if (revision.Status != SalaryRevisionStatus.Pending) { skipped.Add($"{name}: no longer pending."); continue; }
            try
            {
                // ApplyAsync saare checks entity badalne se pehle karta hai, is liye fail hone pe tracker saaf rehta hai
                await ApplyAsync(revision, userId, c.Note, ct);
                applied++;
            }
            catch (Exception ex) when (ex is DomainException or ConflictException)
            {
                skipped.Add($"{name}: {ex.Message}");
            }
        }
        return new BatchResultDto(applied, skipped);
    }

    private async Task ApplyAsync(SalaryRevision revision, Guid userId, string? note, CancellationToken ct)
    {
        var current = await db.EmployeeSalaries.Include(s => s.Overrides)
                          .FirstOrDefaultAsync(s => s.EmployeeId == revision.EmployeeId && s.EffectiveTo == null, ct)
                      ?? throw new ConflictException("The employee has no current salary any more.");
        if (current.Id != revision.CurrentSalaryId)
            throw new ConflictException("The salary changed after this was proposed. Cancel it and propose again.");
        if (!await db.PayrollEmployees.AnyAsync(e => e.Id == revision.EmployeeId && e.IsActive, ct))
            throw new ConflictException("This employee has exited.");

        var salary = EmployeeSalary.Create(revision.TenantId, revision.EmployeeId, current.SalaryTemplateId, current.SalaryGradeId,
            current.CurrencyCode, current.SalaryBasis, revision.ProposedAmount, revision.EffectiveFrom, revision.Reason,
            Trim(revision.Justification ?? $"{revision.Reason}: {revision.ChangePercent:+0.##;-0.##}%"));
        EmployeeSalaryHandlers.CopyOverrides(current, salary);

        // Filtered unique index (ek hi current salary): pehle purani band, phir nayi — ek transaction
        await db.ExecuteInTransactionAsync(async token =>
        {
            current.End(revision.EffectiveFrom.AddDays(-1));
            await db.SaveChangesAsync(token);
            db.EmployeeSalaries.Add(salary);
            revision.MarkApplied(salary.Id, userId, note);
            await db.SaveChangesAsync(token);
        }, ct);
    }

    public async Task Handle(RejectSalaryRevisionCommand c, CancellationToken ct)
    {
        EnsureApprove();
        var revision = await db.SalaryRevisions.FirstOrDefaultAsync(r => r.Id == c.Id, ct) ?? throw new NotFoundException("Salary change", c.Id);
        revision.Reject(UserId, c.Note);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelSalaryRevisionCommand c, CancellationToken ct)
    {
        EnsureManage();
        var revision = await db.SalaryRevisions.FirstOrDefaultAsync(r => r.Id == c.Id, ct) ?? throw new NotFoundException("Salary change", c.Id);
        revision.Cancel(UserId);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────── Bonuses ─────────────────────────

    public async Task<IReadOnlyList<BonusDto>> Handle(GetBonusesQuery q, CancellationToken ct)
    {
        EnsureView();
        var query = db.BonusAwards.AsNoTracking();
        if (q.Status is { } status)
            query = status switch
            {
                // Paid = Direct paid + payroll waale jinka payslip paid; filter memory mein (derived status)
                BonusStatus.Approved or BonusStatus.Paid => query.Where(b => b.Status == BonusStatus.Approved || b.Status == BonusStatus.Paid),
                _ => query.Where(b => b.Status == status)
            };
        if (q.Type is { } type) query = query.Where(b => b.BonusType == type);
        if (q.EmployeeId is { } employeeId) query = query.Where(b => b.EmployeeId == employeeId);
        var list = await BonusDtosAsync(query, ct);
        return q.Status is BonusStatus.Approved or BonusStatus.Paid ? list.Where(b => b.Status == q.Status).ToList() : list;
    }

    public async Task<Guid> Handle(ProposeBonusCommand c, CancellationToken ct)
    {
        EnsureManage();
        var employee = await ActiveEmployeeAsync(c.EmployeeId, ct);
        await EnsureComponentAsync(c.PayComponentId, ComponentType.Earning, ct);
        var currency = await CurrencyAsync(employee.Id, ct) ?? throw new ConflictException("Set up payroll first so the bonus has a currency.");
        var bonus = BonusAward.Propose(currentUser.RequireTenantId(), employee.Id, c.BonusType, c.Title, currency, c.Amount, c.PayComponentId, null, c.Reason);
        db.BonusAwards.Add(bonus);
        await db.SaveChangesAsync(ct);
        return bonus.Id;
    }

    public async Task<BatchResultDto> Handle(ProposeBonusBatchCommand c, CancellationToken ct)
    {
        EnsureManage();
        await EnsureComponentAsync(c.PayComponentId, ComponentType.Earning, ct);
        var ids = c.EmployeeIds.Distinct().ToList();
        var employees = await db.PayrollEmployees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        // Aaj ki salary (aage ki tareekh wala increment abhi lagu nahi)
        var today = Today;
        var salaries = (await db.EmployeeSalaries.AsNoTracking()
                .Where(s => ids.Contains(s.EmployeeId) && s.EffectiveFrom <= today && (s.EffectiveTo == null || s.EffectiveTo >= today))
                .ToListAsync(ct))
            .GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EffectiveFrom).First());
        var baseCurrency = await db.PayrollSettings.Select(s => s.BaseCurrency).FirstOrDefaultAsync(ct);

        var tenantId = currentUser.RequireTenantId();
        var batchId = Guid.NewGuid();
        var skipped = new List<string>();
        var created = 0;
        foreach (var id in ids)
        {
            if (!employees.TryGetValue(id, out var e) || !e.IsActive) { skipped.Add($"{e?.FullName ?? id.ToString()}: not an active employee."); continue; }
            var s = salaries.GetValueOrDefault(id);
            decimal amount;
            if (c.Basis == BonusBasis.Fixed)
                amount = c.Value;
            else if (s is null || MonthlyOf(s.SalaryBasis, s.BasisAmount) is not { } monthly)
            {
                skipped.Add($"{e.FullName}: no monthly salary to take a percentage of.");
                continue;
            }
            else
                amount = Math.Round(monthly * c.Value / 100m, 0, MidpointRounding.AwayFromZero);

            var currency = s?.CurrencyCode ?? baseCurrency;
            if (currency is null) { skipped.Add($"{e.FullName}: no currency."); continue; }
            try
            {
                db.BonusAwards.Add(BonusAward.Propose(tenantId, id, c.BonusType, c.Title, currency, amount, c.PayComponentId, batchId, c.Reason));
                created++;
            }
            catch (DomainException ex)
            {
                skipped.Add($"{e.FullName}: {ex.Message}");
            }
        }
        await db.SaveChangesAsync(ct);
        return new BatchResultDto(created, skipped);
    }

    public async Task Handle(UpdateBonusCommand c, CancellationToken ct)
    {
        EnsureManage();
        var bonus = await db.BonusAwards.FirstOrDefaultAsync(b => b.Id == c.Id, ct) ?? throw new NotFoundException("Bonus", c.Id);
        await EnsureComponentAsync(c.PayComponentId, ComponentType.Earning, ct);
        bonus.Edit(c.BonusType, c.Title, c.Amount, c.PayComponentId, c.Reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task<BatchResultDto> Handle(ApproveBonusesCommand c, CancellationToken ct)
    {
        EnsureApprove();
        if (c.Ids.Count == 0 || c.Ids.Count > 1000)
            throw new DomainException("Pick between 1 and 1000 bonuses.");
        var payout = c.Payout ?? PayoutMethod.Payroll;
        var userId = UserId;
        var ids = c.Ids.Distinct().ToList();
        var bonuses = await db.BonusAwards.Where(b => ids.Contains(b.Id)).ToListAsync(ct);
        var names = await db.PayrollEmployees.AsNoTracking().Where(e => bonuses.Select(b => b.EmployeeId).Contains(e.Id))
                            .ToDictionaryAsync(e => e.Id, e => e.FullName, ct);
        var skipped = new List<string>();
        var approved = 0;
        foreach (var bonus in bonuses)
        {
            var name = names.GetValueOrDefault(bonus.EmployeeId, "—");
            if (bonus.Status != BonusStatus.Pending) { skipped.Add($"{name}: no longer pending."); continue; }
            try
            {
                // Pehle saare checks (period, component), phir badlao — fail hone pe kuch aadha tracked na rahe
                if (!await db.PayrollEmployees.AnyAsync(e => e.Id == bonus.EmployeeId && e.IsActive, ct))
                    throw new ConflictException("This employee has exited.");
                Guid? periodId = payout == PayoutMethod.Payroll ? await NextPayPeriodAsync(bonus.EmployeeId, ct) : null;
                if (periodId is not null)
                    await EnsureComponentAsync(bonus.PayComponentId, ComponentType.Earning, ct);

                bonus.Approve(payout, userId, c.Note);
                if (periodId is { } period)
                {
                    var input = PayrollInput.Create(bonus.TenantId, period, bonus.EmployeeId, bonus.PayComponentId, bonus.Amount, null,
                        InputSource.Bonus, $"BON:{bonus.Id}", Trim($"Bonus: {bonus.Title}"));
                    db.PayrollInputs.Add(input);
                    bonus.AttachPayrollInput(input.Id);
                }
                await db.SaveChangesAsync(ct);
                approved++;
            }
            catch (Exception ex) when (ex is DomainException or ConflictException or NotFoundException)
            {
                skipped.Add($"{name}: {ex.Message}");
            }
        }
        skipped.AddRange(ids.Where(id => bonuses.All(b => b.Id != id)).Select(id => $"{id}: not found."));
        return new BatchResultDto(approved, skipped);
    }

    public async Task Handle(RejectBonusCommand c, CancellationToken ct)
    {
        EnsureApprove();
        var bonus = await db.BonusAwards.FirstOrDefaultAsync(b => b.Id == c.Id, ct) ?? throw new NotFoundException("Bonus", c.Id);
        bonus.Reject(UserId, c.Note);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelBonusCommand c, CancellationToken ct)
    {
        EnsureManage();
        var bonus = await db.BonusAwards.FirstOrDefaultAsync(b => b.Id == c.Id, ct) ?? throw new NotFoundException("Bonus", c.Id);
        bonus.Cancel(UserId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(MarkBonusPaidCommand c, CancellationToken ct)
    {
        EnsureApprove();
        var bonus = await db.BonusAwards.FirstOrDefaultAsync(b => b.Id == c.Id, ct) ?? throw new NotFoundException("Bonus", c.Id);
        bonus.MarkPaid();
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private static decimal? MonthlyOf(SalaryBasis basis, decimal amount) => basis switch
    {
        SalaryBasis.Monthly => amount,
        SalaryBasis.Annual => Math.Round(amount / 12m, 2),
        _ => null
    };

    /// <summary>Mahana / salana salary poore number pe; hourly rate 2 decimal.</summary>
    private static decimal RoundSalary(decimal amount, SalaryBasis basis)
        => basis == SalaryBasis.Hourly ? Math.Round(amount, 2) : Math.Round(amount, 0, MidpointRounding.AwayFromZero);

    private static string Trim(string value) => value.Length > 500 ? value[..500] : value;

    private async Task<Domain.Employees.PayrollEmployee> ActiveEmployeeAsync(Guid id, CancellationToken ct)
    {
        var employee = await db.PayrollEmployees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Employee", id);
        return employee.IsActive ? employee : throw new ConflictException($"{employee.FullName} has exited.");
    }

    private async Task<EmployeeSalary> CurrentSalaryAsync(Guid employeeId, CancellationToken ct)
        => await db.EmployeeSalaries.AsNoTracking().FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.EffectiveTo == null, ct)
           ?? throw new ConflictException("This employee has no salary yet. Assign one in Employee salaries first.");

    private async Task EnsureNoPendingRevisionAsync(Guid employeeId, string name, CancellationToken ct)
    {
        if (await db.SalaryRevisions.AnyAsync(r => r.EmployeeId == employeeId && r.Status == SalaryRevisionStatus.Pending, ct))
            throw new ConflictException($"{name} already has a pending salary change. Edit that one instead.");
    }

    private async Task EnsureNotLiveAsync(BenefitPlan plan, Guid employeeId, CancellationToken ct)
    {
        if (await db.BenefitEnrolments.AnyAsync(e => e.BenefitPlanId == plan.Id && e.EmployeeId == employeeId
                                                     && (e.Status == EnrolmentStatus.Requested || e.Status == EnrolmentStatus.Active), ct))
            throw new ConflictException($"Already enrolled in or waiting for {plan.Name}.");
    }

    private async Task EnsureComponentAsync(Guid componentId, ComponentType type, CancellationToken ct)
    {
        var component = await db.PayComponents.AsNoTracking().FirstOrDefaultAsync(p => p.Id == componentId, ct)
                        ?? throw new NotFoundException("Pay component", componentId);
        if (component.ComponentType != type || !component.IsActive)
            throw new ConflictException($"{component.Name} must be an active {(type == ComponentType.Earning ? "earning" : "deduction")} component.");
    }

    /// <summary>Employee ke pay group ka agla khula period jiska regular run abhi approve/pay nahi hua (Expenses jaisa).</summary>
    private async Task<Guid> NextPayPeriodAsync(Guid employeeId, CancellationToken ct)
    {
        var groupId = await db.PayrollEmployees.Where(e => e.Id == employeeId).Select(e => e.PayGroupId).FirstOrDefaultAsync(ct)
                      ?? throw new ConflictException("This employee is not in a pay group yet, so payroll can't pay them. Pay directly instead.");
        var today = Today;
        return await db.PayPeriods
                   .Where(p => p.PayGroupId == groupId && p.Status == PayPeriodStatus.Open && p.PeriodEnd >= today
                               && !db.PayrollRuns.Any(r => r.PayPeriodId == p.Id && r.RunType == RunType.Regular && ClosedRun.Contains(r.Status)))
                   .OrderBy(p => p.PeriodStart)
                   .Select(p => (Guid?)p.Id)
                   .FirstOrDefaultAsync(ct)
               ?? throw new ConflictException("There is no open pay period for this employee. Generate pay periods in Payroll setup, or pay directly.");
    }

    private async Task<string?> CurrencyAsync(Guid employeeId, CancellationToken ct)
        => await db.EmployeeSalaries.AsNoTracking()
               .Where(s => s.EmployeeId == employeeId && s.EffectiveTo == null)
               .Select(s => s.CurrencyCode)
               .FirstOrDefaultAsync(ct)
           ?? await db.PayrollSettings.Select(s => s.BaseCurrency).FirstOrDefaultAsync(ct);

    /// <summary>Payroll ke paas UserId nahi — login email = work email se pehchaan (Loans / Expenses jaisa).</summary>
    private async Task<Guid?> MyEmployeeIdAsync(CancellationToken ct)
    {
        var email = currentUser.Email?.Trim().ToLower();
        if (string.IsNullOrEmpty(email)) return null;
        return await db.PayrollEmployees.AsNoTracking()
            .Where(e => e.WorkEmail.ToLower() == email && e.IsActive)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IReadOnlyList<BenefitPlanDto>> PlanDtosAsync(CancellationToken ct)
    {
        var plans = await db.BenefitPlans.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
        var counts = await db.BenefitEnrolments.AsNoTracking()
            .Where(e => e.Status == EnrolmentStatus.Active || e.Status == EnrolmentStatus.Requested)
            .GroupBy(e => new { e.BenefitPlanId, e.Status })
            .Select(g => new { g.Key.BenefitPlanId, g.Key.Status, Count = g.Count(), Cost = g.Sum(e => e.EmployerMonthlyCost) })
            .ToListAsync(ct);
        var componentIds = plans.Where(p => p.DeductionComponentId != null).Select(p => p.DeductionComponentId!.Value).ToList();
        var components = await db.PayComponents.AsNoTracking().Where(c => componentIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return plans.Select(p =>
        {
            var active = counts.FirstOrDefault(c => c.BenefitPlanId == p.Id && c.Status == EnrolmentStatus.Active);
            var requested = counts.FirstOrDefault(c => c.BenefitPlanId == p.Id && c.Status == EnrolmentStatus.Requested);
            return new BenefitPlanDto(
                p.Id, p.Name, p.BenefitType, p.Provider, p.Description, p.CurrencyCode, p.EmployerMonthlyCost, p.EmployeeMonthlyCost,
                p.DependentMonthlyCost, p.MaxDependents, p.DeductionComponentId,
                p.DeductionComponentId is { } cid ? components.GetValueOrDefault(cid) : null,
                p.OpenForRequests, p.IsActive, p.SortOrder, active?.Count ?? 0, requested?.Count ?? 0, active?.Cost ?? 0, p.RowVersion);
        }).ToList();
    }

    private async Task<IReadOnlyList<EnrolmentDto>> EnrolmentDtosAsync(IQueryable<BenefitEnrolment> query, Guid? meId, CancellationToken ct)
    {
        var rows = await (
            from e in query
            join p in db.BenefitPlans.AsNoTracking() on e.BenefitPlanId equals p.Id
            join m in db.PayrollEmployees.AsNoTracking() on e.EmployeeId equals m.Id
            orderby e.Status, e.CreatedAt descending
            select new { e, p.Name, p.BenefitType, p.CurrencyCode, p.DeductionComponentId, m.FullName, m.EmployeeCode, m.DepartmentName })
            .Take(1000)
            .ToListAsync(ct);

        return rows.Select(x => new EnrolmentDto(
            x.e.Id, x.e.BenefitPlanId, x.Name, x.BenefitType, x.e.EmployeeId, x.FullName, x.EmployeeCode, x.DepartmentName, x.e.Status,
            x.e.Dependents, x.e.StartDate, x.e.EndDate, x.e.EmployerMonthlyCost, x.e.EmployeeMonthlyCost, x.CurrencyCode,
            x.DeductionComponentId != null && x.e.EmployeeMonthlyCost > 0, x.e.EmployeeNote, x.e.DecisionNote, x.e.DecidedAt, x.e.CreatedAt,
            meId is not null && x.e.EmployeeId == meId && x.e.Status == EnrolmentStatus.Requested)).ToList();
    }

    private async Task<IReadOnlyList<SalaryRevisionDto>> RevisionDtosAsync(IQueryable<SalaryRevision> query, CancellationToken ct)
    {
        var rows = await (
            from r in query
            join m in db.PayrollEmployees.AsNoTracking() on r.EmployeeId equals m.Id
            orderby r.Status, r.EffectiveFrom descending, m.FullName
            select new { r, m.FullName, m.EmployeeCode, m.DepartmentName, m.DesignationTitle })
            .Take(1000)
            .ToListAsync(ct);

        return rows.Select(x => new SalaryRevisionDto(
            x.r.Id, x.r.EmployeeId, x.FullName, x.EmployeeCode, x.DepartmentName, x.DesignationTitle, x.r.Reason, x.r.CurrencyCode,
            x.r.SalaryBasis, x.r.CurrentAmount, x.r.ProposedAmount, x.r.ChangePercent, x.r.EffectiveFrom, x.r.NewTitle, x.r.Justification,
            x.r.Status, x.r.DecidedAt, x.r.DecisionNote, x.r.CreatedAt)).ToList();
    }

    private async Task<IReadOnlyList<BonusDto>> BonusDtosAsync(IQueryable<BonusAward> query, CancellationToken ct)
    {
        var rows = await (
            from b in query
            join m in db.PayrollEmployees.AsNoTracking() on b.EmployeeId equals m.Id
            join c in db.PayComponents.AsNoTracking() on b.PayComponentId equals c.Id into cs
            from c in cs.DefaultIfEmpty()
            join i in db.PayrollInputs.AsNoTracking() on b.PayrollInputId equals i.Id into ins
            from i in ins.DefaultIfEmpty()
            join p in db.PayPeriods.AsNoTracking() on i.PayPeriodId equals p.Id into ps
            from p in ps.DefaultIfEmpty()
            orderby b.Status, b.CreatedAt descending
            select new
            {
                b, m.FullName, m.EmployeeCode, m.DepartmentName, Component = c == null ? "—" : c.Name,
                PeriodStart = p == null ? (DateOnly?)null : p.PeriodStart,
                PeriodEnd = p == null ? (DateOnly?)null : p.PeriodEnd
            })
            .Take(1000)
            .ToListAsync(ct);

        // Payroll se gaye bonus: jis payslip mein input laga woh paid ho chuki?
        var inputIds = rows.Where(r => r.b.PayrollInputId != null).Select(r => r.b.PayrollInputId).ToList();
        var paidInputs = inputIds.Count == 0 ? [] : await (
            from l in db.Payslips.AsNoTracking().Where(s => s.Status == PayslipStatus.Paid).SelectMany(s => s.Lines)
            where l.Source == LineSource.Input && inputIds.Contains(l.SourceReference)
            select l.SourceReference!.Value).Distinct().ToListAsync(ct);

        return rows.Select(x =>
        {
            var b = x.b;
            var status = b.Status == BonusStatus.Approved && b.PayrollInputId is { } inputId && paidInputs.Contains(inputId) ? BonusStatus.Paid : b.Status;
            return new BonusDto(
                b.Id, b.EmployeeId, x.FullName, x.EmployeeCode, x.DepartmentName, b.BonusType, b.Title, b.CurrencyCode, b.Amount,
                b.PayComponentId, x.Component, b.BatchId, b.Reason, status, b.PayoutMethod, x.PeriodStart, x.PeriodEnd, b.PaidAt,
                b.DecidedAt, b.DecisionNote, b.CreatedAt);
        }).ToList();
    }
}
