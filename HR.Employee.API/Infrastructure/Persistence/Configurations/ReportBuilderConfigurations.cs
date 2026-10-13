namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.ReportBuilder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class SavedReportConfiguration : IEntityTypeConfiguration<SavedReport>
{
    public void Configure(EntityTypeBuilder<SavedReport> b)
    {
        b.ToTable("RbReports", t =>
        {
            t.HasCheckConstraint("CK_RbReports_Chart", "\"Chart\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_RbReports_Period", "\"Period\" BETWEEN 1 AND 8");
            t.HasCheckConstraint("CK_RbReports_Custom",
                "(\"Period\" = 8) = (\"FromDate\" IS NOT NULL AND \"ToDate\" IS NOT NULL) AND (\"ToDate\" IS NULL OR \"ToDate\" >= \"FromDate\")");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Source).HasMaxLength(60).IsRequired();
        b.Property(x => x.Spec).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.OwnerName).HasMaxLength(120);

        b.HasIndex(x => new { x.TenantId, x.Name }).HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class LetterTemplateConfiguration : IEntityTypeConfiguration<LetterTemplate>
{
    public void Configure(EntityTypeBuilder<LetterTemplate> b)
    {
        b.ToTable("RbLetterTemplates", t =>
        {
            t.HasCheckConstraint("CK_RbLetterTemplates_Category", "\"Category\" BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_RbLetterTemplates_Languages", "\"Languages\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_RbLetterTemplates_Paper", "\"Paper\" BETWEEN 1 AND 2");
            t.HasCheckConstraint("CK_RbLetterTemplates_Status", "\"Status\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_RbLetterTemplates_Prefix", "\"RefPrefix\" ~ '^[A-Z]{2,6}$'");
            t.HasCheckConstraint("CK_RbLetterTemplates_Version", "\"PublishedVersion\" >= 0");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.RefPrefix).HasMaxLength(6).IsRequired();
        b.Property(x => x.SignatoryName).HasMaxLength(120);
        b.Property(x => x.SignatoryTitle).HasMaxLength(120);
        b.Property(x => x.DraftHtmlEn).HasColumnType("text");
        b.Property(x => x.DraftHtmlAr).HasColumnType("text");

        b.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.TemplateId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Versions).HasField("versions").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.Status }).HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class LetterTemplateVersionConfiguration : IEntityTypeConfiguration<LetterTemplateVersion>
{
    public void Configure(EntityTypeBuilder<LetterTemplateVersion> b)
    {
        b.ToTable("RbLetterTemplateVersions", t =>
        {
            t.HasCheckConstraint("CK_RbLetterTemplateVersions_Version", "\"Version\" >= 1");
            t.HasCheckConstraint("CK_RbLetterTemplateVersions_Paper", "\"Paper\" BETWEEN 1 AND 2");
            t.HasCheckConstraint("CK_RbLetterTemplateVersions_Html", "\"HtmlEn\" IS NOT NULL OR \"HtmlAr\" IS NOT NULL");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.HtmlEn).HasColumnType("text");
        b.Property(x => x.HtmlAr).HasColumnType("text");
        b.Property(x => x.SignatoryName).HasMaxLength(120);
        b.Property(x => x.SignatoryTitle).HasMaxLength(120);
        b.Property(x => x.PublishedByName).HasMaxLength(120);

        b.HasIndex(x => new { x.TemplateId, x.Version }).IsUnique().HasDatabaseName("UX_RbLetterTemplateVersions_Template_Version");
    }
}

public sealed class IssuedLetterConfiguration : IEntityTypeConfiguration<IssuedLetter>
{
    public void Configure(EntityTypeBuilder<IssuedLetter> b)
    {
        b.ToTable("RbIssuedLetters", t =>
        {
            t.HasCheckConstraint("CK_RbIssuedLetters_Language", "\"Language\" IN ('en', 'ar')");
            t.HasCheckConstraint("CK_RbIssuedLetters_Category", "\"Category\" BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_RbIssuedLetters_Paper", "\"Paper\" BETWEEN 1 AND 2");
            t.HasCheckConstraint("CK_RbIssuedLetters_Void",
                "\"IsVoid\" = (\"VoidedAt\" IS NOT NULL) AND \"IsVoid\" = (\"VoidReason\" IS NOT NULL)");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Ref).HasMaxLength(30).IsRequired();
        b.Property(x => x.TemplateName).HasMaxLength(150).IsRequired();
        b.Property(x => x.EmployeeName).HasMaxLength(200).IsRequired();
        b.Property(x => x.EmployeeCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.Language).HasMaxLength(2).IsFixedLength().IsRequired();
        b.Property(x => x.Html).HasColumnType("text").IsRequired();
        b.Property(x => x.IssuedByName).HasMaxLength(120);
        b.Property(x => x.Purpose).HasMaxLength(300);
        b.Property(x => x.VoidReason).HasMaxLength(500);

        b.HasOne<LetterTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.Ref }).IsUnique().HasDatabaseName("UX_RbIssuedLetters_TenantId_Ref");
        b.HasIndex(x => new { x.TenantId, x.IssuedAt }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.TemplateId);
        b.HasIndex(x => x.EmployeeId);
        b.HasIndex(x => x.BatchId).HasFilter("\"BatchId\" IS NOT NULL");
    }
}
