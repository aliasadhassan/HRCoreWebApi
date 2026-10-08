namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Helpdesk;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class HelpdeskCategoryConfiguration : IEntityTypeConfiguration<HelpdeskCategory>
{
    public void Configure(EntityTypeBuilder<HelpdeskCategory> b)
    {
        b.ToTable("HelpdeskCategories", t =>
        {
            t.HasCheckConstraint("CK_HelpdeskCategories_Hours", "\"ResolutionHours\" IS NULL OR \"ResolutionHours\" BETWEEN 1 AND 2000");
            t.HasCheckConstraint("CK_HelpdeskCategories_Confidential", "NOT (\"IsConfidential\" AND \"NeedsManagerApproval\")");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.Icon).HasMaxLength(40);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.DefaultAssigneeEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique()
            .HasDatabaseName("UX_HelpdeskCategories_TenantId_Name").HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.DefaultAssigneeEmployeeId).HasFilter("\"DefaultAssigneeEmployeeId\" IS NOT NULL");
    }
}

public sealed class HelpdeskTicketConfiguration : IEntityTypeConfiguration<HelpdeskTicket>
{
    public void Configure(EntityTypeBuilder<HelpdeskTicket> b)
    {
        b.ToTable("HelpdeskTickets", t =>
        {
            t.HasCheckConstraint("CK_HelpdeskTickets_Status", "\"Status\" BETWEEN 1 AND 8");
            t.HasCheckConstraint("CK_HelpdeskTickets_Priority", "\"Priority\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_HelpdeskTickets_Rating", "\"SatisfactionRating\" IS NULL OR \"SatisfactionRating\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_HelpdeskTickets_Approval", "\"Status\" <> 1 OR \"ApproverEmployeeId\" IS NOT NULL");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Link).HasMaxLength(1000);
        b.Property(x => x.DecisionNote).HasMaxLength(1000);
        b.Property(x => x.Resolution).HasMaxLength(2000);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HelpdeskCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ApproverEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.AssigneeEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Activities).WithOne().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Activities).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique()
            .HasDatabaseName("UX_HelpdeskTickets_TenantId_Code").HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.EmployeeId);
        b.HasIndex(x => x.CategoryId);
        b.HasIndex(x => x.ApproverEmployeeId).HasFilter("\"ApproverEmployeeId\" IS NOT NULL");
        b.HasIndex(x => x.AssigneeEmployeeId).HasFilter("\"AssigneeEmployeeId\" IS NOT NULL");
    }
}

public sealed class TicketActivityConfiguration : IEntityTypeConfiguration<TicketActivity>
{
    public void Configure(EntityTypeBuilder<TicketActivity> b)
    {
        b.ToTable("TicketActivities", t =>
        {
            t.HasCheckConstraint("CK_TicketActivities_Kind", "\"Kind\" BETWEEN 1 AND 8");
            t.HasCheckConstraint("CK_TicketActivities_Status", "\"ToStatus\" BETWEEN 1 AND 8 AND (\"FromStatus\" IS NULL OR \"FromStatus\" BETWEEN 1 AND 8)");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.Note).HasMaxLength(4000);

        b.HasIndex(x => new { x.TicketId, x.At });
        b.HasIndex(x => new { x.TenantId, x.At });
    }
}
