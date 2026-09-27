namespace HR.Payroll.API.Domain.Tax;

using HR.Payroll.API.Domain.Common;

/// <summary>EOBI (PK), GOSI (KSA), GPSSA (UAE nationals), Provident Fund — sab isi shape mein.</summary>
public sealed class ContributionRule : SharedAuditableEntity
{
    public string CountryCode { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public ContributionBase BaseType { get; private set; }
    public decimal? EmployeeRatePercent { get; private set; }
    public decimal? EmployerRatePercent { get; private set; }
    public decimal? EmployeeFixedAmount { get; private set; }
    public decimal? EmployerFixedAmount { get; private set; }
    public decimal? WageCeiling { get; private set; }
    public Guid? EmployeeComponentId { get; private set; }
    public Guid? EmployerComponentId { get; private set; }
    public bool IsOptIn { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }

    private ContributionRule() { }

    public static ContributionRule Create(
        Guid? tenantId, string countryCode, string code, string name, ContributionBase baseType,
        decimal? employeeRatePercent, decimal? employerRatePercent,
        decimal? employeeFixedAmount, decimal? employerFixedAmount,
        decimal? wageCeiling, bool isOptIn, DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        var usesRates = employeeRatePercent is not null || employerRatePercent is not null;
        var usesFixed = employeeFixedAmount is not null || employerFixedAmount is not null;

        if (baseType == ContributionBase.FixedAmount ? !usesFixed || usesRates : !usesRates || usesFixed)
            throw new DomainException("Use percentages for salary-based rules and fixed amounts for fixed rules.");
        if (employeeRatePercent is < 0 or > 100 || employerRatePercent is < 0 or > 100)
            throw new DomainException("Contribution rates must be between 0 and 100 percent.");
        if (employeeFixedAmount is < 0 || employerFixedAmount is < 0 || wageCeiling is <= 0)
            throw new DomainException("Amounts must be positive.");
        if (effectiveTo is not null && effectiveTo < effectiveFrom)
            throw new DomainException("Effective end date cannot be before the start date.");

        return new ContributionRule
        {
            TenantId = tenantId,
            CountryCode = Guard.CountryCode(countryCode),
            Code = Guard.Required(code, "Code", 30).ToUpperInvariant(),
            Name = Guard.Required(name, "Name", 150),
            BaseType = baseType,
            EmployeeRatePercent = employeeRatePercent,
            EmployerRatePercent = employerRatePercent,
            EmployeeFixedAmount = employeeFixedAmount,
            EmployerFixedAmount = employerFixedAmount,
            WageCeiling = wageCeiling,
            IsOptIn = isOptIn,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo
        };
    }

    /// <summary>Tenant apne payslip components se map karta hai (platform rule ke liye bhi).</summary>
    public void MapComponents(Guid? employeeComponentId, Guid? employerComponentId)
    {
        EmployeeComponentId = employeeComponentId;
        EmployerComponentId = employerComponentId;
    }

    public bool IsEffectiveOn(DateOnly date) => date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);

    /// <summary>(employee se katega, employer dega). Base ceiling se upar ho to ceiling pe.</summary>
    public (decimal Employee, decimal Employer) Calculate(decimal baseAmount, int decimals = 2)
    {
        if (BaseType == ContributionBase.FixedAmount)
            return (EmployeeFixedAmount ?? 0m, EmployerFixedAmount ?? 0m);

        var capped = WageCeiling is { } ceiling ? Math.Min(baseAmount, ceiling) : baseAmount;
        return (Money.Percent(capped, EmployeeRatePercent ?? 0m, decimals),
                Money.Percent(capped, EmployerRatePercent ?? 0m, decimals));
    }
}
