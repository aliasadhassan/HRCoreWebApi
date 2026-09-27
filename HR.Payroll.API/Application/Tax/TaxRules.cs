namespace HR.Payroll.API.Application.Tax;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Tax;
using MediatR;
using Microsoft.EntityFrameworkCore;

// ───────────────────────────────── DTOs ─────────────────────────────────
public sealed record TaxSlabInput(decimal FromAmount, decimal? ToAmount, decimal FixedAmount, decimal RatePercent);

public sealed record TaxSlabDto(Guid Id, decimal FromAmount, decimal? ToAmount, decimal FixedAmount, decimal RatePercent);

public sealed record TaxRegimeDto(
    Guid Id, bool IsPlatformDefined, string CountryCode, string Name, byte TaxYearStartMonth, TaxCalcMethod CalcMethod,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive, IReadOnlyList<TaxSlabDto> Slabs);

public sealed record ContributionRuleDto(
    Guid Id, bool IsPlatformDefined, string CountryCode, string Code, string Name, ContributionBase BaseType,
    decimal? EmployeeRatePercent, decimal? EmployerRatePercent, decimal? EmployeeFixedAmount, decimal? EmployerFixedAmount,
    decimal? WageCeiling, Guid? EmployeeComponentId, Guid? EmployerComponentId, bool IsOptIn, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

// ─────────────────────────────── Requests ───────────────────────────────
/// <summary>Tenant ki apni regime. Platform regimes (TenantId NULL) admin seeding se aayengi.</summary>
public sealed record CreateTaxRegimeCommand(
    string CountryCode, string Name, byte TaxYearStartMonth, TaxCalcMethod CalcMethod,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, IReadOnlyList<TaxSlabInput> Slabs) : IRequest<Guid>;

public sealed record GetTaxRegimesQuery(string? CountryCode) : IRequest<IReadOnlyList<TaxRegimeDto>>;

public sealed record CreateContributionRuleCommand(
    string CountryCode, string Code, string Name, ContributionBase BaseType,
    decimal? EmployeeRatePercent, decimal? EmployerRatePercent, decimal? EmployeeFixedAmount, decimal? EmployerFixedAmount,
    decimal? WageCeiling, Guid? EmployeeComponentId, Guid? EmployerComponentId, bool IsOptIn,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<Guid>;

public sealed record GetContributionRulesQuery(string? CountryCode) : IRequest<IReadOnlyList<ContributionRuleDto>>;

// ────────────────────────────── Validators ──────────────────────────────
public sealed class CreateTaxRegimeValidator : AbstractValidator<CreateTaxRegimeCommand>
{
    public CreateTaxRegimeValidator()
    {
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TaxYearStartMonth).InclusiveBetween((byte)1, (byte)12);
        RuleFor(x => x.CalcMethod).IsInEnum();
        RuleFor(x => x.Slabs).NotEmpty().When(x => x.CalcMethod != TaxCalcMethod.None)
            .WithMessage("Add at least one tax slab.");
    }
}

public sealed class CreateContributionRuleValidator : AbstractValidator<CreateContributionRuleCommand>
{
    public CreateContributionRuleValidator()
    {
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.BaseType).IsInEnum();
        RuleFor(x => x).Must(x => x.EmployeeComponentId is not null || x.EmployerComponentId is not null)
            .WithMessage("Map the rule to at least one pay component (employee deduction or employer contribution).");
    }
}

// ─────────────────────────────── Handlers ───────────────────────────────
public sealed class TaxRuleHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateTaxRegimeCommand, Guid>,
    IRequestHandler<GetTaxRegimesQuery, IReadOnlyList<TaxRegimeDto>>,
    IRequestHandler<CreateContributionRuleCommand, Guid>,
    IRequestHandler<GetContributionRulesQuery, IReadOnlyList<ContributionRuleDto>>
{
    public async Task<Guid> Handle(CreateTaxRegimeCommand request, CancellationToken ct)
    {
        var regime = TaxRegime.Create(currentUser.RequireTenantId(), request.CountryCode, request.Name,
            request.TaxYearStartMonth, request.CalcMethod, request.EffectiveFrom, request.EffectiveTo);

        foreach (var slab in request.Slabs.OrderBy(s => s.FromAmount))
            regime.AddSlab(slab.FromAmount, slab.ToAmount, slab.FixedAmount, slab.RatePercent);

        db.TaxRegimes.Add(regime);
        await db.SaveChangesAsync(ct);
        return regime.Id;
    }

    public async Task<IReadOnlyList<TaxRegimeDto>> Handle(GetTaxRegimesQuery request, CancellationToken ct)
    {
        var country = request.CountryCode?.Trim().ToUpperInvariant();

        return await db.TaxRegimes.AsNoTracking()   // query filter: tenant ki apni + platform (TenantId NULL)
            .Where(r => country == null || r.CountryCode == country)
            .OrderBy(r => r.CountryCode).ThenByDescending(r => r.EffectiveFrom)
            .Select(r => new TaxRegimeDto(
                r.Id, r.TenantId == null, r.CountryCode, r.Name, r.TaxYearStartMonth, r.CalcMethod,
                r.EffectiveFrom, r.EffectiveTo, r.IsActive,
                r.Slabs.OrderBy(s => s.FromAmount)
                    .Select(s => new TaxSlabDto(s.Id, s.FromAmount, s.ToAmount, s.FixedAmount, s.RatePercent))
                    .ToList()))
            .ToListAsync(ct);
    }

    public async Task<Guid> Handle(CreateContributionRuleCommand request, CancellationToken ct)
    {
        foreach (var id in new[] { request.EmployeeComponentId, request.EmployerComponentId })
        {
            if (id is { } componentId && !await db.PayComponents.AnyAsync(c => c.Id == componentId && c.IsActive, ct))
                throw new NotFoundException("Pay component", componentId);
        }

        var rule = ContributionRule.Create(currentUser.RequireTenantId(), request.CountryCode, request.Code, request.Name,
            request.BaseType, request.EmployeeRatePercent, request.EmployerRatePercent,
            request.EmployeeFixedAmount, request.EmployerFixedAmount, request.WageCeiling, request.IsOptIn,
            request.EffectiveFrom, request.EffectiveTo);
        rule.MapComponents(request.EmployeeComponentId, request.EmployerComponentId);

        db.ContributionRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule.Id;
    }

    public async Task<IReadOnlyList<ContributionRuleDto>> Handle(GetContributionRulesQuery request, CancellationToken ct)
    {
        var country = request.CountryCode?.Trim().ToUpperInvariant();

        return await db.ContributionRules.AsNoTracking()
            .Where(r => country == null || r.CountryCode == country)
            .OrderBy(r => r.CountryCode).ThenBy(r => r.Code)
            .Select(r => new ContributionRuleDto(
                r.Id, r.TenantId == null, r.CountryCode, r.Code, r.Name, r.BaseType,
                r.EmployeeRatePercent, r.EmployerRatePercent, r.EmployeeFixedAmount, r.EmployerFixedAmount,
                r.WageCeiling, r.EmployeeComponentId, r.EmployerComponentId, r.IsOptIn, r.EffectiveFrom, r.EffectiveTo))
            .ToListAsync(ct);
    }
}
