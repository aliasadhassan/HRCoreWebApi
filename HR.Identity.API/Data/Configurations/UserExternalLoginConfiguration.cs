using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HR.Identity.API.Data.Configurations;

public class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> b)
    {
        b.ToTable("UserExternalLogins");
        b.HasKey(x => x.Id);

        b.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        b.Property(x => x.ProviderKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.ProviderTenantId).HasMaxLength(100);

        b.HasIndex(x => new { x.Provider, x.ProviderKey }).IsUnique();

        b.HasOne(x => x.User).WithMany(u => u.ExternalLogins)
         .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
