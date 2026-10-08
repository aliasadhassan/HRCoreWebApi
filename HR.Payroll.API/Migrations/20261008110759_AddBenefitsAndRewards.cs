using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Payroll.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBenefitsAndRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenefitPlans",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BenefitType = table.Column<byte>(type: "smallint", nullable: false),
                    Provider = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    EmployerMonthlyCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployeeMonthlyCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DependentMonthlyCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDependents = table.Column<short>(type: "smallint", nullable: false),
                    DeductionComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenForRequests = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitPlans", x => x.Id);
                    table.CheckConstraint("CK_BenefitPlans_Costs", "\"EmployerMonthlyCost\" >= 0 AND \"EmployeeMonthlyCost\" >= 0 AND \"DependentMonthlyCost\" >= 0");
                    table.CheckConstraint("CK_BenefitPlans_Dependents", "\"MaxDependents\" BETWEEN 0 AND 10");
                    table.CheckConstraint("CK_BenefitPlans_Order", "\"SortOrder\" BETWEEN 0 AND 999");
                    table.ForeignKey(
                        name: "FK_BenefitPlans_PayComponents_DeductionComponentId",
                        column: x => x.DeductionComponentId,
                        principalSchema: "payroll",
                        principalTable: "PayComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BonusAwards",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BonusType = table.Column<byte>(type: "smallint", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    PayoutMethod = table.Column<byte>(type: "smallint", nullable: true),
                    PayrollInputId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonusAwards", x => x.Id);
                    table.CheckConstraint("CK_BonusAwards_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_BonusAwards_Approved", "\"Status\" NOT IN (2, 5) OR \"PayoutMethod\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_BonusAwards_PayComponents_PayComponentId",
                        column: x => x.PayComponentId,
                        principalSchema: "payroll",
                        principalTable: "PayComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BonusAwards_PayrollEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "payroll",
                        principalTable: "PayrollEmployees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BonusAwards_PayrollInputs_PayrollInputId",
                        column: x => x.PayrollInputId,
                        principalSchema: "payroll",
                        principalTable: "PayrollInputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryRevisions",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<byte>(type: "smallint", nullable: false),
                    CurrentSalaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    SalaryBasis = table.Column<byte>(type: "smallint", nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProposedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    NewTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Justification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    AppliedSalaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryRevisions", x => x.Id);
                    table.CheckConstraint("CK_SalaryRevisions_Amounts", "\"CurrentAmount\" > 0 AND \"ProposedAmount\" > 0");
                    table.CheckConstraint("CK_SalaryRevisions_Applied", "\"Status\" <> 2 OR \"AppliedSalaryId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_SalaryRevisions_EmployeeSalaries_AppliedSalaryId",
                        column: x => x.AppliedSalaryId,
                        principalSchema: "payroll",
                        principalTable: "EmployeeSalaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryRevisions_EmployeeSalaries_CurrentSalaryId",
                        column: x => x.CurrentSalaryId,
                        principalSchema: "payroll",
                        principalTable: "EmployeeSalaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryRevisions_PayrollEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "payroll",
                        principalTable: "PayrollEmployees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BenefitEnrolments",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BenefitPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Dependents = table.Column<short>(type: "smallint", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployerMonthlyCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployeeMonthlyCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployeeNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitEnrolments", x => x.Id);
                    table.CheckConstraint("CK_BenefitEnrolments_Active", "\"Status\" NOT IN (2, 3) OR \"StartDate\" IS NOT NULL");
                    table.CheckConstraint("CK_BenefitEnrolments_Costs", "\"EmployerMonthlyCost\" >= 0 AND \"EmployeeMonthlyCost\" >= 0");
                    table.CheckConstraint("CK_BenefitEnrolments_Dates", "\"EndDate\" IS NULL OR (\"StartDate\" IS NOT NULL AND \"EndDate\" >= \"StartDate\")");
                    table.CheckConstraint("CK_BenefitEnrolments_Dependents", "\"Dependents\" BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "FK_BenefitEnrolments_BenefitPlans_BenefitPlanId",
                        column: x => x.BenefitPlanId,
                        principalSchema: "payroll",
                        principalTable: "BenefitPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenefitEnrolments_PayrollEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "payroll",
                        principalTable: "PayrollEmployees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BenefitEnrolments_BenefitPlanId_EmployeeId",
                schema: "payroll",
                table: "BenefitEnrolments",
                columns: new[] { "BenefitPlanId", "EmployeeId" },
                unique: true,
                filter: "\"Status\" IN (1, 2) AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_BenefitEnrolments_EmployeeId_Status",
                schema: "payroll",
                table: "BenefitEnrolments",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPlans_DeductionComponentId",
                schema: "payroll",
                table: "BenefitPlans",
                column: "DeductionComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPlans_TenantId_Name",
                schema: "payroll",
                table: "BenefitPlans",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_BonusAwards_BatchId",
                schema: "payroll",
                table: "BonusAwards",
                column: "BatchId",
                filter: "\"BatchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BonusAwards_EmployeeId_CreatedAt",
                schema: "payroll",
                table: "BonusAwards",
                columns: new[] { "EmployeeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BonusAwards_PayComponentId",
                schema: "payroll",
                table: "BonusAwards",
                column: "PayComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_BonusAwards_PayrollInputId",
                schema: "payroll",
                table: "BonusAwards",
                column: "PayrollInputId");

            migrationBuilder.CreateIndex(
                name: "IX_BonusAwards_TenantId_Status",
                schema: "payroll",
                table: "BonusAwards",
                columns: new[] { "TenantId", "Status" },
                filter: "\"Status\" IN (1, 2) AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRevisions_AppliedSalaryId",
                schema: "payroll",
                table: "SalaryRevisions",
                column: "AppliedSalaryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRevisions_CurrentSalaryId",
                schema: "payroll",
                table: "SalaryRevisions",
                column: "CurrentSalaryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRevisions_EmployeeId",
                schema: "payroll",
                table: "SalaryRevisions",
                column: "EmployeeId",
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRevisions_TenantId_Status_EffectiveFrom",
                schema: "payroll",
                table: "SalaryRevisions",
                columns: new[] { "TenantId", "Status", "EffectiveFrom" });

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_EmployeeSalaries_TenantId_Id"" ON payroll.""EmployeeSalaries"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayrollInputs_TenantId_Id"" ON payroll.""PayrollInputs"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_BenefitPlans_TenantId_Id"" ON payroll.""BenefitPlans"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_BenefitEnrolments_TenantId_Id"" ON payroll.""BenefitEnrolments"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_SalaryRevisions_TenantId_Id"" ON payroll.""SalaryRevisions"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_BonusAwards_TenantId_Id"" ON payroll.""BonusAwards"" (""TenantId"", ""Id"");
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_BenefitPlans_DeductionComponentId_Tenant' AND conrelid = 'payroll.""BenefitPlans""'::regclass) THEN
  ALTER TABLE payroll.""BenefitPlans"" ADD CONSTRAINT ""FK_BenefitPlans_DeductionComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""DeductionComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_BenefitEnrolments_BenefitPlanId_Tenant' AND conrelid = 'payroll.""BenefitEnrolments""'::regclass) THEN
  ALTER TABLE payroll.""BenefitEnrolments"" ADD CONSTRAINT ""FK_BenefitEnrolments_BenefitPlanId_Tenant"" FOREIGN KEY (""TenantId"", ""BenefitPlanId"") REFERENCES payroll.""BenefitPlans"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_BenefitEnrolments_EmployeeId_Tenant' AND conrelid = 'payroll.""BenefitEnrolments""'::regclass) THEN
  ALTER TABLE payroll.""BenefitEnrolments"" ADD CONSTRAINT ""FK_BenefitEnrolments_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryRevisions_EmployeeId_Tenant' AND conrelid = 'payroll.""SalaryRevisions""'::regclass) THEN
  ALTER TABLE payroll.""SalaryRevisions"" ADD CONSTRAINT ""FK_SalaryRevisions_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryRevisions_CurrentSalaryId_Tenant' AND conrelid = 'payroll.""SalaryRevisions""'::regclass) THEN
  ALTER TABLE payroll.""SalaryRevisions"" ADD CONSTRAINT ""FK_SalaryRevisions_CurrentSalaryId_Tenant"" FOREIGN KEY (""TenantId"", ""CurrentSalaryId"") REFERENCES payroll.""EmployeeSalaries"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryRevisions_AppliedSalaryId_Tenant' AND conrelid = 'payroll.""SalaryRevisions""'::regclass) THEN
  ALTER TABLE payroll.""SalaryRevisions"" ADD CONSTRAINT ""FK_SalaryRevisions_AppliedSalaryId_Tenant"" FOREIGN KEY (""TenantId"", ""AppliedSalaryId"") REFERENCES payroll.""EmployeeSalaries"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_BonusAwards_EmployeeId_Tenant' AND conrelid = 'payroll.""BonusAwards""'::regclass) THEN
  ALTER TABLE payroll.""BonusAwards"" ADD CONSTRAINT ""FK_BonusAwards_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_BonusAwards_PayComponentId_Tenant' AND conrelid = 'payroll.""BonusAwards""'::regclass) THEN
  ALTER TABLE payroll.""BonusAwards"" ADD CONSTRAINT ""FK_BonusAwards_PayComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""PayComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_BonusAwards_PayrollInputId_Tenant' AND conrelid = 'payroll.""BonusAwards""'::regclass) THEN
  ALTER TABLE payroll.""BonusAwards"" ADD CONSTRAINT ""FK_BonusAwards_PayrollInputId_Tenant"" FOREIGN KEY (""TenantId"", ""PayrollInputId"") REFERENCES payroll.""PayrollInputs"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
GRANT SELECT, INSERT, UPDATE, DELETE ON payroll.""BenefitPlans"", payroll.""BenefitEnrolments"", payroll.""SalaryRevisions"", payroll.""BonusAwards"" TO hr_payroll_app;
ALTER TABLE payroll.""BenefitPlans"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""BenefitPlans"";
CREATE POLICY tenant_isolation ON payroll.""BenefitPlans"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""BenefitEnrolments"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""BenefitEnrolments"";
CREATE POLICY tenant_isolation ON payroll.""BenefitEnrolments"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""SalaryRevisions"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""SalaryRevisions"";
CREATE POLICY tenant_isolation ON payroll.""SalaryRevisions"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""BonusAwards"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""BonusAwards"";
CREATE POLICY tenant_isolation ON payroll.""BonusAwards"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenefitEnrolments",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "BonusAwards",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "SalaryRevisions",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "BenefitPlans",
                schema: "payroll");
        }
    }
}
