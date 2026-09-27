namespace HR.Payroll.API.Domain.Setup;

using HR.Payroll.API.Domain.Common;

/// <summary>Har tenant ki ek row. TenantCreated event pe default ban jati hai.</summary>
public sealed class PayrollSettings : AuditableEntity
{
    public string BaseCurrency { get; private set; } = default!;
    public ProrationMethod ProrationMethod { get; private set; } = ProrationMethod.CalendarDays;
    public byte RoundingDecimals { get; private set; } = 2;
    public string PayslipNumberPrefix { get; private set; } = "PS";
    public bool RequireApproval { get; private set; } = true;

    private PayrollSettings() { }

    public static PayrollSettings CreateDefault(Guid tenantId, string baseCurrency)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            BaseCurrency = Guard.Currency(baseCurrency, "Base currency")
        };

    public void Update(string baseCurrency, ProrationMethod prorationMethod, byte roundingDecimals, string payslipNumberPrefix, bool requireApproval)
    {
        if (roundingDecimals > 4)
            throw new DomainException("Rounding decimals must be between 0 and 4.");

        BaseCurrency = Guard.Currency(baseCurrency, "Base currency");
        ProrationMethod = prorationMethod;
        RoundingDecimals = roundingDecimals;
        PayslipNumberPrefix = Guard.Required(payslipNumberPrefix, "Payslip prefix", 10).ToUpperInvariant();
        RequireApproval = requireApproval;
    }
}
