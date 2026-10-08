namespace HR.Payroll.API.Infrastructure.Persistence.Configurations;

using HR.Payroll.API.Domain.Employees;
using HR.Payroll.API.Domain.Inputs;
using HR.Payroll.API.Domain.Rewards;
using HR.Payroll.API.Domain.Salaries;
using HR.Payroll.API.Domain.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Benefits & rewards. Tenant composite FKs + RLS migration ke raw SQL mein (EF composite FKs track nahi karta).

public sealed class BenefitPlanConfiguration : IEntityTypeConfiguration<BenefitPlan>
{
    public void Configure(EntityTypeBuilder<BenefitPlan> b)
    {
        b.ToTable("BenefitPlans", t =>
        {
            t.HasCheckConstraint("CK_BenefitPlans_Costs",
                "\"EmployerMonthlyCost\" >= 0 AND \"EmployeeMonthlyCost\" >= 0 AND \"DependentMonthlyCost\" >= 0");
            t.HasCheckConstraint("CK_BenefitPlans_Dependents", "\"MaxDependents\" BETWEEN 0 AND 10");
            t.HasCheckConstraint("CK_BenefitPlans_Order", "\"SortOrder\" BETWEEN 0 AND 999");
        });
        b.ConfigureAudit();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(150);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();

        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.DeductionComponentId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class BenefitEnrolmentConfiguration : IEntityTypeConfiguration<BenefitEnrolment>
{
    public void Configure(EntityTypeBuilder<BenefitEnrolment> b)
    {
        b.ToTable("BenefitEnrolments", t =>
        {
            t.HasCheckConstraint("CK_BenefitEnrolments_Costs", "\"EmployerMonthlyCost\" >= 0 AND \"EmployeeMonthlyCost\" >= 0");
            t.HasCheckConstraint("CK_BenefitEnrolments_Dependents", "\"Dependents\" BETWEEN 0 AND 10");
            t.HasCheckConstraint("CK_BenefitEnrolments_Dates", "\"EndDate\" IS NULL OR (\"StartDate\" IS NOT NULL AND \"EndDate\" >= \"StartDate\")");
            t.HasCheckConstraint("CK_BenefitEnrolments_Active", "\"Status\" NOT IN (2, 3) OR \"StartDate\" IS NOT NULL");
        });
        b.ConfigureAudit();
        b.Property(x => x.EmployeeNote).HasMaxLength(500);
        b.Property(x => x.DecisionNote).HasMaxLength(500);
        b.Ignore(x => x.IsLive);

        b.HasOne<BenefitPlan>().WithMany().HasForeignKey(x => x.BenefitPlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        // Ek employee ek plan mein ek hi zinda (requested / active) enrolment
        b.HasIndex(x => new { x.BenefitPlanId, x.EmployeeId }).IsUnique().HasFilter("\"Status\" IN (1, 2) AND \"IsDeleted\" = false");
        b.HasIndex(x => new { x.EmployeeId, x.Status });
    }
}

public sealed class SalaryRevisionConfiguration : IEntityTypeConfiguration<SalaryRevision>
{
    public void Configure(EntityTypeBuilder<SalaryRevision> b)
    {
        b.ToTable("SalaryRevisions", t =>
        {
            t.HasCheckConstraint("CK_SalaryRevisions_Amounts", "\"CurrentAmount\" > 0 AND \"ProposedAmount\" > 0");
            t.HasCheckConstraint("CK_SalaryRevisions_Applied", "\"Status\" <> 2 OR \"AppliedSalaryId\" IS NOT NULL");
        });
        b.ConfigureAudit();
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.Property(x => x.NewTitle).HasMaxLength(150);
        b.Property(x => x.Justification).HasMaxLength(1000);
        b.Property(x => x.DecisionNote).HasMaxLength(500);
        b.Ignore(x => x.ChangePercent);

        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeSalary>().WithMany().HasForeignKey(x => x.CurrentSalaryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeSalary>().WithMany().HasForeignKey(x => x.AppliedSalaryId).OnDelete(DeleteBehavior.Restrict);

        // Ek employee ki ek hi pending tajweez
        b.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("\"Status\" = 1 AND \"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.Status, x.EffectiveFrom });
    }
}

public sealed class BonusAwardConfiguration : IEntityTypeConfiguration<BonusAward>
{
    public void Configure(EntityTypeBuilder<BonusAward> b)
    {
        b.ToTable("BonusAwards", t =>
        {
            t.HasCheckConstraint("CK_BonusAwards_Amount", "\"Amount\" > 0");
            t.HasCheckConstraint("CK_BonusAwards_Approved", "\"Status\" NOT IN (2, 5) OR \"PayoutMethod\" IS NOT NULL");
        });
        b.ConfigureAudit();
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.DecisionNote).HasMaxLength(500);

        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.PayComponentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollInput>().WithMany().HasForeignKey(x => x.PayrollInputId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.EmployeeId, x.CreatedAt });
        b.HasIndex(x => x.BatchId).HasFilter("\"BatchId\" IS NOT NULL");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"Status\" IN (1, 2) AND \"IsDeleted\" = false");
    }
}
