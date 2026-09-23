using HR.Identity.API.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HR.Identity.API.Data.Configurations;

public static class AuditableConfigExtensions
{
    public static void ConfigureAuditable<T>(this EntityTypeBuilder<T> b) where T : AuditableEntity
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasQueryFilter(x => !x.IsDeleted);               // soft-deleted rows auto-hide
    }
}
