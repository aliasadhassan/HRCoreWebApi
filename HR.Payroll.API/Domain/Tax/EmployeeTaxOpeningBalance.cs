namespace HR.Payroll.API.Domain.Tax;

using HR.Payroll.API.Domain.Common;

/// <summary>Saal ke beech join: pichhle employer ki income aur kata hua tax (certificate se).</summary>
public sealed class EmployeeTaxOpeningBalance : AuditableEntity
{
    public Guid EmployeeId { get; private set; }
    public DateOnly TaxYearStart { get; private set; }
    public decimal PriorTaxableIncome { get; private set; }
    public decimal PriorTaxPaid { get; private set; }

    private EmployeeTaxOpeningBalance() { }

    public static EmployeeTaxOpeningBalance Create(Guid tenantId, Guid employeeId, DateOnly taxYearStart, decimal priorTaxableIncome, decimal priorTaxPaid)
    {
        var balance = new EmployeeTaxOpeningBalance
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            TaxYearStart = taxYearStart
        };
        balance.Update(priorTaxableIncome, priorTaxPaid);
        return balance;
    }

    public void Update(decimal priorTaxableIncome, decimal priorTaxPaid)
    {
        PriorTaxableIncome = Guard.NotNegative(priorTaxableIncome, "Prior taxable income");
        PriorTaxPaid = Guard.NotNegative(priorTaxPaid, "Prior tax paid");
    }
}
