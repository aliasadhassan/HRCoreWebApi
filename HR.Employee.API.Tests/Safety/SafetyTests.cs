namespace HR.Employee.API.Tests.Safety;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Safety;
using Xunit;

public class SafetyTests
{
    static readonly Guid Tenant = Guid.NewGuid(), Reporter = Guid.NewGuid(), Investigator = Guid.NewGuid(), Hurt = Guid.NewGuid(), Hse = Guid.NewGuid();
    static readonly DateTime Now = new(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    // DB ke baghair ids khali hoti hain; tests ke liye de do (EF Add par deta hai)
    static T WithId<T>(T entity) where T : Entity
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, Guid.NewGuid());
        return entity;
    }

    static IncidentDetails Details(
        IncidentType type = IncidentType.Injury, Guid? affected = null, string? affectedName = null, IncidentTreatment? treatment = null,
        short lostDays = 0, DateTime? at = null, bool reportable = false)
        => new("Slipped on wet floor", null, type, SafetySeverity.Medium, at ?? Now.AddHours(-2), null, "Kitchen", null,
            affected, affectedName, treatment, "Sprain", "Ankle", lostDays, null, null, reportable);

    static SafetyIncident NewIncident(IncidentDetails? d = null, bool anonymous = false)
        => WithId(SafetyIncident.Create(Tenant, "hs-0001", d ?? Details(affected: Hurt), Reporter, anonymous, null, Now));

    static SafetyChecklist Checklist(params string[] items)
    {
        var c = WithId(SafetyChecklist.Create(Tenant, "Fire safety", null, items));
        // EF Add ke baad items ko checklist id milti hai; inspection lines ko apni id chahiye
        foreach (var i in c.Items)
            typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(i, Guid.NewGuid());
        return c;
    }

    [Fact]
    public void New_incident_is_reported_with_code_and_created_entry()
    {
        var i = NewIncident();
        Assert.Equal("HS-0001", i.Code);
        Assert.Equal(IncidentStatus.Reported, i.Status);
        Assert.Equal(IncidentEntryKind.Created, Assert.Single(i.Entries).Kind);
    }

    [Fact]
    public void Injury_needs_a_person_and_not_both_employee_and_name()
    {
        Assert.Throws<DomainException>(() => NewIncident(Details()));
        Assert.Throws<DomainException>(() => NewIncident(Details(affected: Hurt, affectedName: "Visitor")));
        Assert.Throws<DomainException>(() => NewIncident(Details(IncidentType.Hazard, lostDays: 2)));
        Assert.Equal("Visitor", NewIncident(Details(affectedName: "Visitor")).AffectedName);
        Assert.Null(NewIncident(Details(IncidentType.NearMiss)).AffectedEmployeeId);
    }

    [Fact]
    public void Incident_time_cannot_be_future_or_local()
    {
        Assert.Throws<DomainException>(() => NewIncident(Details(affected: Hurt, at: Now.AddHours(1))));
        Assert.Throws<DomainException>(() => NewIncident(Details(affected: Hurt, at: DateTime.SpecifyKind(Now, DateTimeKind.Local))));
        Assert.Throws<DomainException>(() => NewIncident(Details(affected: Hurt, at: Now.AddYears(-6))));
    }

    [Fact]
    public void Hospital_or_fatal_makes_incident_reportable_and_fatal_is_critical()
    {
        var h = NewIncident(Details(affected: Hurt, treatment: IncidentTreatment.Hospitalised));
        Assert.True(h.IsReportable);
        var f = NewIncident(Details(affected: Hurt, treatment: IncidentTreatment.Fatal));
        Assert.Equal(SafetySeverity.Critical, f.Severity);
        Assert.True(f.IsLostTime);
        Assert.True(NewIncident(Details(affected: Hurt, lostDays: 3)).IsLostTime);
        Assert.False(NewIncident(Details(affected: Hurt, treatment: IncidentTreatment.FirstAid)).IsLostTime);
    }

    [Fact]
    public void Anonymous_needs_reporter_and_reporter_cannot_investigate()
    {
        Assert.Throws<DomainException>(() => SafetyIncident.Create(Tenant, "HS-1", Details(affected: Hurt), null, true, null, Now));
        var i = NewIncident(anonymous: true);
        Assert.Throws<DomainException>(() => i.AssignInvestigator(Reporter, "Danish", null, Now));
        i.AssignInvestigator(Investigator, "Laila", null, Now);
        Assert.Equal(IncidentStatus.Investigating, i.Status);
    }

    [Fact]
    public void Close_needs_no_open_actions_root_cause_and_authority_when_reportable()
    {
        var i = NewIncident(Details(affected: Hurt, reportable: true));
        Assert.Throws<DomainException>(() => i.Close("Done", 1, null, Now));
        Assert.Throws<DomainException>(() => i.Close("Done", 0, null, Now));
        i.SetRootCause(RootCauseCategory.UnsafeCondition, "Leaking dishwasher", null, Now);
        Assert.Throws<DomainException>(() => i.Close("Done", 0, null, Now));
        Assert.Throws<DomainException>(() => i.NotifyAuthority(Today.AddDays(1), null, null, Today, Now));
        i.NotifyAuthority(Today, "LAB-77", null, Today, Now);
        i.Close("Dishwasher fixed, mats added", 0, null, Now);
        Assert.Equal(IncidentStatus.Closed, i.Status);
        Assert.Throws<DomainException>(() => i.AddNote("late", null, Now));
        i.Reopen("Happened again", null, Now);
        Assert.Equal(IncidentStatus.Investigating, i.Status);
        Assert.Null(i.ClosedAt);
    }

    [Fact]
    public void Status_move_rejects_closed_reported_and_same()
    {
        var i = NewIncident();
        Assert.Throws<DomainException>(() => i.MoveTo(IncidentStatus.Closed, null, null, Now));
        Assert.Throws<DomainException>(() => i.MoveTo(IncidentStatus.Reported, null, null, Now));
        i.MoveTo(IncidentStatus.ActionPending, "Waiting for mats", null, Now);
        Assert.Throws<DomainException>(() => i.MoveTo(IncidentStatus.ActionPending, null, null, Now));
        Assert.Equal(new[] { IncidentEntryKind.Created, IncidentEntryKind.StatusChanged }, i.Entries.Select(e => e.Kind));
        Assert.True(i.Entries.Last().At > i.Entries.First().At);
    }

    [Fact]
    public void Action_flow_open_start_done_verify_and_reject()
    {
        var a = SafetyAction.Create(Tenant, new("Fix dishwasher", null, Hse, SafetySeverity.High, Today.AddDays(7)), Guid.NewGuid(), null, Today);
        Assert.False(a.IsOverdue(Today));
        Assert.True(a.IsOverdue(Today.AddDays(8)));
        Assert.Throws<DomainException>(() => a.Verify(true, null, Now));
        a.Start();
        a.Complete("Replaced hose", Now);
        a.Verify(false, null, Now);
        Assert.Equal(ActionStatus.Open, a.Status);
        Assert.Null(a.CompletedAt);
        a.Complete("Replaced hose and seal", Now);
        a.Verify(true, Hse, Now);
        Assert.Equal(ActionStatus.Verified, a.Status);
        Assert.Throws<DomainException>(() => a.Cancel(null));
    }

    [Fact]
    public void Action_cannot_have_both_sources_or_past_due()
    {
        Assert.Throws<DomainException>(() => SafetyAction.Create(Tenant, new("x", null, Hse, SafetySeverity.Low, Today), Guid.NewGuid(), Guid.NewGuid(), Today));
        Assert.Throws<DomainException>(() => SafetyAction.Create(Tenant, new("x", null, Hse, SafetySeverity.Low, Today.AddDays(-1)), null, null, Today));
        Assert.Throws<DomainException>(() => SafetyAction.Create(Tenant, new("x", null, Hse, SafetySeverity.Low, Today.AddYears(3)), null, null, Today));
    }

    [Fact]
    public void Checklist_items_must_be_unique_and_within_limit()
    {
        Assert.Throws<DomainException>(() => SafetyChecklist.Create(Tenant, "A", null, []));
        Assert.Throws<DomainException>(() => SafetyChecklist.Create(Tenant, "A", null, ["Exit clear", "exit clear"]));
        Assert.Throws<DomainException>(() => SafetyChecklist.Create(Tenant, "A", null, Enumerable.Range(1, 61).Select(n => $"Item {n}").ToList()));
        Assert.Equal(3, SafetyChecklist.Samples.Count);
        Assert.All(SafetyChecklist.Samples, s => SafetyChecklist.Create(Tenant, s.Name, s.Description, s.Items));
    }

    [Fact]
    public void Inspection_copies_lines_and_scores_pass_over_answered()
    {
        var c = Checklist("Extinguishers charged", "Exits unlocked", "Alarm tested");
        var i = WithId(SafetyInspection.Schedule(Tenant, "hi-0001", "Q4 fire check", c, new(Investigator, null, "HQ", Today.AddDays(3), 90), Today));
        Assert.Equal("HI-0001", i.Code);
        Assert.Equal(3, i.Lines.Count);
        foreach (var l in i.Lines)
            typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(l, Guid.NewGuid());
        var lines = i.Lines.OrderBy(l => l.SortOrder).ToList();

        // har line ka jawab zaroori, fail par note zaroori
        Assert.Throws<DomainException>(() => i.Complete(new Dictionary<Guid, (InspectionResult, string?)>
            { [lines[0].Id] = (InspectionResult.Pass, null) }, null, Now));
        Assert.Throws<DomainException>(() => i.Complete(new Dictionary<Guid, (InspectionResult, string?)>
        {
            [lines[0].Id] = (InspectionResult.Pass, null), [lines[1].Id] = (InspectionResult.Fail, null), [lines[2].Id] = (InspectionResult.NotApplicable, null)
        }, null, Now));

        i.Complete(new Dictionary<Guid, (InspectionResult, string?)>
        {
            [lines[0].Id] = (InspectionResult.Pass, null), [lines[1].Id] = (InspectionResult.Fail, "Back door chained"),
            [lines[2].Id] = (InspectionResult.NotApplicable, null)
        }, "Mostly fine", Now);
        Assert.Equal(InspectionStatus.Completed, i.Status);
        Assert.Equal(50m, i.ScorePercent);
        Assert.Throws<DomainException>(() => i.Cancel(null));
    }

    [Fact]
    public void Inspection_rejects_past_due_bad_repeat_and_inactive_checklist()
    {
        var c = Checklist("One");
        Assert.Throws<DomainException>(() => SafetyInspection.Schedule(Tenant, "HI-1", "t", c, new(Investigator, null, null, Today.AddDays(-1), null), Today));
        Assert.Throws<DomainException>(() => SafetyInspection.Schedule(Tenant, "HI-1", "t", c, new(Investigator, null, null, Today, 3), Today));
        Assert.NotNull(SafetyInspection.Schedule(Tenant, "HI-1", "t", c, new(Investigator, null, null, Today.AddDays(-1), null), Today, allowPastDue: true));
        c.Update("Fire", null, false, ["One"]);
        Assert.Throws<DomainException>(() => SafetyInspection.Schedule(Tenant, "HI-1", "t", c, new(Investigator, null, null, Today, null), Today));
    }
}
