namespace HR.Payroll.API.Tests.Rewards;

using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Rewards;
using Xunit;

public class RewardsTests
{
    static readonly Guid Tenant = Guid.NewGuid(), Employee = Guid.NewGuid(), User = Guid.NewGuid(), Component = Guid.NewGuid();
    static readonly DateOnly Start = new(2026, 10, 1);

    static BenefitPlan Plan(bool open = true, short maxDependents = 4) => BenefitPlan.Create(Tenant, new BenefitPlanData(
        "Medical", BenefitType.Health, null, null, "PKR", 8000, 2000, maxDependents == 0 ? 0 : 3000, maxDependents, null, open, true, 10));

    [Fact]
    public void Enrolment_prices_dependents_and_snapshots_costs()
    {
        var plan = Plan();
        var e = BenefitEnrolment.Request(Tenant, plan, Employee, 2, "family");
        Assert.Equal(EnrolmentStatus.Requested, e.Status);
        Assert.Equal(14000, e.EmployerMonthlyCost);
        Assert.Equal(2000, e.EmployeeMonthlyCost);

        e.Approve(plan, Start, 3, User, null);
        Assert.Equal(EnrolmentStatus.Active, e.Status);
        Assert.Equal(17000, e.EmployerMonthlyCost);
        Assert.True(e.CoversDate(Start.AddDays(30)));
        Assert.False(e.CoversDate(Start.AddDays(-1)));
    }

    [Fact]
    public void Enrolment_rules()
    {
        Assert.Throws<DomainException>(() => BenefitEnrolment.Request(Tenant, Plan(open: false), Employee, 0, null));
        Assert.Throws<DomainException>(() => BenefitEnrolment.Request(Tenant, Plan(maxDependents: 0), Employee, 1, null));

        var plan = Plan();
        var e = BenefitEnrolment.Request(Tenant, plan, Employee, 0, null);
        Assert.Throws<DomainException>(() => e.Cancel(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => e.End(Start, User, null));
        e.Approve(plan, Start, 0, User, null);
        Assert.Throws<DomainException>(() => e.End(Start.AddDays(-1), User, null));
        e.End(Start.AddDays(10), User, "left");
        Assert.False(e.CoversDate(Start.AddDays(11)));
        Assert.True(e.CoversDate(Start.AddDays(10)));
    }

    [Fact]
    public void Plan_validation()
    {
        Assert.Throws<DomainException>(() => BenefitPlan.Create(Tenant, new BenefitPlanData(
            "Life", BenefitType.Life, null, null, "PKR", 1, 0, 0, 0, Component, true, true, 10)));   // nothing to deduct
        Assert.Throws<DomainException>(() => BenefitPlan.Create(Tenant, new BenefitPlanData(
            "Life", BenefitType.Life, null, null, "PKR", 1, 0, 50, 0, null, true, true, 10)));       // dependent cost, no dependents
    }

    [Fact]
    public void Salary_revision_percent_and_guards()
    {
        var r = SalaryRevision.Propose(Tenant, Employee, SalaryChangeReason.Increment, Guid.NewGuid(), "PKR", SalaryBasis.Monthly,
            200000, new DateOnly(2024, 1, 1), 230000, Start, null, null);
        Assert.Equal(15, r.ChangePercent);
        Assert.Throws<DomainException>(() => r.Edit(SalaryChangeReason.Joining, 230000, Start, new DateOnly(2024, 1, 1), null, null));
        Assert.Throws<DomainException>(() => r.Edit(SalaryChangeReason.Increment, 200000, Start, new DateOnly(2024, 1, 1), null, null));
        Assert.Throws<DomainException>(() => r.Edit(SalaryChangeReason.Increment, 230000, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 1), null, null));
        Assert.Throws<DomainException>(() => r.Edit(SalaryChangeReason.Increment, 1_100_000, Start, new DateOnly(2024, 1, 1), null, null));

        r.MarkApplied(Guid.NewGuid(), User, null);
        Assert.Equal(SalaryRevisionStatus.Applied, r.Status);
        Assert.Throws<DomainException>(() => r.Reject(User, "no"));
    }

    [Fact]
    public void Bonus_payout_flow()
    {
        var b = BonusAward.Propose(Tenant, Employee, BonusType.Festival, "Eid bonus", "PKR", 50000.456m, Component, null, null);
        Assert.Equal(50000.46m, b.Amount);
        Assert.Throws<DomainException>(() => b.MarkPaid());

        b.Approve(PayoutMethod.Payroll, User, null);
        b.AttachPayrollInput(Guid.NewGuid());
        Assert.Throws<DomainException>(() => b.MarkPaid());   // payroll bonus paid via payslip
        Assert.Throws<DomainException>(() => b.Cancel(User));

        var direct = BonusAward.Propose(Tenant, Employee, BonusType.Spot, "Spot", "PKR", 1000, Component, null, null);
        direct.Approve(PayoutMethod.Direct, User, null);
        Assert.Throws<DomainException>(() => direct.AttachPayrollInput(Guid.NewGuid()));
        direct.MarkPaid();
        Assert.Equal(BonusStatus.Paid, direct.Status);
    }
}
