namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Organization;
using HR.Employee.API.Domain.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class SafetyIncidentConfiguration : IEntityTypeConfiguration<SafetyIncident>
{
    public void Configure(EntityTypeBuilder<SafetyIncident> b)
    {
        b.ToTable("HsIncidents", t =>
        {
            t.HasCheckConstraint("CK_HsIncidents_Type", "\"Type\" BETWEEN 1 AND 8");
            t.HasCheckConstraint("CK_HsIncidents_Severity", "\"Severity\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_HsIncidents_Status", "\"Status\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_HsIncidents_Treatment", "\"Treatment\" IS NULL OR \"Treatment\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_HsIncidents_RootCause", "\"RootCauseCategory\" IS NULL OR \"RootCauseCategory\" BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_HsIncidents_LostDays", "\"LostDays\" BETWEEN 0 AND 999");
            t.HasCheckConstraint("CK_HsIncidents_Anonymous", "NOT \"Anonymous\" OR \"ReporterEmployeeId\" IS NOT NULL");
            t.HasCheckConstraint("CK_HsIncidents_Affected", "\"AffectedEmployeeId\" IS NULL OR \"AffectedName\" IS NULL");
            t.HasCheckConstraint("CK_HsIncidents_Closed",
                "(\"Status\" = 4) = (\"ClosedAt\" IS NOT NULL AND \"ClosureNote\" IS NOT NULL)");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Area).HasMaxLength(150);
        b.Property(x => x.AffectedName).HasMaxLength(120);
        b.Property(x => x.InjuryNature).HasMaxLength(120);
        b.Property(x => x.BodyPart).HasMaxLength(120);
        b.Property(x => x.Witnesses).HasMaxLength(500);
        b.Property(x => x.ImmediateAction).HasMaxLength(2000);
        b.Property(x => x.RootCause).HasMaxLength(4000);
        b.Property(x => x.AuthorityReference).HasMaxLength(100);
        b.Property(x => x.ClosureNote).HasMaxLength(2000);

        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReporterEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.AffectedEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.InvestigatorEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Entries).WithOne().HasForeignKey(e => e.IncidentId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Entries).HasField("entries").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName("UX_HsIncidents_TenantId_Code");
        b.HasIndex(x => new { x.TenantId, x.OccurredAt }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.LocationId).HasFilter("\"LocationId\" IS NOT NULL");
        b.HasIndex(x => x.DepartmentId).HasFilter("\"DepartmentId\" IS NOT NULL");
        b.HasIndex(x => x.ReporterEmployeeId).HasFilter("\"ReporterEmployeeId\" IS NOT NULL");
        b.HasIndex(x => x.AffectedEmployeeId).HasFilter("\"AffectedEmployeeId\" IS NOT NULL");
        b.HasIndex(x => x.InvestigatorEmployeeId).HasFilter("\"InvestigatorEmployeeId\" IS NOT NULL");
    }
}

public sealed class IncidentEntryConfiguration : IEntityTypeConfiguration<IncidentEntry>
{
    public void Configure(EntityTypeBuilder<IncidentEntry> b)
    {
        b.ToTable("HsIncidentEntries", t => t.HasCheckConstraint("CK_HsIncidentEntries_Kind", "\"Kind\" BETWEEN 1 AND 10"));
        b.HasKey(x => x.Id);

        b.Property(x => x.Text).HasMaxLength(4000);

        b.HasIndex(x => new { x.IncidentId, x.At });
    }
}

public sealed class SafetyActionConfiguration : IEntityTypeConfiguration<SafetyAction>
{
    public void Configure(EntityTypeBuilder<SafetyAction> b)
    {
        b.ToTable("HsActions", t =>
        {
            t.HasCheckConstraint("CK_HsActions_Priority", "\"Priority\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_HsActions_Status", "\"Status\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_HsActions_Source", "\"IncidentId\" IS NULL OR \"InspectionId\" IS NULL");
            t.HasCheckConstraint("CK_HsActions_Done", "\"Status\" NOT IN (3, 4) OR \"CompletedAt\" IS NOT NULL");
            t.HasCheckConstraint("CK_HsActions_Verified", "(\"Status\" = 4) = (\"VerifiedAt\" IS NOT NULL)");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CompletionNote).HasMaxLength(2000);

        b.HasOne<SafetyIncident>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SafetyInspection>().WithMany().HasForeignKey(x => x.InspectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.AssigneeEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.Status, x.DueOn }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.IncidentId).HasFilter("\"IncidentId\" IS NOT NULL");
        b.HasIndex(x => x.InspectionId).HasFilter("\"InspectionId\" IS NOT NULL");
        b.HasIndex(x => x.AssigneeEmployeeId);
    }
}

public sealed class SafetyChecklistConfiguration : IEntityTypeConfiguration<SafetyChecklist>
{
    public void Configure(EntityTypeBuilder<SafetyChecklist> b)
    {
        b.ToTable("HsChecklists");
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.ChecklistId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Items).HasField("items").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_HsChecklists_TenantId_Name");
    }
}

public sealed class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> b)
    {
        b.ToTable("HsChecklistItems");
        b.HasKey(x => x.Id);

        b.Property(x => x.Text).HasMaxLength(300).IsRequired();

        b.HasIndex(x => new { x.ChecklistId, x.SortOrder });
    }
}

public sealed class SafetyInspectionConfiguration : IEntityTypeConfiguration<SafetyInspection>
{
    public void Configure(EntityTypeBuilder<SafetyInspection> b)
    {
        b.ToTable("HsInspections", t =>
        {
            t.HasCheckConstraint("CK_HsInspections_Status", "\"Status\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_HsInspections_Repeat", "\"RepeatDays\" IS NULL OR \"RepeatDays\" BETWEEN 7 AND 365");
            t.HasCheckConstraint("CK_HsInspections_Score", "\"ScorePercent\" IS NULL OR \"ScorePercent\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_HsInspections_Completed", "(\"Status\" = 2) = (\"CompletedAt\" IS NOT NULL)");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Area).HasMaxLength(150);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.ScorePercent).HasPrecision(5, 1);

        b.HasOne<SafetyChecklist>().WithMany().HasForeignKey(x => x.ChecklistId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.InspectorEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.InspectionId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).HasField("lines").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName("UX_HsInspections_TenantId_Code");
        b.HasIndex(x => new { x.TenantId, x.Status, x.DueOn }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.ChecklistId).HasFilter("\"ChecklistId\" IS NOT NULL");
        b.HasIndex(x => x.LocationId).HasFilter("\"LocationId\" IS NOT NULL");
        b.HasIndex(x => x.InspectorEmployeeId);
    }
}

public sealed class InspectionLineConfiguration : IEntityTypeConfiguration<InspectionLine>
{
    public void Configure(EntityTypeBuilder<InspectionLine> b)
    {
        b.ToTable("HsInspectionLines", t =>
            t.HasCheckConstraint("CK_HsInspectionLines_Result", "\"Result\" IS NULL OR \"Result\" BETWEEN 1 AND 3"));
        b.HasKey(x => x.Id);

        b.Property(x => x.Text).HasMaxLength(300).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);

        b.HasIndex(x => new { x.InspectionId, x.SortOrder });
    }
}
