using HR.Employee.API;
using HR.Employee.API.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HR.Employee.API
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options): base(options) { }
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
        }
    }
}
