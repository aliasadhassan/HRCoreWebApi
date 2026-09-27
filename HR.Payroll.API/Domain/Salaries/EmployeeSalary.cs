namespace HR.Payroll.API.Domain.Salaries;

using HR.Payroll.API.Domain.Common;

/// <summary>
/// Effective-dated salary. Increment = purani row End() + nayi row. History kabhi overwrite nahi hoti.
/// Template ke upar employee-level overrides (amount badlo / component add karo / template line hatao).
/// </summary>
public sealed class EmployeeSalary : AuditableEntity
{
    private readonly List<EmployeeSalaryComponent> _overrides = new();

    public Guid EmployeeId { get; private set; }
    public Guid SalaryTemplateId { get; private set; }
    public Guid? SalaryGradeId { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public SalaryBasis SalaryBasis { get; private set; }
    public decimal BasisAmount { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public SalaryChangeReason ChangeReason { get; private set; }
    public string? Remarks { get; private set; }

    public IReadOnlyCollection<EmployeeSalaryComponent> Overrides => _overrides.AsReadOnly();

    public bool IsCurrent => EffectiveTo is null;

    private EmployeeSalary() { }

    public static EmployeeSalary Create(
        Guid tenantId, Guid employeeId, Guid salaryTemplateId, Guid? salaryGradeId, string currencyCode,
        SalaryBasis basis, decimal basisAmount, DateOnly effectiveFrom, SalaryChangeReason reason, string? remarks)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            EmployeeId = Guard.NotEmpty(employeeId, "Employee"),
            SalaryTemplateId = Guard.NotEmpty(salaryTemplateId, "Salary template"),
            SalaryGradeId = salaryGradeId,
            CurrencyCode = Guard.Currency(currencyCode),
            SalaryBasis = basis,
            BasisAmount = Guard.Positive(basisAmount, "Salary amount"),
            EffectiveFrom = effectiveFrom,
            ChangeReason = reason,
            Remarks = Guard.Optional(remarks, "Remarks", 500)
        };

    /// <summary>Nayi salary aane pe purani ka aakhri din.</summary>
    public void End(DateOnly lastDay)
    {
        if (!IsCurrent)
            throw new DomainException("This salary record is already closed.");
        if (lastDay < EffectiveFrom)
            throw new DomainException("A new salary cannot start on or before the current one's start date. Use a correction instead.");
        EffectiveTo = lastDay;
    }

    public bool IsEffectiveOn(DateOnly date) => date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);

    public void SetOverride(Guid payComponentId, CalcType calcType, decimal? amount, decimal? percentage, Guid? baseComponentId)
    {
        var formula = ComponentFormula.Create(payComponentId, calcType, amount, percentage, baseComponentId);
        FindOrAdd(payComponentId).Apply(formula);
    }

    /// <summary>Template ki ye line is employee pe lagu nahi (e.g. company car hai to transport allowance nahi).</summary>
    public void ExcludeComponent(Guid payComponentId) => FindOrAdd(payComponentId).Exclude();

    public void RemoveOverride(Guid payComponentId)
    {
        var item = _overrides.FirstOrDefault(o => o.PayComponentId == payComponentId)
                   ?? throw new DomainException("No override exists for this component.");
        _overrides.Remove(item);
    }

    private EmployeeSalaryComponent FindOrAdd(Guid payComponentId)
    {
        var item = _overrides.FirstOrDefault(o => o.PayComponentId == payComponentId);
        if (item is null)
        {
            item = new EmployeeSalaryComponent(Guard.NotEmpty(payComponentId, "Component"));
            _overrides.Add(item);
        }
        return item;
    }
}

public sealed class EmployeeSalaryComponent : Entity
{
    public Guid EmployeeSalaryId { get; private set; }
    public Guid PayComponentId { get; private set; }
    public bool IsExcluded { get; private set; }
    public CalcType? CalcType { get; private set; }
    public decimal? Amount { get; private set; }
    public decimal? Percentage { get; private set; }
    public Guid? BaseComponentId { get; private set; }

    public ComponentFormula? Formula => IsExcluded || CalcType is null
        ? null
        : new ComponentFormula(CalcType.Value, Amount, Percentage, BaseComponentId);

    private EmployeeSalaryComponent() { }

    internal EmployeeSalaryComponent(Guid payComponentId) => PayComponentId = payComponentId;

    internal void Apply(ComponentFormula formula)
    {
        IsExcluded = false;
        CalcType = formula.CalcType;
        Amount = formula.Amount;
        Percentage = formula.Percentage;
        BaseComponentId = formula.BaseComponentId;
    }

    internal void Exclude()
    {
        IsExcluded = true;
        CalcType = null;
        Amount = null;
        Percentage = null;
        BaseComponentId = null;
    }
}
