namespace HR.Payroll.API.Domain.Tax;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Ek mulk ka ek tax saal ka rule set. Naya mulk ya naya budget = nayi regime row + slabs, code nahi.
/// TenantId NULL = platform regime (hum maintain karte hain).
/// </summary>
public sealed class TaxRegime : SharedAuditableEntity
{
    private readonly List<TaxSlab> _slabs = new();

    public string CountryCode { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public byte TaxYearStartMonth { get; private set; }
    public TaxCalcMethod CalcMethod { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<TaxSlab> Slabs => _slabs.AsReadOnly();

    private TaxRegime() { }

    public static TaxRegime Create(
        Guid? tenantId, string countryCode, string name, byte taxYearStartMonth,
        TaxCalcMethod calcMethod, DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        if (taxYearStartMonth is < 1 or > 12)
            throw new DomainException("Tax year start month must be between 1 and 12.");
        if (effectiveTo is not null && effectiveTo < effectiveFrom)
            throw new DomainException("Effective end date cannot be before the start date.");

        return new TaxRegime
        {
            TenantId = tenantId,
            CountryCode = Guard.CountryCode(countryCode),
            Name = Guard.Required(name, "Name", 150),
            TaxYearStartMonth = taxYearStartMonth,
            CalcMethod = calcMethod,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo
        };
    }

    public void Deactivate() => IsActive = false;

    public bool IsEffectiveOn(DateOnly date) => IsActive && date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);

    /// <summary>Slabs overlap nahi karne chahiye. Tax = Fixed + (Income − From) × Rate%.</summary>
    public TaxSlab AddSlab(decimal fromAmount, decimal? toAmount, decimal fixedAmount, decimal ratePercent)
    {
        Guard.NotNegative(fromAmount, "From amount");
        Guard.NotNegative(fixedAmount, "Fixed amount");
        if (ratePercent is < 0 or > 100)
            throw new DomainException("Tax rate must be between 0 and 100 percent.");
        if (toAmount is not null && toAmount <= fromAmount)
            throw new DomainException("Slab upper limit must be greater than the lower limit.");

        var overlaps = _slabs.Any(s =>
            fromAmount < (s.ToAmount ?? decimal.MaxValue) && (toAmount ?? decimal.MaxValue) > s.FromAmount);
        if (overlaps)
            throw new DomainException("This slab overlaps an existing slab.");

        var slab = new TaxSlab(fromAmount, toAmount, fixedAmount, ratePercent);
        _slabs.Add(slab);
        return slab;
    }

    public void RemoveSlab(Guid slabId)
    {
        var slab = _slabs.FirstOrDefault(s => s.Id == slabId) ?? throw new DomainException("Slab not found.");
        _slabs.Remove(slab);
    }

    /// <summary>Saal bhar ki taxable income pe saal bhar ka tax.</summary>
    public decimal CalculateAnnualTax(decimal annualTaxableIncome, int decimals = 2)
    {
        if (CalcMethod == TaxCalcMethod.None || annualTaxableIncome <= 0)
            return 0m;

        var slab = _slabs
            .Where(s => annualTaxableIncome > s.FromAmount && (s.ToAmount is null || annualTaxableIncome <= s.ToAmount))
            .OrderByDescending(s => s.FromAmount)
            .FirstOrDefault();

        return slab is null
            ? 0m
            : Money.Round(slab.FixedAmount + (annualTaxableIncome - slab.FromAmount) * slab.RatePercent / 100m, decimals);
    }

    /// <summary>Date jis tax saal mein aati hai uska pehla din (PK: 1 July).</summary>
    public DateOnly TaxYearStart(DateOnly date)
    {
        var year = date.Month >= TaxYearStartMonth ? date.Year : date.Year - 1;
        return new DateOnly(year, TaxYearStartMonth, 1);
    }
}

public sealed class TaxSlab : Entity
{
    public Guid TaxRegimeId { get; private set; }
    public decimal FromAmount { get; private set; }
    public decimal? ToAmount { get; private set; }
    public decimal FixedAmount { get; private set; }
    public decimal RatePercent { get; private set; }

    private TaxSlab() { }

    internal TaxSlab(decimal fromAmount, decimal? toAmount, decimal fixedAmount, decimal ratePercent)
    {
        FromAmount = fromAmount;
        ToAmount = toAmount;
        FixedAmount = fixedAmount;
        RatePercent = ratePercent;
    }
}
