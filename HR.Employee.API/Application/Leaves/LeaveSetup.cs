namespace HR.Employee.API.Application.Leaves;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Leaves;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Leave setup page: Leave types, Holidays, Policies, Approvals tabs. Simple CRUD (Attendance setup jaisa).

#region Leave types

public sealed record LeaveTypeDto(
    Guid Id, string Name, string Code, string? Color, bool IsPaid, bool RequiresAttachment,
    bool AllowHalfDay, bool AllowNegativeBalance, short SortOrder, bool IsActive);

public sealed record SaveLeaveTypeRequest(
    string Name, string Code, string? Color, bool IsPaid = true, bool RequiresAttachment = false,
    bool AllowHalfDay = true, bool AllowNegativeBalance = false, short SortOrder = 0, bool IsActive = true);

public sealed record GetLeaveTypesQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<LeaveTypeDto>>;
public sealed record CreateLeaveTypeCommand(SaveLeaveTypeRequest Data) : IRequest<Guid>;
public sealed record UpdateLeaveTypeCommand(Guid Id, SaveLeaveTypeRequest Data) : IRequest;

public sealed class SaveLeaveTypeRequestValidator : AbstractValidator<SaveLeaveTypeRequest>
{
    public SaveLeaveTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").When(x => !string.IsNullOrEmpty(x.Color));
    }
}

public sealed class CreateLeaveTypeValidator : AbstractValidator<CreateLeaveTypeCommand>
{
    public CreateLeaveTypeValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveLeaveTypeRequestValidator());
}

public sealed class UpdateLeaveTypeValidator : AbstractValidator<UpdateLeaveTypeCommand>
{
    public UpdateLeaveTypeValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveLeaveTypeRequestValidator());
}

public sealed class LeaveTypeHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetLeaveTypesQuery, IReadOnlyList<LeaveTypeDto>>,
    IRequestHandler<CreateLeaveTypeCommand, Guid>,
    IRequestHandler<UpdateLeaveTypeCommand>
{
    public async Task<IReadOnlyList<LeaveTypeDto>> Handle(GetLeaveTypesQuery q, CancellationToken ct)
        => await db.LeaveTypes.AsNoTracking()
            .Where(t => q.IncludeInactive || t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new LeaveTypeDto(t.Id, t.Name, t.Code, t.Color, t.IsPaid, t.RequiresAttachment,
                                          t.AllowHalfDay, t.AllowNegativeBalance, t.SortOrder, t.IsActive))
            .ToListAsync(ct);

    public async Task<Guid> Handle(CreateLeaveTypeCommand c, CancellationToken ct)
    {
        var d = c.Data;
        await EnsureCodeIsFreeAsync(d.Code, null, ct);
        var type = LeaveType.Create(currentUser.RequireTenantId(), d.Name, d.Code, NullIfEmpty(d.Color), d.IsPaid,
                                    d.RequiresAttachment, d.AllowHalfDay, d.AllowNegativeBalance, d.SortOrder);
        if (!d.IsActive) type.Deactivate();
        db.LeaveTypes.Add(type);
        await db.SaveChangesAsync(ct);
        return type.Id;
    }

    public async Task Handle(UpdateLeaveTypeCommand c, CancellationToken ct)
    {
        var d = c.Data;
        var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == c.Id, ct) ?? throw new NotFoundException("Leave type", c.Id);
        await EnsureCodeIsFreeAsync(d.Code, c.Id, ct);
        type.Update(d.Name, d.Code, NullIfEmpty(d.Color), d.IsPaid, d.RequiresAttachment, d.AllowHalfDay, d.AllowNegativeBalance, d.SortOrder);
        if (d.IsActive) type.Activate(); else type.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureCodeIsFreeAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.LeaveTypes.AnyAsync(t => t.Code == normalized && t.Id != exceptId, ct))
            throw new ConflictException($"A leave type with code '{normalized}' already exists.");
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

#endregion

#region Holidays

public sealed record HolidayDto(Guid Id, DateOnly Date, string Name, Guid? LocationId, string? LocationName, bool IsOptional);

public sealed record SaveHolidayRequest(DateOnly Date, string Name, Guid? LocationId, bool IsOptional = false);

public sealed record GetHolidaysQuery(short Year) : IRequest<IReadOnlyList<HolidayDto>>;
public sealed record CreateHolidayCommand(SaveHolidayRequest Data) : IRequest<Guid>;
public sealed record UpdateHolidayCommand(Guid Id, SaveHolidayRequest Data) : IRequest;
public sealed record DeleteHolidayCommand(Guid Id) : IRequest;

public sealed class SaveHolidayRequestValidator : AbstractValidator<SaveHolidayRequest>
{
    public SaveHolidayRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
}

public sealed class CreateHolidayValidator : AbstractValidator<CreateHolidayCommand>
{
    public CreateHolidayValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveHolidayRequestValidator());
}

public sealed class UpdateHolidayValidator : AbstractValidator<UpdateHolidayCommand>
{
    public UpdateHolidayValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveHolidayRequestValidator());
}

public sealed class HolidayHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetHolidaysQuery, IReadOnlyList<HolidayDto>>,
    IRequestHandler<CreateHolidayCommand, Guid>,
    IRequestHandler<UpdateHolidayCommand>,
    IRequestHandler<DeleteHolidayCommand>
{
    public async Task<IReadOnlyList<HolidayDto>> Handle(GetHolidaysQuery q, CancellationToken ct)
    {
        var from = new DateOnly(q.Year, 1, 1);
        var to = new DateOnly(q.Year, 12, 31);
        return await db.Holidays.AsNoTracking()
            .Where(h => h.Date >= from && h.Date <= to)
            .OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Id, h.Date, h.Name, h.LocationId,
                db.Locations.Where(l => l.Id == h.LocationId).Select(l => l.Name).FirstOrDefault(), h.IsOptional))
            .ToListAsync(ct);
    }

    public async Task<Guid> Handle(CreateHolidayCommand c, CancellationToken ct)
    {
        var d = c.Data;
        await EnsureValidAsync(d, null, ct);
        var holiday = Holiday.Create(currentUser.RequireTenantId(), d.Date, d.Name, d.LocationId, d.IsOptional);
        db.Holidays.Add(holiday);
        await db.SaveChangesAsync(ct);
        return holiday.Id;
    }

    public async Task Handle(UpdateHolidayCommand c, CancellationToken ct)
    {
        var holiday = await db.Holidays.FirstOrDefaultAsync(h => h.Id == c.Id, ct) ?? throw new NotFoundException("Holiday", c.Id);
        await EnsureValidAsync(c.Data, c.Id, ct);
        holiday.Update(c.Data.Date, c.Data.Name, c.Data.LocationId, c.Data.IsOptional);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(DeleteHolidayCommand c, CancellationToken ct)
    {
        var holiday = await db.Holidays.FirstOrDefaultAsync(h => h.Id == c.Id, ct) ?? throw new NotFoundException("Holiday", c.Id);
        db.Holidays.Remove(holiday);   // soft delete (AppDbContext)
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureValidAsync(SaveHolidayRequest d, Guid? exceptId, CancellationToken ct)
    {
        if (d.LocationId is { } locationId && !await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            throw new NotFoundException("Location", locationId);
        if (await db.Holidays.AnyAsync(h => h.Date == d.Date && h.LocationId == d.LocationId && h.Id != exceptId, ct))
            throw new ConflictException("There is already a holiday on this date for this location.");
    }
}

#endregion

#region Policies

public sealed record LeavePolicyRuleDto(
    Guid LeaveTypeId, string? LeaveTypeName, decimal AnnualEntitlement, AccrualMethod AccrualMethod, decimal MaxCarryForward,
    byte? CarryForwardExpiryMonths, short MinServiceDays, short? MaxConsecutiveDays, Gender? ApplicableGender,
    EmploymentType? ApplicableEmploymentTypes);

public sealed record LeavePolicyDto(
    Guid Id, string Name, Guid? LocationId, string? LocationName, DateOnly EffectiveFrom, bool IsActive,
    IReadOnlyList<LeavePolicyRuleDto> Rules);

public sealed record SaveLeavePolicyRequest(
    string Name, Guid? LocationId, DateOnly EffectiveFrom, bool IsActive, IReadOnlyList<LeavePolicyRuleDto>? Rules);

public sealed record GetLeavePoliciesQuery : IRequest<IReadOnlyList<LeavePolicyDto>>;
public sealed record CreateLeavePolicyCommand(SaveLeavePolicyRequest Data) : IRequest<Guid>;
public sealed record UpdateLeavePolicyCommand(Guid Id, SaveLeavePolicyRequest Data) : IRequest;

public sealed class SaveLeavePolicyRequestValidator : AbstractValidator<SaveLeavePolicyRequest>
{
    public SaveLeavePolicyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Rules).Must(r => r is null || r.Select(x => x.LeaveTypeId).Distinct().Count() == r.Count)
            .WithMessage("Each leave type can appear only once in a policy.");
        RuleForEach(x => x.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.LeaveTypeId).NotEmpty();
            rule.RuleFor(r => r.AnnualEntitlement).InclusiveBetween(0, 365);
            rule.RuleFor(r => r.MaxCarryForward).InclusiveBetween(0, 365);
            rule.RuleFor(r => r.AccrualMethod).IsInEnum();
            rule.RuleFor(r => r.MinServiceDays).GreaterThanOrEqualTo((short)0);
            rule.RuleFor(r => r.MaxConsecutiveDays).GreaterThan((short)0).When(r => r.MaxConsecutiveDays is not null);
        });
    }
}

public sealed class CreateLeavePolicyValidator : AbstractValidator<CreateLeavePolicyCommand>
{
    public CreateLeavePolicyValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveLeavePolicyRequestValidator());
}

public sealed class UpdateLeavePolicyValidator : AbstractValidator<UpdateLeavePolicyCommand>
{
    public UpdateLeavePolicyValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveLeavePolicyRequestValidator());
}

public sealed class LeavePolicyHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetLeavePoliciesQuery, IReadOnlyList<LeavePolicyDto>>,
    IRequestHandler<CreateLeavePolicyCommand, Guid>,
    IRequestHandler<UpdateLeavePolicyCommand>
{
    public async Task<IReadOnlyList<LeavePolicyDto>> Handle(GetLeavePoliciesQuery q, CancellationToken ct)
    {
        var policies = await db.LeavePolicies.AsNoTracking().Include(p => p.Rules)
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.LocationId != null).ThenBy(p => p.Name)
            .ToListAsync(ct);
        var locations = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var types = await db.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);

        return policies.Select(p => new LeavePolicyDto(
            p.Id, p.Name, p.LocationId, p.LocationId is { } id ? locations.GetValueOrDefault(id) : null, p.EffectiveFrom, p.IsActive,
            p.Rules
                .OrderBy(r => types.TryGetValue(r.LeaveTypeId, out var t) ? t.SortOrder : short.MaxValue)
                .Select(r => new LeavePolicyRuleDto(
                    r.LeaveTypeId, types.TryGetValue(r.LeaveTypeId, out var t) ? t.Name : null, r.AnnualEntitlement, r.AccrualMethod,
                    r.MaxCarryForward, r.CarryForwardExpiryMonths, r.MinServiceDays, r.MaxConsecutiveDays,
                    r.ApplicableGender, r.ApplicableEmploymentTypes))
                .ToList())).ToList();
    }

    public async Task<Guid> Handle(CreateLeavePolicyCommand c, CancellationToken ct)
    {
        var d = c.Data;
        await EnsureValidAsync(d, null, ct);
        var policy = LeavePolicy.Create(currentUser.RequireTenantId(), d.Name, d.LocationId, d.EffectiveFrom);
        if (!d.IsActive) policy.Deactivate();
        ApplyRules(policy, d.Rules);
        db.LeavePolicies.Add(policy);
        await db.SaveChangesAsync(ct);
        return policy.Id;
    }

    public async Task Handle(UpdateLeavePolicyCommand c, CancellationToken ct)
    {
        var d = c.Data;
        var policy = await db.LeavePolicies.Include(p => p.Rules).FirstOrDefaultAsync(p => p.Id == c.Id, ct)
                     ?? throw new NotFoundException("Leave policy", c.Id);
        if (policy.LocationId != d.LocationId || policy.EffectiveFrom != d.EffectiveFrom)
            throw new ConflictException("A policy's location and start date cannot change. Create a new policy instead.");
        await EnsureValidAsync(d, c.Id, ct);

        policy.Rename(d.Name);
        if (d.IsActive) policy.Activate(); else policy.Deactivate();
        ApplyRules(policy, d.Rules);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Request ki list hi poori sach hai: jo rule list mein nahi, woh hat jata hai.</summary>
    private static void ApplyRules(LeavePolicy policy, IReadOnlyList<LeavePolicyRuleDto>? rules)
    {
        rules ??= [];
        foreach (var stale in policy.Rules.Where(r => rules.All(x => x.LeaveTypeId != r.LeaveTypeId)).ToList())
            policy.RemoveRule(stale.LeaveTypeId);

        foreach (var r in rules)
            policy.SetRule(r.LeaveTypeId, r.AnnualEntitlement, r.AccrualMethod, r.MaxCarryForward, r.CarryForwardExpiryMonths,
                           r.MinServiceDays, r.MaxConsecutiveDays, r.ApplicableGender, r.ApplicableEmploymentTypes);
    }

    private async Task EnsureValidAsync(SaveLeavePolicyRequest d, Guid? exceptId, CancellationToken ct)
    {
        if (d.LocationId is { } locationId && !await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            throw new NotFoundException("Location", locationId);

        var typeIds = (d.Rules ?? []).Select(r => r.LeaveTypeId).Distinct().ToList();
        var known = await db.LeaveTypes.CountAsync(t => typeIds.Contains(t.Id), ct);
        if (known != typeIds.Count)
            throw new NotFoundException("Leave type", string.Join(", ", typeIds));

        if (d.IsActive && await db.LeavePolicies.AnyAsync(p => p.IsActive && p.LocationId == d.LocationId && p.Id != exceptId, ct))
            throw new ConflictException(d.LocationId is null
                ? "There is already an active default policy. Deactivate it first."
                : "This location already has an active policy. Deactivate it first.");
    }
}

#endregion

#region Approval settings

public sealed record LeaveApprovalSettingsDto(
    ApproverType Level1Approver, ApproverType? Level2Approver, byte? AutoApproveAfterDays, bool AllowCancelAfterApproval);

public sealed record GetLeaveApprovalSettingsQuery : IRequest<LeaveApprovalSettingsDto>;
public sealed record SaveLeaveApprovalSettingsCommand(
    ApproverType Level1Approver, ApproverType? Level2Approver, byte? AutoApproveAfterDays, bool AllowCancelAfterApproval) : IRequest;

public sealed class SaveLeaveApprovalSettingsValidator : AbstractValidator<SaveLeaveApprovalSettingsCommand>
{
    public SaveLeaveApprovalSettingsValidator()
    {
        RuleFor(x => x.Level1Approver).IsInEnum();
        RuleFor(x => x.Level2Approver).IsInEnum();
        RuleFor(x => x.Level2Approver).NotEqual(x => x.Level1Approver).When(x => x.Level2Approver is not null)
            .WithMessage("Both approval levels cannot use the same approver.");
        RuleFor(x => x.AutoApproveAfterDays).InclusiveBetween((byte)1, (byte)30).When(x => x.AutoApproveAfterDays is not null);
    }
}

public sealed class LeaveApprovalSettingsHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetLeaveApprovalSettingsQuery, LeaveApprovalSettingsDto>,
    IRequestHandler<SaveLeaveApprovalSettingsCommand>
{
    public async Task<LeaveApprovalSettingsDto> Handle(GetLeaveApprovalSettingsQuery q, CancellationToken ct)
    {
        var s = await db.LeaveApprovalSettings.AsNoTracking().FirstOrDefaultAsync(ct)
                ?? LeaveApprovalSettings.CreateDefault(currentUser.RequireTenantId());
        return new LeaveApprovalSettingsDto(s.Level1Approver, s.Level2Approver, s.AutoApproveAfterDays, s.AllowCancelAfterApproval);
    }

    public async Task Handle(SaveLeaveApprovalSettingsCommand c, CancellationToken ct)
    {
        var s = await db.LeaveApprovalSettings.FirstOrDefaultAsync(ct);
        if (s is null)
        {
            s = LeaveApprovalSettings.CreateDefault(currentUser.RequireTenantId());
            db.LeaveApprovalSettings.Add(s);
        }
        s.Configure(c.Level1Approver, c.Level2Approver, c.AutoApproveAfterDays, c.AllowCancelAfterApproval);
        await db.SaveChangesAsync(ct);
    }
}

#endregion
