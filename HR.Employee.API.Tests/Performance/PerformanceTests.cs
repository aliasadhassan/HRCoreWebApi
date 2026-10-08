namespace HR.Employee.API.Tests.Performance;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Performance;
using Xunit;

public class PerformanceTests
{
    static readonly Guid Tenant = Guid.NewGuid(), Employee = Guid.NewGuid(), Manager = Guid.NewGuid(), User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 10, 8);
    static readonly DateTime Now = new(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);

    static ReviewCycle NewCycle(bool selfReview = true)
    {
        var cycle = ReviewCycle.Create(Tenant, "2026 H2", null, Today.AddMonths(-3), Today.AddMonths(3), selfReview,
            selfReview ? Today.AddDays(7) : null, Today.AddDays(21));
        cycle.Launch(Now);
        return cycle;
    }

    [Fact]
    public void Self_due_must_come_before_manager_due()
        => Assert.Throws<DomainException>(() => ReviewCycle.Create(Tenant, "X", null, Today, Today, true, Today.AddDays(9), Today.AddDays(3)));

    [Fact]
    public void Period_is_locked_after_launch()
    {
        var cycle = NewCycle();
        Assert.Throws<DomainException>(() => cycle.Update("2026 H2", null, Today, Today.AddMonths(3), true, Today.AddDays(7), Today.AddDays(21)));

        cycle.Update("2026 H2 review", null, cycle.PeriodStart, cycle.PeriodEnd, true, Today.AddDays(10), Today.AddDays(30));
        Assert.Equal(Today.AddDays(30), cycle.ManagerReviewDue);
    }

    [Fact]
    public void Review_flows_self_then_manager_then_acknowledged()
    {
        var review = PerformanceReview.Create(Tenant, NewCycle(), Employee, Manager);
        Assert.Equal(ReviewStatus.SelfReview, review.Status);

        Assert.Throws<DomainException>(() => review.SaveManager(4, "Good", null, null, true, User, Now));
        Assert.Throws<DomainException>(() => review.SaveSelf(4, null, true, Now));

        review.SaveSelf(4, "Shipped payroll", true, Now);
        Assert.Equal(ReviewStatus.ManagerReview, review.Status);

        review.SaveManager(5, "Strong half", "Ownership", null, true, User, Now);
        Assert.Equal(ReviewStatus.Shared, review.Status);
        Assert.Throws<DomainException>(() => review.SaveManager(3, "x", null, null, false, User, Now));

        review.Acknowledge("Thanks", Now);
        Assert.Equal(ReviewStatus.Acknowledged, review.Status);
        Assert.Equal("Thanks", review.EmployeeComment);
    }

    [Fact]
    public void Without_self_review_the_manager_starts_and_reopen_clears_acknowledgement()
    {
        var review = PerformanceReview.Create(Tenant, NewCycle(selfReview: false), Employee, Manager);
        Assert.Equal(ReviewStatus.ManagerReview, review.Status);

        review.SaveManager(3, "Meets", null, null, true, User, Now);
        review.Acknowledge(null, Now);
        review.Reopen();

        Assert.Equal(ReviewStatus.ManagerReview, review.Status);
        Assert.Null(review.AcknowledgedAt);
        Assert.Equal((byte)3, review.ManagerRating);
    }

    [Fact]
    public void Rating_outside_scale_is_rejected()
        => Assert.Throws<DomainException>(() => PerformanceReview.Create(Tenant, NewCycle(), Employee, Manager).SaveSelf(6, "x", false, Now));

    [Fact]
    public void Self_reviewer_is_dropped()
        => Assert.Null(PerformanceReview.Create(Tenant, NewCycle(), Employee, Employee).ReviewerEmployeeId);

    [Fact]
    public void Goal_completes_at_100_and_must_be_reopened_to_edit()
    {
        var goal = Goal.Create(Tenant, Employee, new GoalDetails("Cut run time", null, null, 40, null, Today.AddMonths(2)));

        goal.CheckIn(30, GoalStatus.NotStarted, null, User, Now);
        Assert.Equal(GoalStatus.OnTrack, goal.Status);

        goal.CheckIn(100, GoalStatus.AtRisk, "Done", User, Now);
        Assert.Equal(GoalStatus.Completed, goal.Status);
        Assert.Equal(Now, goal.CompletedAt);
        Assert.Throws<DomainException>(() => goal.Update(new GoalDetails("x", null, null, null, null, Today)));

        goal.Reopen(User, Now);
        Assert.Equal(GoalStatus.OnTrack, goal.Status);
        Assert.Null(goal.CompletedAt);
        Assert.Equal(3, goal.CheckIns.Count);
    }

    [Fact]
    public void Goal_due_before_start_is_rejected()
        => Assert.Throws<DomainException>(() => Goal.Create(Tenant, Employee, new GoalDetails("x", null, null, null, Today, Today.AddDays(-1))));
}
