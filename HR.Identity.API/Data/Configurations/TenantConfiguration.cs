using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HR.Identity.API.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("Tenants");
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        b.Property(x => x.LegalName).HasMaxLength(300);
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.PrimaryEmail).HasMaxLength(256);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.Plan).HasMaxLength(50).IsRequired();

        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.EntraTenantId).IsUnique().HasFilter("[EntraTenantId] IS NOT NULL");

        b.HasOne(x => x.Settings).WithOne(s => s.Tenant)
         .HasForeignKey<TenantSettings>(s => s.TenantId);
    }
}
