namespace HR.Payroll.API.Domain.Runs;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Status machine:
///   Draft → Processing → Calculated → Approved → Paid
///                ↓            ↓
///              Failed     (recalculate) → Processing
///   Draft / Calculated / Failed → Cancelled
/// Approved ke baad koi recalculation nahi — yahi "calculation ≠ payment" wali deewar hai.
/// Ek period ka ek regular run: DB unique index (UX_Runs_RegularPerPeriod).
/// </summary>
public sealed class PayrollRun : AuditableEntity
{
    public Guid PayGroupId { get; private set; }
    public Guid PayPeriodId { get; private set; }
    public RunType RunType { get; private set; }
    public RunStatus Status { get; private set; }
    public string CurrencyCode { get; private set; } = default!;

    public int TotalEmployees { get; private set; }
    public int ProcessedEmployees { get; private set; }
    public decimal TotalGross { get; private set; }
    public decimal TotalDeductions { get; private set; }
    public decimal TotalNet { get; private set; }
    public decimal TotalEmployerCost { get; private set; }

    public DateTime? StartedAt { get; private set; }
    public DateTime? CalculatedAt { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public string? FailureReason { get; private set; }

    /// <summary>Payslips dobara calculate ho sakti hain?</summary>
    public bool CanRecalculate => Status is RunStatus.Draft or RunStatus.Calculated or RunStatus.Failed;

    private PayrollRun() { }

    public static PayrollRun Create(Guid tenantId, Guid payGroupId, Guid payPeriodId, RunType runType, string currencyCode)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            PayGroupId = Guard.NotEmpty(payGroupId, "Pay group"),
            PayPeriodId = Guard.NotEmpty(payPeriodId, "Pay period"),
            RunType = runType,
            Status = RunStatus.Draft,
            CurrencyCode = Guard.Currency(currencyCode)
        };

    /// <summary>API isay call karke 202 deti hai; worker queue se kaam uthata hai.</summary>
    public void BeginProcessing(int totalEmployees)
    {
        if (!CanRecalculate)
            throw new DomainException($"A run in '{Status}' status cannot be processed.");
        if (totalEmployees < 0)
            throw new DomainException("Employee count cannot be negative.");

        Status = RunStatus.Processing;
        TotalEmployees = totalEmployees;
        ProcessedEmployees = 0;
        FailureReason = null;
        StartedAt = DateTime.UtcNow;
        Raise(new PayrollRunStartedDomainEvent(this));
    }

    /// <summary>Progress bar ke liye (polling / SignalR).</summary>
    public void RecordProgress(int processedEmployees)
    {
        EnsureStatus(RunStatus.Processing);
        ProcessedEmployees = Math.Clamp(processedEmployees, 0, TotalEmployees);
    }

    public void CompleteCalculation(decimal totalGross, decimal totalDeductions, decimal totalNet, decimal totalEmployerCost)
    {
        EnsureStatus(RunStatus.Processing);
        if (totalNet != totalGross - totalDeductions)
            throw new DomainException("Run totals do not balance.");

        TotalGross = totalGross;
        TotalDeductions = totalDeductions;
        TotalNet = totalNet;
        TotalEmployerCost = totalEmployerCost;
        ProcessedEmployees = TotalEmployees;
        CalculatedAt = DateTime.UtcNow;
        Status = RunStatus.Calculated;
        Raise(new PayrollRunCalculatedDomainEvent(this));
    }

    public void Fail(string reason)
    {
        EnsureStatus(RunStatus.Processing);
        FailureReason = Guard.Required(reason, "Failure reason", 1000);
        Status = RunStatus.Failed;
    }

    public void Approve(Guid approvedBy)
    {
        EnsureStatus(RunStatus.Calculated);
        ApprovedBy = Guard.NotEmpty(approvedBy, "Approver");
        ApprovedAt = DateTime.UtcNow;
        Status = RunStatus.Approved;
        Raise(new PayrollRunApprovedDomainEvent(this));   // handler: PayPeriod.Lock(), payslips notify
    }

    public void MarkPaid(DateTime paidAt)
    {
        EnsureStatus(RunStatus.Approved);
        PaidAt = paidAt;
        Status = RunStatus.Paid;
        Raise(new PayrollRunPaidDomainEvent(this));
    }

    public void Cancel()
    {
        if (!CanRecalculate)
            throw new DomainException($"A run in '{Status}' status cannot be cancelled.");
        Status = RunStatus.Cancelled;
    }

    private void EnsureStatus(RunStatus expected)
    {
        if (Status != expected)
            throw new DomainException($"This action needs the run to be '{expected}', but it is '{Status}'.");
    }
}
