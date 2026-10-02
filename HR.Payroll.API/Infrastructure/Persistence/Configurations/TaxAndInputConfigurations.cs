namespace HR.Payroll.API.Infrastructure.Persistence.Configurations;

using HR.Payroll.API.Domain.Employees;
using HR.Payroll.API.Domain.Inputs;
using HR.Payroll.API.Domain.Setup;
using HR.Payroll.API.Domain.Tax;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class TaxRegimeConfiguration : IEntityTypeConfiguration<TaxRegime>
{
    public void Configure(EntityTypeBuilder<TaxRegime> b)
    {
        b.ToTable("TaxRegimes", t => t.HasCheckConstraint("CK_TaxRegimes_Month", "\"TaxYearStartMonth\" BETWEEN 1 AND 12"));
        b.ConfigureAudit();
        b.Property(x => x.CountryCode).AsCountryCode().IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();

        b.HasMany(x => x.Slabs).WithOne().HasForeignKey(s => s.TaxRegimeId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Slabs).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.CountryCode, x.EffectiveFrom }).IncludeProperties(x => new { x.TenantId, x.EffectiveTo, x.IsActive });
    }
}

public sealed class TaxSlabConfiguration : IEntityTypeConfiguration<TaxSlab>
{
    public void Configure(EntityTypeBuilder<TaxSlab> b)
    {
        b.ToTable("TaxSlabs", t => t.HasCheckConstraint("CK_TaxSlab", "\"ToAmount\" IS NULL OR \"ToAmount\" > \"FromAmount\""));
        b.HasKey(x => x.Id);
        b.Property(x => x.RatePercent).AsRate();
        b.HasIndex(x => new { x.TaxRegimeId, x.FromAmount }).IsUnique();
    }
}

public sealed class ContributionRuleConfiguration : IEntityTypeConfiguration<ContributionRule>
{
    public void Configure(EntityTypeBuilder<ContributionRule> b)
    {
        b.ToTable("ContributionRules");
        b.ConfigureAudit();
        b.Property(x => x.CountryCode).AsCountryCode().IsRequired();
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.EmployeeRatePercent).AsRate();
        b.Property(x => x.EmployerRatePercent).AsRate();

        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.EmployeeComponentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.EmployerComponentId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CountryCode, x.Code, x.EffectiveFrom });
    }
}

public sealed class EmployeeTaxOpeningBalanceConfiguration : IEntityTypeConfiguration<EmployeeTaxOpeningBalance>
{
    public void Configure(EntityTypeBuilder<EmployeeTaxOpeningBalance> b)
    {
        b.ToTable("EmployeeTaxOpeningBalances");
        b.ConfigureAudit();
        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeId, x.TaxYearStart }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class PayrollInputConfiguration : IEntityTypeConfiguration<PayrollInput>
{
    public void Configure(EntityTypeBuilder<PayrollInput> b)
    {
        b.ToTable("PayrollInputs", t => t.HasCheckConstraint("CK_PayrollInputs_Value", "\"Amount\" IS NOT NULL OR \"Quantity\" IS NOT NULL"));
        b.ConfigureAudit();
        b.Property(x => x.Quantity).HasPrecision(9, 2);
        b.Property(x => x.SourceReference).HasMaxLength(100).IsRequired();
        b.Property(x => x.Remarks).HasMaxLength(500);

        b.HasOne<PayPeriod>().WithMany().HasForeignKey(x => x.PayPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.PayComponentId).OnDelete(DeleteBehavior.Restrict);

        // Same import dobara chale to duplicate nahi
        b.HasIndex(x => new { x.PayPeriodId, x.EmployeeId, x.PayComponentId, x.Source, x.SourceReference })
         .IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class UnpaidLeaveDayConfiguration : IEntityTypeConfiguration<UnpaidLeaveDay>
{
    public void Configure(EntityTypeBuilder<UnpaidLeaveDay> b)
    {
        b.ToTable("EmployeeUnpaidLeaveDays", t => t.HasCheckConstraint("CK_UnpaidLeaveDay", "\"DayFraction\" IN (0.50, 1.00)"));
        b.HasKey(x => x.Id);
        b.Property(x => x.DayFraction).HasPrecision(3, 2);
        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.LeaveRequestId, x.LeaveDate }).IsUnique();   // event redelivery safe
        b.HasIndex(x => new { x.EmployeeId, x.LeaveDate });
    }
}

public sealed class EmployeeLoanConfiguration : IEntityTypeConfiguration<EmployeeLoan>
{
    public void Configure(EntityTypeBuilder<EmployeeLoan> b)
    {
        b.ToTable("EmployeeLoans", t => t.HasCheckConstraint("CK_Loans_Amounts",
            "[PrincipalAmount] > 0 AND [InstallmentAmount] > 0 AND [OutstandingAmount] BETWEEN 0 AND [PrincipalAmount]"));
        b.ConfigureAudit();
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.Property(x => x.Remarks).HasMaxLength(500);

        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.DeductionComponentId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.EmployeeId).HasFilter("[Status] = 1 AND \"IsDeleted\" = false");
    }
}

