namespace HR.Employee.API.Tests.Engagement;

using System.Reflection;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Engagement;
using Xunit;

public class EngagementTests
{
    static readonly Guid Tenant = Guid.NewGuid(), Person = Guid.NewGuid(), Dept = Guid.NewGuid();
    static readonly DateTime Now = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    static readonly SurveyQuestionInput[] Pulse =
    [
        new("Recommend us?", QuestionType.Nps, null, true),
        new("Tools are good", QuestionType.Rating, null, true),
        new("Stay a year?", QuestionType.YesNo, null, false),
        new("Best part", QuestionType.Choice, ["Team", "Pay", "Work"], false),
        new("Anything else", QuestionType.Text, null, false)
    ];

    // DB ke baghair ids khali hoti hain; tests ke liye de do (EF Add par deta hai)
    static Survey Published(bool anonymous = true, SurveyAudience audience = SurveyAudience.Everyone, DateOnly? closesOn = null)
    {
        var s = Survey.Create(Tenant, new("Pulse", null, anonymous, audience, audience == SurveyAudience.Department ? Dept : null, closesOn), Pulse, Today);
        var id = typeof(Entity).GetProperty(nameof(Entity.Id))!;
        id.SetValue(s, Guid.NewGuid());
        foreach (var q in s.Questions)
            id.SetValue(q, Guid.NewGuid());
        s.Publish(Today, Now);
        return s;
    }

    static List<SurveyAnswerInput> Answers(Survey s, short nps = 9, short rating = 4, string? text = null)
        => [new(s.Questions[0].Id, nps, null), new(s.Questions[1].Id, rating, null), new(s.Questions[4].Id, null, text)];

    [Fact]
    public void Draft_survey_needs_questions_to_publish_and_locks_them_after()
    {
        var empty = Survey.Create(Tenant, new("Empty", null, true, SurveyAudience.Everyone, null, null), [], Today);
        Assert.Throws<DomainException>(() => empty.Publish(Today, Now));

        var s = Published();
        Assert.Equal(SurveyStatus.Open, s.Status);
        Assert.Equal(5, s.Questions.Count);
        Assert.Equal(["Team", "Pay", "Work"], s.Questions[3].OptionList);

        Assert.Throws<DomainException>(() => s.Update(new("Pulse", null, true, SurveyAudience.Everyone, null, null), Pulse, Today));
        Assert.Throws<DomainException>(() => s.Update(new("Pulse", null, false, SurveyAudience.Everyone, null, null), null, Today));
        s.Update(new("Pulse Q4", "New text", true, SurveyAudience.Everyone, null, Today.AddDays(7)), null, Today);
        Assert.Equal("Pulse Q4", s.Title);
        Assert.Equal(Today.AddDays(7), s.ClosesOn);
    }

    [Fact]
    public void Questions_are_validated()
    {
        SurveyDetails d = new("S", null, false, SurveyAudience.Everyone, null, null);
        Assert.Throws<DomainException>(() => Survey.Create(Tenant, d, [new("Pick", QuestionType.Choice, ["Only one"], true)], Today));
        Assert.Throws<DomainException>(() => Survey.Create(Tenant, d, [new("Pick", QuestionType.Choice, ["A", "a"], true)], Today));
        Assert.Throws<DomainException>(() => Survey.Create(Tenant, d, [new(" ", QuestionType.Rating, null, true)], Today));
        Assert.Throws<DomainException>(() => Survey.Create(Tenant, new("S", null, false, SurveyAudience.Department, null, null), [], Today));
        Assert.Throws<DomainException>(() => Survey.Create(Tenant, d with { ClosesOn = Today.AddDays(-1) }, [], Today));
        var many = Enumerable.Range(0, Survey.MaxQuestions + 1).Select(i => new SurveyQuestionInput($"Q{i}", QuestionType.Rating, null, false)).ToList();
        Assert.Throws<DomainException>(() => Survey.Create(Tenant, d, many, Today));
    }

    [Fact]
    public void Anonymous_response_does_not_keep_who_answered()
    {
        var s = Published(anonymous: true);
        var r = SurveyResponse.Create(s, Person, Dept, Answers(s, text: "  More coffee "), Today);
        Assert.Null(r.EmployeeId);
        Assert.Equal(Dept, r.DepartmentId);
        Assert.Equal(3, r.Answers.Count);
        Assert.Equal("More coffee", r.Answers[2].Text);

        var named = Published(anonymous: false);
        Assert.Equal(Person, SurveyResponse.Create(named, Person, Dept, Answers(named), Today).EmployeeId);
    }

    [Fact]
    public void Response_answers_are_checked_against_the_questions()
    {
        var s = Published();
        Assert.Throws<DomainException>(() => SurveyResponse.Create(s, Person, Dept, [new(s.Questions[1].Id, 4, null)], Today));
        Assert.Throws<DomainException>(() => SurveyResponse.Create(s, Person, Dept, Answers(s, nps: 11), Today));
        Assert.Throws<DomainException>(() => SurveyResponse.Create(s, Person, Dept, Answers(s, rating: 0), Today));
        Assert.Throws<DomainException>(() => SurveyResponse.Create(s, Person, Dept, [.. Answers(s), new(s.Questions[3].Id, 3, null)], Today));
        Assert.Throws<DomainException>(() => SurveyResponse.Create(s, Person, Dept, [.. Answers(s), new(s.Questions[2].Id, null, "yes")], Today));
        Assert.Throws<DomainException>(() => SurveyResponse.Create(s, Person, Dept, [.. Answers(s), new(Guid.NewGuid(), 1, null)], Today));
        Assert.Throws<DomainException>(() => SurveyResponse.Create(s, Person, Dept, [.. Answers(s), new(s.Questions[0].Id, 5, null)], Today));
        var ok = SurveyResponse.Create(s, Person, Dept, [.. Answers(s), new(s.Questions[2].Id, 1, null), new(s.Questions[3].Id, 2, null)], Today);
        Assert.Equal(4, ok.Answers.Count);
    }

    [Fact]
    public void Survey_stops_taking_answers_after_close_date_or_close()
    {
        var s = Published(closesOn: Today);
        Assert.True(s.IsAcceptingOn(Today));
        Assert.False(s.IsAcceptingOn(Today.AddDays(1)));
        Assert.Equal(SurveyStatus.Closed, s.StatusOn(Today.AddDays(1)));
        Assert.Throws<DomainException>(() => SurveyResponse.Create(s, Person, Dept, Answers(s), Today.AddDays(1)));

        s.Close(Now);
        Assert.Throws<DomainException>(() => SurveyResponse.Create(s, Person, Dept, Answers(s), Today));
        Assert.Throws<DomainException>(() => s.Reopen(Today.AddDays(-1), Today));
        s.Reopen(null, Today);
        Assert.True(s.IsAcceptingOn(Today.AddDays(30)));
    }

    [Fact]
    public void Department_survey_targets_only_that_department()
    {
        var s = Published(audience: SurveyAudience.Department);
        Assert.True(s.Targets(Dept));
        Assert.False(s.Targets(Guid.NewGuid()));
        Assert.True(Published().Targets(Guid.NewGuid()));
    }

    [Fact]
    public void Recognition_rules()
    {
        var other = Guid.NewGuid();
        var r = Recognition.Create(Tenant, Person, other, RecognitionValue.Teamwork, "  Stayed late to ship the release ");
        Assert.Equal("Stayed late to ship the release", r.Message);
        Assert.Throws<DomainException>(() => Recognition.Create(Tenant, Person, Person, RecognitionValue.Teamwork, "Me!"));
        Assert.Throws<DomainException>(() => Recognition.Create(Tenant, Person, other, (RecognitionValue)99, "Thanks a lot"));
        Assert.Throws<DomainException>(() => Recognition.Create(Tenant, Person, other, RecognitionValue.Helping, "ok"));
        Assert.Throws<DomainException>(() => Recognition.Create(Tenant, Person, other, RecognitionValue.Helping, new string('x', 501)));
    }
}
