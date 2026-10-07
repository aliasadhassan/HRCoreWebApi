using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HR.Identity.API.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.ConfigureAuditable();

        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(500);
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.AvatarUrl).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.NormalizedEmail }).IsUnique()
         .HasFilter("\"IsDeleted\" = false");
        // H5: ek email = ek company. Login email se user dhoondta hai, is liye poore system mein unique
        b.HasIndex(x => x.NormalizedEmail).IsUnique()
         .HasDatabaseName("UX_Users_NormalizedEmail")
         .HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique()
         .HasFilter("[EmployeeId] IS NOT NULL AND [IsDeleted] = 0");

        b.HasOne(x => x.Tenant).WithMany(t => t.Users)
         .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
