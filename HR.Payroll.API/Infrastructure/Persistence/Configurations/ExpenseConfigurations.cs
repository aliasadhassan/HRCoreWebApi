namespace HR.Payroll.API.Infrastructure.Persistence.Configurations;

using HR.Payroll.API.Domain.Employees;
using HR.Payroll.API.Domain.Expenses;
using HR.Payroll.API.Domain.Inputs;
using HR.Payroll.API.Domain.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Expenses & travel. Tenant composite FKs + RLS migration ke raw SQL mein (EF composite FKs track nahi karta).

public sealed class ExpensePolicyConfiguration : IEntityTypeConfiguration<ExpensePolicy>
{
    public void Configure(EntityTypeBuilder<ExpensePolicy> b)
    {
        b.ToTable("ExpensePolicies", t =>
        {
            t.HasCheckConstraint("CK_ExpensePolicies_Advance", "\"MaxAdvancePercent\" > 0 AND \"MaxAdvancePercent\" <= 100");
            t.HasCheckConstraint("CK_ExpensePolicies_Window", "\"SubmitWithinDays\" BETWEEN 1 AND 365");
            t.HasCheckConstraint("CK_ExpensePolicies_Receipt", "\"ReceiptRequiredAbove\" IS NULL OR \"ReceiptRequiredAbove\" >= 0");
        });
        b.ConfigureAudit();
        b.Property(x => x.MaxAdvancePercent).HasPrecision(5, 2);

        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.ReimbursementComponentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayComponent>().WithMany().HasForeignKey(x => x.AdvanceRecoveryComponentId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.TenantId).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> b)
    {
        b.ToTable("ExpenseCategories", t => t.HasCheckConstraint("CK_ExpenseCategories_Max", "\"MaxPerClaim\" IS NULL OR \"MaxPerClaim\" > 0"));
        b.ConfigureAudit();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class TravelRequestConfiguration : IEntityTypeConfiguration<TravelRequest>
{
    public void Configure(EntityTypeBuilder<TravelRequest> b)
    {
        b.ToTable("TravelRequests", t =>
        {
            t.HasCheckConstraint("CK_TravelRequests_Dates", "\"ReturnDate\" >= \"DepartDate\"");
            t.HasCheckConstraint("CK_TravelRequests_Amounts",
                "\"EstimatedCost\" > 0 AND \"AdvanceRequested\" BETWEEN 0 AND \"EstimatedCost\" AND \"AdvanceApproved\" BETWEEN 0 AND \"EstimatedCost\"");
            t.HasCheckConstraint("CK_TravelRequests_AdvancePaid", "\"AdvanceStatus\" < 2 OR \"AdvancePaidAt\" IS NOT NULL");
        });
        b.ConfigureAudit();
        b.Property(x => x.Purpose).HasMaxLength(500).IsRequired();
        b.Property(x => x.Destination).HasMaxLength(200).IsRequired();
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.Property(x => x.DecisionComment).HasMaxLength(500);

        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollInput>().WithMany().HasForeignKey(x => x.AdvancePayrollInputId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.EmployeeId, x.DepartDate });
        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"Status\" = 1 AND \"IsDeleted\" = false");
    }
}

public sealed class ExpenseClaimConfiguration : IEntityTypeConfiguration<ExpenseClaim>
{
    public void Configure(EntityTypeBuilder<ExpenseClaim> b)
    {
        b.ToTable("ExpenseClaims", t =>
        {
            t.HasCheckConstraint("CK_ExpenseClaims_Amounts",
                "\"TotalAmount\" > 0 AND \"AdvanceAdjusted\" >= 0 AND (\"ApprovedAmount\" IS NULL OR \"ApprovedAmount\" BETWEEN 0 AND \"TotalAmount\")");
            t.HasCheckConstraint("CK_ExpenseClaims_Approved", "\"Status\" NOT IN (2, 5) OR (\"ApprovedAmount\" > 0 AND \"PayoutMethod\" IS NOT NULL)");
        });
        b.ConfigureAudit();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.CurrencyCode).AsCurrency().IsRequired();
        b.Property(x => x.DecisionComment).HasMaxLength(500);
        b.Ignore(x => x.NetPayable);

        b.HasOne<PayrollEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TravelRequest>().WithMany().HasForeignKey(x => x.TravelRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollInput>().WithMany().HasForeignKey(x => x.PayrollInputId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollInput>().WithMany().HasForeignKey(x => x.RecoveryPayrollInputId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ExpenseClaimId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.EmployeeId, x.CreatedAt });
        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"Status\" IN (1, 2) AND \"IsDeleted\" = false");
        // Ek travel ka ek hi zinda claim (advance do baar adjust na ho)
        b.HasIndex(x => x.TravelRequestId).IsUnique().HasFilter("\"TravelRequestId\" IS NOT NULL AND \"Status\" IN (1, 2, 5) AND \"IsDeleted\" = false");
    }
}

public sealed class ExpenseClaimLineConfiguration : IEntityTypeConfiguration<ExpenseClaimLine>
{
    public void Configure(EntityTypeBuilder<ExpenseClaimLine> b)
    {
        b.ToTable("ExpenseClaimLines", t => t.HasCheckConstraint("CK_ExpenseClaimLines_Amount", "\"Amount\" > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Merchant).HasMaxLength(150);
        b.Property(x => x.ReceiptNumber).HasMaxLength(100);

        b.HasOne<ExpenseCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
