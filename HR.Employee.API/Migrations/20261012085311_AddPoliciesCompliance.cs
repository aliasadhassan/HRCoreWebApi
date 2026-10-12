using System;
using HR.Shared.Library.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPoliciesCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplianceObligations",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Authority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Category = table.Column<byte>(type: "smallint", nullable: false),
                    Frequency = table.Column<byte>(type: "smallint", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LinkUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RemindDaysBefore = table.Column<short>(type: "smallint", nullable: false),
                    NextDueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDay = table.Column<byte>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ComplianceObligations", x => x.Id);
                    table.CheckConstraint("CK_ComplianceObligations_Category", "\"Category\" BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_ComplianceObligations_DueDay", "\"DueDay\" BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_ComplianceObligations_Frequency", "\"Frequency\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ComplianceObligations_Remind", "\"RemindDaysBefore\" BETWEEN 0 AND 90");
                    table.ForeignKey(
                        name: "FK_ComplianceObligations_Employees_OwnerEmployeeId",
                        column: x => x.OwnerEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<byte>(type: "smallint", nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    DocumentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiresAcknowledgement = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgeWithinDays = table.Column<short>(type: "smallint", nullable: true),
                    ReviewEveryMonths = table.Column<short>(type: "smallint", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    HasUnpublishedChanges = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    EffectiveOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AckVersion = table.Column<int>(type: "integer", nullable: true),
                    AckDueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    LastReviewedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    NextReviewOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
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
                    table.PrimaryKey("PK_Policies", x => x.Id);
                    table.CheckConstraint("CK_Policies_AckDays", "\"AcknowledgeWithinDays\" IS NULL OR \"AcknowledgeWithinDays\" BETWEEN 1 AND 365");
                    table.CheckConstraint("CK_Policies_Category", "\"Category\" BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_Policies_Published", "\"Status\" = 1 OR \"CurrentVersion\" > 0");
                    table.CheckConstraint("CK_Policies_ReviewMonths", "\"ReviewEveryMonths\" IS NULL OR \"ReviewEveryMonths\" BETWEEN 1 AND 60");
                    table.CheckConstraint("CK_Policies_Status", "\"Status\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Policies_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "employee",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Policies_Employees_OwnerEmployeeId",
                        column: x => x.OwnerEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceFilings",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodDueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    FiledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EvidenceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceFilings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceFilings_ComplianceObligations_ObligationId",
                        column: x => x.ObligationId,
                        principalSchema: "employee",
                        principalTable: "ComplianceObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PolicyAcknowledgements",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyAcknowledgements", x => x.Id);
                    table.CheckConstraint("CK_PolicyAcknowledgements_Version", "\"VersionNumber\" >= 1");
                    table.ForeignKey(
                        name: "FK_PolicyAcknowledgements_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PolicyAcknowledgements_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalSchema: "employee",
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PolicyVersions",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    DocumentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ChangeSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RequiresAcknowledgement = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyVersions", x => x.Id);
                    table.CheckConstraint("CK_PolicyVersions_Number", "\"Number\" >= 1");
                    table.ForeignKey(
                        name: "FK_PolicyVersions_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalSchema: "employee",
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFilings_TenantId_FiledOn",
                schema: "employee",
                table: "ComplianceFilings",
                columns: new[] { "TenantId", "FiledOn" });

            migrationBuilder.CreateIndex(
                name: "UX_ComplianceFilings_Obligation_Period",
                schema: "employee",
                table: "ComplianceFilings",
                columns: new[] { "ObligationId", "PeriodDueOn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceObligations_OwnerEmployeeId",
                schema: "employee",
                table: "ComplianceObligations",
                column: "OwnerEmployeeId",
                filter: "\"OwnerEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceObligations_TenantId_NextDueOn",
                schema: "employee",
                table: "ComplianceObligations",
                columns: new[] { "TenantId", "NextDueOn" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_ComplianceObligations_TenantId_Title",
                schema: "employee",
                table: "ComplianceObligations",
                columns: new[] { "TenantId", "Title" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_DepartmentId",
                schema: "employee",
                table: "Policies",
                column: "DepartmentId",
                filter: "\"DepartmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_OwnerEmployeeId",
                schema: "employee",
                table: "Policies",
                column: "OwnerEmployeeId",
                filter: "\"OwnerEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_TenantId_Status",
                schema: "employee",
                table: "Policies",
                columns: new[] { "TenantId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_Policies_TenantId_Title",
                schema: "employee",
                table: "Policies",
                columns: new[] { "TenantId", "Title" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgements_EmployeeId",
                schema: "employee",
                table: "PolicyAcknowledgements",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "UX_PolicyAcknowledgements_Policy_Employee_Version",
                schema: "employee",
                table: "PolicyAcknowledgements",
                columns: new[] { "PolicyId", "EmployeeId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyVersions_TenantId_PublishedAt",
                schema: "employee",
                table: "PolicyVersions",
                columns: new[] { "TenantId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_PolicyVersions_PolicyId_Number",
                schema: "employee",
                table: "PolicyVersions",
                columns: new[] { "PolicyId", "Number" },
                unique: true);

            // Composite tenant FKs, grants aur RLS (tenancy audit 03/04 jaisa) — HR.Shared.Library TenantMigrations
            const string schema = "employee";
            migrationBuilder.AddTenantKey(schema, "Policies");
            migrationBuilder.AddTenantKey(schema, "ComplianceObligations");
            migrationBuilder.AddTenantForeignKey(schema, "Policies", "DepartmentId", "Departments");
            migrationBuilder.AddTenantForeignKey(schema, "Policies", "OwnerEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "PolicyVersions", "PolicyId", "Policies", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "PolicyAcknowledgements", "PolicyId", "Policies", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "PolicyAcknowledgements", "EmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "ComplianceObligations", "OwnerEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "ComplianceFilings", "ObligationId", "ComplianceObligations", "CASCADE");
            migrationBuilder.EnableTenantIsolation(schema, "hr_employee_app",
                "Policies", "PolicyVersions", "PolicyAcknowledgements", "ComplianceObligations", "ComplianceFilings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceFilings",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "PolicyAcknowledgements",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "PolicyVersions",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "ComplianceObligations",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "Policies",
                schema: "employee");
        }
    }
}
