namespace HR.Payroll.API.Domain.Runs;

using HR.Payroll.API.Domain.Common;

public sealed record PayslipSnapshot(
    string EmployeeCode, string EmployeeName, string? DepartmentName, string? DesignationTitle,
    string? BankAccountMasked, string CurrencyCode, DateOnly PeriodStart, DateOnly PeriodEnd);

public sealed record PayslipLineData(
    Guid PayComponentId, string ComponentCode, string ComponentName, ComponentType ComponentType,
    decimal Amount, decimal? Quantity, decimal? Rate, bool IsTaxable, LineSource Source,
    Guid? SourceReference, short SortOrder);

/// <summary>
/// Alag aggregate (run ke andar nahi) — 2000 employees ka run load karna payslips ke saath mehenga hai.
/// Unique (RunId, EmployeeId): retry naya payslip nahi banata, isi ko Recalculate karta hai.
/// Naam/department SNAPSHOT: baad mein employee badle to purani payslip na badle.
/// </summary>
public sealed class Payslip : AuditableEntity
{
    private readonly List<PayslipLine> _lines = new();

    public Guid PayrollRunId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string PayslipNumber { get; private set; } = default!;

    public string EmployeeCode { get; private set; } = default!;
    public string EmployeeName { get; private set; } = default!;
    public string? DepartmentName { get; private set; }
    public string? DesignationTitle { get; private set; }
    public string? BankAccountMasked { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }

    public decimal PeriodDays { get; private set; }
    public decimal PayableDays { get; private set; }
    public decimal UnpaidLeaveDays { get; private set; }

    public decimal GrossEarnings { get; private set; }
    public decimal TotalDeductions { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal NetPay { get; private set; }
    public decimal EmployerContributions { get; private set; }
    public decimal TaxableIncome { get; private set; }

    public PayslipStatus Status { get; private set; }
    public DateTime CalculatedAt { get; private set; }
    public string? HoldReason { get; private set; }

    public IReadOnlyCollection<PayslipLine> Lines => _lines.AsReadOnly();

    private Payslip() { }

    public static Payslip Create(Guid tenantId, Guid payrollRunId, Guid employeeId, string payslipNumber, PayslipSnapshot snapshot)
    {
        if (snapshot.PeriodEnd < snapshot.PeriodStart)
            throw new DomainException("Payslip period is invalid.");

        return new Payslip
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            PayrollRunId = Guard.NotEmpty(payrollRunId, "Payroll run"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            PayslipNumber = Guard.Required(payslipNumber, "Payslip number", 30),
            EmployeeCode = Guard.Required(snapshot.EmployeeCode, "Employee code", 20),
            EmployeeName = Guard.Required(snapshot.EmployeeName, "Employee name", 300),
            DepartmentName = Guard.Optional(snapshot.DepartmentName, "Department", 150),
            DesignationTitle = Guard.Optional(snapshot.DesignationTitle, "Designation", 150),
            BankAccountMasked = Guard.Optional(snapshot.BankAccountMasked, "Bank account", 50),
            CurrencyCode = Guard.Currency(snapshot.CurrencyCode),
            PeriodStart = snapshot.PeriodStart,
            PeriodEnd = snapshot.PeriodEnd,
            Status = PayslipStatus.Calculated,
            CalculatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Engine ka nateeja. Lines POORI replace hoti hain — recalculation idempotent.
    /// Run status check application karti hai (run.CanRecalculate) kyun ke run alag aggregate hai.
    /// </summary>
    public void ApplyCalculation(
        IReadOnlyCollection<PayslipLineData> lines, decimal periodDays, decimal payableDays,
        decimal unpaidLeaveDays, decimal taxableIncome)
    {
        if (Status == PayslipStatus.Paid)
            throw new DomainException("A paid payslip cannot be recalculated.");
        if (periodDays <= 0 || payableDays < 0 || payableDays > periodDays || unpaidLeaveDays < 0)
            throw new DomainException("Payslip day counts are invalid.");
        if (lines.Any(l => l.Amount < 0))
            throw new DomainException("Payslip line amounts cannot be negative; use a deduction component instead.");

        _lines.Clear();
        foreach (var line in lines.OrderBy(l => l.ComponentType).ThenBy(l => l.SortOrder))
            _lines.Add(new PayslipLine(line));

        PeriodDays = periodDays;
        PayableDays = payableDays;
        UnpaidLeaveDays = unpaidLeaveDays;
        TaxableIncome = Guard.NotNegative(taxableIncome, "Taxable income");

        GrossEarnings = Sum(ComponentType.Earning);
        TotalDeductions = Sum(ComponentType.Deduction);
        EmployerContributions = Sum(ComponentType.EmployerContribution);
        TaxAmount = _lines.Where(l => l.Source == LineSource.Tax).Sum(l => l.Amount);
        NetPay = GrossEarnings - TotalDeductions;
        CalculatedAt = DateTime.UtcNow;

        if (NetPay < 0)
        {
            Status = PayslipStatus.OnHold;
            HoldReason = "Deductions exceed earnings.";
        }
        else
        {
            Status = PayslipStatus.Calculated;
            HoldReason = null;
        }
    }

    public void PutOnHold(string reason)
    {
        if (Status == PayslipStatus.Paid)
            throw new DomainException("A paid payslip cannot be put on hold.");
        Status = PayslipStatus.OnHold;
        HoldReason = Guard.Required(reason, "Hold reason", 500);
    }

    public void Release()
    {
        if (Status != PayslipStatus.OnHold)
            throw new DomainException("Only a payslip on hold can be released.");
        if (NetPay < 0)
            throw new DomainException("Net pay is negative; fix the deductions first.");
        Status = PayslipStatus.Calculated;
        HoldReason = null;
    }

    public void MarkPaid()
    {
        if (Status != PayslipStatus.Calculated)
            throw new DomainException("Only a calculated payslip can be marked as paid.");
        Status = PayslipStatus.Paid;
    }

    /// <summary>Is payslip se kaunse loans kate (recalculation pe wapas karne ke liye).</summary>
    public IEnumerable<(Guid LoanId, decimal Amount)> LoanDeductions()
        => _lines.Where(l => l.Source == LineSource.Loan && l.SourceReference is not null)
                 .Select(l => (l.SourceReference!.Value, l.Amount));

    private decimal Sum(ComponentType type) => _lines.Where(l => l.ComponentType == type).Sum(l => l.Amount);
}

public sealed class PayslipLine : TenantChildEntity
{
    public Guid PayslipId { get; private set; }
    public Guid PayComponentId { get; private set; }
    public string ComponentCode { get; private set; } = default!;
    public string ComponentName { get; private set; } = default!;
    public ComponentType ComponentType { get; private set; }
    public decimal Amount { get; private set; }
    public decimal? Quantity { get; private set; }
    public decimal? Rate { get; private set; }
    public bool IsTaxable { get; private set; }
    public LineSource Source { get; private set; }
    public Guid? SourceReference { get; private set; }
    public short SortOrder { get; private set; }

    private PayslipLine() { }

    internal PayslipLine(PayslipLineData data)
    {
        PayComponentId = data.PayComponentId;
        ComponentCode = data.ComponentCode;
        ComponentName = data.ComponentName;
        ComponentType = data.ComponentType;
        Amount = data.Amount;
        Quantity = data.Quantity;
        Rate = data.Rate;
        IsTaxable = data.IsTaxable;
        Source = data.Source;
        SourceReference = data.SourceReference;
        SortOrder = data.SortOrder;
    }
}
