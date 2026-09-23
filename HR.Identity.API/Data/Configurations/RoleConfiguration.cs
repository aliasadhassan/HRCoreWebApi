using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HR.Identity.API.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique()
         .HasFilter("[IsDeleted] = 0");

        b.HasOne(x => x.Tenant).WithMany()
         .HasForeignKey(x => x.TenantId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}
