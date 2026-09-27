namespace HR.Payroll.API.Domain.Setup;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Employees ka group jo ek hi frequency, currency aur mulk pe pay hota hai.
/// Run hamesha PayGroup + PayPeriod ke liye chalta hai.
/// </summary>
public sealed class PayGroup : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public PayFrequency PayFrequency { get; private set; }
    public string CountryCode { get; private set; } = default!;
    public string CurrencyCode { get; private set; } = default!;
    public DateOnly AnchorDate { get; private set; }       // weekly/bi-weekly cycle ka pehla din
    public short PayDayOffset { get; private set; }        // PeriodEnd + N = PayDate
    public bool IsActive { get; private set; } = true;

    private PayGroup() { }

    public static PayGroup Create(
        Guid tenantId, string name, string code, PayFrequency frequency,
        string countryCode, string currencyCode, DateOnly anchorDate, short payDayOffset)
    {
        var group = new PayGroup
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            PayFrequency = frequency,
            CountryCode = Guard.CountryCode(countryCode),
            CurrencyCode = Guard.Currency(currencyCode)
        };
        group.SetAnchor(anchorDate);
        group.Update(name, code, payDayOffset);
        return group;
    }

    /// <summary>Frequency, mulk aur currency create ke baad nahi badalte — periods aur payslips unpe tike hain.</summary>
    public void Update(string name, string code, short payDayOffset)
    {
        if (payDayOffset is < 0 or > 31)
            throw new DomainException("Pay day offset must be between 0 and 31 days.");

        Name = Guard.Required(name, "Name", 100);
        Code = Guard.Required(code, "Code", 20).ToUpperInvariant();
        PayDayOffset = payDayOffset;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    private void SetAnchor(DateOnly anchorDate)
    {
        // Monthly / semi-monthly hamesha mahine ki 1 tareekh se chalte hain
        AnchorDate = PayFrequency is PayFrequency.Monthly or PayFrequency.SemiMonthly
            ? new DateOnly(anchorDate.Year, anchorDate.Month, 1)
            : anchorDate;
    }
}
