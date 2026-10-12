namespace HR.Employee.API.Domain.Relations;

using HR.Employee.API.Domain.Common;

/// <summary>
/// Case ka status (company apne naam rakh sakti hai). Har stage ek fixed phase mein aata hai:
/// Open → Review → Investigation → Resolution → Closed. TargetDays = is stage mein aate hi case ki deadline itne din aagay.
/// </summary>
public sealed class CaseStage : AuditableEntity
{
    public const int MaxTargetDays = 365;

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public CasePhase Phase { get; private set; }
    public short SortOrder { get; private set; }
    public short? TargetDays { get; private set; }
    public bool IsActive { get; private set; } = true;

    private CaseStage() { }

    public static CaseStage Create(Guid tenantId, StageDetails details)
    {
        var stage = new CaseStage { TenantId = Guard.NotEmpty(tenantId, "Tenant") };
        stage.Update(details);
        return stage;
    }

    public void Update(StageDetails d)
    {
        if (!Enum.IsDefined(d.Phase))
            throw new DomainException("Select a valid phase.");
        if (d.TargetDays is < 1 or > MaxTargetDays)
            throw new DomainException($"Target days must be between 1 and {MaxTargetDays}.");
        if (d.SortOrder is < 0 or > 999)
            throw new DomainException("Sort order must be between 0 and 999.");
        if (d.Phase == CasePhase.Closed && d.TargetDays is not null)
            throw new DomainException("A closed stage has no deadline.");
        Name = Guard.Required(d.Name, "Name", 60);
        Description = Guard.Optional(d.Description, "Description", 300);
        Phase = d.Phase;
        SortOrder = d.SortOrder;
        TargetDays = d.TargetDays;
        IsActive = d.IsActive;
    }

    /// <summary>Company ke liye shuru ke stages (pehla case bante hi ban jate hain).</summary>
    public static IReadOnlyList<StageDetails> Defaults { get; } =
    [
        new("Open", "New case, not yet looked at.", CasePhase.Open, 10, 2, true),
        new("Under review", "HR is checking the facts and deciding next steps.", CasePhase.Review, 20, 5, true),
        new("Investigation", "Interviews and evidence gathering.", CasePhase.Investigation, 30, 20, true),
        new("Resolution", "Findings agreed, actions being carried out.", CasePhase.Resolution, 40, 10, true),
        new("Closed", "Case is finished.", CasePhase.Closed, 50, null, true),
    ];
}

public sealed record StageDetails(string Name, string? Description, CasePhase Phase, short SortOrder, short? TargetDays, bool IsActive);
