namespace HR.Shared.Library.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Audit page ka ek row: kisne, kab, kis record mein kya badla. Teeno services (Employee / Payroll / Identity)
/// ki apni "AuditLogs" table, shape ek. Sirf likha jata hai — app roles ke paas UPDATE / DELETE grant hi nahi.
/// Har service ka DbContext SaveChanges mein khud banata hai.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime At { get; private set; }
    public Guid? UserId { get; private set; }
    public string? UserName { get; private set; }
    public AuditAction Action { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string? EntityLabel { get; private set; }
    public Guid? SubjectEmployeeId { get; private set; }
    /// <summary>"POST api/leave/requests/{id}/approve" — kis kaam se ye badlaav aaya.</summary>
    public string? Operation { get; private set; }
    /// <summary>Ek request ke saare badlaav ek hi id — Activity mein ek line.</summary>
    public Guid CorrelationId { get; private set; }
    public string Changes { get; private set; } = "[]";

    private AuditLog() { }

    public static AuditLog From(Guid tenantId, AuditCapture c, AuditContext who)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            At = who.At,
            UserId = who.UserId,
            UserName = Cut(who.UserName, 150),
            Action = c.Action,
            EntityType = Cut(c.EntityType, 80) ?? string.Empty,
            EntityId = c.EntityId,
            EntityLabel = Cut(c.Label, 200),
            SubjectEmployeeId = c.SubjectEmployeeId,
            Operation = Cut(who.Operation, 200),
            CorrelationId = who.CorrelationId,
            Changes = AuditTrail.Serialize(c.Changes)
        };

    private static string? Cut(string? s, int max)
    {
        if (s is null || s.Length <= max)
            return s;
        return s[..max];
    }
}

/// <summary>Ek SaveChanges ka "kisne / kab / kis request se" — har captured row pe yahi lagta hai.</summary>
public sealed record AuditContext(DateTime At, Guid? UserId, string? UserName, string? Operation, Guid CorrelationId);

/// <summary>Har service ke OnModelCreating mein: modelBuilder.ApplyConfiguration(new AuditLogConfiguration()).</summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", t => t.HasCheckConstraint("CK_AuditLogs_Action", "\"Action\" BETWEEN 1 AND 3"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.UserName).HasMaxLength(150);
        builder.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.EntityLabel).HasMaxLength(200);
        builder.Property(x => x.Operation).HasMaxLength(200);
        builder.Property(x => x.Changes).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.At }).HasDatabaseName("IX_AuditLogs_TenantId_At").IsDescending(false, true);
        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId }).HasDatabaseName("IX_AuditLogs_TenantId_Entity");
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.At }).HasDatabaseName("IX_AuditLogs_TenantId_UserId_At");
        builder.HasIndex(x => new { x.TenantId, x.SubjectEmployeeId }).HasDatabaseName("IX_AuditLogs_TenantId_Subject")
            .HasFilter("\"SubjectEmployeeId\" IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.CorrelationId }).HasDatabaseName("IX_AuditLogs_TenantId_CorrelationId");
    }
}
