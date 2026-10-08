namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ReviewCycleConfiguration : IEntityTypeConfiguration<ReviewCycle>
{
    public void Configure(EntityTypeBuilder<ReviewCycle> b)
    {
        b.ToTable("ReviewCycles", t =>
        {
            t.HasCheckConstraint("CK_ReviewCycles_Status", "\"Status\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_ReviewCycles_Period", "\"PeriodEnd\" >= \"PeriodStart\"");
            t.HasCheckConstraint("CK_ReviewCycles_SelfDue",
                "(\"IncludeSelfReview\" AND \"SelfReviewDue\" IS NOT NULL AND \"SelfReviewDue\" <= \"ManagerReviewDue\") OR (NOT \"IncludeSelfReview\" AND \"SelfReviewDue\" IS NULL)");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class PerformanceReviewConfiguration : IEntityTypeConfiguration<PerformanceReview>
{
    public void Configure(EntityTypeBuilder<PerformanceReview> b)
    {
        b.ToTable("PerformanceReviews", t =>
        {
            t.HasCheckConstraint("CK_PerformanceReviews_Status", "\"Status\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_PerformanceReviews_SelfRating", "\"SelfRating\" IS NULL OR \"SelfRating\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_PerformanceReviews_ManagerRating", "\"ManagerRating\" IS NULL OR \"ManagerRating\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_PerformanceReviews_Reviewer", "\"ReviewerEmployeeId\" IS NULL OR \"ReviewerEmployeeId\" <> \"EmployeeId\"");
            t.HasCheckConstraint("CK_PerformanceReviews_Shared",
                "\"Status\" < 3 OR (\"ManagerRating\" IS NOT NULL AND \"ManagerSubmittedAt\" IS NOT NULL)");
        });
        b.ConfigureAuditable();

        b.Property(x => x.SelfSummary).HasMaxLength(4000);
        b.Property(x => x.ManagerSummary).HasMaxLength(4000);
        b.Property(x => x.Strengths).HasMaxLength(2000);
        b.Property(x => x.Improvements).HasMaxLength(2000);
        b.Property(x => x.EmployeeComment).HasMaxLength(2000);

        b.HasOne<ReviewCycle>().WithMany().HasForeignKey(x => x.ReviewCycleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReviewerEmployeeId).OnDelete(DeleteBehavior.Restrict);

        // Ek cycle mein ek employee ka ek review
        b.HasIndex(x => new { x.TenantId, x.ReviewCycleId, x.EmployeeId }).IsUnique()
            .HasDatabaseName("UX_PerformanceReviews_Cycle_Employee").HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.ReviewCycleId);
        b.HasIndex(x => x.EmployeeId);
        b.HasIndex(x => x.ReviewerEmployeeId).HasFilter("\"ReviewerEmployeeId\" IS NOT NULL");
    }
}

public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> b)
    {
        b.ToTable("Goals", t =>
        {
            t.HasCheckConstraint("CK_Goals_Status", "\"Status\" BETWEEN 1 AND 6");
            t.HasCheckConstraint("CK_Goals_Progress", "\"Progress\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Goals_Weight", "\"Weight\" IS NULL OR \"Weight\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Goals_Dates", "\"StartDate\" IS NULL OR \"DueDate\" >= \"StartDate\"");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReviewCycle>().WithMany().HasForeignKey(x => x.ReviewCycleId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.CheckIns).WithOne().HasForeignKey(c => c.GoalId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.CheckIns).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Status }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.EmployeeId);
        b.HasIndex(x => x.ReviewCycleId).HasFilter("\"ReviewCycleId\" IS NOT NULL");
    }
}

public sealed class GoalCheckInConfiguration : IEntityTypeConfiguration<GoalCheckIn>
{
    public void Configure(EntityTypeBuilder<GoalCheckIn> b)
    {
        b.ToTable("GoalCheckIns", t =>
        {
            t.HasCheckConstraint("CK_GoalCheckIns_Status", "\"Status\" BETWEEN 1 AND 6");
            t.HasCheckConstraint("CK_GoalCheckIns_Progress", "\"Progress\" BETWEEN 0 AND 100");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.Note).HasMaxLength(1000);

        b.HasIndex(x => new { x.GoalId, x.At });
        b.HasIndex(x => new { x.TenantId, x.At });
    }
}
