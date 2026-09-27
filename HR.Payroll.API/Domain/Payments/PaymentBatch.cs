namespace HR.Payroll.API.Domain.Payments;

using HR.Payroll.API.Domain.Common;

/// <summary>Bank file. Dobara generate ho to wahi payments — naye nahi (payments payslip se unique hain).</summary>
public sealed class PaymentBatch : AuditableEntity
{
    public Guid PayrollRunId { get; private set; }
    public PaymentFileFormat FileFormat { get; private set; }
    public string? FileStorageKey { get; private set; }
    public decimal TotalAmount { get; private set; }
    public int PaymentCount { get; private set; }
    public DateTime GeneratedAt { get; private set; }

    private PaymentBatch() { }

    public static PaymentBatch Create(Guid tenantId, Guid payrollRunId, PaymentFileFormat format, decimal totalAmount, int paymentCount)
    {
        if (paymentCount < 1)
            throw new DomainException("A payment batch needs at least one payment.");

        return new PaymentBatch
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            PayrollRunId = Guard.NotEmpty(payrollRunId, "Payroll run"),
            FileFormat = format,
            TotalAmount = Guard.Positive(totalAmount, "Total amount"),
            PaymentCount = paymentCount,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public void AttachFile(string storageKey) => FileStorageKey = Guard.Required(storageKey, "Storage key", 500);
}
