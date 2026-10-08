namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Assets;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AssetCategoryConfiguration : IEntityTypeConfiguration<AssetCategory>
{
    public void Configure(EntityTypeBuilder<AssetCategory> b)
    {
        b.ToTable("AssetCategories");
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.Icon).HasMaxLength(40);

        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        b.ToTable("Assets", t =>
        {
            t.HasCheckConstraint("CK_Assets_Status", "\"Status\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Assets_Condition", "\"Condition\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Assets_PurchaseCost", "\"PurchaseCost\" IS NULL OR \"PurchaseCost\" >= 0");
            t.HasCheckConstraint("CK_Assets_Warranty", "\"WarrantyUntil\" IS NULL OR \"PurchaseDate\" IS NULL OR \"WarrantyUntil\" >= \"PurchaseDate\"");
        });
        b.ConfigureAuditable();

        b.Property(x => x.AssetTag).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Brand).HasMaxLength(100);
        b.Property(x => x.Model).HasMaxLength(100);
        b.Property(x => x.SerialNumber).HasMaxLength(100);
        b.Property(x => x.Vendor).HasMaxLength(150);
        b.Property(x => x.Notes).HasMaxLength(1000);

        b.HasOne<AssetCategory>().WithMany().HasForeignKey(x => x.AssetCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Assignments).WithOne().HasForeignKey(a => a.AssetId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.Events).WithOne().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.OpenAssignment);

        b.HasIndex(x => new { x.TenantId, x.AssetTag }).IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.SerialNumber }).IsUnique().HasFilter("\"SerialNumber\" IS NOT NULL AND \"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.AssetCategoryId);
        b.HasIndex(x => x.LocationId).HasFilter("\"LocationId\" IS NOT NULL");
    }
}

public sealed class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    public void Configure(EntityTypeBuilder<AssetAssignment> b)
    {
        b.ToTable("AssetAssignments", t =>
        {
            t.HasCheckConstraint("CK_AssetAssignments_ConditionOut", "\"ConditionOut\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_AssetAssignments_ConditionIn", "\"ConditionIn\" IS NULL OR \"ConditionIn\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_AssetAssignments_DueBack", "\"DueBack\" IS NULL OR \"DueBack\" >= \"AssignedOn\"");
            t.HasCheckConstraint("CK_AssetAssignments_Returned",
                "(\"ReturnedOn\" IS NULL AND \"ConditionIn\" IS NULL) OR (\"ReturnedOn\" >= \"AssignedOn\" AND \"ConditionIn\" IS NOT NULL)");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.AssignNote).HasMaxLength(500);
        b.Property(x => x.ReturnNote).HasMaxLength(500);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        // Ek asset ki sirf ek khuli assignment
        b.HasIndex(x => x.AssetId).IsUnique()
            .HasDatabaseName("UX_AssetAssignments_AssetId_Open")
            .HasFilter("\"ReturnedOn\" IS NULL");
        b.HasIndex(x => new { x.AssetId, x.AssignedOn });
        b.HasIndex(x => x.EmployeeId);
    }
}

public sealed class AssetEventConfiguration : IEntityTypeConfiguration<AssetEvent>
{
    public void Configure(EntityTypeBuilder<AssetEvent> b)
    {
        b.ToTable("AssetEvents", t =>
        {
            t.HasCheckConstraint("CK_AssetEvents_Type", "\"Type\" BETWEEN 1 AND 6");
            t.HasCheckConstraint("CK_AssetEvents_Status", "\"Status\" BETWEEN 1 AND 5");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.Detail).HasMaxLength(500);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.At });
        b.HasIndex(x => new { x.AssetId, x.At });
        b.HasIndex(x => x.EmployeeId).HasFilter("\"EmployeeId\" IS NOT NULL");
    }
}
