namespace HR.Payroll.API.Infrastructure.Persistence.Configurations;

using HR.Payroll.API.Domain.Employees;
using HR.Payroll.API.Domain.Salaries;
using HR.Payroll.API.Domain.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PayrollEmployeeConfiguration : IEntityTypeConfiguration<PayrollEmployee>
{
    public void Configure(EntityTypeBuilder<PayrollEmployee> b)
    {
        b.ToTable("PayrollEmployees");
        b.ConfigureAudit();

        // Id = Employee API ka EmployeeId â€” hum generate nahi karte
        b.Property(x => x.Id).HasColumnName("EmployeeId").ValueGeneratedNever();

        b.Property(x => x.EmployeeCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(300).IsRequired();
        b.Property(x => x.WorkEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.DepartmentName).HasMaxLength(150);
        b.Property(x => x.DesignationTitle).HasMaxLength(150);
        b.Property(x => x.EmploymentType).HasMaxLength(50).IsRequired();
        b.Property(x => x.BankName).HasMaxLength(100);
        b.Property(x => x.BankAccountTitle).HasMaxLength(150);
        b.Property(x => x.BankAccountNumber).HasMaxLength(256);
        b.Property(x => x.Iban).HasMaxLength(256);
        b.Property(x => x.TaxIdentifier).HasMaxLength(256);

        b.HasOne<PayGroup>().WithMany().HasForeignKey(x => x.PayGroupId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.EmployeeCode }).IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.PayGroupId }).IncludeProperties(x => new { x.IsActive, x.JoiningDate, x.ExitDate });
    }
}

public sealed class SalaryGradeConfiguration : IEntityTypeConfiguration<SalaryGrade>
{
    public void Configure(EntityTypeBuilder<SalaryGrade> b)
    {
        b.ToTable("SalaryGrades");
        b.ConfigureAudit();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class SalaryTemplateConfiguration : IEntityTypeConfiguration<SalaryTemplate>
{
    public void Configure(EntityTypeBuilder<SalaryTemplate> b)
    {
        b.ToTable("SalaryTemplates");
        b.ConfigureAudit();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasOne<SalaryGrade>().WithMany().HasForeignKey(x => x.SalaryGradeId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SalaryTemplateId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SalaryTemplateLineConfiguration : IEntityTypeConfiguration<SalaryTemplateLine>
{
    public void Configure(EntityTypeBuilder<SalaryTemplateLine> b)
    {
        b.ToTable("SalaryTemplateLines");
        b.HasKey(x => x.Id);
        b.Property(x => x.Percentage).AsRate();
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.PayComponentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.BaseComponentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SalaryTemplateId, x.PayComponentId }).IsUnique();
    }
}

public sealed class EmployeeSalaryConfiguration : IEntityTypeConfiguration<EmployeeSalary>
{
    public void Configure(EntityTypeBuilder<EmployeeSalary> b)
    {
        b.ToTable("EmployeeSalaries", t =>
        {
            t.HasCheckConstraint("CK_EmpSalary_Amount", "\"BasisAmount\" > 0");
            t.HasCheckConstraint("CK_EmpSalary_Dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
        });
        b.ConfigureAudit();
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.Property(x => x.Remarks).HasMaxLength(500);

        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SalaryTemplate>().WithMany().HasForeignKey(x => x.SalaryTemplateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SalaryGrade>().WithMany().HasForeignKey(x => x.SalaryGradeId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Overrides).WithOne().HasForeignKey(o => o.EmployeeSalaryId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Overrides).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Ek employee ki ek hi CURRENT salary
        b.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("\"EffectiveTo\" IS NULL AND \"IsDeleted\" = false")
         .HasDatabaseName("UX_EmpSalary_Current");
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom }).IsDescending(false, true);
    }
}

public sealed class EmployeeSalaryComponentConfiguration : IEntityTypeConfiguration<EmployeeSalaryComponent>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryComponent> b)
    {
        b.ToTable("EmployeeSalaryComponents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Percentage).AsRate();
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.PayComponentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.BaseComponentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeSalaryId, x.PayComponentId }).IsUnique();
    }
}

