namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Relations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class CaseStageConfiguration : IEntityTypeConfiguration<CaseStage>
{
    public void Configure(EntityTypeBuilder<CaseStage> b)
    {
        b.ToTable("ErCaseStages", t =>
        {
            t.HasCheckConstraint("CK_ErCaseStages_Phase", "\"Phase\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_ErCaseStages_TargetDays", "\"TargetDays\" IS NULL OR (\"TargetDays\" BETWEEN 1 AND 365 AND \"Phase\" <> 5)");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);

        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_ErCaseStages_TenantId_Name");
    }
}

public sealed class ErCaseConfiguration : IEntityTypeConfiguration<ErCase>
{
    public void Configure(EntityTypeBuilder<ErCase> b)
    {
        b.ToTable("ErCases", t =>
        {
            t.HasCheckConstraint("CK_ErCases_Type", "\"Type\" BETWEEN 1 AND 8");
            t.HasCheckConstraint("CK_ErCases_Severity", "\"Severity\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_ErCases_Confidentiality", "\"Confidentiality\" IN (1, 2)");
            t.HasCheckConstraint("CK_ErCases_Source", "\"Source\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_ErCases_Phase", "\"Phase\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_ErCases_Escalation", "\"EscalationLevel\" BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_ErCases_Hidden", "NOT \"ReporterHidden\" OR \"ReporterEmployeeId\" IS NOT NULL");
            t.HasCheckConstraint("CK_ErCases_Closed",
                "(\"Phase\" = 5) = (\"ClosedAt\" IS NOT NULL AND \"Outcome\" IS NOT NULL AND \"ActionTaken\" IS NOT NULL AND \"ResolutionSummary\" IS NOT NULL)");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Location).HasMaxLength(200);
        b.Property(x => x.ResolutionSummary).HasMaxLength(4000);

        b.HasOne<CaseStage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReporterEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.OwnerEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Members).WithOne().HasForeignKey(m => m.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Parties).WithOne().HasForeignKey(p => p.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Entries).WithOne().HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Evidence).WithOne().HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Members).HasField("members").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Parties).HasField("parties").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Entries).HasField("entries").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Evidence).HasField("evidence").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName("UX_ErCases_TenantId_Code");
        b.HasIndex(x => new { x.TenantId, x.Phase }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.StageId);
        b.HasIndex(x => x.ReporterEmployeeId).HasFilter("\"ReporterEmployeeId\" IS NOT NULL");
        b.HasIndex(x => x.OwnerEmployeeId).HasFilter("\"OwnerEmployeeId\" IS NOT NULL");
    }
}

public sealed class CaseMemberConfiguration : IEntityTypeConfiguration<CaseMember>
{
    public void Configure(EntityTypeBuilder<CaseMember> b)
    {
        b.ToTable("ErCaseMembers", t => t.HasCheckConstraint("CK_ErCaseMembers_Role", "\"Role\" BETWEEN 1 AND 3"));
        b.HasKey(x => x.Id);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CaseId, x.EmployeeId }).IsUnique().HasDatabaseName("UX_ErCaseMembers_Case_Employee");
        b.HasIndex(x => x.EmployeeId);
    }
}

public sealed class CasePartyConfiguration : IEntityTypeConfiguration<CaseParty>
{
    public void Configure(EntityTypeBuilder<CaseParty> b)
    {
        b.ToTable("ErCaseParties", t =>
        {
            t.HasCheckConstraint("CK_ErCaseParties_Role", "\"Role\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_ErCaseParties_Who", "(\"EmployeeId\" IS NULL) <> (\"ExternalName\" IS NULL)");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.ExternalName).HasMaxLength(150);
        b.Property(x => x.Note).HasMaxLength(500);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CaseId, x.EmployeeId }).IsUnique().HasFilter("\"EmployeeId\" IS NOT NULL")
            .HasDatabaseName("UX_ErCaseParties_Case_Employee");
        b.HasIndex(x => x.EmployeeId).HasFilter("\"EmployeeId\" IS NOT NULL");
    }
}

public sealed class CaseEntryConfiguration : IEntityTypeConfiguration<CaseEntry>
{
    public void Configure(EntityTypeBuilder<CaseEntry> b)
    {
        b.ToTable("ErCaseEntries", t => t.HasCheckConstraint("CK_ErCaseEntries_Kind", "\"Kind\" BETWEEN 1 AND 18"));
        b.HasKey(x => x.Id);

        b.Property(x => x.Text).HasMaxLength(4000);

        b.HasIndex(x => new { x.CaseId, x.At });
    }
}

public sealed class CaseEvidenceConfiguration : IEntityTypeConfiguration<CaseEvidence>
{
    public void Configure(EntityTypeBuilder<CaseEvidence> b)
    {
        b.ToTable("ErCaseEvidence");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);

        b.HasIndex(x => x.CaseId);
    }
}

public sealed class CaseAccessLogConfiguration : IEntityTypeConfiguration<CaseAccessLog>
{
    public void Configure(EntityTypeBuilder<CaseAccessLog> b)
    {
        b.ToTable("ErCaseAccessLogs", t => t.HasCheckConstraint("CK_ErCaseAccessLogs_Action", "\"Action\" BETWEEN 1 AND 3"));
        b.HasKey(x => x.Id);

        b.Property(x => x.UserName).HasMaxLength(150);
        b.Property(x => x.Note).HasMaxLength(1000);

        b.HasOne<ErCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CaseId, x.At });
        b.HasIndex(x => x.EmployeeId).HasFilter("\"EmployeeId\" IS NOT NULL");
    }
}
