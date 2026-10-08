namespace HR.Employee.API.Tests.Helpdesk;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Helpdesk;
using Xunit;

public class HelpdeskTests
{
    static readonly Guid Tenant = Guid.NewGuid(), Employee = Guid.NewGuid(), Manager = Guid.NewGuid(), User = Guid.NewGuid();
    static readonly DateTime Now = new(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);

    static HelpdeskCategory Category(bool approval = false, bool confidential = false, short? hours = 24, Guid? assignee = null)
    {
        var c = HelpdeskCategory.Create(Tenant, new("Letters", null, "mail", approval, confidential, hours, assignee, 10, true));
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(c, Guid.NewGuid());
        return c;
    }

    static HelpdeskTicket Ticket(HelpdeskCategory c, Guid? approver = null)
        => HelpdeskTicket.Create(Tenant, "hd-0001", Employee, c, "Salary certificate", null, null, TicketPriority.Normal, approver, User, Now);

    [Fact]
    public void Ticket_without_approval_opens_with_sla_and_default_assignee()
    {
        var assignee = Guid.NewGuid();
        var t = Ticket(Category(hours: 48, assignee: assignee), Manager);
        Assert.Equal("HD-0001", t.Code);
        Assert.Equal(TicketStatus.Open, t.Status);
        Assert.Null(t.ApproverEmployeeId);
        Assert.Equal(Now.AddHours(48), t.DueAt);
        Assert.Equal(assignee, t.AssigneeEmployeeId);
        Assert.Single(t.Activities);
    }

    [Fact]
    public void Approval_category_waits_for_manager_and_sla_starts_on_approval()
    {
        var c = Category(approval: true);
        var t = Ticket(c, Manager);
        Assert.Equal(TicketStatus.PendingApproval, t.Status);
        Assert.Null(t.DueAt);
        Assert.Throws<DomainException>(() => t.SetStatus(TicketStatus.InProgress, null, User, Now));

        t.Approve("ok", User, c, Now.AddHours(5));
        Assert.Equal(TicketStatus.Open, t.Status);
        Assert.Equal(Now.AddHours(29), t.DueAt);

        var noManager = Ticket(c, null);
        Assert.Equal(TicketStatus.Open, noManager.Status);
    }

    [Fact]
    public void Rejection_needs_a_reason_and_closes_the_ticket()
    {
        var t = Ticket(Category(approval: true), Manager);
        Assert.Throws<DomainException>(() => t.Reject(" ", User, Now));
        t.Reject("Not this quarter", User, Now);
        Assert.Equal(TicketStatus.Rejected, t.Status);
        Assert.True(t.IsFinal);
        Assert.Throws<DomainException>(() => t.Comment("hello", false, false, User, Now));
    }

    [Fact]
    public void Confidential_category_cannot_need_approval()
        => Assert.Throws<DomainException>(() => HelpdeskCategory.Create(Tenant, new("Private", null, null, true, true, null, null, 1, true)));

    [Fact]
    public void Employee_reply_moves_waiting_ticket_back_to_in_progress()
    {
        var t = Ticket(Category());
        Assert.Throws<DomainException>(() => t.SetStatus(TicketStatus.WaitingOnEmployee, null, User, Now));
        t.SetStatus(TicketStatus.WaitingOnEmployee, "Which bank?", User, Now);
        Assert.Equal(Now, t.FirstResponseAt);

        t.Comment("HBL", false, false, User, Now.AddHours(1));
        Assert.Equal(TicketStatus.InProgress, t.Status);
        Assert.Throws<DomainException>(() => t.Comment("note", true, false, User, Now));
    }

    [Fact]
    public void Resolve_confirm_and_reopen()
    {
        var t = Ticket(Category());
        Assert.Throws<DomainException>(() => t.SetStatus(TicketStatus.Resolved, null, User, Now));
        t.SetStatus(TicketStatus.Resolved, "Letter emailed", User, Now);
        Assert.Equal("Letter emailed", t.Resolution);

        t.Reopen("Wrong salary on it", User, Now);
        Assert.Equal(TicketStatus.InProgress, t.Status);
        Assert.Null(t.Resolution);

        t.SetStatus(TicketStatus.Resolved, "Fixed", User, Now);
        Assert.Throws<DomainException>(() => t.Confirm(6, User, Now));
        t.Confirm(4, User, Now);
        Assert.Equal(TicketStatus.Closed, t.Status);
        Assert.Equal((byte)4, t.SatisfactionRating);

        Assert.Throws<DomainException>(() => t.Reopen("late", User, Now.AddDays(31)));
    }

    [Fact]
    public void Cancel_only_before_work_finishes()
    {
        var t = Ticket(Category());
        t.SetStatus(TicketStatus.InProgress, null, User, Now);
        Assert.Throws<DomainException>(() => t.Cancel(null, User, Now));

        var other = Ticket(Category());
        other.Cancel("Not needed", User, Now);
        Assert.Equal(TicketStatus.Cancelled, other.Status);
    }

    [Fact]
    public void Link_must_be_http()
        => Assert.Throws<DomainException>(() =>
            HelpdeskTicket.Create(Tenant, "HD-1", Employee, Category(), "x", null, "javascript:alert(1)", TicketPriority.Low, null, User, Now));
}
