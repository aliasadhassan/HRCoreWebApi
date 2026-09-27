namespace HR.Payroll.API.Domain.Employees;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Employee API ka LOCAL copy. Id = Employee API wala EmployeeId (EF mein ValueGeneratedNever).
/// Naam/department events se aate hain; bank + tax identity sirf Payroll ki apni.
/// </summary>
public sealed class PayrollEmployee : AuditableEntity
{
    public string EmployeeCode { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string WorkEmail { get; private set; } = default!;
    public Guid DepartmentId { get; private set; }
    public string? DepartmentName { get; private set; }
    public string? DesignationTitle { get; private set; }
    public Guid LocationId { get; private set; }
    public string EmploymentType { get; private set; } = default!;   // "FullTime", "Contract"... (Employee API se, yahan logic nahi)
    public DateOnly JoiningDate { get; private set; }
    public DateOnly? ExitDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    public Guid? PayGroupId { get; private set; }
    public bool IsTaxExempt { get; private set; }

    // Application layer encrypt karke deti hai
    public string? BankName { get; private set; }
    public string? BankAccountTitle { get; private set; }
    public string? BankAccountNumber { get; private set; }
    public string? Iban { get; private set; }
    public string? TaxIdentifier { get; private set; }

    public DateTime LastSyncedAt { get; private set; }

    private PayrollEmployee() { }

    // ─────────────────────── Sync (Employee API events) ───────────────────────
    public static PayrollEmployee CreateFromSync(
        Guid employeeId, Guid tenantId, string employeeCode, string fullName, string workEmail,
        Guid departmentId, Guid locationId, string employmentType, DateOnly joiningDate, DateTime occurredAt)
    {
        var employee = new PayrollEmployee
        {
            Id = Guard.NotEmpty(employeeId, "Employee"),
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            JoiningDate = joiningDate,
            LastSyncedAt = occurredAt
        };
        employee.ApplyIdentity(employeeCode, fullName, workEmail);
        employee.DepartmentId = Guard.NotEmpty(departmentId, "Department");
        employee.LocationId = Guard.NotEmpty(locationId, "Location");
        employee.EmploymentType = Guard.Required(employmentType, "Employment type", 50);
        return employee;
    }

    /// <summary>Events out-of-order aa sakte hain — purana event naye data ko overwrite na kare.</summary>
    public bool IsStale(DateTime occurredAt) => occurredAt < LastSyncedAt;

    public void SyncProfile(string employeeCode, string fullName, string workEmail, DateTime occurredAt)
    {
        if (IsStale(occurredAt)) return;
        ApplyIdentity(employeeCode, fullName, workEmail);
        LastSyncedAt = occurredAt;
    }

    public void SyncJob(Guid departmentId, string? departmentName, string? designationTitle, Guid locationId, string employmentType, DateTime occurredAt)
    {
        if (IsStale(occurredAt)) return;
        DepartmentId = Guard.NotEmpty(departmentId, "Department");
        DepartmentName = Guard.Optional(departmentName, "Department", 150);
        DesignationTitle = Guard.Optional(designationTitle, "Designation", 150);
        LocationId = Guard.NotEmpty(locationId, "Location");
        EmploymentType = Guard.Required(employmentType, "Employment type", 50);
        LastSyncedAt = occurredAt;
    }

    public void SyncExit(DateOnly exitDate, DateTime occurredAt)
    {
        if (IsStale(occurredAt)) return;
        ExitDate = exitDate;
        IsActive = false;
        LastSyncedAt = occurredAt;
    }

    // ─────────────────────────── Payroll-owned data ───────────────────────────
    public void AssignPayGroup(Guid? payGroupId) => PayGroupId = payGroupId;

    public void SetTaxExempt(bool value) => IsTaxExempt = value;

    public void SetBankDetails(string? bankName, string? accountTitle, string? encryptedAccountNumber, string? encryptedIban)
    {
        BankName = Guard.Optional(bankName, "Bank name", 100);
        BankAccountTitle = Guard.Optional(accountTitle, "Account title", 150);
        BankAccountNumber = Guard.Optional(encryptedAccountNumber, "Account number", 256);
        Iban = Guard.Optional(encryptedIban, "IBAN", 256);
    }

    public void SetTaxIdentifier(string? encryptedValue) => TaxIdentifier = Guard.Optional(encryptedValue, "Tax identifier", 256);

    /// <summary>Kya ye employee is period mein kisi bhi din kaam pe tha?</summary>
    public bool IsEmployedDuring(DateOnly periodStart, DateOnly periodEnd)
        => JoiningDate <= periodEnd && (ExitDate is null || ExitDate >= periodStart);

    private void ApplyIdentity(string employeeCode, string fullName, string workEmail)
    {
        EmployeeCode = Guard.Required(employeeCode, "Employee code", 20);
        FullName = Guard.Required(fullName, "Full name", 300);
        WorkEmail = Guard.Required(workEmail, "Work email", 256);
    }
}
