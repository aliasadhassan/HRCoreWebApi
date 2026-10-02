namespace HR.Payroll.API.Infrastructure.Persistence.Configurations;

using HR.Payroll.API.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal static class ConfigurationExtensions
{
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> builder) where T : AuditableBase
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.RowVersion).IsRowVersion();
    }

    public static PropertyBuilder<TProperty> AsCountryCode<TProperty>(this PropertyBuilder<TProperty> property)
        => property.HasMaxLength(2).IsFixedLength().IsUnicode(false);

    public static PropertyBuilder<TProperty> AsCurrency<TProperty>(this PropertyBuilder<TProperty> property)
        => property.HasMaxLength(3).IsFixedLength().IsUnicode(false);

    public static PropertyBuilder<TProperty> AsRate<TProperty>(this PropertyBuilder<TProperty> property)
        => property.HasPrecision(9, 4);
}
