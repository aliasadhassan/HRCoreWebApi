using HR.Identity.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HR.Identity.API.Data.Configurations;

/// <summary>Table SQL se bana hai: sql/license/08_tenant_subscriptions.sql (Identity ke EF migrations abhi nahi).</summary>
public class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> b)
    {
        b.ToTable("TenantSubscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.PlanCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.TenantId).IsUnique()
         .HasDatabaseName("UX_TenantSubscriptions_Current")
         .HasFilter("\"Status\" IN (1, 2, 3)");
        b.HasIndex(x => new { x.TenantId, x.StartDate });

        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TenantSubscription>().WithMany().HasForeignKey(x => x.RenewedFromId).OnDelete(DeleteBehavior.Restrict);
    }
}
