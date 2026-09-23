using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HR.Identity.API.Data.Configurations;

public class LoginAuditConfiguration : IEntityTypeConfiguration<LoginAudit>
{
    public void Configure(EntityTypeBuilder<LoginAudit> b)
    {
        b.ToTable("LoginAudit");
        b.HasKey(x => x.Id);

        b.Property(x => x.EmailAttempted).HasMaxLength(256).IsRequired();
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.FailureReason).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(45).IsUnicode(false);
        b.Property(x => x.UserAgent).HasMaxLength(300);

        b.HasIndex(x => new { x.UserId, x.OccurredAt }).IsDescending(false, true);
        b.HasIndex(x => new { x.TenantId, x.OccurredAt }).IsDescending(false, true);
    }
}
