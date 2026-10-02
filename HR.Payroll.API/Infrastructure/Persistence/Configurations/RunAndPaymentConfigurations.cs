namespace HR.Payroll.API.Infrastructure.Persistence.Configurations;

using HR.Payroll.API.Domain.Employees;
using HR.Payroll.API.Domain.Inputs;
using HR.Payroll.API.Domain.Payments;
using HR.Payroll.API.Domain.Runs;
using HR.Payroll.API.Domain.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> b)
    {
        b.ToTable("PayrollRuns", t => t.HasCheckConstraint("CK_Runs_Status", "\"Status\" BETWEEN 1 AND 7"));
        b.ConfigureAudit();
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.Property(x => x.FailureReason).HasMaxLength(1000);

        b.HasOne<PayGroup>().WithMany().HasForeignKey(x => x.PayGroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayPeriod>().WithMany().HasForeignKey(x => x.PayPeriodId).OnDelete(DeleteBehavior.Restrict);

        // â­ Ek period ka ek hi regular run (cancelled chhod kar) â€” "salary 2X" yahin rukti hai
        b.HasIndex(x => x.PayPeriodId).IsUnique()
         .HasFilter("[RunType] = 1 AND [Status] <> 6 AND \"IsDeleted\" = false")
         .HasDatabaseName("UX_Runs_RegularPerPeriod");
        b.HasIndex(x => new { x.TenantId, x.Status }).IncludeProperties(x => new { x.PayGroupId, x.PayPeriodId });
    }
}

public sealed class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> b)
    {
        b.ToTable("Payslips", t => t.HasCheckConstraint("CK_Payslips_Net", "\"NetPay\" = \"GrossEarnings\" - \"TotalDeductions\""));
        b.ConfigureAudit();

        b.Property(x => x.PayslipNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.EmployeeCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.EmployeeName).HasMaxLength(300).IsRequired();
        b.Property(x => x.DepartmentName).HasMaxLength(150);
        b.Property(x => x.DesignationTitle).HasMaxLength(150);
        b.Property(x => x.BankAccountMasked).HasMaxLength(50);
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.Property(x => x.HoldReason).HasMaxLength(500);
        b.Property(x => x.PeriodDays).HasPrecision(5, 2);
        b.Property(x => x.PayableDays).HasPrecision(5, 2);
        b.Property(x => x.UnpaidLeaveDays).HasPrecision(5, 2);

        b.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PayslipId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        // â­ Ek run mein ek employee ki ek payslip
        b.HasIndex(x => new { x.PayrollRunId, x.EmployeeId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.PayslipNumber }).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.PeriodStart }).IsDescending(false, true)
         .IncludeProperties(x => new { x.NetPay, x.Status });
    }
}

public sealed class PayslipLineConfiguration : IEntityTypeConfiguration<PayslipLine>
{
    public void Configure(EntityTypeBuilder<PayslipLine> b)
    {
        b.ToTable("PayslipLines");
        b.HasKey(x => x.Id);
        b.Property(x => x.ComponentCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.ComponentName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(9, 2);
        b.Property(x => x.Rate).HasPrecision(18, 4);
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.PayComponentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LoanRepaymentConfiguration : IEntityTypeConfiguration<LoanRepayment>
{
    public void Configure(EntityTypeBuilder<LoanRepayment> b)
    {
        b.ToTable("LoanRepayments", t => t.HasCheckConstraint("CK_LoanRepayment", "\"Amount\" > 0"));
        b.ConfigureAudit();
        b.HasOne<EmployeeLoan>().WithMany().HasForeignKey(x => x.EmployeeLoanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Payslip>().WithMany().HasForeignKey(x => x.PayslipId).OnDelete(DeleteBehavior.Restrict);

        // â­ Ek installment ek payslip se ek hi dafa
        b.HasIndex(x => new { x.EmployeeLoanId, x.PayslipId }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class PaymentBatchConfiguration : IEntityTypeConfiguration<PaymentBatch>
{
    public void Configure(EntityTypeBuilder<PaymentBatch> b)
    {
        b.ToTable("PaymentBatches");
        b.ConfigureAudit();
        b.Property(x => x.FileStorageKey).HasMaxLength(500);
        b.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" > 0"));
        b.ConfigureAudit();
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.Property(x => x.Reference).HasMaxLength(100);

        b.HasOne<Payslip>().WithMany().HasForeignKey(x => x.PayslipId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PaymentBatch>().WithMany().HasForeignKey(x => x.PaymentBatchId).OnDelete(DeleteBehavior.Restrict);

        // â­ Ek payslip ka ek hi LIVE payment (Pending/Paid)
        b.HasIndex(x => x.PayslipId).IsUnique().HasFilter("\"Status\" IN (1, 2) AND \"IsDeleted\" = false");
    }
}


