using System;
using HR.Shared.Library.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErCaseStages",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Phase = table.Column<byte>(type: "smallint", nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    TargetDays = table.Column<short>(type: "smallint", nullable: true),
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
                    table.PrimaryKey("PK_ErCaseStages", x => x.Id);
                    table.CheckConstraint("CK_ErCaseStages_Phase", "\"Phase\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ErCaseStages_TargetDays", "\"TargetDays\" IS NULL OR (\"TargetDays\" BETWEEN 1 AND 365 AND \"Phase\" <> 5)");
                });

            migrationBuilder.CreateTable(
                name: "ErCases",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<byte>(type: "smallint", nullable: false),
                    Severity = table.Column<byte>(type: "smallint", nullable: false),
                    Confidentiality = table.Column<byte>(type: "smallint", nullable: false),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<byte>(type: "smallint", nullable: false),
                    ReportedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IncidentOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReporterEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReporterHidden = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EscalationLevel = table.Column<byte>(type: "smallint", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    Outcome = table.Column<byte>(type: "smallint", nullable: true),
                    ActionTaken = table.Column<byte>(type: "smallint", nullable: true),
                    ResolutionSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ReopenCount = table.Column<short>(type: "smallint", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
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
                    table.PrimaryKey("PK_ErCases", x => x.Id);
                    table.CheckConstraint("CK_ErCases_Closed", "(\"Phase\" = 5) = (\"ClosedAt\" IS NOT NULL AND \"Outcome\" IS NOT NULL AND \"ActionTaken\" IS NOT NULL AND \"ResolutionSummary\" IS NOT NULL)");
                    table.CheckConstraint("CK_ErCases_Confidentiality", "\"Confidentiality\" IN (1, 2)");
                    table.CheckConstraint("CK_ErCases_Escalation", "\"EscalationLevel\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_ErCases_Hidden", "NOT \"ReporterHidden\" OR \"ReporterEmployeeId\" IS NOT NULL");
                    table.CheckConstraint("CK_ErCases_Phase", "\"Phase\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ErCases_Severity", "\"Severity\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_ErCases_Source", "\"Source\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_ErCases_Type", "\"Type\" BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_ErCases_Employees_OwnerEmployeeId",
                        column: x => x.OwnerEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ErCases_Employees_ReporterEmployeeId",
                        column: x => x.ReporterEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ErCases_ErCaseStages_StageId",
                        column: x => x.StageId,
                        principalSchema: "employee",
                        principalTable: "ErCaseStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ErCaseAccessLogs",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Action = table.Column<byte>(type: "smallint", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    At = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErCaseAccessLogs", x => x.Id);
                    table.CheckConstraint("CK_ErCaseAccessLogs_Action", "\"Action\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_ErCaseAccessLogs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ErCaseAccessLogs_ErCases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "employee",
                        principalTable: "ErCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ErCaseEntries",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Shared = table.Column<bool>(type: "boolean", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErCaseEntries", x => x.Id);
                    table.CheckConstraint("CK_ErCaseEntries_Kind", "\"Kind\" BETWEEN 1 AND 18");
                    table.ForeignKey(
                        name: "FK_ErCaseEntries_ErCases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "employee",
                        principalTable: "ErCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ErCaseEvidence",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsRestricted = table.Column<bool>(type: "boolean", nullable: false),
                    FromReporter = table.Column<bool>(type: "boolean", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErCaseEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErCaseEvidence_ErCases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "employee",
                        principalTable: "ErCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ErCaseMembers",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<byte>(type: "smallint", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErCaseMembers", x => x.Id);
                    table.CheckConstraint("CK_ErCaseMembers_Role", "\"Role\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_ErCaseMembers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ErCaseMembers_ErCases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "employee",
                        principalTable: "ErCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ErCaseParties",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Role = table.Column<byte>(type: "smallint", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErCaseParties", x => x.Id);
                    table.CheckConstraint("CK_ErCaseParties_Role", "\"Role\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_ErCaseParties_Who", "(\"EmployeeId\" IS NULL) <> (\"ExternalName\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ErCaseParties_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ErCaseParties_ErCases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "employee",
                        principalTable: "ErCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErCaseAccessLogs_CaseId_At",
                schema: "employee",
                table: "ErCaseAccessLogs",
                columns: new[] { "CaseId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_ErCaseAccessLogs_EmployeeId",
                schema: "employee",
                table: "ErCaseAccessLogs",
                column: "EmployeeId",
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ErCaseEntries_CaseId_At",
                schema: "employee",
                table: "ErCaseEntries",
                columns: new[] { "CaseId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_ErCaseEvidence_CaseId",
                schema: "employee",
                table: "ErCaseEvidence",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ErCaseMembers_EmployeeId",
                schema: "employee",
                table: "ErCaseMembers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "UX_ErCaseMembers_Case_Employee",
                schema: "employee",
                table: "ErCaseMembers",
                columns: new[] { "CaseId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErCaseParties_EmployeeId",
                schema: "employee",
                table: "ErCaseParties",
                column: "EmployeeId",
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ErCaseParties_Case_Employee",
                schema: "employee",
                table: "ErCaseParties",
                columns: new[] { "CaseId", "EmployeeId" },
                unique: true,
                filter: "\"EmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ErCases_OwnerEmployeeId",
                schema: "employee",
                table: "ErCases",
                column: "OwnerEmployeeId",
                filter: "\"OwnerEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ErCases_ReporterEmployeeId",
                schema: "employee",
                table: "ErCases",
                column: "ReporterEmployeeId",
                filter: "\"ReporterEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ErCases_StageId",
                schema: "employee",
                table: "ErCases",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_ErCases_TenantId_Phase",
                schema: "employee",
                table: "ErCases",
                columns: new[] { "TenantId", "Phase" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_ErCases_TenantId_Code",
                schema: "employee",
                table: "ErCases",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ErCaseStages_TenantId_Name",
                schema: "employee",
                table: "ErCaseStages",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            // Composite tenant FKs, grants aur RLS (tenancy audit 03/04 jaisa) — HR.Shared.Library TenantMigrations
            const string schema = "employee";
            migrationBuilder.AddTenantKey(schema, "ErCaseStages");
            migrationBuilder.AddTenantKey(schema, "ErCases");
            migrationBuilder.AddTenantForeignKey(schema, "ErCases", "StageId", "ErCaseStages");
            migrationBuilder.AddTenantForeignKey(schema, "ErCases", "ReporterEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "ErCases", "OwnerEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "ErCaseMembers", "CaseId", "ErCases", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "ErCaseMembers", "EmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "ErCaseParties", "CaseId", "ErCases", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "ErCaseParties", "EmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "ErCaseEntries", "CaseId", "ErCases", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "ErCaseEvidence", "CaseId", "ErCases", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "ErCaseAccessLogs", "CaseId", "ErCases", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "ErCaseAccessLogs", "EmployeeId", "Employees");
            migrationBuilder.EnableTenantIsolation(schema, "hr_employee_app",
                "ErCaseStages", "ErCases", "ErCaseMembers", "ErCaseParties", "ErCaseEntries", "ErCaseEvidence", "ErCaseAccessLogs");
            // Access log sirf likha jata hai: app na badal sake na mita sake (case delete hone par CASCADE owner role chalata hai)
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON employee.\"ErCaseAccessLogs\" FROM hr_employee_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErCaseAccessLogs",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "ErCaseEntries",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "ErCaseEvidence",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "ErCaseMembers",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "ErCaseParties",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "ErCases",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "ErCaseStages",
                schema: "employee");
        }
    }
}
