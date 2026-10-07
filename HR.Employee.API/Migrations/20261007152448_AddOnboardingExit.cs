using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingExit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChecklistTemplates",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ChecklistTemplates", x => x.Id);
                    table.CheckConstraint("CK_ChecklistTemplates_Kind", "\"Kind\" IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "ChecklistTemplateTasks",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Owner = table.Column<byte>(type: "smallint", nullable: false),
                    DueOffsetDays = table.Column<short>(type: "smallint", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTemplateTasks", x => x.Id);
                    table.CheckConstraint("CK_ChecklistTemplateTasks_Offset", "\"DueOffsetDays\" BETWEEN -365 AND 365");
                    table.CheckConstraint("CK_ChecklistTemplateTasks_Owner", "\"Owner\" BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_ChecklistTemplateTasks_ChecklistTemplates_ChecklistTemplate~",
                        column: x => x.ChecklistTemplateId,
                        principalSchema: "employee",
                        principalTable: "ChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LifecycleCases",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    ChecklistTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    AnchorDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExitType = table.Column<byte>(type: "smallint", nullable: true),
                    NoticeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EligibleForRehire = table.Column<bool>(type: "boolean", nullable: true),
                    InterviewNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_LifecycleCases", x => x.Id);
                    table.CheckConstraint("CK_LifecycleCases_Closed", "(\"Status\" = 1) = (\"ClosedAt\" IS NULL)");
                    table.CheckConstraint("CK_LifecycleCases_Exit", "\"Kind\" = 1 OR (\"ExitType\" IS NOT NULL AND \"NoticeDate\" IS NOT NULL AND \"Reason\" IS NOT NULL AND \"AnchorDate\" >= \"NoticeDate\")");
                    table.CheckConstraint("CK_LifecycleCases_Kind", "\"Kind\" IN (1, 2)");
                    table.CheckConstraint("CK_LifecycleCases_Status", "\"Status\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_LifecycleCases_ChecklistTemplates_ChecklistTemplateId",
                        column: x => x.ChecklistTemplateId,
                        principalSchema: "employee",
                        principalTable: "ChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LifecycleCases_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LifecycleTasks",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LifecycleCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Owner = table.Column<byte>(type: "smallint", nullable: false),
                    AssigneeEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifecycleTasks", x => x.Id);
                    table.CheckConstraint("CK_LifecycleTasks_Closed", "(\"Status\" = 1) = (\"CompletedAt\" IS NULL)");
                    table.CheckConstraint("CK_LifecycleTasks_Owner", "\"Owner\" BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_LifecycleTasks_Status", "\"Status\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_LifecycleTasks_Employees_AssigneeEmployeeId",
                        column: x => x.AssigneeEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LifecycleTasks_LifecycleCases_LifecycleCaseId",
                        column: x => x.LifecycleCaseId,
                        principalSchema: "employee",
                        principalTable: "LifecycleCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplates_TenantId_Kind_Name",
                schema: "employee",
                table: "ChecklistTemplates",
                columns: new[] { "TenantId", "Kind", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_ChecklistTemplates_TenantId_Kind_Default",
                schema: "employee",
                table: "ChecklistTemplates",
                columns: new[] { "TenantId", "Kind" },
                unique: true,
                filter: "\"IsDefault\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplateTasks_ChecklistTemplateId_SortOrder",
                schema: "employee",
                table: "ChecklistTemplateTasks",
                columns: new[] { "ChecklistTemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleCases_ChecklistTemplateId",
                schema: "employee",
                table: "LifecycleCases",
                column: "ChecklistTemplateId",
                filter: "\"ChecklistTemplateId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleCases_TenantId_Kind_Status",
                schema: "employee",
                table: "LifecycleCases",
                columns: new[] { "TenantId", "Kind", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_LifecycleCases_EmployeeId_Kind_Open",
                schema: "employee",
                table: "LifecycleCases",
                columns: new[] { "EmployeeId", "Kind" },
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleTasks_AssigneeEmployeeId",
                schema: "employee",
                table: "LifecycleTasks",
                column: "AssigneeEmployeeId",
                filter: "\"AssigneeEmployeeId\" IS NOT NULL AND \"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleTasks_LifecycleCaseId_SortOrder",
                schema: "employee",
                table: "LifecycleTasks",
                columns: new[] { "LifecycleCaseId", "SortOrder" });

            // Tenancy (step 03/04 jaisa): composite FKs (TenantId, XId), hr_employee_app grants, RLS. Idempotent.
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_ChecklistTemplates_TenantId_Id"" ON employee.""ChecklistTemplates"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_LifecycleCases_TenantId_Id"" ON employee.""LifecycleCases"" (""TenantId"", ""Id"");
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ChecklistTemplateTasks_ChecklistTemplateId_Tenant' AND conrelid = 'employee.""ChecklistTemplateTasks""'::regclass) THEN
  ALTER TABLE employee.""ChecklistTemplateTasks"" ADD CONSTRAINT ""FK_ChecklistTemplateTasks_ChecklistTemplateId_Tenant"" FOREIGN KEY (""TenantId"", ""ChecklistTemplateId"") REFERENCES employee.""ChecklistTemplates"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LifecycleCases_EmployeeId_Tenant' AND conrelid = 'employee.""LifecycleCases""'::regclass) THEN
  ALTER TABLE employee.""LifecycleCases"" ADD CONSTRAINT ""FK_LifecycleCases_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LifecycleCases_ChecklistTemplateId_Tenant' AND conrelid = 'employee.""LifecycleCases""'::regclass) THEN
  ALTER TABLE employee.""LifecycleCases"" ADD CONSTRAINT ""FK_LifecycleCases_ChecklistTemplateId_Tenant"" FOREIGN KEY (""TenantId"", ""ChecklistTemplateId"") REFERENCES employee.""ChecklistTemplates"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LifecycleTasks_LifecycleCaseId_Tenant' AND conrelid = 'employee.""LifecycleTasks""'::regclass) THEN
  ALTER TABLE employee.""LifecycleTasks"" ADD CONSTRAINT ""FK_LifecycleTasks_LifecycleCaseId_Tenant"" FOREIGN KEY (""TenantId"", ""LifecycleCaseId"") REFERENCES employee.""LifecycleCases"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LifecycleTasks_AssigneeEmployeeId_Tenant' AND conrelid = 'employee.""LifecycleTasks""'::regclass) THEN
  ALTER TABLE employee.""LifecycleTasks"" ADD CONSTRAINT ""FK_LifecycleTasks_AssigneeEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""AssigneeEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
GRANT SELECT, INSERT, UPDATE, DELETE ON employee.""ChecklistTemplates"", employee.""ChecklistTemplateTasks"", employee.""LifecycleCases"", employee.""LifecycleTasks"" TO hr_employee_app;
ALTER TABLE employee.""ChecklistTemplates"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""ChecklistTemplates"";
CREATE POLICY tenant_isolation ON employee.""ChecklistTemplates"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""ChecklistTemplateTasks"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""ChecklistTemplateTasks"";
CREATE POLICY tenant_isolation ON employee.""ChecklistTemplateTasks"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""LifecycleCases"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""LifecycleCases"";
CREATE POLICY tenant_isolation ON employee.""LifecycleCases"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""LifecycleTasks"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""LifecycleTasks"";
CREATE POLICY tenant_isolation ON employee.""LifecycleTasks"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChecklistTemplateTasks",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "LifecycleTasks",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "LifecycleCases",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "ChecklistTemplates",
                schema: "employee");
        }
    }
}
