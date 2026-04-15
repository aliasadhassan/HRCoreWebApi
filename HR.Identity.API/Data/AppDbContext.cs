using HR.Identity.API.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshTokenConfiguration> RefreshTokenConfiguration => Set<RefreshTokenConfiguration>();

        // Configure unique constraints and relationships
        // For example, ensure that the Email field in User is unique
        // This is where E.F Core decides the database schema
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}
