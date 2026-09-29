namespace HR.Payroll.API.Domain.Salaries;

using HR.Payroll.API.Domain.Common;

/// <summary>Salary ki "recipe": BASIC = 60% of gross, HRA = 40% of BASIC, TRANSPORT = 5000 fixed.</summary>
public sealed class SalaryTemplate : AuditableEntity
{
    private readonly List<SalaryTemplateLine> _lines = new();

    public string Name { get; private set; } = default!;
    public Guid? SalaryGradeId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<SalaryTemplateLine> Lines => _lines.AsReadOnly();

    private SalaryTemplate() { }

    public static SalaryTemplate Create(Guid tenantId, string name, Guid? salaryGradeId)
        => new()
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Name = Guard.Required(name, "Name", 100),
            SalaryGradeId = salaryGradeId
        };

    public void Update(string name, Guid? salaryGradeId)
    {
        Name = Guard.Required(name, "Name", 100);
        SalaryGradeId = salaryGradeId;
    }

    public SalaryTemplateLine SetLine(Guid payComponentId, CalcType calcType, decimal? amount, decimal? percentage, Guid? baseComponentId)
    {
        var formula = ComponentFormula.Create(payComponentId, calcType, amount, percentage, baseComponentId);

        var line = _lines.FirstOrDefault(l => l.PayComponentId == payComponentId);
        if (line is null)
        {
            line = new SalaryTemplateLine(Guard.NotEmpty(payComponentId, "Component"));
            _lines.Add(line);
        }
        line.Apply(formula);

        EnsureNoCircularReference();
        EnsureSingleRemainder();
        return line;
    }

    /// <summary>Template form poori recipe ek saath bhejta hai — purani lines hata kar nayi.</summary>
    public void ReplaceLines(IEnumerable<(Guid PayComponentId, CalcType CalcType, decimal? Amount, decimal? Percentage, Guid? BaseComponentId)> lines)
    {
        var incoming = lines.ToList();
        if (incoming.Count == 0)
            throw new DomainException("A salary template needs at least one component.");
        if (incoming.Select(l => l.PayComponentId).Distinct().Count() != incoming.Count)
            throw new DomainException("A component can appear only once in a template.");

        _lines.Clear();
        foreach (var l in incoming)
        {
            var line = new SalaryTemplateLine(Guard.NotEmpty(l.PayComponentId, "Component"));
            line.Apply(ComponentFormula.Create(l.PayComponentId, l.CalcType, l.Amount, l.Percentage, l.BaseComponentId));
            _lines.Add(line);
        }

        var componentIds = _lines.Select(l => l.PayComponentId).ToHashSet();
        if (_lines.Any(l => l.BaseComponentId is { } b && !componentIds.Contains(b)))
            throw new DomainException("A percentage line must be based on another component in the same template.");

        EnsureNoCircularReference();
        EnsureSingleRemainder();
    }

    public void RemoveLine(Guid payComponentId)
    {
        var line = _lines.FirstOrDefault(l => l.PayComponentId == payComponentId)
                   ?? throw new DomainException("This template has no line for the selected component.");

        if (_lines.Any(l => l.BaseComponentId == payComponentId))
            throw new DomainException("Another line is calculated as a percentage of this component. Change that line first.");

        _lines.Remove(line);
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    /// <summary>Gross ka "baqi hissa" sirf ek component le sakta hai.</summary>
    private void EnsureSingleRemainder()
    {
        if (_lines.Count(l => l.CalcType == CalcType.Remainder) > 1)
            throw new DomainException("Only one component can take the remainder of the gross.");
    }

    /// <summary>HRA = % of BASIC, BASIC = % of HRA → infinite loop. Save hone se pehle pakdo.</summary>
    private void EnsureNoCircularReference()
    {
        var bases = _lines.Where(l => l.BaseComponentId is not null)
                          .ToDictionary(l => l.PayComponentId, l => l.BaseComponentId!.Value);

        foreach (var start in bases.Keys)
        {
            var current = start;
            for (var depth = 0; bases.TryGetValue(current, out var next); depth++)
            {
                if (next == start || depth > bases.Count)
                    throw new DomainException("Percentage components form a loop in this template.");
                current = next;
            }
        }
    }
}

public sealed class SalaryTemplateLine : Entity
{
    public Guid SalaryTemplateId { get; private set; }
    public Guid PayComponentId { get; private set; }
    public CalcType CalcType { get; private set; }
    public decimal? Amount { get; private set; }
    public decimal? Percentage { get; private set; }
    public Guid? BaseComponentId { get; private set; }

    public ComponentFormula Formula => new(CalcType, Amount, Percentage, BaseComponentId);

    private SalaryTemplateLine() { }

    internal SalaryTemplateLine(Guid payComponentId) => PayComponentId = payComponentId;

    internal void Apply(ComponentFormula formula)
    {
        CalcType = formula.CalcType;
        Amount = formula.Amount;
        Percentage = formula.Percentage;
        BaseComponentId = formula.BaseComponentId;
    }
}
