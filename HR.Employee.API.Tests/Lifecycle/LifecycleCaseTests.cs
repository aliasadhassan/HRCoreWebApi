namespace HR.Employee.API.Tests.Lifecycle;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Lifecycle;
using Xunit;

public class LifecycleCaseTests
{
    static readonly Guid Tenant = Guid.NewGuid(), EmployeeId = Guid.NewGuid();
    static readonly DateOnly Joining = new(2026, 10, 12);
    static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    static ChecklistTemplate Template(LifecycleKind kind, params TemplateTaskData[] tasks)
    {
        var template = ChecklistTemplate.Create(Tenant, kind, "Standard", null);
        template.ReplaceTasks(tasks);
        return template;
    }

    [Fact]
    public void Onboarding_copies_template_tasks_with_due_dates_from_joining_date()
    {
        var template = Template(LifecycleKind.Onboarding,
            new("Laptop", null, TaskOwner.It, -3, true),
            new("Welcome", null, TaskOwner.Manager, 0, false),
            new("30-day check-in", null, TaskOwner.Manager, 30, false));

        var c = LifecycleCase.StartOnboarding(Tenant, EmployeeId, Joining, template);

        Assert.Equal(CaseStatus.InProgress, c.Status);
        Assert.Equal(Joining, c.AnchorDate);
        Assert.Equal(new DateOnly?[] { Joining.AddDays(-3), Joining, Joining.AddDays(30) }, c.Tasks.OrderBy(t => t.SortOrder).Select(t => t.DueDate));
        Assert.All(c.Tasks, t => Assert.Equal(LifecycleTaskStatus.Open, t.Status));
    }

    [Fact]
    public void Template_of_other_kind_or_inactive_is_rejected()
    {
        var exitTemplate = Template(LifecycleKind.Exit, new TemplateTaskData("Return laptop", null, TaskOwner.It, 0, true));
        Assert.Throws<DomainException>(() => LifecycleCase.StartOnboarding(Tenant, EmployeeId, Joining, exitTemplate));

        var inactive = Template(LifecycleKind.Onboarding);
        inactive.Deactivate();
        Assert.Throws<DomainException>(() => LifecycleCase.StartOnboarding(Tenant, EmployeeId, Joining, inactive));
    }

    [Fact]
    public void Moving_last_working_day_shifts_open_task_due_dates_only()
    {
        var lwd = new DateOnly(2026, 10, 31);
        var c = LifecycleCase.StartExit(Tenant, EmployeeId, ExitType.Resignation, new DateOnly(2026, 10, 1), lwd, "Moving abroad", null);
        c.AddTask("Handover", null, TaskOwner.Employee, null, lwd.AddDays(-3), true);

        c.SetExitDetails(ExitType.Resignation, new DateOnly(2026, 10, 1), lwd.AddDays(7), "Moving abroad", true, "Good team");

        Assert.Equal(lwd.AddDays(7), c.AnchorDate);
        Assert.Equal(lwd.AddDays(4), c.Tasks.Single().DueDate);
        Assert.True(c.EligibleForRehire);
    }

    [Fact]
    public void Last_working_day_before_notice_date_is_rejected()
        => Assert.Throws<DomainException>(() =>
            LifecycleCase.StartExit(Tenant, EmployeeId, ExitType.Resignation, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 9), "x", null));

    [Fact]
    public void Case_completes_only_when_required_tasks_are_closed()
    {
        var c = LifecycleCase.StartOnboarding(Tenant, EmployeeId, Joining, null);
        var task = c.AddTask("Contract", null, TaskOwner.Hr, null, null, true);

        Assert.Throws<DomainException>(() => c.Complete(null, Now));
        Assert.Throws<DomainException>(() => c.SkipTask(task.Id, null, null, Now));   // required: note needed

        c.SkipTask(task.Id, null, "Signed on paper", Now);
        c.Complete(null, Now);

        Assert.Equal(CaseStatus.Completed, c.Status);
        Assert.Equal(Now, c.ClosedAt);
        Assert.Throws<DomainException>(() => c.AddTask("Late", null, TaskOwner.Hr, null, null, false));
    }

    [Fact]
    public void Reopened_task_clears_completion()
    {
        var c = LifecycleCase.StartOnboarding(Tenant, EmployeeId, Joining, null);
        var task = c.AddTask("Laptop", null, TaskOwner.It, null, null, true);

        c.CompleteTask(task.Id, Guid.NewGuid(), null, Now);
        Assert.Throws<DomainException>(() => c.CompleteTask(task.Id, null, null, Now));
        c.ReopenTask(task.Id);

        Assert.Equal(LifecycleTaskStatus.Open, task.Status);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.CompletedByUserId);
    }

    [Fact]
    public void Notice_and_withdraw_restore_previous_status()
    {
        var onProbation = Employee.Create(Tenant, "E1", "Bilal", null, "Ahmed", "b@x.com", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, EmploymentType.FullTime, new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 1));

        onProbation.ServeNotice(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        Assert.Equal(EmploymentStatus.OnNotice, onProbation.EmploymentStatus);
        Assert.Throws<DomainException>(() => onProbation.ServeNotice(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 31)));

        onProbation.WithdrawNotice(new DateOnly(2026, 3, 5));
        Assert.Equal(EmploymentStatus.Probation, onProbation.EmploymentStatus);
        Assert.Equal(3, onProbation.JobHistory.Count);
    }
}
