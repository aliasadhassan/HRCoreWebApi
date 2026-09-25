namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> b)
    {
        b.ToTable("Locations");
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.CountryCode).AsCountryCode().IsRequired();
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.AddressLine).HasMaxLength(300);
        b.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasOne<Department>().WithMany().HasForeignKey(x => x.ParentDepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.HeadEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> b)
    {
        b.ToTable("Designations");
        b.ConfigureAuditable();

        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => new { x.TenantId, x.Title }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
