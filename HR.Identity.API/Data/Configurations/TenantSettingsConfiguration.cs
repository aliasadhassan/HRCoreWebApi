using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HR.Identity.API.Data.Configurations;

public class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> b)
    {
        b.ToTable("TenantSettings", t =>
            t.HasCheckConstraint("CK_TS_FYStart", "[FiscalYearStartMonth] BETWEEN 1 AND 12"));
        b.HasKey(x => x.TenantId);

        b.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
        b.Property(x => x.DateFormat).HasMaxLength(20).IsRequired();
        b.Property(x => x.Locale).HasMaxLength(10).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
