namespace HR.Payroll.API.Application.Expenses;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Expenses;
using HR.Payroll.API.Domain.Inputs;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Expenses & travel page: My expenses (employee), Claims, Travel, Advances tabs + Expense policy/categories.
// Reimbursement payroll mein PayrollInput (Source = Expense) ban kar agli salary ke saath jata hai,
// ya HR "Direct" de kar paid mark karta hai. Travel advance claim approve hone pe usi mein kat jata hai.

#region DTOs

public sealed record ExpenseCategoryDto(
    Guid Id, string Code, string Name, string? Description, decimal? MaxPerClaim, bool ReceiptRequired, bool IsActive, short SortOrder);

public sealed record ExpenseLineDto(
    Guid Id, Guid CategoryId, string CategoryName, DateOnly ExpenseDate, string Description, string? Merchant,
    decimal Amount, string? ReceiptNumber, bool HasReceipt);

/// <summary>Status = asal haalat: payroll payout wala claim us run ke "paid" hone pe Paid dikhta hai.</summary>
public sealed record ExpenseClaimDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? DepartmentName,
    string Title, Guid? TravelRequestId, string? TravelDestination, string CurrencyCode, decimal TotalAmount,
    ExpenseClaimStatus Status, decimal? ApprovedAmount, decimal AdvanceAdjusted, decimal NetPayable, decimal ToRecover,
    PayoutMethod? PayoutMethod, DateOnly? PayPeriodStart, DateOnly? PayPeriodEnd, DateTime? PaidAt,
    DateTime? DecidedAt, string? DecisionComment, DateTime CreatedAt, bool CanCancel, IReadOnlyList<ExpenseLineDto> Lines);

public sealed record TravelRequestDto(
    Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeCode, string? DepartmentName,
    string Purpose, string Destination, DateOnly DepartDate, DateOnly ReturnDate, TravelMode TravelMode,
    string CurrencyCode, decimal EstimatedCost, decimal AdvanceRequested, TravelRequestStatus Status,
    decimal AdvanceApproved, AdvanceStatus AdvanceStatus, PayoutMethod? AdvancePayoutMethod, DateTime? AdvancePaidAt,
    Guid? ClaimId, DateTime? DecidedAt, string? DecisionComment, DateTime CreatedAt, bool CanCancel, bool CanClaim);

public sealed record ExpensePolicyDto(
    bool ClaimsEnabled, bool TravelEnabled, decimal? ReceiptRequiredAbove, short SubmitWithinDays,
    bool AdvancesEnabled, decimal MaxAdvancePercent, Guid? ReimbursementComponentId, Guid? AdvanceRecoveryComponentId);

public sealed record MyExpensesDto(
    Guid? EmployeeId, string? CurrencyCode, ExpensePolicyDto Policy, IReadOnlyList<ExpenseCategoryDto> Categories,
    IReadOnlyList<ExpenseClaimDto> Claims, IReadOnlyList<TravelRequestDto> Travel);

#endregion

#region Queries + commands

public sealed record GetMyExpensesQuery : IRequest<MyExpensesDto>;
public sealed record GetExpenseClaimsQuery(ExpenseClaimStatus? Status, Guid? EmployeeId) : IRequest<IReadOnlyList<ExpenseClaimDto>>;
public sealed record GetTravelRequestsQuery(TravelRequestStatus? Status, bool AdvancesOnly, Guid? EmployeeId) : IRequest<IReadOnlyList<TravelRequestDto>>;

public sealed record ExpenseLineInput(
    Guid CategoryId, DateOnly ExpenseDate, string Description, string? Merchant, decimal Amount, string? ReceiptNumber, bool HasReceipt);

public sealed record SubmitExpenseClaimCommand(string Title, Guid? TravelRequestId, IReadOnlyList<ExpenseLineInput> Lines) : IRequest<Guid>;
public sealed record CancelExpenseClaimCommand(Guid Id) : IRequest;

/// <summary>ApprovedAmount khaali = poora claim. Payout khaali = policy mein reimbursement component ho to Payroll, warna Direct.</summary>
public sealed record ApproveExpenseClaimCommand(Guid Id, decimal? ApprovedAmount, PayoutMethod? Payout, string? Comment) : IRequest;
public sealed record RejectExpenseClaimCommand(Guid Id, string? Comment) : IRequest;
public sealed record MarkExpenseClaimPaidCommand(Guid Id) : IRequest;

public sealed record SubmitTravelRequestCommand(
    string Purpose, string Destination, DateOnly DepartDate, DateOnly ReturnDate, TravelMode TravelMode,
    decimal EstimatedCost, decimal AdvanceRequested) : IRequest<Guid>;
public sealed record CancelTravelRequestCommand(Guid Id) : IRequest;
public sealed record ApproveTravelRequestCommand(Guid Id, decimal? AdvanceApproved, string? Comment) : IRequest;
public sealed record RejectTravelRequestCommand(Guid Id, string? Comment) : IRequest;
public sealed record PayTravelAdvanceCommand(Guid Id, PayoutMethod Payout) : IRequest;

public sealed record GetExpensePolicyQuery : IRequest<ExpensePolicyDto>;
public sealed record SaveExpensePolicyCommand(
    bool ClaimsEnabled, bool TravelEnabled, decimal? ReceiptRequiredAbove, short SubmitWithinDays,
    bool AdvancesEnabled, decimal MaxAdvancePercent, Guid? ReimbursementComponentId, Guid? AdvanceRecoveryComponentId) : IRequest;

public sealed record GetExpenseCategoriesQuery : IRequest<IReadOnlyList<ExpenseCategoryDto>>;
public sealed record SaveExpenseCategoryCommand(
    Guid? Id, string Code, string Name, string? Description, decimal? MaxPerClaim, bool ReceiptRequired, bool IsActive, short SortOrder) : IRequest<Guid>;

public sealed class SubmitExpenseClaimValidator : AbstractValidator<SubmitExpenseClaimCommand>
{
    public SubmitExpenseClaimValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one expense.")
            .Must(l => l.Count <= 50).WithMessage("A claim can have at most 50 expenses.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.CategoryId).NotEmpty();
            line.RuleFor(l => l.Description).NotEmpty().MaximumLength(300);
            line.RuleFor(l => l.Merchant).MaximumLength(150);
            line.RuleFor(l => l.Amount).GreaterThan(0);
            line.RuleFor(l => l.ReceiptNumber).MaximumLength(100);
        });
    }
}

public sealed class ApproveExpenseClaimValidator : AbstractValidator<ApproveExpenseClaimCommand>
{
    public ApproveExpenseClaimValidator()
    {
        RuleFor(x => x.ApprovedAmount).GreaterThan(0).When(x => x.ApprovedAmount is not null);
        RuleFor(x => x.Payout).IsInEnum().When(x => x.Payout is not null);
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}

public sealed class RejectExpenseClaimValidator : AbstractValidator<RejectExpenseClaimCommand>
{
    public RejectExpenseClaimValidator()
        => RuleFor(x => x.Comment).NotEmpty().WithMessage("Please give a reason for rejecting.").MaximumLength(500);
}

public sealed class SubmitTravelRequestValidator : AbstractValidator<SubmitTravelRequestCommand>
{
    public SubmitTravelRequestValidator()
    {
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TravelMode).IsInEnum();
        RuleFor(x => x.ReturnDate).GreaterThanOrEqualTo(x => x.DepartDate).WithMessage("Return date cannot be before the departure date.");
        RuleFor(x => x.EstimatedCost).GreaterThan(0);
        RuleFor(x => x.AdvanceRequested).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.EstimatedCost)
            .WithMessage("Advance cannot be more than the estimated cost.");
    }
}

public sealed class ApproveTravelRequestValidator : AbstractValidator<ApproveTravelRequestCommand>
{
    public ApproveTravelRequestValidator()
    {
        RuleFor(x => x.AdvanceApproved).GreaterThanOrEqualTo(0).When(x => x.AdvanceApproved is not null);
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}

public sealed class RejectTravelRequestValidator : AbstractValidator<RejectTravelRequestCommand>
{
    public RejectTravelRequestValidator()
        => RuleFor(x => x.Comment).NotEmpty().WithMessage("Please give a reason for rejecting.").MaximumLength(500);
}

public sealed class PayTravelAdvanceValidator : AbstractValidator<PayTravelAdvanceCommand>
{
    public PayTravelAdvanceValidator() => RuleFor(x => x.Payout).IsInEnum();
}

public sealed class SaveExpensePolicyValidator : AbstractValidator<SaveExpensePolicyCommand>
{
    public SaveExpensePolicyValidator()
    {
        RuleFor(x => x.ReceiptRequiredAbove).GreaterThanOrEqualTo(0).When(x => x.ReceiptRequiredAbove is not null);
        RuleFor(x => x.SubmitWithinDays).InclusiveBetween((short)1, (short)365);
        RuleFor(x => x.MaxAdvancePercent).GreaterThan(0).LessThanOrEqualTo(100);
    }
}

public sealed class SaveExpenseCategoryValidator : AbstractValidator<SaveExpenseCategoryCommand>
{
    public SaveExpenseCategoryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("Code can only have letters, numbers, - and _.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.MaxPerClaim).GreaterThan(0).When(x => x.MaxPerClaim is not null);
    }
}

#endregion

public sealed class ExpenseHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetMyExpensesQuery, MyExpensesDto>,
    IRequestHandler<GetExpenseClaimsQuery, IReadOnlyList<ExpenseClaimDto>>,
    IRequestHandler<GetTravelRequestsQuery, IReadOnlyList<TravelRequestDto>>,
    IRequestHandler<SubmitExpenseClaimCommand, Guid>,
    IRequestHandler<CancelExpenseClaimCommand>,
    IRequestHandler<ApproveExpenseClaimCommand>,
    IRequestHandler<RejectExpenseClaimCommand>,
    IRequestHandler<MarkExpenseClaimPaidCommand>,
    IRequestHandler<SubmitTravelRequestCommand, Guid>,
    IRequestHandler<CancelTravelRequestCommand>,
    IRequestHandler<ApproveTravelRequestCommand>,
    IRequestHandler<RejectTravelRequestCommand>,
    IRequestHandler<PayTravelAdvanceCommand>,
    IRequestHandler<GetExpensePolicyQuery, ExpensePolicyDto>,
    IRequestHandler<SaveExpensePolicyCommand>,
    IRequestHandler<GetExpenseCategoriesQuery, IReadOnlyList<ExpenseCategoryDto>>,
    IRequestHandler<SaveExpenseCategoryCommand, Guid>
{
    private static readonly ExpenseClaimStatus[] LiveClaim = [ExpenseClaimStatus.Submitted, ExpenseClaimStatus.Approved, ExpenseClaimStatus.Paid];
    private static readonly RunStatus[] ClosedRun = [RunStatus.Approved, RunStatus.Paid];
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private void EnsureCanViewAll()
    {
        if (!currentUser.HasPermission(Permissions.PayrollViewAll) && !currentUser.HasPermission(Permissions.PayrollApprove))
            throw new UnauthorizedAccessException("You do not have permission to see everyone's expenses.");
    }

    // ───────────────────────── Employee ─────────────────────────

    public async Task<MyExpensesDto> Handle(GetMyExpensesQuery q, CancellationToken ct)
    {
        var policy = ToDto(await PolicyAsync(ct));
        var categories = (await CategoriesAsync(ct)).Where(c => c.IsActive).ToList();
        var meId = await MyEmployeeIdAsync(ct);
        if (meId is not { } me)
            return new MyExpensesDto(null, null, policy, categories, [], []);

        var claims = await ToClaimDtosAsync(db.ExpenseClaims.AsNoTracking().Where(c => c.EmployeeId == me), me, ct);
        var travel = await ToTravelDtosAsync(db.TravelRequests.AsNoTracking().Where(t => t.EmployeeId == me), me, ct);
        return new MyExpensesDto(me, await CurrencyAsync(me, ct), policy, categories, claims, travel);
    }

    public async Task<Guid> Handle(SubmitExpenseClaimCommand c, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct) ?? throw new UnauthorizedAccessException("Your login is not linked to a payroll employee.");
        var policy = await PolicyAsync(ct);
        if (!policy.ClaimsEnabled)
            throw new DomainException("Expense claims are not open at the moment.");

        var categories = (await CategoriesAsync(ct)).ToDictionary(x => x.Id);
        var earliest = Today.AddDays(-policy.SubmitWithinDays);
        foreach (var line in c.Lines)
        {
            if (!categories.TryGetValue(line.CategoryId, out var category) || !category.IsActive)
                throw new DomainException("One of the expense types is no longer available. Pick another.");
            if (line.ExpenseDate > Today)
                throw new DomainException($"\"{line.Description}\" is dated in the future.");
            if (line.ExpenseDate < earliest)
                throw new DomainException($"\"{line.Description}\" is older than {policy.SubmitWithinDays} days and can't be claimed.");
            if (policy.NeedsReceipt(line.Amount, category.ReceiptRequired) && !line.HasReceipt && string.IsNullOrWhiteSpace(line.ReceiptNumber))
                throw new DomainException($"\"{line.Description}\" needs a receipt.");
        }
        foreach (var group in c.Lines.GroupBy(l => l.CategoryId))
        {
            var category = categories[group.Key];
            if (category.MaxPerClaim is { } max && group.Sum(l => l.Amount) > max)
                throw new DomainException($"{category.Name} is limited to {max:N0} per claim.");
        }

        if (c.TravelRequestId is { } travelId)
        {
            var travel = await db.TravelRequests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == travelId, ct)
                         ?? throw new NotFoundException("Travel request", travelId);
            if (travel.EmployeeId != me)
                throw new DomainException("You can only link your own trips.");
            if (travel.Status != TravelRequestStatus.Approved)
                throw new DomainException("Only an approved trip can be claimed.");
            if (await db.ExpenseClaims.AnyAsync(x => x.TravelRequestId == travelId && LiveClaim.Contains(x.Status), ct))
                throw new ConflictException("This trip already has a claim.");
        }

        var claim = ExpenseClaim.Submit(currentUser.RequireTenantId(), me, c.Title, await CurrencyAsync(me, ct)
                                        ?? throw new ConflictException("Payroll base currency is not set yet. Please contact HR."),
                                        c.TravelRequestId,
                                        c.Lines.Select(l => new ExpenseLineData(l.CategoryId, l.ExpenseDate, l.Description, l.Merchant,
                                                                                l.Amount, l.ReceiptNumber, l.HasReceipt)));
        db.ExpenseClaims.Add(claim);
        await db.SaveChangesAsync(ct);
        return claim.Id;
    }

    public async Task Handle(CancelExpenseClaimCommand c, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct) ?? throw new UnauthorizedAccessException("Your login is not linked to a payroll employee.");
        var claim = await db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Expense claim", c.Id);
        claim.Cancel(me);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> Handle(SubmitTravelRequestCommand c, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct) ?? throw new UnauthorizedAccessException("Your login is not linked to a payroll employee.");
        var policy = await PolicyAsync(ct);
        if (!policy.TravelEnabled)
            throw new DomainException("Travel requests are not open at the moment.");
        if (c.DepartDate < Today.AddDays(-30))
            throw new DomainException("Trips that ended more than a month ago can't be requested.");
        if (c.AdvanceRequested > 0)
        {
            if (!policy.AdvancesEnabled)
                throw new DomainException("Travel advances are not offered at the moment.");
            var max = policy.MaxAdvanceFor(c.EstimatedCost);
            if (c.AdvanceRequested > max)
                throw new DomainException($"The most you can ask as an advance is {max:N0} ({policy.MaxAdvancePercent:0.##}% of the estimate).");
        }

        var request = TravelRequest.Submit(currentUser.RequireTenantId(), me, c.Purpose, c.Destination, c.DepartDate, c.ReturnDate,
                                           c.TravelMode, await CurrencyAsync(me, ct)
                                           ?? throw new ConflictException("Payroll base currency is not set yet. Please contact HR."),
                                           c.EstimatedCost, c.AdvanceRequested);
        db.TravelRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request.Id;
    }

    public async Task Handle(CancelTravelRequestCommand c, CancellationToken ct)
    {
        var me = await MyEmployeeIdAsync(ct) ?? throw new UnauthorizedAccessException("Your login is not linked to a payroll employee.");
        var request = await db.TravelRequests.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Travel request", c.Id);
        request.Cancel(me);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────── HR: lists ─────────────────────────

    public async Task<IReadOnlyList<ExpenseClaimDto>> Handle(GetExpenseClaimsQuery q, CancellationToken ct)
    {
        EnsureCanViewAll();
        var claims = db.ExpenseClaims.AsNoTracking();
        if (q.EmployeeId is { } employeeId) claims = claims.Where(x => x.EmployeeId == employeeId);
        var rows = await ToClaimDtosAsync(claims, null, ct);
        // Status filter DTO pe: payroll se paid hua claim DB mein abhi Approved ho sakta hai
        return q.Status is { } status ? rows.Where(r => r.Status == status).ToList() : rows;
    }

    public async Task<IReadOnlyList<TravelRequestDto>> Handle(GetTravelRequestsQuery q, CancellationToken ct)
    {
        EnsureCanViewAll();
        var travel = db.TravelRequests.AsNoTracking();
        if (q.Status is { } status) travel = travel.Where(t => t.Status == status);
        if (q.AdvancesOnly) travel = travel.Where(t => t.AdvanceStatus != AdvanceStatus.None);
        if (q.EmployeeId is { } employeeId) travel = travel.Where(t => t.EmployeeId == employeeId);
        return await ToTravelDtosAsync(travel, null, ct);
    }

    // ───────────────────────── HR: decisions ─────────────────────────

    public async Task Handle(ApproveExpenseClaimCommand c, CancellationToken ct)
    {
        var approver = currentUser.UserId ?? throw new UnauthorizedAccessException("Approver could not be identified.");
        var claim = await db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Expense claim", c.Id);
        var policy = await PolicyAsync(ct);
        var payout = c.Payout ?? (policy.ReimbursementComponentId is null ? PayoutMethod.Direct : PayoutMethod.Payroll);

        TravelRequest? travel = null;
        if (claim.TravelRequestId is { } travelId)
            travel = await db.TravelRequests.FirstOrDefaultAsync(t => t.Id == travelId, ct);
        var advance = travel?.AdvanceStatus == AdvanceStatus.Paid ? travel.AdvanceApproved : 0m;

        claim.Approve(approver, c.ApprovedAmount ?? claim.TotalAmount, advance, payout, c.Comment);
        if (advance > 0)
            travel!.Settle();

        var toRecover = advance - claim.AdvanceAdjusted;
        var needsReimbursement = payout == PayoutMethod.Payroll && claim.NetPayable > 0;
        var needsRecovery = toRecover > 0 && policy.AdvanceRecoveryComponentId is not null;
        if (needsReimbursement || needsRecovery)
        {
            var periodId = await NextPayPeriodAsync(claim.EmployeeId, ct);
            PayrollInput? reimbursement = null, recovery = null;
            if (needsReimbursement)
            {
                var component = policy.ReimbursementComponentId
                                ?? throw new ConflictException("Set a reimbursement component in the expense policy, or pay this claim directly.");
                await EnsureComponentAsync(component, ComponentType.Earning, ct);
                reimbursement = PayrollInput.Create(claim.TenantId, periodId, claim.EmployeeId, component, claim.NetPayable, null,
                                                    InputSource.Expense, $"EXP:{claim.Id}", Trim($"Expense claim: {claim.Title}"));
                db.PayrollInputs.Add(reimbursement);
            }
            if (needsRecovery)
            {
                await EnsureComponentAsync(policy.AdvanceRecoveryComponentId!.Value, ComponentType.Deduction, ct);
                recovery = PayrollInput.Create(claim.TenantId, periodId, claim.EmployeeId, policy.AdvanceRecoveryComponentId.Value, toRecover,
                                               null, InputSource.Expense, $"REC:{claim.Id}", Trim($"Unspent travel advance: {claim.Title}"));
                db.PayrollInputs.Add(recovery);
            }
            claim.AttachPayrollInputs(reimbursement?.Id, recovery?.Id);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RejectExpenseClaimCommand c, CancellationToken ct)
    {
        var approver = currentUser.UserId ?? throw new UnauthorizedAccessException("Approver could not be identified.");
        var claim = await db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Expense claim", c.Id);
        claim.Reject(approver, c.Comment!);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(MarkExpenseClaimPaidCommand c, CancellationToken ct)
    {
        var claim = await db.ExpenseClaims.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Expense claim", c.Id);
        if (claim.PayoutMethod == PayoutMethod.Payroll)
            throw new DomainException("This claim is paid with salary; it shows as paid once that payroll run is paid.");
        claim.MarkPaid();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ApproveTravelRequestCommand c, CancellationToken ct)
    {
        var approver = currentUser.UserId ?? throw new UnauthorizedAccessException("Approver could not be identified.");
        var request = await db.TravelRequests.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Travel request", c.Id);
        request.Approve(approver, c.AdvanceApproved ?? request.AdvanceRequested, c.Comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RejectTravelRequestCommand c, CancellationToken ct)
    {
        var approver = currentUser.UserId ?? throw new UnauthorizedAccessException("Approver could not be identified.");
        var request = await db.TravelRequests.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Travel request", c.Id);
        request.Reject(approver, c.Comment!);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(PayTravelAdvanceCommand c, CancellationToken ct)
    {
        var request = await db.TravelRequests.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Travel request", c.Id);
        Guid? inputId = null;
        if (c.Payout == PayoutMethod.Payroll)
        {
            if (request.AdvanceStatus != AdvanceStatus.Approved)
                throw new DomainException("There is no approved advance waiting to be paid.");
            var component = (await PolicyAsync(ct)).ReimbursementComponentId
                            ?? throw new ConflictException("Set a reimbursement component in the expense policy, or pay the advance directly.");
            await EnsureComponentAsync(component, ComponentType.Earning, ct);
            var input = PayrollInput.Create(request.TenantId, await NextPayPeriodAsync(request.EmployeeId, ct), request.EmployeeId, component,
                                            request.AdvanceApproved, null, InputSource.Expense, $"ADV:{request.Id}",
                                            Trim($"Travel advance: {request.Destination}"));
            db.PayrollInputs.Add(input);
            inputId = input.Id;
        }
        request.MarkAdvancePaid(c.Payout, inputId);
        await db.SaveChangesAsync(ct);
    }

    // ───────────────────────── Policy + categories ─────────────────────────

    public async Task<ExpensePolicyDto> Handle(GetExpensePolicyQuery q, CancellationToken ct) => ToDto(await PolicyAsync(ct));

    public async Task Handle(SaveExpensePolicyCommand c, CancellationToken ct)
    {
        if (c.ReimbursementComponentId is { } earning) await EnsureComponentAsync(earning, ComponentType.Earning, ct);
        if (c.AdvanceRecoveryComponentId is { } deduction) await EnsureComponentAsync(deduction, ComponentType.Deduction, ct);

        var policy = await db.ExpensePolicies.FirstOrDefaultAsync(ct);
        if (policy is null)
        {
            policy = ExpensePolicy.CreateDefault(currentUser.RequireTenantId());
            db.ExpensePolicies.Add(policy);
        }
        policy.Update(c.ClaimsEnabled, c.TravelEnabled, c.ReceiptRequiredAbove, c.SubmitWithinDays,
                      c.AdvancesEnabled, c.MaxAdvancePercent, c.ReimbursementComponentId, c.AdvanceRecoveryComponentId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> Handle(GetExpenseCategoriesQuery q, CancellationToken ct) => await CategoriesAsync(ct);

    public async Task<Guid> Handle(SaveExpenseCategoryCommand c, CancellationToken ct)
    {
        await CategoriesAsync(ct);   // defaults pehle ban jayen, warna naya code unse takra sakta hai
        var code = c.Code.Trim().ToUpperInvariant();
        if (await db.ExpenseCategories.AnyAsync(x => x.Code == code && x.Id != c.Id, ct))
            throw new ConflictException($"An expense type with code {code} already exists.");

        ExpenseCategory category;
        if (c.Id is { } id)
        {
            category = await db.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Expense type", id);
            category.Update(code, c.Name, c.Description, c.MaxPerClaim, c.ReceiptRequired, c.IsActive, c.SortOrder);
        }
        else
        {
            category = ExpenseCategory.Create(currentUser.RequireTenantId(), code, c.Name, c.Description, c.MaxPerClaim, c.ReceiptRequired, c.SortOrder);
            db.ExpenseCategories.Add(category);
        }
        await db.SaveChangesAsync(ct);
        return category.Id;
    }

    // ───────────────────────── helpers ─────────────────────────

    /// <summary>Tenant ki pehli visit pe default qismein bana do, taake employee foran claim kar sake.</summary>
    private async Task<IReadOnlyList<ExpenseCategoryDto>> CategoriesAsync(CancellationToken ct)
    {
        if (!await db.ExpenseCategories.AnyAsync(ct))
        {
            var tenantId = currentUser.RequireTenantId();
            short order = 0;
            foreach (var (code, name, receipt) in ExpenseCategory.Defaults)
                db.ExpenseCategories.Add(ExpenseCategory.Create(tenantId, code, name, null, null, receipt, order += 10));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Do requests ne saath defaults banaye: unique (TenantId, Code) ne doosri rok di — jo bani wahi chalegi
                foreach (var added in db.ExpenseCategories.Local.ToList())
                    db.ExpenseCategories.Remove(added);   // Added entity ko Remove = detach
            }
        }

        return await db.ExpenseCategories.AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new ExpenseCategoryDto(x.Id, x.Code, x.Name, x.Description, x.MaxPerClaim, x.ReceiptRequired, x.IsActive, x.SortOrder))
            .ToListAsync(ct);
    }

    private async Task<ExpensePolicy> PolicyAsync(CancellationToken ct)
        => await db.ExpensePolicies.AsNoTracking().FirstOrDefaultAsync(ct) ?? ExpensePolicy.CreateDefault(currentUser.RequireTenantId());

    private static ExpensePolicyDto ToDto(ExpensePolicy p)
        => new(p.ClaimsEnabled, p.TravelEnabled, p.ReceiptRequiredAbove, p.SubmitWithinDays, p.AdvancesEnabled, p.MaxAdvancePercent,
               p.ReimbursementComponentId, p.AdvanceRecoveryComponentId);

    private async Task EnsureComponentAsync(Guid componentId, ComponentType type, CancellationToken ct)
    {
        var component = await db.PayComponents.AsNoTracking().FirstOrDefaultAsync(p => p.Id == componentId, ct)
                        ?? throw new NotFoundException("Pay component", componentId);
        if (component.ComponentType != type || !component.IsActive)
            throw new ConflictException($"{component.Name} must be an active {(type == ComponentType.Earning ? "earning" : "deduction")} component.");
    }

    /// <summary>Employee ke pay group ka agla khula period jiska run abhi approve/pay nahi hua.</summary>
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
    {
        var today = Today;
        return await db.EmployeeSalaries.AsNoTracking()
                   .Where(s => s.EmployeeId == employeeId && s.EffectiveFrom <= today && (s.EffectiveTo == null || s.EffectiveTo >= today))
                   .OrderByDescending(s => s.EffectiveFrom)
                   .Select(s => s.CurrencyCode)
                   .FirstOrDefaultAsync(ct)
               ?? await db.PayrollSettings.Select(s => s.BaseCurrency).FirstOrDefaultAsync(ct);
    }

    /// <summary>Payroll ke paas UserId nahi — login email = work email se pehchaan (Loans jaisa).</summary>
    private async Task<Guid?> MyEmployeeIdAsync(CancellationToken ct)
    {
        var email = currentUser.Email?.Trim().ToLower();
        if (string.IsNullOrEmpty(email)) return null;
        return await db.PayrollEmployees.AsNoTracking()
            .Where(e => e.WorkEmail.ToLower() == email && e.IsActive)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static string Trim(string value) => value.Length > 500 ? value[..500] : value;

    private async Task<IReadOnlyList<ExpenseClaimDto>> ToClaimDtosAsync(IQueryable<ExpenseClaim> claims, Guid? meId, CancellationToken ct)
    {
        var rows = await (
            from c in claims
            join e in db.PayrollEmployees.AsNoTracking() on c.EmployeeId equals e.Id
            join t in db.TravelRequests.AsNoTracking() on c.TravelRequestId equals t.Id into ts
            from t in ts.DefaultIfEmpty()
            join i in db.PayrollInputs.AsNoTracking() on c.PayrollInputId equals i.Id into ins
            from i in ins.DefaultIfEmpty()
            join p in db.PayPeriods.AsNoTracking() on i.PayPeriodId equals p.Id into ps
            from p in ps.DefaultIfEmpty()
            orderby c.Status, c.CreatedAt descending
            select new
            {
                c, Lines = c.Lines.ToList(), e.FullName, e.EmployeeCode, e.DepartmentName,
                Destination = t == null ? null : t.Destination,
                Advance = t == null ? 0m : t.AdvanceApproved,
                PeriodStart = p == null ? (DateOnly?)null : p.PeriodStart,
                PeriodEnd = p == null ? (DateOnly?)null : p.PeriodEnd
            })
            .AsSplitQuery()
            .Take(500)
            .ToListAsync(ct);

        // Payroll se gaye claims: jis payslip mein input laga woh paid ho chuki?
        var inputIds = rows.Where(r => r.c.PayrollInputId != null).Select(r => r.c.PayrollInputId).ToList();
        var paidInputs = inputIds.Count == 0 ? [] : await (
            from l in db.Payslips.AsNoTracking().Where(s => s.Status == PayslipStatus.Paid).SelectMany(s => s.Lines)
            where l.Source == LineSource.Input && inputIds.Contains(l.SourceReference)
            select l.SourceReference!.Value).Distinct().ToListAsync(ct);

        var categoryNames = await db.ExpenseCategories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return rows.Select(x =>
        {
            var c = x.c;
            var status = c.Status == ExpenseClaimStatus.Approved && c.PayrollInputId is { } inputId && paidInputs.Contains(inputId)
                ? ExpenseClaimStatus.Paid : c.Status;
            var toRecover = c.Status is ExpenseClaimStatus.Approved or ExpenseClaimStatus.Paid
                ? Math.Max(0, x.Advance - c.AdvanceAdjusted) : 0;
            return new ExpenseClaimDto(
                c.Id, c.EmployeeId, x.FullName, x.EmployeeCode, x.DepartmentName, c.Title, c.TravelRequestId, x.Destination,
                c.CurrencyCode, c.TotalAmount, status, c.ApprovedAmount, c.AdvanceAdjusted, c.NetPayable, toRecover,
                c.PayoutMethod, x.PeriodStart, x.PeriodEnd, c.PaidAt, c.DecidedAt, c.DecisionComment, c.CreatedAt,
                meId is not null && c.EmployeeId == meId && c.Status == ExpenseClaimStatus.Submitted,
                x.Lines.OrderBy(l => l.ExpenseDate).Select(l => new ExpenseLineDto(
                    l.Id, l.CategoryId, categoryNames.GetValueOrDefault(l.CategoryId, "—"), l.ExpenseDate, l.Description, l.Merchant,
                    l.Amount, l.ReceiptNumber, l.HasReceipt)).ToList());
        }).ToList();
    }

    private async Task<IReadOnlyList<TravelRequestDto>> ToTravelDtosAsync(IQueryable<TravelRequest> travel, Guid? meId, CancellationToken ct)
    {
        var rows = await (
            from t in travel
            join e in db.PayrollEmployees.AsNoTracking() on t.EmployeeId equals e.Id
            orderby t.Status, t.DepartDate descending
            select new
            {
                t, e.FullName, e.EmployeeCode, e.DepartmentName,
                ClaimId = db.ExpenseClaims.Where(c => c.TravelRequestId == t.Id && LiveClaim.Contains(c.Status))
                                          .Select(c => (Guid?)c.Id).FirstOrDefault()
            })
            .Take(500)
            .ToListAsync(ct);

        return rows.Select(x => new TravelRequestDto(
            x.t.Id, x.t.EmployeeId, x.FullName, x.EmployeeCode, x.DepartmentName, x.t.Purpose, x.t.Destination,
            x.t.DepartDate, x.t.ReturnDate, x.t.TravelMode, x.t.CurrencyCode, x.t.EstimatedCost, x.t.AdvanceRequested, x.t.Status,
            x.t.AdvanceApproved, x.t.AdvanceStatus, x.t.AdvancePayoutMethod, x.t.AdvancePaidAt, x.ClaimId,
            x.t.DecidedAt, x.t.DecisionComment, x.t.CreatedAt,
            meId is not null && x.t.EmployeeId == meId && x.t.Status == TravelRequestStatus.Pending,
            meId is not null && x.t.EmployeeId == meId && x.t.Status == TravelRequestStatus.Approved && x.ClaimId is null)).ToList();
    }
}
