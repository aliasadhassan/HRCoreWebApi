using System;
using HR.Shared.Library.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HsChecklists",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_HsChecklists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HsIncidents",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<byte>(type: "smallint", nullable: false),
                    Severity = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Area = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReporterEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    AffectedEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffectedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Treatment = table.Column<byte>(type: "smallint", nullable: true),
                    InjuryNature = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BodyPart = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LostDays = table.Column<short>(type: "smallint", nullable: false),
                    Witnesses = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImmediateAction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InvestigatorEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RootCauseCategory = table.Column<byte>(type: "smallint", nullable: true),
                    RootCause = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsReportable = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorityNotifiedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AuthorityReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ClosureNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_HsIncidents", x => x.Id);
                    table.CheckConstraint("CK_HsIncidents_Affected", "\"AffectedEmployeeId\" IS NULL OR \"AffectedName\" IS NULL");
                    table.CheckConstraint("CK_HsIncidents_Anonymous", "NOT \"Anonymous\" OR \"ReporterEmployeeId\" IS NOT NULL");
                    table.CheckConstraint("CK_HsIncidents_Closed", "(\"Status\" = 4) = (\"ClosedAt\" IS NOT NULL AND \"ClosureNote\" IS NOT NULL)");
                    table.CheckConstraint("CK_HsIncidents_LostDays", "\"LostDays\" BETWEEN 0 AND 999");
                    table.CheckConstraint("CK_HsIncidents_RootCause", "\"RootCauseCategory\" IS NULL OR \"RootCauseCategory\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_HsIncidents_Severity", "\"Severity\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_HsIncidents_Status", "\"Status\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_HsIncidents_Treatment", "\"Treatment\" IS NULL OR \"Treatment\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_HsIncidents_Type", "\"Type\" BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_HsIncidents_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "employee",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HsIncidents_Employees_AffectedEmployeeId",
                        column: x => x.AffectedEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HsIncidents_Employees_InvestigatorEmployeeId",
                        column: x => x.InvestigatorEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HsIncidents_Employees_ReporterEmployeeId",
                        column: x => x.ReporterEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HsIncidents_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "employee",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HsChecklistItems",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    Text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HsChecklistItems_HsChecklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalSchema: "employee",
                        principalTable: "HsChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HsInspections",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Area = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    InspectorEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RepeatDays = table.Column<short>(type: "smallint", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ScorePercent = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_HsInspections", x => x.Id);
                    table.CheckConstraint("CK_HsInspections_Completed", "(\"Status\" = 2) = (\"CompletedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_HsInspections_Repeat", "\"RepeatDays\" IS NULL OR \"RepeatDays\" BETWEEN 7 AND 365");
                    table.CheckConstraint("CK_HsInspections_Score", "\"ScorePercent\" IS NULL OR \"ScorePercent\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_HsInspections_Status", "\"Status\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_HsInspections_Employees_InspectorEmployeeId",
                        column: x => x.InspectorEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HsInspections_HsChecklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalSchema: "employee",
                        principalTable: "HsChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HsInspections_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "employee",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HsIncidentEntries",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsIncidentEntries", x => x.Id);
                    table.CheckConstraint("CK_HsIncidentEntries_Kind", "\"Kind\" BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_HsIncidentEntries_HsIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "employee",
                        principalTable: "HsIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HsActions",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: true),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssigneeEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<byte>(type: "smallint", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    CompletionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    VerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_HsActions", x => x.Id);
                    table.CheckConstraint("CK_HsActions_Done", "\"Status\" NOT IN (3, 4) OR \"CompletedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_HsActions_Priority", "\"Priority\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_HsActions_Source", "\"IncidentId\" IS NULL OR \"InspectionId\" IS NULL");
                    table.CheckConstraint("CK_HsActions_Status", "\"Status\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_HsActions_Verified", "(\"Status\" = 4) = (\"VerifiedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_HsActions_Employees_AssigneeEmployeeId",
                        column: x => x.AssigneeEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HsActions_HsIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "employee",
                        principalTable: "HsIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HsActions_HsInspections_InspectionId",
                        column: x => x.InspectionId,
                        principalSchema: "employee",
                        principalTable: "HsInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HsInspectionLines",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    Text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Result = table.Column<byte>(type: "smallint", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HsInspectionLines", x => x.Id);
                    table.CheckConstraint("CK_HsInspectionLines_Result", "\"Result\" IS NULL OR \"Result\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_HsInspectionLines_HsInspections_InspectionId",
                        column: x => x.InspectionId,
                        principalSchema: "employee",
                        principalTable: "HsInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HsActions_AssigneeEmployeeId",
                schema: "employee",
                table: "HsActions",
                column: "AssigneeEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HsActions_IncidentId",
                schema: "employee",
                table: "HsActions",
                column: "IncidentId",
                filter: "\"IncidentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HsActions_InspectionId",
                schema: "employee",
                table: "HsActions",
                column: "InspectionId",
                filter: "\"InspectionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HsActions_TenantId_Status_DueOn",
                schema: "employee",
                table: "HsActions",
                columns: new[] { "TenantId", "Status", "DueOn" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HsChecklistItems_ChecklistId_SortOrder",
                schema: "employee",
                table: "HsChecklistItems",
                columns: new[] { "ChecklistId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_HsChecklists_TenantId_Name",
                schema: "employee",
                table: "HsChecklists",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidentEntries_IncidentId_At",
                schema: "employee",
                table: "HsIncidentEntries",
                columns: new[] { "IncidentId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_AffectedEmployeeId",
                schema: "employee",
                table: "HsIncidents",
                column: "AffectedEmployeeId",
                filter: "\"AffectedEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_DepartmentId",
                schema: "employee",
                table: "HsIncidents",
                column: "DepartmentId",
                filter: "\"DepartmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_InvestigatorEmployeeId",
                schema: "employee",
                table: "HsIncidents",
                column: "InvestigatorEmployeeId",
                filter: "\"InvestigatorEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_LocationId",
                schema: "employee",
                table: "HsIncidents",
                column: "LocationId",
                filter: "\"LocationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_ReporterEmployeeId",
                schema: "employee",
                table: "HsIncidents",
                column: "ReporterEmployeeId",
                filter: "\"ReporterEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_TenantId_OccurredAt",
                schema: "employee",
                table: "HsIncidents",
                columns: new[] { "TenantId", "OccurredAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HsIncidents_TenantId_Status",
                schema: "employee",
                table: "HsIncidents",
                columns: new[] { "TenantId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_HsIncidents_TenantId_Code",
                schema: "employee",
                table: "HsIncidents",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HsInspectionLines_InspectionId_SortOrder",
                schema: "employee",
                table: "HsInspectionLines",
                columns: new[] { "InspectionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HsInspections_ChecklistId",
                schema: "employee",
                table: "HsInspections",
                column: "ChecklistId",
                filter: "\"ChecklistId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HsInspections_InspectorEmployeeId",
                schema: "employee",
                table: "HsInspections",
                column: "InspectorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HsInspections_LocationId",
                schema: "employee",
                table: "HsInspections",
                column: "LocationId",
                filter: "\"LocationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HsInspections_TenantId_Status_DueOn",
                schema: "employee",
                table: "HsInspections",
                columns: new[] { "TenantId", "Status", "DueOn" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_HsInspections_TenantId_Code",
                schema: "employee",
                table: "HsInspections",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            // Composite tenant FKs, grants aur RLS (tenancy audit 03/04 jaisa) — HR.Shared.Library TenantMigrations
            const string schema = "employee";
            migrationBuilder.AddTenantKey(schema, "HsIncidents");
            migrationBuilder.AddTenantKey(schema, "HsChecklists");
            migrationBuilder.AddTenantKey(schema, "HsInspections");
            migrationBuilder.AddTenantForeignKey(schema, "HsIncidents", "LocationId", "Locations");
            migrationBuilder.AddTenantForeignKey(schema, "HsIncidents", "DepartmentId", "Departments");
            migrationBuilder.AddTenantForeignKey(schema, "HsIncidents", "ReporterEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "HsIncidents", "AffectedEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "HsIncidents", "InvestigatorEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "HsIncidentEntries", "IncidentId", "HsIncidents", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "HsActions", "IncidentId", "HsIncidents");
            migrationBuilder.AddTenantForeignKey(schema, "HsActions", "InspectionId", "HsInspections");
            migrationBuilder.AddTenantForeignKey(schema, "HsActions", "AssigneeEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "HsChecklistItems", "ChecklistId", "HsChecklists", "CASCADE");
            migrationBuilder.AddTenantForeignKey(schema, "HsInspections", "ChecklistId", "HsChecklists");
            migrationBuilder.AddTenantForeignKey(schema, "HsInspections", "LocationId", "Locations");
            migrationBuilder.AddTenantForeignKey(schema, "HsInspections", "InspectorEmployeeId", "Employees");
            migrationBuilder.AddTenantForeignKey(schema, "HsInspectionLines", "InspectionId", "HsInspections", "CASCADE");
            migrationBuilder.EnableTenantIsolation(schema, "hr_employee_app",
                "HsIncidents", "HsIncidentEntries", "HsActions", "HsChecklists", "HsChecklistItems", "HsInspections", "HsInspectionLines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HsActions",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "HsChecklistItems",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "HsIncidentEntries",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "HsInspectionLines",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "HsIncidents",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "HsInspections",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "HsChecklists",
                schema: "employee");
        }
    }
}
