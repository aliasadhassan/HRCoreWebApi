namespace HR.Payroll.API.Domain.Salaries;

using HR.Payroll.API.Domain.Common;

public sealed class SalaryGrade : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string CurrencyCode { get; private set; } = default!;
    public decimal? MinAnnual { get; private set; }
    public decimal? MaxAnnual { get; private set; }
    public bool IsActive { get; private set; } = true;

    private SalaryGrade() { }

    public static SalaryGrade Create(Guid tenantId, string code, string name, string currencyCode, decimal? minAnnual, decimal? maxAnnual)
    {
        var grade = new SalaryGrade { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        grade.Update(code, name, currencyCode, minAnnual, maxAnnual);
        return grade;
    }

    public void Update(string code, string name, string currencyCode, decimal? minAnnual, decimal? maxAnnual)
    {
        if (minAnnual is < 0 || maxAnnual is < 0)
            throw new DomainException("Salary band cannot be negative.");
        if (minAnnual is not null && maxAnnual is not null && maxAnnual < minAnnual)
            throw new DomainException("Band maximum must be greater than the minimum.");

        Code = Guard.Required(code, "Code", 20).ToUpperInvariant();
        Name = Guard.Required(name, "Name", 100);
        CurrencyCode = Guard.Currency(currencyCode);
        MinAnnual = minAnnual;
        MaxAnnual = maxAnnual;
    }

    /// <summary>Band se bahar salary block nahi karte — HR ko warning dikhate hain.</summary>
    public bool IsWithinBand(decimal annualAmount)
        => (MinAnnual is null || annualAmount >= MinAnnual) && (MaxAnnual is null || annualAmount <= MaxAnnual);

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
