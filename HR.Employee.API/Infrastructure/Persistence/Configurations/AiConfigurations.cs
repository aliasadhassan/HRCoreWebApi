namespace HR.Employee.API.Infrastructure.Persistence.Configurations;

using HR.Employee.API.Domain.Ai;
using HR.Employee.API.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AiSettingsConfiguration : IEntityTypeConfiguration<AiSettings>
{
    public void Configure(EntityTypeBuilder<AiSettings> b)
    {
        b.ToTable("AiSettings", t =>
        {
            t.HasCheckConstraint("CK_AiSettings_Areas", "\"Areas\" BETWEEN 0 AND 127");
            t.HasCheckConstraint("CK_AiSettings_Limit", "\"MonthlyLimit\" BETWEEN 0 AND 100000");
            t.HasCheckConstraint("CK_AiSettings_History", "\"HistoryDays\" IN (30, 90, 180, 365)");
        });
        b.ConfigureAuditable();
        b.HasIndex(x => x.TenantId).IsUnique().HasDatabaseName("UX_AiSettings_TenantId").HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> b)
    {
        b.ToTable("AiConversations", t => t.HasCheckConstraint("CK_AiConversations_Turns", "\"TurnCount\" >= 0"));
        b.ConfigureAuditable();

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();

        b.HasMany(x => x.Messages).WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Messages).HasField("messages").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.TenantId, x.UserId, x.LastMessageAt }).HasFilter("\"IsDeleted\" = false");
    }
}

public sealed class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> b)
    {
        b.ToTable("AiMessages", t =>
        {
            t.HasCheckConstraint("CK_AiMessages_Role", "\"Role\" BETWEEN 1 AND 2");
            t.HasCheckConstraint("CK_AiMessages_Tokens", "\"InputTokens\" >= 0 AND \"OutputTokens\" >= 0 AND \"CacheReadTokens\" >= 0");
        });
        b.HasKey(x => x.Id);

        b.Property(x => x.Text).HasColumnType("text").IsRequired();
        b.Property(x => x.Payload).HasColumnType("jsonb");
        b.Property(x => x.Tools).HasColumnType("jsonb");

        b.HasIndex(x => new { x.ConversationId, x.Seq }).IsUnique().HasDatabaseName("UX_AiMessages_Conversation_Seq");
        // Mahine ka hisaab (sawal ginna) — tenant + waqt
        b.HasIndex(x => new { x.TenantId, x.At });
    }
}

public sealed class AiInsightConfiguration : IEntityTypeConfiguration<AiInsight>
{
    public void Configure(EntityTypeBuilder<AiInsight> b)
    {
        b.ToTable("AiInsights", t =>
        {
            t.HasCheckConstraint("CK_AiInsights_Level", "\"Level\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_AiInsights_Status", "\"Status\" BETWEEN 1 AND 2");
            t.HasCheckConstraint("CK_AiInsights_Link", "\"Link\" IS NULL OR \"Link\" LIKE '/app/%'");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Area).HasMaxLength(40).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Metric).HasMaxLength(40);
        b.Property(x => x.MetricLabel).HasMaxLength(120);
        b.Property(x => x.Source).HasMaxLength(200);
        b.Property(x => x.Link).HasMaxLength(200);
        b.Property(x => x.RunByName).HasMaxLength(120);

        b.HasIndex(x => new { x.TenantId, x.RunAt }).HasFilter("\"IsDeleted\" = false");
        b.HasIndex(x => x.RunId);
    }
}

public sealed class AiScreeningConfiguration : IEntityTypeConfiguration<AiScreening>
{
    public void Configure(EntityTypeBuilder<AiScreening> b)
    {
        b.ToTable("AiScreenings", t =>
        {
            t.HasCheckConstraint("CK_AiScreenings_Score", "\"Score\" BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_AiScreenings_Fit", "\"Fit\" BETWEEN 1 AND 3");
        });
        b.ConfigureAuditable();

        b.Property(x => x.Summary).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Result).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ByName).HasMaxLength(120);

        b.HasOne<JobApplication>().WithMany().HasForeignKey(x => x.JobApplicationId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.JobApplicationId, x.At });
        b.HasIndex(x => new { x.TenantId, x.At }).HasFilter("\"IsDeleted\" = false");
    }
}
