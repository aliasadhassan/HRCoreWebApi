namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Organization;
using HR.Employee.API.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> b)
    {
        b.ToTable("Policies", t =>
        {
            t.HasCheckConstraint("CK_Policies_Status", "\"Status\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_Policies_Category", "\"Category\" BETWEEN 1 AND 8");
            t.HasCheckConstraint("CK_Policies_AckDays", "\"AcknowledgeWithinDays\" IS NULL OR \"AcknowledgeWithinDays\" BETWEEN 1 AND 365");
            t.HasCheckConstraint("CK_Policies_ReviewMonths", "\"ReviewEveryMonths\" IS NULL OR \"ReviewEveryMonths\" BETWEEN 1 AND 60");
            t.HasCheckConstraint("CK_Policies_Published", "\"Status\" = 1 OR \"CurrentVersion\" > 0");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(500);
        b.Property(x => x.Body).HasMaxLength(Policy.BodyMaxLength);
        b.Property(x => x.DocumentUrl).HasMaxLength(1000);

        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.OwnerEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.PolicyId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.Title }).IsUnique()
            .HasDatabaseName("UX_Policies_TenantId_Title").HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.DepartmentId).HasFilter("\"DepartmentId\" IS NOT NULL");
        b.HasIndex(x => x.OwnerEmployeeId).HasFilter("\"OwnerEmployeeId\" IS NOT NULL");
    }
}

public sealed class PolicyVersionConfiguration : IEntityTypeConfiguration<PolicyVersion>
{
    public void Configure(EntityTypeBuilder<PolicyVersion> b)
    {
        b.ToTable("PolicyVersions", t => t.HasCheckConstraint("CK_PolicyVersions_Number", "\"Number\" >= 1"));
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Body).HasMaxLength(Policy.BodyMaxLength);
        b.Property(x => x.DocumentUrl).HasMaxLength(1000);
        b.Property(x => x.ChangeSummary).HasMaxLength(500);

        b.HasIndex(x => new { x.PolicyId, x.Number }).IsUnique().HasDatabaseName("UX_PolicyVersions_PolicyId_Number");
        b.HasIndex(x => new { x.TenantId, x.PublishedAt });
    }
}

public sealed class PolicyAcknowledgementConfiguration : IEntityTypeConfiguration<PolicyAcknowledgement>
{
    public void Configure(EntityTypeBuilder<PolicyAcknowledgement> b)
    {
        b.ToTable("PolicyAcknowledgements", t => t.HasCheckConstraint("CK_PolicyAcknowledgements_Version", "\"VersionNumber\" >= 1"));
        b.HasKey(x => x.Id);

        b.HasOne<Policy>().WithMany().HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.PolicyId, x.EmployeeId, x.VersionNumber }).IsUnique()
            .HasDatabaseName("UX_PolicyAcknowledgements_Policy_Employee_Version");
        b.HasIndex(x => x.EmployeeId);
    }
}

public sealed class ComplianceObligationConfiguration : IEntityTypeConfiguration<ComplianceObligation>
{
    public void Configure(EntityTypeBuilder<ComplianceObligation> b)
    {
        b.ToTable("ComplianceObligations", t =>
        {
            t.HasCheckConstraint("CK_ComplianceObligations_Category", "\"Category\" BETWEEN 1 AND 6");
            t.HasCheckConstraint("CK_ComplianceObligations_Frequency", "\"Frequency\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_ComplianceObligations_Remind", "\"RemindDaysBefore\" BETWEEN 0 AND 90");
            t.HasCheckConstraint("CK_ComplianceObligations_DueDay", "\"DueDay\" BETWEEN 1 AND 31");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Authority).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.LinkUrl).HasMaxLength(1000);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.OwnerEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Filings).WithOne().HasForeignKey(f => f.ObligationId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Filings).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.Title }).IsUnique()
            .HasDatabaseName("UX_ComplianceObligations_TenantId_Title").HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.NextDueOn }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.OwnerEmployeeId).HasFilter("\"OwnerEmployeeId\" IS NOT NULL");
    }
}

public sealed class ComplianceFilingConfiguration : IEntityTypeConfiguration<ComplianceFiling>
{
    public void Configure(EntityTypeBuilder<ComplianceFiling> b)
    {
        b.ToTable("ComplianceFilings");
        b.HasKey(x => x.Id);

        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.EvidenceUrl).HasMaxLength(1000);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasIndex(x => new { x.ObligationId, x.PeriodDueOn }).IsUnique().HasDatabaseName("UX_ComplianceFilings_Obligation_Period");
        b.HasIndex(x => new { x.TenantId, x.FiledOn });
    }
}
