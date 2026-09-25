namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Leaves;
using HR.Employee.API.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        b.ToTable("Holidays");
        b.ConfigureAuditable();

        b.Property(x => x.Date).HasColumnName("HolidayDate");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();

        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.LocationId, x.Date }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> b)
    {
        b.ToTable("LeaveTypes");
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Color).HasMaxLength(7).IsFixedLength().IsUnicode(false);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class LeavePolicyConfiguration : IEntityTypeConfiguration<LeavePolicy>
{
    public void Configure(EntityTypeBuilder<LeavePolicy> b)
    {
        b.ToTable("LeavePolicies");
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(150).IsRequired();

        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Rules).WithOne().HasForeignKey(r => r.LeavePolicyId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Rules).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Har location (aur tenant default = NULL) ki sirf EK active policy
        b.HasIndex(x => new { x.TenantId, x.LocationId }).IsUnique().HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");
    }
}

public sealed class LeavePolicyRuleConfiguration : IEntityTypeConfiguration<LeavePolicyRule>
{
    public void Configure(EntityTypeBuilder<LeavePolicyRule> b)
    {
        b.ToTable("LeavePolicyRules", t =>
            t.HasCheckConstraint("CK_LPR_Entitlement", "[AnnualEntitlement] >= 0 AND [MaxCarryForward] >= 0"));
        b.HasKey(x => x.Id);

        b.Property(x => x.AnnualEntitlement).HasPrecision(5, 2);
        b.Property(x => x.MaxCarryForward).HasPrecision(5, 2);

        b.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.LeavePolicyId, x.LeaveTypeId }).IsUnique();
    }
}

public sealed class LeaveApprovalSettingsConfiguration : IEntityTypeConfiguration<LeaveApprovalSettings>
{
    public void Configure(EntityTypeBuilder<LeaveApprovalSettings> b)
    {
        b.ToTable("LeaveApprovalSettings", t =>
        {
            t.HasCheckConstraint("CK_LAS_Levels", "[ApprovalLevels] IN (1, 2)");
            t.HasCheckConstraint("CK_LAS_L2",
                "([ApprovalLevels] = 1 AND [Level2Approver] IS NULL) OR ([ApprovalLevels] = 2 AND [Level2Approver] IS NOT NULL)");
        });
        b.ConfigureAuditable();

        b.HasIndex(x => x.TenantId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> b)
    {
        b.ToTable("LeaveBalances");
        b.ConfigureAuditable();
        b.Ignore(x => x.Available);

        b.Property(x => x.Entitled).HasPrecision(5, 2);
        b.Property(x => x.CarriedForward).HasPrecision(5, 2);
        b.Property(x => x.Adjusted).HasPrecision(5, 2);
        b.Property(x => x.Used).HasPrecision(5, 2);
        b.Property(x => x.Pending).HasPrecision(5, 2);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.LeaveYear }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        b.ToTable("LeaveRequests", t =>
        {
            t.HasCheckConstraint("CK_LR_Dates", "[EndDate] >= [StartDate]");
            t.HasCheckConstraint("CK_LR_HalfDay",
                "([IsHalfDay] = 0 AND [HalfDayPeriod] IS NULL) OR ([IsHalfDay] = 1 AND [HalfDayPeriod] IS NOT NULL AND [StartDate] = [EndDate])");
        });
        b.ConfigureAuditable();

        b.Property(x => x.TotalDays).HasPrecision(5, 2);
        b.Property(x => x.Reason).HasMaxLength(1000);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.AttachmentDocumentId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Approvals).WithOne().HasForeignKey(a => a.LeaveRequestId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Approvals).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.EmployeeId, x.StartDate }).IsDescending(false, true);
        b.HasIndex(x => new { x.TenantId, x.Status }).IncludeProperties(x => new { x.EmployeeId, x.StartDate, x.EndDate });
        // Calendar + "On Leave today" card
        b.HasIndex(x => new { x.TenantId, x.StartDate, x.EndDate }).HasFilter("[Status] IN (1, 2) AND [IsDeleted] = 0");
    }
}

public sealed class LeaveRequestApprovalConfiguration : IEntityTypeConfiguration<LeaveRequestApproval>
{
    public void Configure(EntityTypeBuilder<LeaveRequestApproval> b)
    {
        b.ToTable("LeaveRequestApprovals");
        b.HasKey(x => x.Id);

        b.Property(x => x.Comment).HasMaxLength(500);

        b.HasIndex(x => new { x.LeaveRequestId, x.Level }).IsUnique();
        // "My approvals" inbox
        b.HasIndex(x => x.AssignedApproverId).HasFilter("[Decision] = 0");
    }
}
