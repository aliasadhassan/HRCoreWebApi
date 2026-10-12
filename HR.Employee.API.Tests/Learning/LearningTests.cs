namespace HR.Employee.API.Tests.Learning;

using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Learning;
using Xunit;

public class LearningTests
{
    static readonly Guid Tenant = Guid.NewGuid(), User = Guid.NewGuid(), Person = Guid.NewGuid(), SkillId = Guid.NewGuid();
    static readonly DateTime Now = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    static Course NewCourse(bool self = true, short? days = 14, bool active = true)
        => Course.Create(Tenant, new("SQL basics", CourseCategory.Technical, CourseFormat.Online, null, null, null, 4.25m, SkillId, 3, self, days, active));

    [Fact]
    public void Enrollment_gets_default_due_date_and_moves_through_progress_to_completion()
    {
        var e = CourseEnrollment.Create(NewCourse(), Person, EnrollmentSource.Self, null, User, Today);
        Assert.Equal(Today.AddDays(14), e.DueOn);
        Assert.Equal(EnrollmentStatus.NotStarted, e.Status);
        Assert.Null(e.AssignedByUserId);

        e.SetProgress(50, Now);
        Assert.Equal(EnrollmentStatus.InProgress, e.Status);
        Assert.Equal(Now, e.StartedAt);

        e.Complete(Today, 90, 4, " Good ", null, Today, Now);
        Assert.Equal(EnrollmentStatus.Completed, e.Status);
        Assert.Equal(100, e.Progress);
        Assert.Equal("Good", e.Feedback);
        Assert.False(e.IsOpen);
        Assert.Throws<DomainException>(() => e.Cancel(Now));
    }

    [Fact]
    public void Course_rounds_duration_and_rejects_level_without_skill()
    {
        var c = NewCourse();
        Assert.Equal(4.3m, c.DurationHours);
        Assert.Throws<DomainException>(() => Course.Create(Tenant,
            new("X", CourseCategory.Other, CourseFormat.Video, null, null, null, null, null, 2, true, null, true)));
        Assert.Throws<DomainException>(() => Course.Create(Tenant,
            new("X", CourseCategory.Other, CourseFormat.Video, null, null, "ftp://x", null, null, null, true, null, true)));
    }

    [Fact]
    public void Self_enrol_needs_an_open_active_course_and_due_date_not_in_past()
    {
        Assert.Throws<DomainException>(() => CourseEnrollment.Create(NewCourse(self: false), Person, EnrollmentSource.Self, null, User, Today));
        Assert.Throws<DomainException>(() => CourseEnrollment.Create(NewCourse(active: false), Person, EnrollmentSource.Assigned, null, User, Today));
        Assert.Throws<DomainException>(() => CourseEnrollment.Create(NewCourse(), Person, EnrollmentSource.Assigned, Today.AddDays(-1), User, Today));

        var assigned = CourseEnrollment.Create(NewCourse(self: false, days: null), Person, EnrollmentSource.Assigned, null, User, Today);
        Assert.Null(assigned.DueOn);
        Assert.Equal(User, assigned.AssignedByUserId);
    }

    [Fact]
    public void Overdue_only_while_open()
    {
        var e = CourseEnrollment.Create(NewCourse(), Person, EnrollmentSource.Self, Today, User, Today);
        Assert.True(e.IsOverdueOn(Today.AddDays(1)));
        Assert.False(e.IsOverdueOn(Today));
        e.Complete(Today, null, null, null, null, Today, Now);
        Assert.False(e.IsOverdueOn(Today.AddDays(5)));
    }

    [Fact]
    public void Completion_rules()
    {
        var e = CourseEnrollment.Create(NewCourse(), Person, EnrollmentSource.Self, null, User, Today);
        Assert.Throws<DomainException>(() => e.SetProgress(100, Now));
        Assert.Throws<DomainException>(() => e.Complete(Today.AddDays(1), null, null, null, null, Today, Now));
        Assert.Throws<DomainException>(() => e.Complete(Today, 101, null, null, null, Today, Now));
        Assert.Throws<DomainException>(() => e.Complete(Today, null, 6, null, null, Today, Now));
    }

    [Fact]
    public void Course_completion_only_raises_a_skill()
    {
        var s = EmployeeSkill.Create(Person, SkillId, 4, SkillSource.Assessed, User, Now);
        Assert.False(s.RaiseTo(3, User, Now));
        Assert.Equal(SkillSource.Assessed, s.Source);
        Assert.True(s.RaiseTo(5, User, Now));
        Assert.Equal(5, s.Level);
        Assert.Equal(SkillSource.Course, s.Source);
        Assert.Throws<DomainException>(() => s.Rate(0, SkillSource.Self, User, Now));
        Assert.Throws<DomainException>(() => RoleSkill.Create(Guid.NewGuid(), SkillId, 6));
    }

    [Fact]
    public void Career_goal_cannot_target_current_role_and_resets_after_achieved()
    {
        var current = Guid.NewGuid();
        var target = Guid.NewGuid();
        var g = CareerGoal.Create(Person);
        Assert.Throws<DomainException>(() => g.Set(current, current, null, null, User, Today, Now));
        Assert.Throws<DomainException>(() => g.Set(target, current, Today.AddDays(-1), null, User, Today, Now));

        g.Set(target, current, Today.AddMonths(6), "Lead a team", User, Today, Now);
        g.MarkAchieved(Today, User, Now);
        Assert.Equal(CareerGoalStatus.Achieved, g.Status);
        Assert.Throws<DomainException>(() => g.MarkAchieved(Today, User, Now));

        g.Set(Guid.NewGuid(), target, null, null, User, Today, Now);
        Assert.Equal(CareerGoalStatus.Active, g.Status);
        Assert.Null(g.AchievedOn);
    }
}
