namespace HR.Payroll.API.Domain.Runs;

using HR.Payroll.API.Domain.Common;

/// <summary>Unique (LoanId, PayslipId): ek installment ek payslip se ek hi dafa.</summary>
public sealed class LoanRepayment : AuditableEntity
{
    public Guid EmployeeLoanId { get; private set; }
    public Guid PayslipId { get; private set; }
    public decimal Amount { get; private set; }

    private LoanRepayment() { }

    public static LoanRepayment Create(Guid tenantId, Guid employeeLoanId, Guid payslipId, decimal amount)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeLoanId = Guard.NotEmpty(employeeLoanId, "Loan"),
            PayslipId = Guard.NotEmpty(payslipId, "Payslip"),
            Amount = Guard.Positive(amount, "Repayment")
        };
}
