using System;
using HR.Shared.Library.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddReportBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RbLetterTemplates",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<byte>(type: "smallint", nullable: false),
                    Languages = table.Column<byte>(type: "smallint", nullable: false),
                    Paper = table.Column<byte>(type: "smallint", nullable: false),
                    ShowLetterhead = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPageNumbers = table.Column<bool>(type: "boolean", nullable: false),
                    RefPrefix = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    SignatoryName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SignatoryTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DraftHtmlEn = table.Column<string>(type: "text", nullable: true),
                    DraftHtmlAr = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    PublishedVersion = table.Column<int>(type: "integer", nullable: false),
                    HasUnpublishedChanges = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_RbLetterTemplates", x => x.Id);
                    table.CheckConstraint("CK_RbLetterTemplates_Category", "\"Category\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_RbLetterTemplates_Languages", "\"Languages\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_RbLetterTemplates_Paper", "\"Paper\" BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_RbLetterTemplates_Prefix", "\"RefPrefix\" ~ '^[A-Z]{2,6}$'");
                    table.CheckConstraint("CK_RbLetterTemplates_Status", "\"Status\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_RbLetterTemplates_Version", "\"PublishedVersion\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "RbReports",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Spec = table.Column<string>(type: "jsonb", nullable: false),
                    Chart = table.Column<byte>(type: "smallint", nullable: false),
                    Period = table.Column<byte>(type: "smallint", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OwnerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
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
                    table.PrimaryKey("PK_RbReports", x => x.Id);
                    table.CheckConstraint("CK_RbReports_Chart", "\"Chart\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_RbReports_Custom", "(\"Period\" = 8) = (\"FromDate\" IS NOT NULL AND \"ToDate\" IS NOT NULL) AND (\"ToDate\" IS NULL OR \"ToDate\" >= \"FromDate\")");
                    table.CheckConstraint("CK_RbReports_Period", "\"Period\" BETWEEN 1 AND 8");
                });

            migrationBuilder.CreateTable(
                name: "RbIssuedLetters",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ref = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<byte>(type: "smallint", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Language = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Html = table.Column<string>(type: "text", nullable: false),
                    Paper = table.Column<byte>(type: "smallint", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsVoid = table.Column<bool>(type: "boolean", nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
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
                    table.PrimaryKey("PK_RbIssuedLetters", x => x.Id);
                    table.CheckConstraint("CK_RbIssuedLetters_Category", "\"Category\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_RbIssuedLetters_Language", "\"Language\" IN ('en', 'ar')");
                    table.CheckConstraint("CK_RbIssuedLetters_Paper", "\"Paper\" BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_RbIssuedLetters_Void", "\"IsVoid\" = (\"VoidedAt\" IS NOT NULL) AND \"IsVoid\" = (\"VoidReason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RbIssuedLetters_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RbIssuedLetters_RbLetterTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "employee",
                        principalTable: "RbLetterTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RbLetterTemplateVersions",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    HtmlEn = table.Column<string>(type: "text", nullable: true),
                    HtmlAr = table.Column<string>(type: "text", nullable: true),
                    Paper = table.Column<byte>(type: "smallint", nullable: false),
                    ShowLetterhead = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPageNumbers = table.Column<bool>(type: "boolean", nullable: false),
                    SignatoryName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SignatoryTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RbLetterTemplateVersions", x => x.Id);
                    table.CheckConstraint("CK_RbLetterTemplateVersions_Html", "\"HtmlEn\" IS NOT NULL OR \"HtmlAr\" IS NOT NULL");
                    table.CheckConstraint("CK_RbLetterTemplateVersions_Paper", "\"Paper\" BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_RbLetterTemplateVersions_Version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_RbLetterTemplateVersions_RbLetterTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "employee",
                        principalTable: "RbLetterTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RbIssuedLetters_BatchId",
                schema: "employee",
                table: "RbIssuedLetters",
                column: "BatchId",
                filter: "\"BatchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RbIssuedLetters_EmployeeId",
                schema: "employee",
                table: "RbIssuedLetters",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_RbIssuedLetters_TemplateId",
                schema: "employee",
                table: "RbIssuedLetters",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_RbIssuedLetters_TenantId_IssuedAt",
                schema: "employee",
                table: "RbIssuedLetters",
                columns: new[] { "TenantId", "IssuedAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_RbIssuedLetters_TenantId_Ref",
                schema: "employee",
                table: "RbIssuedLetters",
                columns: new[] { "TenantId", "Ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RbLetterTemplates_TenantId_Status",
                schema: "employee",
                table: "RbLetterTemplates",
                columns: new[] { "TenantId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_RbLetterTemplateVersions_Template_Version",
                schema: "employee",
                table: "RbLetterTemplateVersions",
                columns: new[] { "TemplateId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RbReports_TenantId_Name",
                schema: "employee",
                table: "RbReports",
                columns: new[] { "TenantId", "Name" },
                filter: "\"IsDeleted\" = false");

            // Composite tenant FKs, grants aur RLS (tenancy audit 03/04 jaisa) — HR.Shared.Library TenantMigrations
            const string schema = "employee";
            migrationBuilder.AddTenantKey(schema, "RbLetterTemplates");
            migrationBuilder.AddTenantForeignKey(schema, "RbLetterTemplateVersions", "TemplateId", "RbLetterTemplates", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "RbIssuedLetters", "TemplateId", "RbLetterTemplates");
            migrationBuilder.AddTenantForeignKey(schema, "RbIssuedLetters", "EmployeeId", "Employees");
            migrationBuilder.EnableTenantIsolation(schema, "hr_employee_app",
                "RbReports", "RbLetterTemplates", "RbLetterTemplateVersions", "RbIssuedLetters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RbIssuedLetters",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "RbLetterTemplateVersions",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "RbReports",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "RbLetterTemplates",
                schema: "employee");
        }
    }
}
