namespace HR.Employee.API.Domain.Leaves;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;

/// <summary>Location-wise policy. LocationId null = tenant default (jis location ki apni policy na ho).</summary>
public sealed class LeavePolicy : AuditableEntity
{
    private readonly List<LeavePolicyRule> _rules = new();

    public string Name { get; private set; } = default!;
    public Guid? LocationId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<LeavePolicyRule> Rules => _rules.AsReadOnly();

    private LeavePolicy() { }

    public static LeavePolicy Create(Guid tenantId, string name, Guid? locationId, DateOnly effectiveFrom)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Name = Guard.Required(name, "Name", 150),
            LocationId = locationId,
            EffectiveFrom = effectiveFrom
        };

    public void Rename(string name) => Name = Guard.Required(name, "Name", 150);
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    /// <summary>Rule add karo ya existing update karo (ek leave type = ek rule).</summary>
    public LeavePolicyRule SetRule(
        Guid leaveTypeId, decimal annualEntitlement, AccrualMethod accrualMethod,
        decimal maxCarryForward, byte? carryForwardExpiryMonths, short minServiceDays,
        short? maxConsecutiveDays, Gender? applicableGender, EmploymentType? applicableEmploymentTypes)
    {
        var rule = _rules.FirstOrDefault(r => r.LeaveTypeId == leaveTypeId);
        if (rule is null)
        {
            rule = new LeavePolicyRule(Guard.NotEmpty(leaveTypeId, "Leave type"));
            _rules.Add(rule);
        }

        rule.Update(annualEntitlement, accrualMethod, maxCarryForward, carryForwardExpiryMonths,
                    minServiceDays, maxConsecutiveDays, applicableGender, applicableEmploymentTypes);
        return rule;
    }

    public void RemoveRule(Guid leaveTypeId)
    {
        var rule = _rules.FirstOrDefault(r => r.LeaveTypeId == leaveTypeId)
                   ?? throw new DomainException("This policy has no rule for the selected leave type.");
        _rules.Remove(rule);
    }

    public LeavePolicyRule? RuleFor(Guid leaveTypeId) => _rules.FirstOrDefault(r => r.LeaveTypeId == leaveTypeId);
}

public sealed class LeavePolicyRule : Entity
{
    public Guid LeavePolicyId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public decimal AnnualEntitlement { get; private set; }
    public AccrualMethod AccrualMethod { get; private set; }
    public decimal MaxCarryForward { get; private set; }
    public byte? CarryForwardExpiryMonths { get; private set; }
    public short MinServiceDays { get; private set; }
    public short? MaxConsecutiveDays { get; private set; }
    public Gender? ApplicableGender { get; private set; }                    // maternity / paternity
    public EmploymentType? ApplicableEmploymentTypes { get; private set; }   // null = sab

    private LeavePolicyRule() { }

    internal LeavePolicyRule(Guid leaveTypeId) => LeaveTypeId = leaveTypeId;

    internal void Update(
        decimal annualEntitlement, AccrualMethod accrualMethod, decimal maxCarryForward,
        byte? carryForwardExpiryMonths, short minServiceDays, short? maxConsecutiveDays,
        Gender? applicableGender, EmploymentType? applicableEmploymentTypes)
    {
        if (annualEntitlement is < 0 or > 365)
            throw new DomainException("Annual entitlement must be between 0 and 365 days.");
        if (maxCarryForward is < 0 or > 365)
            throw new DomainException("Carry forward must be between 0 and 365 days.");
        if (minServiceDays < 0)
            throw new DomainException("Minimum service days cannot be negative.");
        if (maxConsecutiveDays is <= 0)
            throw new DomainException("Maximum consecutive days must be greater than zero.");

        AnnualEntitlement = annualEntitlement;
        AccrualMethod = accrualMethod;
        MaxCarryForward = maxCarryForward;
        CarryForwardExpiryMonths = carryForwardExpiryMonths;
        MinServiceDays = minServiceDays;
        MaxConsecutiveDays = maxConsecutiveDays;
        ApplicableGender = applicableGender;
        ApplicableEmploymentTypes = applicableEmploymentTypes;
    }

    /// <summary>Kya ye employee is date pe is leave ka haqdar hai?</summary>
    public bool IsEligible(Employee employee, DateOnly onDate)
    {
        if (employee.JoiningDate.AddDays(MinServiceDays) > onDate)
            return false;
        if (ApplicableGender is not null && employee.Gender != ApplicableGender)
            return false;
        if (ApplicableEmploymentTypes is { } types && (types & employee.EmploymentType) == 0)
            return false;
        return true;
    }
}
