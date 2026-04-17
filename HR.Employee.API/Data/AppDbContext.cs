using HR.Employee.API;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HR.Employee.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options): base(options) { }
        public DbSet<Employee> Employees => Set<Employee>();

        // Configure unique constraints and relationships
        // For example, ensure that the Email field in User is unique
        // This is where E.F Core decides the database schema
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HR.Employee>()
                .HasIndex(u => u.Email)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}
