namespace HR.Payroll.API.Domain.Inputs;

using HR.Payroll.API.Domain.Common;

/// <summary>Is period ka variable amount: overtime, bonus, commission, manual deduction, hourly hours.</summary>
public sealed class PayrollInput : AuditableEntity
{
    public Guid PayPeriodId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid PayComponentId { get; private set; }
    public decimal? Amount { get; private set; }
    public decimal? Quantity { get; private set; }
    public InputSource Source { get; private set; }
    public string SourceReference { get; private set; } = string.Empty;   // import batch id — duplicate import rokta hai
    public string? Remarks { get; private set; }

    private PayrollInput() { }

    public static PayrollInput Create(
        Guid tenantId, Guid payPeriodId, Guid employeeId, Guid payComponentId,
        decimal? amount, decimal? quantity, InputSource source, string? sourceReference, string? remarks)
    {
        var input = new PayrollInput
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            PayPeriodId = Guard.NotEmpty(payPeriodId, "Pay period"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            PayComponentId = Guard.NotEmpty(payComponentId, "Component"),
            Source = source,
            SourceReference = Guard.Optional(sourceReference, "Source reference", 100) ?? string.Empty
        };
        input.Update(amount, quantity, remarks);
        return input;
    }

    public void Update(decimal? amount, decimal? quantity, string? remarks)
    {
        if (amount is null && quantity is null)
            throw new DomainException("Enter an amount or a quantity.");
        if (amount is < 0 || quantity is < 0)
            throw new DomainException("Amount and quantity cannot be negative.");
        if (quantity is > 744)
            throw new DomainException("Quantity looks wrong: a month has at most 744 hours.");

        Amount = amount;
        Quantity = quantity;
        Remarks = Guard.Optional(remarks, "Remarks", 500);
    }
}
