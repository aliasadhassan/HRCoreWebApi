namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Organization;
using HR.Employee.API.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class JobOpeningConfiguration : IEntityTypeConfiguration<JobOpening>
{
    public void Configure(EntityTypeBuilder<JobOpening> b)
    {
        b.ToTable("JobOpenings", t =>
        {
            t.HasCheckConstraint("CK_JobOpenings_Status", "\"Status\" BETWEEN 1 AND 6");
            t.HasCheckConstraint("CK_JobOpenings_Reason", "\"Reason\" BETWEEN 1 AND 2");
            t.HasCheckConstraint("CK_JobOpenings_EmploymentType", "\"EmploymentType\" IN (1, 2, 4, 8)");
            t.HasCheckConstraint("CK_JobOpenings_Openings", "\"Openings\" BETWEEN 1 AND 500");
            t.HasCheckConstraint("CK_JobOpenings_Salary",
                "(\"SalaryMin\" IS NULL OR \"SalaryMin\" >= 0) AND (\"SalaryMax\" IS NULL OR \"SalaryMax\" >= 0) AND (\"SalaryMin\" IS NULL OR \"SalaryMax\" IS NULL OR \"SalaryMax\" >= \"SalaryMin\")");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.SalaryMin).HasPrecision(18, 2);
        b.Property(x => x.SalaryMax).HasPrecision(18, 2);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Requirements).HasMaxLength(4000);
        b.Property(x => x.ReviewNote).HasMaxLength(500);
        b.Property(x => x.CloseNote).HasMaxLength(500);

        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Designation>().WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.HiringManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReplacesEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.DepartmentId);
        b.HasIndex(x => x.DesignationId).HasFilter("\"DesignationId\" IS NOT NULL");
        b.HasIndex(x => x.LocationId).HasFilter("\"LocationId\" IS NOT NULL");
        b.HasIndex(x => x.HiringManagerEmployeeId).HasFilter("\"HiringManagerEmployeeId\" IS NOT NULL");
        b.HasIndex(x => x.ReplacesEmployeeId).HasFilter("\"ReplacesEmployeeId\" IS NOT NULL");
    }
}

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> b)
    {
        b.ToTable("Candidates", t =>
        {
            t.HasCheckConstraint("CK_Candidates_Source", "\"Source\" BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_Candidates_Experience", "\"ExperienceYears\" IS NULL OR \"ExperienceYears\" BETWEEN 0 AND 60");
            t.HasCheckConstraint("CK_Candidates_Referral", "\"ReferredByEmployeeId\" IS NULL OR \"Source\" = 2");
        });
        b.ConfigureAuditable();

        b.Ignore(x => x.FullName);
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.CurrentCompany).HasMaxLength(150);
        b.Property(x => x.CurrentTitle).HasMaxLength(150);
        b.Property(x => x.ExperienceYears).HasPrecision(4, 1);
        b.Property(x => x.ResumeUrl).HasMaxLength(1000);
        b.Property(x => x.LinkedInUrl).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReferredByEmployeeId).OnDelete(DeleteBehavior.Restrict);

        // Ek tenant mein ek email ka ek candidate
        b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique()
            .HasDatabaseName("UX_Candidates_TenantId_Email").HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.ReferredByEmployeeId).HasFilter("\"ReferredByEmployeeId\" IS NOT NULL");
    }
}

public sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> b)
    {
        b.ToTable("JobApplications", t =>
        {
            t.HasCheckConstraint("CK_JobApplications_Stage", "\"Stage\" BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_JobApplications_Rating", "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_JobApplications_OfferStatus", "\"OfferStatus\" IS NULL OR \"OfferStatus\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_JobApplications_OfferSalary", "\"OfferSalary\" IS NULL OR \"OfferSalary\" >= 0");
            t.HasCheckConstraint("CK_JobApplications_Hired",
                "(\"Stage\" = 5) = (\"HiredEmployeeId\" IS NOT NULL AND \"HiredAt\" IS NOT NULL)");
        });
        b.ConfigureAuditable();

        b.Property(x => x.RejectReason).HasMaxLength(1000);
        b.Property(x => x.OfferSalary).HasPrecision(18, 2);

        b.HasOne<JobOpening>().WithMany().HasForeignKey(x => x.JobOpeningId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Candidate>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.HiredEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Events).WithOne().HasForeignKey(e => e.JobApplicationId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Events).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Ek candidate ek job par ek baar
        b.HasIndex(x => new { x.TenantId, x.JobOpeningId, x.CandidateId }).IsUnique()
            .HasDatabaseName("UX_JobApplications_Job_Candidate").HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => new { x.TenantId, x.Stage }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.JobOpeningId);
        b.HasIndex(x => x.CandidateId);
        b.HasIndex(x => x.HiredEmployeeId).HasFilter("\"HiredEmployeeId\" IS NOT NULL");
    }
}

public sealed class ApplicationEventConfiguration : IEntityTypeConfiguration<ApplicationEvent>
{
    public void Configure(EntityTypeBuilder<ApplicationEvent> b)
    {
        b.ToTable("ApplicationEvents", t =>
        {
            t.HasCheckConstraint("CK_ApplicationEvents_Kind", "\"Kind\" BETWEEN 1 AND 6");
            t.HasCheckConstraint("CK_ApplicationEvents_Stage", "\"ToStage\" BETWEEN 1 AND 7 AND (\"FromStage\" IS NULL OR \"FromStage\" BETWEEN 1 AND 7)");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.Note).HasMaxLength(1000);

        b.HasIndex(x => new { x.JobApplicationId, x.At });
        b.HasIndex(x => new { x.TenantId, x.At });
    }
}

public sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> b)
    {
        b.ToTable("Interviews", t =>
        {
            t.HasCheckConstraint("CK_Interviews_Status", "\"Status\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_Interviews_Mode", "\"Mode\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_Interviews_Duration", "\"DurationMinutes\" BETWEEN 5 AND 480");
            t.HasCheckConstraint("CK_Interviews_Rating", "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Interviews_Recommendation", "\"Recommendation\" IS NULL OR \"Recommendation\" BETWEEN 1 AND 4");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Title).HasMaxLength(100).IsRequired();
        b.Property(x => x.LocationOrLink).HasMaxLength(500);
        b.Property(x => x.Feedback).HasMaxLength(4000);

        b.HasOne<JobApplication>().WithMany().HasForeignKey(x => x.JobApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.InterviewerEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.JobApplicationId);
        b.HasIndex(x => new { x.TenantId, x.InterviewerEmployeeId, x.ScheduledAt }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.InterviewerEmployeeId);
        b.HasIndex(x => new { x.TenantId, x.ScheduledAt }).HasFilter("\"IsDeleted\" = false");
    }
}
