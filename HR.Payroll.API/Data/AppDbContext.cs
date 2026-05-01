using HR.Payroll.API;
using HR.Payroll.API.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HR.Payroll.API.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Payroll> Payrolls => Set<Payroll>();

        // Configure unique constraints and relationships
        // For example, ensure that the Email field in User is unique
        // This is where E.F Core decides the database schema in OnModelCreating method of ModelBuilder class
    }
}
