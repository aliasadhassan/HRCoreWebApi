namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Attendance;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Leaves;
using HR.Employee.API.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Attendance tables apne "attendance" schema mein — isi DbContext mein (leave/holiday/location ke saath
/// ek transaction), lekin alag schema taake kabhi HR.Attendance.API nikalni ho to tables pehle se alag hon.
/// </summary>
internal static class AttendanceSchema
{
    public const string Name = "attendance";
}

public sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> b)
    {
        b.ToTable("Shifts", AttendanceSchema.Name, t =>
        {
            t.HasCheckConstraint("CK_Shift_Times", "\"StartTime\" <> \"EndTime\"");
            t.HasCheckConstraint("CK_Shift_Break", "\"BreakMinutes\" >= 0");
        });
        b.ConfigureAuditable();
        b.Ignore(x => x.CrossesMidnight);
        b.Ignore(x => x.NetMinutes);

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Color).HasMaxLength(7).IsFixedLength().IsUnicode(false);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> b)
    {
        b.ToTable("ShiftAssignments", AttendanceSchema.Name, t =>
        {
            t.HasCheckConstraint("CK_SA_Dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
            t.HasCheckConstraint("CK_SA_OffDays", "\"WeeklyOffDays\" IS NULL OR \"WeeklyOffDays\" BETWEEN 0 AND 127");
        });
        b.ConfigureAuditable();

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);

        // "Is date pe kaunsi shift" lookup
        b.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom }).IsDescending(false, true).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.ShiftId);
    }
}

public sealed class RosterEntryConfiguration : IEntityTypeConfiguration<RosterEntry>
{
    public void Configure(EntityTypeBuilder<RosterEntry> b)
    {
        b.ToTable("RosterEntries", AttendanceSchema.Name);
        b.ConfigureAuditable();
        b.Ignore(x => x.IsOff);

        b.Property(x => x.Note).HasMaxLength(200);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.EmployeeId, x.WorkDate }).IsUnique().HasFilter("\"IsDeleted\" = false");
        // Roster calendar (team/week view)
        b.HasIndex(x => new { x.TenantId, x.WorkDate }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.ShiftId);
    }
}

public sealed class AttendancePolicyConfiguration : IEntityTypeConfiguration<AttendancePolicy>
{
    public void Configure(EntityTypeBuilder<AttendancePolicy> b)
    {
        b.ToTable("AttendancePolicies", AttendanceSchema.Name, t =>
        {
            t.HasCheckConstraint("CK_AP_DayMinutes", "\"HalfDayMinutes\" > 0 AND \"HalfDayMinutes\" < \"FullDayMinutes\"");
            t.HasCheckConstraint("CK_AP_Geofence",
                "\"RequireGeofence\" = false OR (\"GeoLatitude\" IS NOT NULL AND \"GeoLongitude\" IS NOT NULL AND \"GeoRadiusMeters\" > 0)");
            t.HasCheckConstraint("CK_AP_OtRates",
                "\"OvertimeRateWorkday\" >= 1 AND \"OvertimeRateWeeklyOff\" >= 1 AND \"OvertimeRateHoliday\" >= 1");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.GeoLatitude).HasPrecision(9, 6);
        b.Property(x => x.GeoLongitude).HasPrecision(9, 6);
        b.Property(x => x.OvertimeRateWorkday).HasPrecision(4, 2);
        b.Property(x => x.OvertimeRateWeeklyOff).HasPrecision(4, 2);
        b.Property(x => x.OvertimeRateHoliday).HasPrecision(4, 2);

        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);

        // Har location (aur tenant default = NULL) ki sirf EK active policy. NULLS NOT DISTINCT taake
        // do "tenant default" policies na ban sakein (Postgres 15+, Supabase pe hai).
        b.HasIndex(x => new { x.TenantId, x.LocationId }).IsUnique().AreNullsDistinct(false)
            .HasFilter("\"IsActive\" = true AND \"IsDeleted\" = false");
    }
}

public sealed class AttendanceDeviceConfiguration : IEntityTypeConfiguration<AttendanceDevice>
{
    public void Configure(EntityTypeBuilder<AttendanceDevice> b)
    {
        b.ToTable("AttendanceDevices", AttendanceSchema.Name);
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.SerialNumber).HasMaxLength(50).IsRequired().IsUnicode(false);
        b.Property(x => x.Vendor).HasMaxLength(50);
        b.Property(x => x.ApiKeyHash).HasMaxLength(128).IsUnicode(false);

        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.SerialNumber }).IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.LocationId);
    }
}

public sealed class AttendanceDayConfiguration : IEntityTypeConfiguration<AttendanceDay>
{
    public void Configure(EntityTypeBuilder<AttendanceDay> b)
    {
        b.ToTable("AttendanceDays", AttendanceSchema.Name, t =>
        {
            t.HasCheckConstraint("CK_AD_Minutes",
                "\"WorkedMinutes\" >= 0 AND \"LateMinutes\" >= 0 AND \"EarlyLeaveMinutes\" >= 0 AND \"OvertimeMinutes\" >= 0");
            t.HasCheckConstraint("CK_AD_Schedule",
                "\"ScheduledStart\" IS NULL OR \"ScheduledEnd\" IS NULL OR \"ScheduledEnd\" > \"ScheduledStart\"");
            t.HasCheckConstraint("CK_AD_InOut", "\"FirstIn\" IS NULL OR \"LastOut\" IS NULL OR \"LastOut\" >= \"FirstIn\"");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Remarks).HasMaxLength(500);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Punches).WithOne().HasForeignKey(p => p.AttendanceDayId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Punches).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.EmployeeId, x.WorkDate }).IsUnique().HasFilter("\"IsDeleted\" = false");
        // Timesheet (tenant/day) + dashboard "present today" card
        b.HasIndex(x => new { x.TenantId, x.WorkDate })
            .IncludeProperties(x => new { x.EmployeeId, x.Status })
            .HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.ShiftId);
        b.HasIndex(x => x.LeaveRequestId).HasFilter("\"LeaveRequestId\" IS NOT NULL");
    }
}

public sealed class AttendancePunchConfiguration : IEntityTypeConfiguration<AttendancePunch>
{
    public void Configure(EntityTypeBuilder<AttendancePunch> b)
    {
        b.ToTable("AttendancePunches", AttendanceSchema.Name);
        b.HasKey(x => x.Id);

        b.Property(x => x.Latitude).HasPrecision(9, 6);
        b.Property(x => x.Longitude).HasPrecision(9, 6);
        b.Property(x => x.IpAddress).HasMaxLength(45).IsUnicode(false);
        b.Property(x => x.Note).HasMaxLength(200);
        b.Property(x => x.RecordedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        b.HasOne<AttendanceDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);

        // Biometric re-sync duplicate na bane
        b.HasIndex(x => new { x.AttendanceDayId, x.PunchedAt }).IsUnique();
        b.HasIndex(x => x.DeviceId).HasFilter("\"DeviceId\" IS NOT NULL");
    }
}

public sealed class AttendanceRequestConfiguration : IEntityTypeConfiguration<AttendanceRequest>
{
    public void Configure(EntityTypeBuilder<AttendanceRequest> b)
    {
        b.ToTable("AttendanceRequests", AttendanceSchema.Name, t =>
        {
            // Type 4 = Overtime: minutes zaroori, in/out nahi. Baqi types: in ya out zaroori.
            t.HasCheckConstraint("CK_AR_Shape",
                "(\"Type\" = 4 AND \"OvertimeMinutes\" > 0 AND \"RequestedIn\" IS NULL AND \"RequestedOut\" IS NULL) OR " +
                "(\"Type\" <> 4 AND \"OvertimeMinutes\" IS NULL AND (\"RequestedIn\" IS NOT NULL OR \"RequestedOut\" IS NOT NULL))");
            t.HasCheckConstraint("CK_AR_InOut", "\"RequestedIn\" IS NULL OR \"RequestedOut\" IS NULL OR \"RequestedOut\" > \"RequestedIn\"");
            t.HasCheckConstraint("CK_AR_Approved", "\"ApprovedMinutes\" IS NULL OR (\"ApprovedMinutes\" > 0 AND \"ApprovedMinutes\" <= \"OvertimeMinutes\")");
        });
        b.ConfigureAuditable();

        b.Property(x => x.OvertimeRate).HasPrecision(4, 2);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.DecisionComment).HasMaxLength(500);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendanceDay>().WithMany().HasForeignKey(x => x.AttendanceDayId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.EmployeeId, x.WorkDate }).IsDescending(false, true);
        b.HasIndex(x => new { x.TenantId, x.Type, x.Status }).IncludeProperties(x => new { x.EmployeeId, x.WorkDate });
        // "My approvals" inbox
        b.HasIndex(x => x.AssignedApproverId).HasFilter("\"Status\" = 1 AND \"IsDeleted\" = false");
        // Ek din ki ek type ki sirf ek chalti (pending/approved) request
        b.HasIndex(x => new { x.EmployeeId, x.WorkDate, x.Type }).IsUnique()
            .HasFilter("\"Status\" IN (1, 2) AND \"IsDeleted\" = false");
        b.HasIndex(x => x.AttendanceDayId).HasFilter("\"AttendanceDayId\" IS NOT NULL");
    }
}
