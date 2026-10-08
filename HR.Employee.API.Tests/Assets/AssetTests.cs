namespace HR.Employee.API.Tests.Assets;

using HR.Employee.API.Domain.Assets;
using HR.Employee.API.Domain.Common;
using Xunit;

public class AssetTests
{
    static readonly Guid Tenant = Guid.NewGuid(), Category = Guid.NewGuid(), Employee = Guid.NewGuid(), User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 10, 8);
    static readonly DateTime Now = new(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);

    static Asset NewAsset(string tag = "ast-0001")
        => Asset.Create(Tenant, tag, new AssetDetails("Laptop", Category, "Apple", "M3", "SN1", null, Today.AddMonths(-1), 2500m, null, Today.AddYears(1), AssetCondition.New, null), User, Now);

    [Fact]
    public void Create_normalises_tag_and_logs_created()
    {
        var asset = NewAsset();

        Assert.Equal("AST-0001", asset.AssetTag);
        Assert.Equal(AssetStatus.Available, asset.Status);
        Assert.Equal(AssetEventType.Created, Assert.Single(asset.Events).Type);
    }

    [Fact]
    public void Tag_with_spaces_is_rejected()
        => Assert.Throws<DomainException>(() => NewAsset("AST 1"));

    [Fact]
    public void Assign_then_return_moves_status_and_closes_assignment()
    {
        var asset = NewAsset();
        var assignment = asset.Assign(Employee, Today.AddDays(-10), Today.AddDays(20), "Dev laptop", User, Now);

        Assert.Equal(AssetStatus.Assigned, asset.Status);
        Assert.Same(assignment, asset.OpenAssignment);
        Assert.Throws<DomainException>(() => asset.Assign(Guid.NewGuid(), Today, null, null, User, Now));

        asset.Return(Today, AssetCondition.Fair, AssetStatus.Available, null, User, Now);

        Assert.Equal(AssetStatus.Available, asset.Status);
        Assert.Equal(AssetCondition.Fair, asset.Condition);
        Assert.Null(asset.OpenAssignment);
        Assert.Equal(Today, assignment.ReturnedOn);
        Assert.Equal(new[] { AssetEventType.Created, AssetEventType.Assigned, AssetEventType.Returned }, asset.Events.Select(e => e.Type));
    }

    [Fact]
    public void Return_before_assigned_date_or_lost_without_note_is_rejected()
    {
        var asset = NewAsset();
        asset.Assign(Employee, Today, null, null, User, Now);

        Assert.Throws<DomainException>(() => asset.Return(Today.AddDays(-1), AssetCondition.Good, AssetStatus.Available, null, User, Now));
        Assert.Throws<DomainException>(() => asset.Return(Today, AssetCondition.Good, AssetStatus.Lost, " ", User, Now));
        Assert.Throws<DomainException>(() => asset.Return(Today, AssetCondition.Good, AssetStatus.Retired, "x", User, Now));
    }

    [Fact]
    public void Status_change_needs_return_first_and_note_for_retire()
    {
        var asset = NewAsset();
        asset.Assign(Employee, Today, null, null, User, Now);
        Assert.Throws<DomainException>(() => asset.ChangeStatus(AssetStatus.InRepair, null, User, Now));

        asset.Return(Today, AssetCondition.Damaged, AssetStatus.InRepair, "Screen", User, Now);
        Assert.Throws<DomainException>(() => asset.Assign(Employee, Today, null, null, User, Now));
        Assert.Throws<DomainException>(() => asset.ChangeStatus(AssetStatus.Retired, null, User, Now));
        Assert.Throws<DomainException>(() => asset.ChangeStatus(AssetStatus.Assigned, null, User, Now));

        asset.ChangeStatus(AssetStatus.Retired, "Beyond repair", User, Now);
        Assert.Equal(AssetStatus.Retired, asset.Status);
        Assert.Equal(AssetStatus.Retired, asset.Events.Last().Status);
    }

    [Fact]
    public void Only_the_holder_can_acknowledge_once()
    {
        var asset = NewAsset();
        var assignment = asset.Assign(Employee, Today, null, null, User, Now);

        Assert.Throws<DomainException>(() => asset.Acknowledge(assignment.Id, Guid.NewGuid(), null, Now));
        asset.Acknowledge(assignment.Id, Employee, null, Now);
        asset.Acknowledge(assignment.Id, Employee, null, Now.AddHours(1));

        Assert.Equal(Now, assignment.AcknowledgedAt);
        Assert.Single(asset.Events, e => e.Type == AssetEventType.Acknowledged);
    }

    [Fact]
    public void Warranty_before_purchase_is_rejected()
        => Assert.Throws<DomainException>(() => Asset.Create(Tenant, "X1",
            new AssetDetails("Phone", Category, null, null, null, null, Today, null, null, Today.AddDays(-1), AssetCondition.Good, null), User, Now));
}
