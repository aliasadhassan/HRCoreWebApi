using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Employee.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewCycles",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    IncludeSelfReview = table.Column<bool>(type: "boolean", nullable: false),
                    SelfReviewDue = table.Column<DateOnly>(type: "date", nullable: true),
                    ManagerReviewDue = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    LaunchedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
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
                    table.PrimaryKey("PK_ReviewCycles", x => x.Id);
                    table.CheckConstraint("CK_ReviewCycles_Period", "\"PeriodEnd\" >= \"PeriodStart\"");
                    table.CheckConstraint("CK_ReviewCycles_SelfDue", "(\"IncludeSelfReview\" AND \"SelfReviewDue\" IS NOT NULL AND \"SelfReviewDue\" <= \"ManagerReviewDue\") OR (NOT \"IncludeSelfReview\" AND \"SelfReviewDue\" IS NULL)");
                    table.CheckConstraint("CK_ReviewCycles_Status", "\"Status\" BETWEEN 1 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "Goals",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewCycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Weight = table.Column<byte>(type: "smallint", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Progress = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
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
                    table.PrimaryKey("PK_Goals", x => x.Id);
                    table.CheckConstraint("CK_Goals_Dates", "\"StartDate\" IS NULL OR \"DueDate\" >= \"StartDate\"");
                    table.CheckConstraint("CK_Goals_Progress", "\"Progress\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_Goals_Status", "\"Status\" BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_Goals_Weight", "\"Weight\" IS NULL OR \"Weight\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_Goals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Goals_ReviewCycles_ReviewCycleId",
                        column: x => x.ReviewCycleId,
                        principalSchema: "employee",
                        principalTable: "ReviewCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceReviews",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    SelfRating = table.Column<byte>(type: "smallint", nullable: true),
                    SelfSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SelfSubmittedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ManagerRating = table.Column<byte>(type: "smallint", nullable: true),
                    ManagerSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Strengths = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Improvements = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ManagerSubmittedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    ManagerSubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
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
                    table.PrimaryKey("PK_PerformanceReviews", x => x.Id);
                    table.CheckConstraint("CK_PerformanceReviews_ManagerRating", "\"ManagerRating\" IS NULL OR \"ManagerRating\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_PerformanceReviews_Reviewer", "\"ReviewerEmployeeId\" IS NULL OR \"ReviewerEmployeeId\" <> \"EmployeeId\"");
                    table.CheckConstraint("CK_PerformanceReviews_SelfRating", "\"SelfRating\" IS NULL OR \"SelfRating\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_PerformanceReviews_Shared", "\"Status\" < 3 OR (\"ManagerRating\" IS NOT NULL AND \"ManagerSubmittedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_PerformanceReviews_Status", "\"Status\" BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_Employees_ReviewerEmployeeId",
                        column: x => x.ReviewerEmployeeId,
                        principalSchema: "employee",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceReviews_ReviewCycles_ReviewCycleId",
                        column: x => x.ReviewCycleId,
                        principalSchema: "employee",
                        principalTable: "ReviewCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalCheckIns",
                schema: "employee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Progress = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalCheckIns", x => x.Id);
                    table.CheckConstraint("CK_GoalCheckIns_Progress", "\"Progress\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_GoalCheckIns_Status", "\"Status\" BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_GoalCheckIns_Goals_GoalId",
                        column: x => x.GoalId,
                        principalSchema: "employee",
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoalCheckIns_GoalId_At",
                schema: "employee",
                table: "GoalCheckIns",
                columns: new[] { "GoalId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalCheckIns_TenantId_At",
                schema: "employee",
                table: "GoalCheckIns",
                columns: new[] { "TenantId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Goals_EmployeeId",
                schema: "employee",
                table: "Goals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_ReviewCycleId",
                schema: "employee",
                table: "Goals",
                column: "ReviewCycleId",
                filter: "\"ReviewCycleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_TenantId_EmployeeId_Status",
                schema: "employee",
                table: "Goals",
                columns: new[] { "TenantId", "EmployeeId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_EmployeeId",
                schema: "employee",
                table: "PerformanceReviews",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewCycleId",
                schema: "employee",
                table: "PerformanceReviews",
                column: "ReviewCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_ReviewerEmployeeId",
                schema: "employee",
                table: "PerformanceReviews",
                column: "ReviewerEmployeeId",
                filter: "\"ReviewerEmployeeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PerformanceReviews_Cycle_Employee",
                schema: "employee",
                table: "PerformanceReviews",
                columns: new[] { "TenantId", "ReviewCycleId", "EmployeeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCycles_TenantId_Name",
                schema: "employee",
                table: "ReviewCycles",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCycles_TenantId_Status",
                schema: "employee",
                table: "ReviewCycles",
                columns: new[] { "TenantId", "Status" },
                filter: "\"IsDeleted\" = false");

            // Tenant safety (03/04 jaisa): composite tenant FKs, app role grants, RLS
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_ReviewCycles_TenantId_Id"" ON employee.""ReviewCycles"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Goals_TenantId_Id"" ON employee.""Goals"" (""TenantId"", ""Id"");
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PerformanceReviews_ReviewCycleId_Tenant' AND conrelid = 'employee.""PerformanceReviews""'::regclass) THEN
  ALTER TABLE employee.""PerformanceReviews"" ADD CONSTRAINT ""FK_PerformanceReviews_ReviewCycleId_Tenant"" FOREIGN KEY (""TenantId"", ""ReviewCycleId"") REFERENCES employee.""ReviewCycles"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PerformanceReviews_EmployeeId_Tenant' AND conrelid = 'employee.""PerformanceReviews""'::regclass) THEN
  ALTER TABLE employee.""PerformanceReviews"" ADD CONSTRAINT ""FK_PerformanceReviews_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PerformanceReviews_ReviewerEmployeeId_Tenant' AND conrelid = 'employee.""PerformanceReviews""'::regclass) THEN
  ALTER TABLE employee.""PerformanceReviews"" ADD CONSTRAINT ""FK_PerformanceReviews_ReviewerEmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""ReviewerEmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Goals_EmployeeId_Tenant' AND conrelid = 'employee.""Goals""'::regclass) THEN
  ALTER TABLE employee.""Goals"" ADD CONSTRAINT ""FK_Goals_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES employee.""Employees"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Goals_ReviewCycleId_Tenant' AND conrelid = 'employee.""Goals""'::regclass) THEN
  ALTER TABLE employee.""Goals"" ADD CONSTRAINT ""FK_Goals_ReviewCycleId_Tenant"" FOREIGN KEY (""TenantId"", ""ReviewCycleId"") REFERENCES employee.""ReviewCycles"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_GoalCheckIns_GoalId_Tenant' AND conrelid = 'employee.""GoalCheckIns""'::regclass) THEN
  ALTER TABLE employee.""GoalCheckIns"" ADD CONSTRAINT ""FK_GoalCheckIns_GoalId_Tenant"" FOREIGN KEY (""TenantId"", ""GoalId"") REFERENCES employee.""Goals"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
GRANT SELECT, INSERT, UPDATE, DELETE ON employee.""ReviewCycles"", employee.""PerformanceReviews"", employee.""Goals"", employee.""GoalCheckIns"" TO hr_employee_app;
ALTER TABLE employee.""ReviewCycles"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""ReviewCycles"";
CREATE POLICY tenant_isolation ON employee.""ReviewCycles"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""PerformanceReviews"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""PerformanceReviews"";
CREATE POLICY tenant_isolation ON employee.""PerformanceReviews"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""Goals"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""Goals"";
CREATE POLICY tenant_isolation ON employee.""Goals"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE employee.""GoalCheckIns"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON employee.""GoalCheckIns"";
CREATE POLICY tenant_isolation ON employee.""GoalCheckIns"" TO hr_employee_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoalCheckIns",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "PerformanceReviews",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "Goals",
                schema: "employee");

            migrationBuilder.DropTable(
                name: "ReviewCycles",
                schema: "employee");
        }
    }
}
