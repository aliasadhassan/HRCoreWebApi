namespace HR.Payroll.API.Domain.Rewards;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Company ka benefit (medical insurance, life cover, gym...). Kharcha mahana: company + employee ka hissa + har dependent.
/// DeductionComponentId ho to employee ka hissa har payroll run mein khud kat jata hai (loan installment jaisa).
/// </summary>
public sealed class BenefitPlan : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public BenefitType BenefitType { get; private set; }
    public string? Provider { get; private set; }
    public string? Description { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public decimal EmployerMonthlyCost { get; private set; }
    public decimal EmployeeMonthlyCost { get; private set; }
    public decimal DependentMonthlyCost { get; private set; }
    public short MaxDependents { get; private set; }
    public Guid? DeductionComponentId { get; private set; }
    public bool OpenForRequests { get; private set; }
    public bool IsActive { get; private set; } = true;
    public short SortOrder { get; private set; }

    private BenefitPlan() { }

    public static BenefitPlan Create(Guid tenantId, BenefitPlanData d)
    {
        var plan = new BenefitPlan { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        plan.Update(d);
        return plan;
    }

    public void Update(BenefitPlanData d)
    {
        Name = Guard.Required(d.Name, "Name", 100);
        BenefitType = Enum.IsDefined(d.BenefitType) ? d.BenefitType : throw new DomainException("Pick a benefit type.");
        Provider = Guard.Optional(d.Provider, "Provider", 150);
        Description = Guard.Optional(d.Description, "Description", 1000);
        CurrencyCode = Guard.Currency(d.CurrencyCode);
        EmployerMonthlyCost = Guard.NotNegative(d.EmployerMonthlyCost, "Company cost");
        EmployeeMonthlyCost = Guard.NotNegative(d.EmployeeMonthlyCost, "Employee contribution");
        DependentMonthlyCost = Guard.NotNegative(d.DependentMonthlyCost, "Cost per dependent");
        if (d.MaxDependents is < 0 or > 10)
            throw new DomainException("Dependents must be between 0 and 10.");
        MaxDependents = d.MaxDependents;
        if (MaxDependents == 0 && DependentMonthlyCost > 0)
            throw new DomainException("Set the number of dependents allowed, or make the cost per dependent zero.");
        if (EmployeeMonthlyCost == 0 && d.DeductionComponentId is not null)
            throw new DomainException("There is no employee contribution to deduct. Clear the deduction component.");
        DeductionComponentId = d.DeductionComponentId;
        OpenForRequests = d.OpenForRequests;
        IsActive = d.IsActive;
        if (d.SortOrder is < 0 or > 999)
            throw new DomainException("Order must be between 0 and 999.");
        SortOrder = d.SortOrder;
    }

    /// <summary>Company ka mahana kharcha ek enrolment pe.</summary>
    public decimal EmployerCostFor(short dependents) => EmployerMonthlyCost + DependentMonthlyCost * dependents;
}

public sealed record BenefitPlanData(
    string Name, BenefitType BenefitType, string? Provider, string? Description, string CurrencyCode,
    decimal EmployerMonthlyCost, decimal EmployeeMonthlyCost, decimal DependentMonthlyCost, short MaxDependents,
    Guid? DeductionComponentId, bool OpenForRequests, bool IsActive, short SortOrder);
