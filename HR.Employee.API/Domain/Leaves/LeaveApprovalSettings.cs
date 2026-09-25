namespace HR.Employee.API.Domain.Leaves;

using HR.Employee.API.Domain.Common;

/// <summary>Tenant ki approval chain (1 row per tenant): 1 level ya 2 level.</summary>
public sealed class LeaveApprovalSettings : AuditableEntity
{
    public byte ApprovalLevels { get; private set; } = 1;
    public ApproverType Level1Approver { get; private set; } = ApproverType.LineManager;
    public ApproverType? Level2Approver { get; private set; }
    public byte? AutoApproveAfterDays { get; private set; }
    public bool AllowCancelAfterApproval { get; private set; } = true;

    private LeaveApprovalSettings() { }

    /// <summary>Naye tenant ka default: sirf line manager.</summary>
    public static LeaveApprovalSettings CreateDefault(Guid tenantId)
        => new() { TenantId = Guard.NotEmpty(tenantId, "Tenant") };

    public void Configure(ApproverType level1, ApproverType? level2, byte? autoApproveAfterDays, bool allowCancelAfterApproval)
    {
        if (level2 == level1)
            throw new DomainException("Both approval levels cannot use the same approver.");
        if (autoApproveAfterDays is 0 or > 30)
            throw new DomainException("Auto-approve must be between 1 and 30 days.");

        Level1Approver = level1;
        Level2Approver = level2;
        ApprovalLevels = (byte)(level2 is null ? 1 : 2);
        AutoApproveAfterDays = autoApproveAfterDays;
        AllowCancelAfterApproval = allowCancelAfterApproval;
    }

    /// <summary>Submit ke waqt isi se LeaveRequest ka approval snapshot banta hai.</summary>
    public IReadOnlyList<(byte Level, ApproverType Approver)> Chain()
    {
        var chain = new List<(byte, ApproverType)> { (1, Level1Approver) };
        if (Level2Approver is { } level2)
            chain.Add((2, level2));
        return chain;
    }
}
