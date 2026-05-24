using HR.Employee.API.Domain.Common;
using HR.Employee.API.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace HR.Employee.API.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Employees> Employees => Set<Employees>();
        
        // Configure unique constraints and relationships
        // For example, ensure that the Email field in User is unique
        // This is where E.F Core decides the database schema
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Employees>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Employees>()
                .HasIndex(u => u.Cnic)
                .IsUnique();

            base.OnModelCreating(modelBuilder);

            // Ignore Domain Events property
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(IHasDomainEvents).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Ignore(nameof(IHasDomainEvents.DomainEvents));
                }
            }

            // MassTransit Outbox Tables
            modelBuilder.AddTransactionalOutboxEntities(); // This will add the necessary tables for MassTransit's Outbox pattern
        }
    }
}