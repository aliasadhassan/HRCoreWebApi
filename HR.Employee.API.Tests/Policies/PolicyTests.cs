namespace HR.Employee.API.Tests.Policies;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Policies;
using Xunit;

public class PolicyTests
{
    static readonly Guid Tenant = Guid.NewGuid(), User = Guid.NewGuid();
    static readonly DateTime Now = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    static PolicyDetails Details(string? body = "Be kind", bool ack = true, short? months = 12)
        => new("Code of conduct", PolicyCategory.Conduct, null, body, null, null, null, ack, ack ? (short)14 : null, months);

    [Fact]
    public void First_publish_needs_acknowledgement_and_sets_review_date()
    {
        var p = Policy.Create(Tenant, Details());
        var v = p.Publish(null, false, null, User, Now);

        Assert.Equal(PolicyStatus.Published, p.Status);
        Assert.Equal(1, v.Number);
        Assert.True(v.RequiresAcknowledgement);
        Assert.Equal(1, p.AckVersion);
        Assert.Equal(Today.AddDays(14), p.AckDueOn);
        Assert.Equal(Today.AddMonths(12), p.NextReviewOn);
        Assert.False(p.HasUnpublishedChanges);
    }

    [Fact]
    public void Minor_republish_keeps_old_acknowledgements_and_major_one_resets_them()
    {
        var p = Policy.Create(Tenant, Details());
        p.Publish(null, false, null, User, Now);

        p.Update(Details("Be kind and honest"), Today);
        Assert.True(p.HasUnpublishedChanges);
        Assert.Throws<DomainException>(() => p.Publish(null, false, null, User, Now));
        var minor = p.Publish("Typo fix", false, null, User, Now);
        Assert.False(minor.RequiresAcknowledgement);
        Assert.Equal(1, p.AckVersion);

        p.Update(Details("New rules"), Today);
        p.Publish("New rules", true, null, User, Now);
        Assert.Equal(3, p.AckVersion);
    }

    [Fact]
    public void Settings_change_alone_is_not_an_unpublished_change()
    {
        var p = Policy.Create(Tenant, Details());
        p.Publish(null, false, null, User, Now);
        p.Update(Details(months: 6), Today);
        Assert.False(p.HasUnpublishedChanges);
        Assert.Equal(Today.AddMonths(6), p.NextReviewOn);
        Assert.Throws<DomainException>(() => p.Publish("x", false, null, User, Now));
    }

    [Fact]
    public void Turning_acknowledgement_off_clears_it_and_back_on_starts_from_current_version()
    {
        var p = Policy.Create(Tenant, Details());
        p.Publish(null, false, null, User, Now);
        p.Update(Details(ack: false), Today);
        Assert.False(p.NeedsAcknowledgement);
        p.Update(Details(), Today);
        Assert.Equal(1, p.AckVersion);
        Assert.Equal(Today.AddDays(14), p.AckDueOn);
    }

    [Fact]
    public void Archived_policy_cannot_be_edited_until_restored()
    {
        var p = Policy.Create(Tenant, Details());
        p.Archive(Now);
        Assert.Throws<DomainException>(() => p.Update(Details(), Today));
        p.Restore();
        Assert.Equal(PolicyStatus.Draft, p.Status);
        Assert.Throws<DomainException>(() => Policy.Create(Tenant, Details(body: null)).Publish(null, false, null, User, Now));
    }

    static ComplianceObligation Obligation(ObligationFrequency f, DateOnly due)
        => ComplianceObligation.Create(Tenant, new("EOBI", "EOBI", ObligationCategory.SocialSecurity, f, null, null, null, 10, due, true));

    [Fact]
    public void Monthly_obligation_keeps_its_day_of_month()
    {
        var o = Obligation(ObligationFrequency.Monthly, new DateOnly(2026, 1, 31));
        o.RecordFiling(new DateOnly(2026, 1, 30), null, null, null, User, Now);
        Assert.Equal(new DateOnly(2026, 2, 28), o.NextDueOn);
        o.RecordFiling(new DateOnly(2026, 3, 1), "CH-2", null, null, User, Now);
        Assert.Equal(new DateOnly(2026, 3, 31), o.NextDueOn);
        Assert.True(o.Filings.Last().IsLate);
    }

    [Fact]
    public void Undo_only_the_latest_filing()
    {
        var o = Obligation(ObligationFrequency.Quarterly, new DateOnly(2026, 3, 31));
        var first = o.RecordFiling(new DateOnly(2026, 3, 30), null, null, null, User, Now);
        var second = o.RecordFiling(new DateOnly(2026, 6, 30), null, null, null, User, Now);
        Assert.Throws<DomainException>(() => o.UndoFiling(first));
        o.UndoFiling(second);
        Assert.Equal(new DateOnly(2026, 6, 30), o.NextDueOn);
    }

    [Fact]
    public void State_follows_due_date_and_reminder_window()
    {
        Assert.Equal(ObligationState.Overdue, Obligation(ObligationFrequency.Annually, Today.AddDays(-1)).StateOn(Today));
        Assert.Equal(ObligationState.DueSoon, Obligation(ObligationFrequency.Annually, Today.AddDays(10)).StateOn(Today));
        Assert.Equal(ObligationState.Upcoming, Obligation(ObligationFrequency.Annually, Today.AddDays(11)).StateOn(Today));
        var once = Obligation(ObligationFrequency.Once, Today);
        once.RecordFiling(Today, null, null, null, User, Now);
        Assert.Equal(ObligationState.Completed, once.StateOn(Today));
        Assert.Throws<DomainException>(() => once.RecordFiling(Today, null, null, null, User, Now));
    }
}
