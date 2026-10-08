namespace HR.Payroll.API.Infrastructure.Persistence.Configurations;

using HR.Payroll.API.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs", t => t.HasCheckConstraint("CK_AuditLogs_Action", "\"Action\" BETWEEN 1 AND 3"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.UserName).HasMaxLength(150);
        b.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
        b.Property(x => x.EntityLabel).HasMaxLength(200);
        b.Property(x => x.Operation).HasMaxLength(200);
        b.Property(x => x.Changes).HasColumnType("jsonb").IsRequired();

        b.HasIndex(x => new { x.TenantId, x.At }).HasDatabaseName("IX_AuditLogs_TenantId_At").IsDescending(false, true);
        b.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId }).HasDatabaseName("IX_AuditLogs_TenantId_Entity");
        b.HasIndex(x => new { x.TenantId, x.UserId, x.At }).HasDatabaseName("IX_AuditLogs_TenantId_UserId_At");
        b.HasIndex(x => new { x.TenantId, x.SubjectEmployeeId }).HasDatabaseName("IX_AuditLogs_TenantId_Subject")
            .HasFilter("\"SubjectEmployeeId\" IS NOT NULL");
        b.HasIndex(x => new { x.TenantId, x.CorrelationId }).HasDatabaseName("IX_AuditLogs_TenantId_CorrelationId");
    }
}
