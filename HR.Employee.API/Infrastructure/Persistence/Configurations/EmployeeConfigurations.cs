namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("Employees", t =>
            t.HasCheckConstraint("CK_Employees_ExitAfterJoin", "[ExitDate] IS NULL OR [ExitDate] >= [JoiningDate]"));
        b.ConfigureAuditable();

        b.Property(x => x.EmployeeCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.MiddleName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.NationalityCode).AsCountryCode();
        b.Property(x => x.NationalIdNumber).HasMaxLength(256);
        b.Property(x => x.PhotoStorageKey).HasMaxLength(500);
        b.Property(x => x.WorkEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.PersonalEmail).HasMaxLength(256);
        b.Property(x => x.WorkPhone).HasMaxLength(50);
        b.Property(x => x.PersonalPhone).HasMaxLength(50);
        b.Property(x => x.ExitReason).HasMaxLength(500);

        // Value object → isi table ke columns
        b.OwnsOne(x => x.Address, a =>
        {
            a.Property(p => p.Line1).HasColumnName("AddressLine1").HasMaxLength(300);
            a.Property(p => p.Line2).HasColumnName("AddressLine2").HasMaxLength(300);
            a.Property(p => p.City).HasColumnName("City").HasMaxLength(100);
            a.Property(p => p.State).HasColumnName("State").HasMaxLength(100);
            a.Property(p => p.PostalCode).HasColumnName("PostalCode").HasMaxLength(20);
            a.Property(p => p.CountryCode).HasColumnName("CountryCode").AsCountryCode();
        });

        b.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Designation).WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.EmergencyContacts).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.JobHistory).WithOne().HasForeignKey(h => h.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Documents).WithOne().HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.Navigation(x => x.EmergencyContacts).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.JobHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.EmployeeCode }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.TenantId, x.WorkEmail }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL AND [IsDeleted] = 0");
        b.HasIndex(x => new { x.TenantId, x.EmploymentStatus }).IncludeProperties(x => new { x.DepartmentId, x.LocationId });
    }
}

public sealed class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmergencyContact> b)
    {
        b.ToTable("EmployeeEmergencyContacts");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Relationship).HasMaxLength(50).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(50).IsRequired();
        b.Property(x => x.AlternatePhone).HasMaxLength(50);
    }
}

public sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> b)
    {
        b.ToTable("EmployeeDocuments");
        b.ConfigureAuditable();

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();

        // Passport/visa/contract expiry alerts
        b.HasIndex(x => new { x.TenantId, x.ExpiryDate }).HasFilter("[ExpiryDate] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public sealed class JobHistoryEntryConfiguration : IEntityTypeConfiguration<JobHistoryEntry>
{
    public void Configure(EntityTypeBuilder<JobHistoryEntry> b)
    {
        b.ToTable("EmployeeJobHistory");
        b.ConfigureAuditable();

        b.Property(x => x.Remarks).HasMaxLength(500);
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveDate }).IsDescending(false, true);
    }
}
