using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HR.Identity.API.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(x => x.Id);
        b.Ignore(x => x.IsActive);

        b.Property(x => x.TokenHash).HasMaxLength(32).IsRequired();   // varbinary(32)
        b.Property(x => x.CreatedByIp).HasMaxLength(45).IsUnicode(false);
        b.Property(x => x.RevokedByIp).HasMaxLength(45).IsUnicode(false);
        b.Property(x => x.UserAgent).HasMaxLength(300);
        b.Property(x => x.RevokedReason).HasMaxLength(100);

        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId).HasFilter("[RevokedAt] IS NULL").IncludeProperties(x => x.ExpiresAt);
        b.HasIndex(x => x.FamilyId);

        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
         .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> b)
    {
        b.ToTable("UserTokens");
        b.HasKey(x => x.Id);

        b.Property(x => x.TokenHash).HasMaxLength(32).IsRequired();

        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.Purpose }).HasFilter("[UsedAt] IS NULL");

        b.HasOne(x => x.User).WithMany(u => u.Tokens)
         .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
