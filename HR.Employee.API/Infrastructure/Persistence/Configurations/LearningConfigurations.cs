namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Learning;
using HR.Employee.API.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> b)
    {
        b.ToTable("Skills", t => t.HasCheckConstraint("CK_Skills_Category", "\"Category\" BETWEEN 1 AND 7"));
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique()
            .HasDatabaseName("UX_Skills_TenantId_Name").HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("Courses", t =>
        {
            t.HasCheckConstraint("CK_Courses_Category", "\"Category\" BETWEEN 1 AND 8");
            t.HasCheckConstraint("CK_Courses_Format", "\"Format\" BETWEEN 1 AND 6");
            t.HasCheckConstraint("CK_Courses_Duration", "\"DurationHours\" IS NULL OR (\"DurationHours\" > 0 AND \"DurationHours\" <= 1000)");
            t.HasCheckConstraint("CK_Courses_SkillLevel", "\"SkillLevel\" IS NULL OR (\"SkillId\" IS NOT NULL AND \"SkillLevel\" BETWEEN 1 AND 5)");
            t.HasCheckConstraint("CK_Courses_CompleteWithin", "\"CompleteWithinDays\" IS NULL OR \"CompleteWithinDays\" BETWEEN 1 AND 365");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.LinkUrl).HasMaxLength(1000);
        b.Property(x => x.DurationHours).HasPrecision(6, 1);

        b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.Title }).IsUnique()
            .HasDatabaseName("UX_Courses_TenantId_Title").HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.SkillId).HasFilter("\"SkillId\" IS NOT NULL");
    }
}

public sealed class CourseEnrollmentConfiguration : IEntityTypeConfiguration<CourseEnrollment>
{
    public void Configure(EntityTypeBuilder<CourseEnrollment> b)
    {
        b.ToTable("CourseEnrollments", t =>
        {
            t.HasCheckConstraint("CK_CourseEnrollments_Source", "\"Source\" BETWEEN 1 AND 2");
            t.HasCheckConstraint("CK_CourseEnrollments_Status", "\"Status\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_CourseEnrollments_Progress", "\"Progress\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_CourseEnrollments_Score", "\"Score\" IS NULL OR \"Score\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_CourseEnrollments_Rating", "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_CourseEnrollments_Completed", "\"Status\" <> 3 OR \"CompletedOn\" IS NOT NULL");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Feedback).HasMaxLength(1000);
        b.Property(x => x.CertificateUrl).HasMaxLength(1000);

        b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CourseId, x.EmployeeId }).IsUnique()
            .HasDatabaseName("UX_CourseEnrollments_Open").HasFilter("\"IsDeleted\" = false AND \"Status\" IN (1, 2)");
        b.HasIndex(x => new { x.EmployeeId, x.Status }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.Status, x.DueOn }).HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class EmployeeSkillConfiguration : IEntityTypeConfiguration<EmployeeSkill>
{
    public void Configure(EntityTypeBuilder<EmployeeSkill> b)
    {
        b.ToTable("EmployeeSkills", t =>
        {
            t.HasCheckConstraint("CK_EmployeeSkills_Level", "\"Level\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_EmployeeSkills_Source", "\"Source\" BETWEEN 1 AND 3");
        });
        b.HasKey(x => x.Id);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.EmployeeId, x.SkillId }).IsUnique().HasDatabaseName("UX_EmployeeSkills_Employee_Skill");
        b.HasIndex(x => x.SkillId);
    }
}

public sealed class RoleSkillConfiguration : IEntityTypeConfiguration<RoleSkill>
{
    public void Configure(EntityTypeBuilder<RoleSkill> b)
    {
        b.ToTable("RoleSkills", t => t.HasCheckConstraint("CK_RoleSkills_Level", "\"RequiredLevel\" BETWEEN 1 AND 5"));
        b.HasKey(x => x.Id);

        b.HasOne<Designation>().WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.DesignationId, x.SkillId }).IsUnique().HasDatabaseName("UX_RoleSkills_Designation_Skill");
        b.HasIndex(x => x.SkillId);
    }
}

public sealed class CareerGoalConfiguration : IEntityTypeConfiguration<CareerGoal>
{
    public void Configure(EntityTypeBuilder<CareerGoal> b)
    {
        b.ToTable("CareerGoals", t =>
        {
            t.HasCheckConstraint("CK_CareerGoals_Status", "\"Status\" BETWEEN 1 AND 2");
            t.HasCheckConstraint("CK_CareerGoals_Achieved", "\"Status\" <> 2 OR \"AchievedOn\" IS NOT NULL");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Designation>().WithMany().HasForeignKey(x => x.TargetDesignationId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.EmployeeId).IsUnique().HasDatabaseName("UX_CareerGoals_EmployeeId");
        b.HasIndex(x => x.TargetDesignationId);
    }
}
