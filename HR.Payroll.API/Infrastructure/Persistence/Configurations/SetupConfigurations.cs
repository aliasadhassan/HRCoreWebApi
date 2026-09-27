namespace HR.Payroll.API.Infrastructure.Persistence.Configurations;

using HR.Payroll.API.Domain.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PayrollSettingsConfiguration : IEntityTypeConfiguration<PayrollSettings>
{
    public void Configure(EntityTypeBuilder<PayrollSettings> b)
    {
        b.ToTable("PayrollSettings");
        b.ConfigureAudit();
        b.Property(x => x.BaseCurrency).AsCurrency().IsRequired();
        b.Property(x => x.PayslipNumberPrefix).HasMaxLength(10).IsRequired();
        b.HasIndex(x => x.TenantId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class PayGroupConfiguration : IEntityTypeConfiguration<PayGroup>
{
    public void Configure(EntityTypeBuilder<PayGroup> b)
    {
        b.ToTable("PayGroups");
        b.ConfigureAudit();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.CountryCode).AsCountryCode().IsRequired();
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class PayPeriodConfiguration : IEntityTypeConfiguration<PayPeriod>
{
    public void Configure(EntityTypeBuilder<PayPeriod> b)
    {
        b.ToTable("PayPeriods", t => t.HasCheckConstraint("CK_PayPeriods_Dates", "[PeriodEnd] >= [PeriodStart] AND [PayDate] >= [PeriodStart]"));
        b.ConfigureAudit();
        b.HasOne<PayGroup>().WithMany().HasForeignKey(x => x.PayGroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PayGroupId, x.PeriodStart }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class PayComponentConfiguration : IEntityTypeConfiguration<PayComponent>
{
    public void Configure(EntityTypeBuilder<PayComponent> b)
    {
        b.ToTable("PayComponents");
        b.ConfigureAudit();
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.SystemCode).HasMaxLength(30);
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.DefaultBaseComponentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.TenantId, x.SystemCode }).IsUnique().HasFilter("[SystemCode] IS NOT NULL AND [IsDeleted] = 0");
    }
}
