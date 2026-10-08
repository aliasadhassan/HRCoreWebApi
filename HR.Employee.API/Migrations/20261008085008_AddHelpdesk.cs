using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpdesk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelpdeskCategories",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Icon = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NeedsManagerApproval = table.Column<bool>(type: "boolean", nullable: false),
                    IsConfidential = table.Column<bool>(type: "boolean", nullable: false),
                    ResolutionHours = table.Column<short>(type: "smallint", nullable: true),
                    DefaultAssigneeEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_HelpdeskCategories", x => x.Id);
                    table.CheckConstraint("CK_HelpdeskCategories_Confidential", "NOT (\"IsConfidential\" AND \"NeedsManagerApproval\")");
                    table.CheckConstraint("CK_HelpdeskCategories_Hours", "\"ResolutionHours\" IS NULL OR \"ResolutionHours\" BETWEEN 1 AND 2000");
                    table.ForeignKey(
                        name: "FK_HelpdeskCategories_Employees_DefaultAssigneeEmployeeId",
                        column: x => x.DefaultAssigneeEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskTickets",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    IsConfidential = table.Column<bool>(type: "boolean", nullable: false),
                    RaisedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApproverEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AssigneeEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    FirstResponseAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    SatisfactionRating = table.Column<byte>(type: "smallint", nullable: true),
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
                    table.PrimaryKey("PK_HelpdeskTickets", x => x.Id);
                    table.CheckConstraint("CK_HelpdeskTickets_Approval", "\"Status\" <> 1 OR \"ApproverEmployeeId\" IS NOT NULL");
                    table.CheckConstraint("CK_HelpdeskTickets_Priority", "\"Priority\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_HelpdeskTickets_Rating", "\"SatisfactionRating\" IS NULL OR \"SatisfactionRating\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_HelpdeskTickets_Status", "\"Status\" BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_HelpdeskTickets_Employees_ApproverEmployeeId",
                        column: x => x.ApproverEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskTickets_Employees_AssigneeEmployeeId",
                        column: x => x.AssigneeEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskTickets_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskTickets_HelpdeskCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "employee",
                        principalTable: "HelpdeskCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketActivities",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    FromStatus = table.Column<byte>(type: "smallint", nullable: true),
                    ToStatus = table.Column<byte>(type: "smallint", nullable: false),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketActivities", x => x.Id);
                    table.CheckConstraint("CK_TicketActivities_Kind", "\"Kind\" BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_TicketActivities_Status", "\"ToStatus\" BETWEEN 1 AND 8 AND (\"FromStatus\" IS NULL OR \"FromStatus\" BETWEEN 1 AND 8)");
                    table.ForeignKey(
                        name: "FK_TicketActivities_HelpdeskTickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "employee",
                        principalTable: "HelpdeskTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskCategories_DefaultAssigneeEmployeeId",
                schema: "employee",
                table: "HelpdeskCategories",
                column: "DefaultAssigneeEmployeeId",
                filter: "\"DefaultAssigneeEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_HelpdeskCategories_TenantId_Name",
                schema: "employee",
                table: "HelpdeskCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_ApproverEmployeeId",
                schema: "employee",
                table: "HelpdeskTickets",
                column: "ApproverEmployeeId",
                filter: "\"ApproverEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_AssigneeEmployeeId",
                schema: "employee",
                table: "HelpdeskTickets",
                column: "AssigneeEmployeeId",
                filter: "\"AssigneeEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_CategoryId",
                schema: "employee",
                table: "HelpdeskTickets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_EmployeeId",
                schema: "employee",
                table: "HelpdeskTickets",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_TenantId_Status",
                schema: "employee",
                table: "HelpdeskTickets",
                columns: new[] { "TenantId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_HelpdeskTickets_TenantId_Code",
                schema: "employee",
                table: "HelpdeskTickets",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_TenantId_At",
                schema: "employee",
                table: "TicketActivities",
                columns: new[] { "TenantId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_TicketId_At",
                schema: "employee",
                table: "TicketActivities",
                columns: new[] { "TicketId", "At" });

            // Composite tenant FKs, grants aur RLS (tenancy audit 03/04 jaisa)
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_HelpdeskCategories_TenantId_Id"" ON employee.""HelpdeskCategories"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_HelpdeskTickets_TenantId_Id"" ON employee.""HelpdeskTickets"" (""TenantId"", ""Id"");
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_HelpdeskCategories_DefaultAssigneeEmployeeId_Tenant' AND conrelid = 'employee.""HelpdeskCategories""'::regclass) THEN
  ALTER TABLE employee.""HelpdeskCategories"" ADD CONSTRAINT ""FK_HelpdeskCategories_DefaultAssigneeEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""DefaultAssigneeEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_HelpdeskTickets_EmployeeId_Tenant' AND conrelid = 'employee.""HelpdeskTickets""'::regclass) THEN
  ALTER TABLE employee.""HelpdeskTickets"" ADD CONSTRAINT ""FK_HelpdeskTickets_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_HelpdeskTickets_CategoryId_Tenant' AND conrelid = 'employee.""HelpdeskTickets""'::regclass) THEN
  ALTER TABLE employee.""HelpdeskTickets"" ADD CONSTRAINT ""FK_HelpdeskTickets_CategoryId_Tenant"" FOREIGN KEY (""TenantId"", ""CategoryId"") REFERENCES employee.""HelpdeskCategories"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_HelpdeskTickets_ApproverEmployeeId_Tenant' AND conrelid = 'employee.""HelpdeskTickets""'::regclass) THEN
  ALTER TABLE employee.""HelpdeskTickets"" ADD CONSTRAINT ""FK_HelpdeskTickets_ApproverEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""ApproverEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_HelpdeskTickets_AssigneeEmployeeId_Tenant' AND conrelid = 'employee.""HelpdeskTickets""'::regclass) THEN
  ALTER TABLE employee.""HelpdeskTickets"" ADD CONSTRAINT ""FK_HelpdeskTickets_AssigneeEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""AssigneeEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TicketActivities_TicketId_Tenant' AND conrelid = 'employee.""TicketActivities""'::regclass) THEN
  ALTER TABLE employee.""TicketActivities"" ADD CONSTRAINT ""FK_TicketActivities_TicketId_Tenant"" FOREIGN KEY (""TenantId"", ""TicketId"") REFERENCES employee.""HelpdeskTickets"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
GRANT SELECT, INSERT, UPDATE, DELETE ON employee.""HelpdeskCategories"", employee.""HelpdeskTickets"", employee.""TicketActivities"" TO hr_employee_app;
ALTER TABLE employee.""HelpdeskCategories"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""HelpdeskCategories"";
CREATE POLICY tenant_isolation ON employee.""HelpdeskCategories"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""HelpdeskTickets"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""HelpdeskTickets"";
CREATE POLICY tenant_isolation ON employee.""HelpdeskTickets"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""TicketActivities"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""TicketActivities"";
CREATE POLICY tenant_isolation ON employee.""TicketActivities"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketActivities",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "HelpdeskTickets",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "HelpdeskCategories",
                schema: "employee");
        }
    }
}
