namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ChecklistTemplateConfiguration : IEntityTypeConfiguration<ChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplate> b)
    {
        b.ToTable("ChecklistTemplates", t => t.HasCheckConstraint("CK_ChecklistTemplates_Kind", "\"Kind\" IN (1, 2)"));
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasMany(x => x.Tasks).WithOne().HasForeignKey(t => t.ChecklistTemplateId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.Kind, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
        // Har kind ka sirf ek default
        b.HasIndex(x => new { x.TenantId, x.Kind }).IsUnique()
            .HasDatabaseName("UX_ChecklistTemplates_TenantId_Kind_Default")
            .HasFilter("\"IsDefault\" = true AND \"IsDeleted\" = false");
    }
}

public sealed class ChecklistTemplateTaskConfiguration : IEntityTypeConfiguration<ChecklistTemplateTask>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplateTask> b)
    {
        b.ToTable("ChecklistTemplateTasks", t =>
        {
            t.HasCheckConstraint("CK_ChecklistTemplateTasks_Offset", "\"DueOffsetDays\" BETWEEN -365 AND 365");
            t.HasCheckConstraint("CK_ChecklistTemplateTasks_Owner", "\"Owner\" BETWEEN 1 AND 6");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);

        b.HasIndex(x => new { x.ChecklistTemplateId, x.SortOrder });
    }
}

public sealed class LifecycleCaseConfiguration : IEntityTypeConfiguration<LifecycleCase>
{
    public void Configure(EntityTypeBuilder<LifecycleCase> b)
    {
        b.ToTable("LifecycleCases", t =>
        {
            t.HasCheckConstraint("CK_LifecycleCases_Kind", "\"Kind\" IN (1, 2)");
            t.HasCheckConstraint("CK_LifecycleCases_Status", "\"Status\" IN (1, 2, 3)");
            t.HasCheckConstraint("CK_LifecycleCases_Exit",
                "\"Kind\" = 1 OR (\"ExitType\" IS NOT NULL AND \"NoticeDate\" IS NOT NULL AND \"Reason\" IS NOT NULL AND \"AnchorDate\" >= \"NoticeDate\")");
            t.HasCheckConstraint("CK_LifecycleCases_Closed", "(\"Status\" = 1) = (\"ClosedAt\" IS NULL)");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.InterviewNotes).HasMaxLength(4000);
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ChecklistTemplate>().WithMany().HasForeignKey(x => x.ChecklistTemplateId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Tasks).WithOne().HasForeignKey(t => t.LifecycleCaseId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Ek employee ka ek kind ka sirf ek chalta case
        b.HasIndex(x => new { x.EmployeeId, x.Kind }).IsUnique()
            .HasDatabaseName("UX_LifecycleCases_EmployeeId_Kind_Open")
            .HasFilter("\"Status\" = 1 AND \"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.Kind, x.Status }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.ChecklistTemplateId).HasFilter("\"ChecklistTemplateId\" IS NOT NULL");
    }
}

public sealed class LifecycleTaskConfiguration : IEntityTypeConfiguration<LifecycleTask>
{
    public void Configure(EntityTypeBuilder<LifecycleTask> b)
    {
        b.ToTable("LifecycleTasks", t =>
        {
            t.HasCheckConstraint("CK_LifecycleTasks_Owner", "\"Owner\" BETWEEN 1 AND 6");
            t.HasCheckConstraint("CK_LifecycleTasks_Status", "\"Status\" IN (1, 2, 3)");
            t.HasCheckConstraint("CK_LifecycleTasks_Closed", "(\"Status\" = 1) = (\"CompletedAt\" IS NULL)");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Note).HasMaxLength(1000);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.AssigneeEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.LifecycleCaseId, x.SortOrder });
        // "My tasks": assignee ke khule tasks
        b.HasIndex(x => x.AssigneeEmployeeId).HasFilter("\"AssigneeEmployeeId\" IS NOT NULL AND \"Status\" = 1");
    }
}
