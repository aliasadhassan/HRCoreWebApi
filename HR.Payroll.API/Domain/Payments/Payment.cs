namespace HR.Payroll.API.Domain.Payments;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Ek payslip ka ek hi LIVE payment (Pending/Paid) — DB unique index.
/// Failed ya Reversed ke baad naya payment ban sakta hai (e.g. ghalat account number).
/// </summary>
public sealed class Payment : AuditableEntity
{
    public Guid PayslipId { get; private set; }
    public Guid? PaymentBatchId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public PaymentStatus Status { get; private set; }
    public string? Reference { get; private set; }
    public DateTime? PaidAt { get; private set; }

    private Payment() { }

    public static Payment Create(Guid tenantId, Guid payslipId, PaymentMethod method, decimal amount, string currencyCode, Guid? paymentBatchId)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            PayslipId = Guard.NotEmpty(payslipId, "Payslip"),
            PaymentBatchId = paymentBatchId,
            Method = method,
            Amount = Guard.Positive(amount, "Amount"),
            CurrencyCode = Guard.Currency(currencyCode),
            Status = PaymentStatus.Pending
        };

    public void MarkPaid(string? reference, DateTime paidAt)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("Only a pending payment can be marked as paid.");
        Reference = Guard.Optional(reference, "Reference", 100);
        PaidAt = paidAt;
        Status = PaymentStatus.Paid;
    }

    public void MarkFailed(string? reason)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("Only a pending payment can fail.");
        Reference = Guard.Optional(reason, "Reason", 100);
        Status = PaymentStatus.Failed;
    }

    public void Reverse(string reference)
    {
        if (Status != PaymentStatus.Paid)
            throw new DomainException("Only a paid payment can be reversed.");
        Reference = Guard.Required(reference, "Reversal reference", 100);
        Status = PaymentStatus.Reversed;
    }
}
