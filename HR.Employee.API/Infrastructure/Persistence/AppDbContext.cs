using HR.Employee.API.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HR.Employee.API.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Automatically finds all entities implementing IHasDomainEvents and ignores the property globally
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(IHasDomainEvents).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType).Ignore(nameof(IHasDomainEvents.DomainEvents));
                }
            }
        }
    }
}
