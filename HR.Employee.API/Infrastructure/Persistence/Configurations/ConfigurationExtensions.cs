namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal static class ConfigurationExtensions
{
    public static void ConfigureAuditable<T>(this EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RowVersion).IsRowVersion();
    }

    public static PropertyBuilder<TProperty> AsCountryCode<TProperty>(this PropertyBuilder<TProperty> property)
        => property.HasMaxLength(2).IsFixedLength().IsUnicode(false);
}
