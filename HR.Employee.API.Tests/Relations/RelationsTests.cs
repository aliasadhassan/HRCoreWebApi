namespace HR.Employee.API.Tests.Relations;

using System.Reflection;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Relations;
using Xunit;

public class RelationsTests
{
    static readonly Guid Tenant = Guid.NewGuid(), Reporter = Guid.NewGuid(), Owner = Guid.NewGuid(), Respondent = Guid.NewGuid(), Hr = Guid.NewGuid();
    static readonly DateTime Now = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    // DB ke baghair ids khali hoti hain; tests ke liye de do (EF Add par deta hai)
    static T WithId<T>(T entity) where T : Entity
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, Guid.NewGuid());
        return entity;
    }

    static CaseStage Stage(string name, CasePhase phase, short? days = null)
        => WithId(CaseStage.Create(Tenant, new(name, null, phase, (short)((byte)phase * 10), days, true)));

    static readonly CaseStage Open = Stage("Open", CasePhase.Open, 2), Investigation = Stage("Investigation", CasePhase.Investigation, 20),
        Closed = Stage("Closed", CasePhase.Closed);

    static ErCase NewCase(CaseType type = CaseType.Grievance, CaseConfidentiality conf = CaseConfidentiality.Standard, bool hidden = false)
        => WithId(ErCase.Create(Tenant, "er-0001", new("Overtime", null, type, CaseSeverity.Medium, conf, null, null), CaseSource.Employee,
            Open, Today, Reporter, hidden, null, Today, Now));

    [Fact]
    public void New_case_starts_in_stage_with_deadline_and_created_entry()
    {
        var c = NewCase();
        Assert.Equal("ER-0001", c.Code);
        Assert.Equal(CasePhase.Open, c.Phase);
        Assert.Equal(Today.AddDays(2), c.DueOn);
        Assert.Equal(CaseEntryKind.Created, Assert.Single(c.Entries).Kind);
    }

    [Fact]
    public void Harassment_and_whistleblowing_must_be_restricted()
    {
        Assert.Throws<DomainException>(() => NewCase(CaseType.Harassment));
        Assert.Throws<DomainException>(() => NewCase(CaseType.Whistleblowing));
        Assert.Equal(CaseConfidentiality.Restricted, NewCase(CaseType.Harassment, CaseConfidentiality.Restricted).Confidentiality);
        Assert.Equal((CaseSeverity.High, CaseConfidentiality.Restricted), CaseDetails.DefaultsFor(CaseType.Discrimination));
    }

    [Fact]
    public void Involved_people_can_never_work_on_the_case()
    {
        var c = NewCase();
        c.AddParty(Respondent, null, "Mona", CasePartyRole.Respondent, null, null, Now);
        Assert.Throws<DomainException>(() => c.ChangeOwner(Reporter, "Danish", null, Now));
        Assert.Throws<DomainException>(() => c.AddMember(Respondent, "Mona", CaseMemberRole.Viewer, null, Now));
        c.ChangeOwner(Owner, "Laila", null, Now);
        Assert.Throws<DomainException>(() => c.AddParty(Owner, null, "Laila", CasePartyRole.Witness, null, null, Now));
        Assert.Throws<DomainException>(() => c.AddMember(Owner, "Laila", CaseMemberRole.Owner, null, Now));
    }

    [Fact]
    public void Changing_owner_keeps_old_owner_as_investigator()
    {
        var c = NewCase();
        c.ChangeOwner(Owner, "Laila", null, Now);
        c.ChangeOwner(Hr, "Asma", null, Now);
        Assert.Equal(Hr, c.OwnerEmployeeId);
        Assert.Equal(CaseMemberRole.Investigator, c.RoleOf(Owner));
        Assert.Equal(CaseMemberRole.Owner, c.RoleOf(Hr));
        Assert.Throws<DomainException>(() => c.RemoveMember(Hr, "Asma", null, Now));
    }

    [Fact]
    public void Close_needs_owner_outcome_and_summary_and_locks_the_case()
    {
        var c = NewCase();
        Assert.Throws<DomainException>(() => c.Close(Closed, CaseOutcome.Substantiated, CaseAction.Coaching, "Done", null, Now));
        c.ChangeOwner(Owner, "Laila", null, Now);
        Assert.Throws<DomainException>(() => c.Close(Investigation, CaseOutcome.Substantiated, CaseAction.Coaching, "Done", null, Now));
        Assert.Throws<DomainException>(() => c.Close(Closed, CaseOutcome.Substantiated, CaseAction.Coaching, " ", null, Now));
        Assert.Throws<DomainException>(() => c.MoveTo(Closed, null, null, Today, Now));
        c.Close(Closed, CaseOutcome.Substantiated, CaseAction.Coaching, "Done", null, Now);
        Assert.True(c.IsClosed);
        Assert.Null(c.DueOn);
        Assert.Throws<DomainException>(() => c.AddNote("late", false, null, Now));

        c.Reopen(Investigation, "Appeal", null, Today, Now);
        Assert.Equal(CasePhase.Investigation, c.Phase);
        Assert.Null(c.Outcome);
        Assert.Equal(1, c.ReopenCount);
        Assert.Equal(Today.AddDays(20), c.DueOn);
    }

    [Fact]
    public void Escalation_stops_at_level_three()
    {
        var c = NewCase();
        for (var i = 0; i < ErCase.MaxEscalation; i++)
            c.Escalate("Late", null, null, Now);
        Assert.Equal(ErCase.MaxEscalation, c.EscalationLevel);
        Assert.Throws<DomainException>(() => c.Escalate("Again", null, null, Now));
        Assert.Throws<DomainException>(() => c.Escalate(" ", null, null, Now));
    }

    [Fact]
    public void Shared_messages_need_a_reporter_and_timeline_order_is_kept()
    {
        var team = WithId(ErCase.Create(Tenant, "ER-0002", new("Audit", null, CaseType.Misconduct, CaseSeverity.Low, CaseConfidentiality.Standard, null, null),
            CaseSource.Hr, Open, Today, null, false, null, Today, Now));
        Assert.Throws<DomainException>(() => team.AddNote("hi", true, null, Now));
        Assert.Throws<DomainException>(() => ErCase.Create(Tenant, "ER-0003", new("x", null, CaseType.Other, CaseSeverity.Low, CaseConfidentiality.Standard, null, null),
            CaseSource.Hr, Open, Today, null, true, null, Today, Now));

        team.AddNote("one", false, null, Now);
        team.AddNote("two", false, null, Now);
        var times = team.Entries.Select(e => e.At).ToList();
        Assert.Equal(times.OrderBy(t => t), times);
        Assert.Equal(times.Count, times.Distinct().Count());
    }

    [Fact]
    public void Break_glass_adds_viewer_with_reason_and_evidence_needs_a_link()
    {
        var c = NewCase(CaseType.Harassment, CaseConfidentiality.Restricted, hidden: true);
        Assert.Throws<DomainException>(() => c.BreakGlass(Hr, "Asma", "", null, Now));
        c.BreakGlass(Hr, "Asma", "Legal hold", null, Now);
        Assert.Equal(CaseMemberRole.Viewer, c.RoleOf(Hr));
        Assert.Throws<DomainException>(() => c.BreakGlass(Hr, "Asma", "Again", null, Now));
        Assert.Throws<DomainException>(() => c.AddEvidence("Shot", "not a link", null, true, false, null, Now));
        Assert.True(c.AddEvidence("Shot", "https://files.example.com/a.png", null, true, false, null, Now).IsRestricted);
    }

    [Fact]
    public void Stage_rules()
    {
        Assert.Throws<DomainException>(() => CaseStage.Create(Tenant, new("Done", null, CasePhase.Closed, 50, 5, true)));
        Assert.Throws<DomainException>(() => CaseStage.Create(Tenant, new("Long", null, CasePhase.Review, 10, 400, true)));
        Assert.Equal(5, CaseStage.Defaults.Count);
        Assert.Single(CaseStage.Defaults, d => d.Phase == CasePhase.Closed);
    }
}
