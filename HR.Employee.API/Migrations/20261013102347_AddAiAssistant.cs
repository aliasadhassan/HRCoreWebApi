using System;
using HR.Shared.Library.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAssistant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiConversations",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TurnCount = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConversations", x => x.Id);
                    table.CheckConstraint("CK_AiConversations_Turns", "\"TurnCount\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "AiInsights",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Area = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Level = table.Column<byte>(type: "smallint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Metric = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    MetricLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Link = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    RunAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    RunByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInsights", x => x.Id);
                    table.CheckConstraint("CK_AiInsights_Level", "\"Level\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_AiInsights_Link", "\"Link\" IS NULL OR \"Link\" LIKE '/app/%'");
                    table.CheckConstraint("CK_AiInsights_Status", "\"Status\" BETWEEN 1 AND 2");
                });

            migrationBuilder.CreateTable(
                name: "AiScreenings",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<byte>(type: "smallint", nullable: false),
                    Fit = table.Column<byte>(type: "smallint", nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Result = table.Column<string>(type: "jsonb", nullable: false),
                    ByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    At = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiScreenings", x => x.Id);
                    table.CheckConstraint("CK_AiScreenings_Fit", "\"Fit\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_AiScreenings_Score", "\"Score\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_AiScreenings_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalSchema: "employee",
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiSettings",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Areas = table.Column<int>(type: "integer", nullable: false),
                    MonthlyLimit = table.Column<int>(type: "integer", nullable: false),
                    HistoryDays = table.Column<short>(type: "smallint", nullable: false),
                    SelfService = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSettings", x => x.Id);
                    table.CheckConstraint("CK_AiSettings_Areas", "\"Areas\" BETWEEN 0 AND 127");
                    table.CheckConstraint("CK_AiSettings_History", "\"HistoryDays\" IN (30, 90, 180, 365)");
                    table.CheckConstraint("CK_AiSettings_Limit", "\"MonthlyLimit\" BETWEEN 0 AND 100000");
                });

            migrationBuilder.CreateTable(
                name: "AiMessages",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seq = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<byte>(type: "smallint", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    Tools = table.Column<string>(type: "jsonb", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    CacheReadTokens = table.Column<int>(type: "integer", nullable: false),
                    Failed = table.Column<bool>(type: "boolean", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMessages", x => x.Id);
                    table.CheckConstraint("CK_AiMessages_Role", "\"Role\" BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_AiMessages_Tokens", "\"InputTokens\" >= 0 AND \"OutputTokens\" >= 0 AND \"CacheReadTokens\" >= 0");
                    table.ForeignKey(
                        name: "FK_AiMessages_AiConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "employee",
                        principalTable: "AiConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_TenantId_UserId_LastMessageAt",
                schema: "employee",
                table: "AiConversations",
                columns: new[] { "TenantId", "UserId", "LastMessageAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AiInsights_RunId",
                schema: "employee",
                table: "AiInsights",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_AiInsights_TenantId_RunAt",
                schema: "employee",
                table: "AiInsights",
                columns: new[] { "TenantId", "RunAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_TenantId_At",
                schema: "employee",
                table: "AiMessages",
                columns: new[] { "TenantId", "At" });

            migrationBuilder.CreateIndex(
                name: "UX_AiMessages_Conversation_Seq",
                schema: "employee",
                table: "AiMessages",
                columns: new[] { "ConversationId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiScreenings_JobApplicationId_At",
                schema: "employee",
                table: "AiScreenings",
                columns: new[] { "JobApplicationId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_AiScreenings_TenantId_At",
                schema: "employee",
                table: "AiScreenings",
                columns: new[] { "TenantId", "At" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_AiSettings_TenantId",
                schema: "employee",
                table: "AiSettings",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            // Composite tenant FKs, grants aur RLS (tenancy audit 03/04 jaisa) — HR.Shared.Library TenantMigrations
            const string schema = "employee";
            migrationBuilder.AddTenantKey(schema, "AiConversations");
            migrationBuilder.AddTenantForeignKey(schema, "AiMessages", "ConversationId", "AiConversations", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "AiScreenings", "JobApplicationId", "JobApplications");
            migrationBuilder.EnableTenantIsolation(schema, "hr_employee_app",
                "AiSettings", "AiConversations", "AiMessages", "AiInsights", "AiScreenings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiInsights",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "AiMessages",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "AiScreenings",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "AiSettings",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "AiConversations",
                schema: "employee");
        }
    }
}
