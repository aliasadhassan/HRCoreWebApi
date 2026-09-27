namespace HR.Payroll.API.Domain.Salaries;

using HR.Payroll.API.Domain.Common;

/// <summary>Value object: ek component ki amount kaise banti hai (template line aur override dono yahi use karte hain).</summary>
public sealed record ComponentFormula(CalcType CalcType, decimal? Amount, decimal? Percentage, Guid? BaseComponentId)
{
    public static ComponentFormula Create(Guid componentId, CalcType calcType, decimal? amount, decimal? percentage, Guid? baseComponentId)
    {
        switch (calcType)
        {
            case CalcType.Fixed:
                return new(calcType, Guard.NotNegative(amount ?? throw new DomainException("Amount is required for a fixed component."), "Amount"), null, null);

            case CalcType.PercentOfComponent:
                if (baseComponentId is null)
                    throw new DomainException("A percentage component needs a base component.");
                if (baseComponentId == componentId)
                    throw new DomainException("A component cannot be a percentage of itself.");
                return new(calcType, null, Guard.Percentage(percentage ?? throw new DomainException("Percentage is required."), "Percentage"), baseComponentId);

            case CalcType.PercentOfGross:
                return new(calcType, null, Guard.Percentage(percentage ?? throw new DomainException("Percentage is required."), "Percentage"), null);

            case CalcType.Variable:
                throw new DomainException("Variable components come from payroll inputs, not from a salary structure.");

            default:
                throw new DomainException("Unknown calculation type.");
        }
    }
}
