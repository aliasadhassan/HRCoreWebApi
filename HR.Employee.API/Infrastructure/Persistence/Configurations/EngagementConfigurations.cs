namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Engagement;
using HR.Employee.API.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class SurveyConfiguration : IEntityTypeConfiguration<Survey>
{
    public void Configure(EntityTypeBuilder<Survey> b)
    {
        b.ToTable("Surveys", t =>
        {
            t.HasCheckConstraint("CK_Surveys_Status", "\"Status\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_Surveys_Audience", "(\"Audience\" = 1 AND \"DepartmentId\" IS NULL) OR (\"Audience\" = 2 AND \"DepartmentId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_Surveys_Published", "\"Status\" = 1 OR \"PublishedAt\" IS NOT NULL");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);

        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Questions).WithOne().HasForeignKey(q => q.SurveyId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Questions).HasField("questions").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.DepartmentId).HasFilter("\"DepartmentId\" IS NOT NULL");
    }
}

public sealed class SurveyQuestionConfiguration : IEntityTypeConfiguration<SurveyQuestion>
{
    public void Configure(EntityTypeBuilder<SurveyQuestion> b)
    {
        b.ToTable("SurveyQuestions", t =>
        {
            t.HasCheckConstraint("CK_SurveyQuestions_Type", "\"Type\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_SurveyQuestions_Options", "(\"Type\" = 4) = (\"Options\" IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Ignore(x => x.OptionList);

        b.Property(x => x.Text).HasMaxLength(300).IsRequired();
        b.Property(x => x.Options).HasMaxLength(1100);

        b.HasIndex(x => new { x.SurveyId, x.Order }).IsUnique().HasDatabaseName("UX_SurveyQuestions_Survey_Order");
    }
}

public sealed class SurveyParticipationConfiguration : IEntityTypeConfiguration<SurveyParticipation>
{
    public void Configure(EntityTypeBuilder<SurveyParticipation> b)
    {
        b.ToTable("SurveyParticipations");
        b.HasKey(x => x.Id);

        b.HasOne<Survey>().WithMany().HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.SurveyId, x.EmployeeId }).IsUnique().HasDatabaseName("UX_SurveyParticipations_Survey_Employee");
        b.HasIndex(x => x.EmployeeId);
    }
}

public sealed class SurveyResponseConfiguration : IEntityTypeConfiguration<SurveyResponse>
{
    public void Configure(EntityTypeBuilder<SurveyResponse> b)
    {
        b.ToTable("SurveyResponses");
        b.HasKey(x => x.Id);

        b.HasOne<Survey>().WithMany().HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Answers).WithOne().HasForeignKey(a => a.ResponseId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Answers).HasField("answers").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => x.SurveyId);
        b.HasIndex(x => x.EmployeeId).HasFilter("\"EmployeeId\" IS NOT NULL");
        b.HasIndex(x => x.DepartmentId).HasFilter("\"DepartmentId\" IS NOT NULL");
    }
}

public sealed class SurveyAnswerConfiguration : IEntityTypeConfiguration<SurveyAnswer>
{
    public void Configure(EntityTypeBuilder<SurveyAnswer> b)
    {
        b.ToTable("SurveyAnswers", t =>
        {
            t.HasCheckConstraint("CK_SurveyAnswers_Value", "\"Value\" IS NULL OR \"Value\" BETWEEN 0 AND 10");
            t.HasCheckConstraint("CK_SurveyAnswers_Present", "\"Value\" IS NOT NULL OR \"Text\" IS NOT NULL");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.Text).HasMaxLength(2000);

        b.HasOne<SurveyQuestion>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.ResponseId, x.QuestionId }).IsUnique().HasDatabaseName("UX_SurveyAnswers_Response_Question");
        b.HasIndex(x => x.QuestionId);
    }
}

public sealed class RecognitionConfiguration : IEntityTypeConfiguration<Recognition>
{
    public void Configure(EntityTypeBuilder<Recognition> b)
    {
        b.ToTable("Recognitions", t =>
        {
            t.HasCheckConstraint("CK_Recognitions_Value", "\"Value\" BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_Recognitions_NotSelf", "\"FromEmployeeId\" <> \"ToEmployeeId\"");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Message).HasMaxLength(500).IsRequired();

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.FromEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ToEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.FromEmployeeId).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.ToEmployeeId).HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class RecognitionCheerConfiguration : IEntityTypeConfiguration<RecognitionCheer>
{
    public void Configure(EntityTypeBuilder<RecognitionCheer> b)
    {
        b.ToTable("RecognitionCheers");
        b.HasKey(x => x.Id);

        b.HasOne<Recognition>().WithMany().HasForeignKey(x => x.RecognitionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.RecognitionId, x.EmployeeId }).IsUnique().HasDatabaseName("UX_RecognitionCheers_Recognition_Employee");
        b.HasIndex(x => x.EmployeeId);
    }
}
