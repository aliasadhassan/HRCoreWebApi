namespace HR.Payroll.API.Domain.Setup;

using HR.Payroll.API.Domain.Common;

/// <summary>Engine in codes se apne built-in components pehchanta hai.</summary>
public static class SystemComponentCodes
{
    public const string Basic = "BASIC";
    public const string IncomeTax = "INCOME_TAX";
}

/// <summary>Salary ka building block: BASIC, HRA, OT, BONUS, LOAN, EOBI_EE...</summary>
public sealed class PayComponent : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? SystemCode { get; private set; }
    public ComponentType ComponentType { get; private set; }
    public CalcType DefaultCalcType { get; private set; }
    public Guid? DefaultBaseComponentId { get; private set; }
    public bool IsTaxable { get; private set; } = true;
    public bool IsProrated { get; private set; } = true;
    public bool IsRecurring { get; private set; } = true;
    public bool ShowOnPayslip { get; private set; } = true;
    public short SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public bool IsSystem => SystemCode is not null;

    private PayComponent() { }

    public static PayComponent Create(
        Guid tenantId, string code, string name, ComponentType type, CalcType defaultCalcType,
        Guid? defaultBaseComponentId, bool isTaxable, bool isProrated, bool isRecurring, bool showOnPayslip,
        short sortOrder, string? systemCode = null)
    {
        var component = new PayComponent
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            SystemCode = Guard.Optional(systemCode, "System code", 30)?.ToUpperInvariant()
        };
        component.Update(code, name, type, defaultCalcType, defaultBaseComponentId, isTaxable, isProrated, isRecurring, showOnPayslip, sortOrder);
        return component;
    }

    public void Update(
        string code, string name, ComponentType type, CalcType defaultCalcType,
        Guid? defaultBaseComponentId, bool isTaxable, bool isProrated, bool isRecurring, bool showOnPayslip, short sortOrder)
    {
        if (IsSystem && Code is not null && !string.Equals(code?.Trim(), Code, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("The code of a system component cannot be changed.");
        if (defaultCalcType == CalcType.PercentOfComponent && defaultBaseComponentId is null)
            throw new DomainException("A percentage component needs a base component.");
        if (defaultBaseComponentId is not null && defaultBaseComponentId == Id && Id != Guid.Empty)
            throw new DomainException("A component cannot be a percentage of itself.");
        if (type == ComponentType.Deduction && isTaxable)
            throw new DomainException("A deduction cannot be marked as taxable income.");

        Code = Guard.Required(code, "Code", 30).ToUpperInvariant();
        Name = Guard.Required(name, "Name", 100);
        ComponentType = type;
        DefaultCalcType = defaultCalcType;
        DefaultBaseComponentId = defaultCalcType == CalcType.PercentOfComponent ? defaultBaseComponentId : null;
        IsTaxable = isTaxable;
        IsProrated = isProrated;
        IsRecurring = isRecurring;
        ShowOnPayslip = showOnPayslip;
        SortOrder = sortOrder;
    }

    public void Activate() => IsActive = true;

    public void Deactivate()
    {
        if (IsSystem)
            throw new DomainException("System components cannot be deactivated.");
        IsActive = false;
    }
}
