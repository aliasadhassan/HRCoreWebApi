using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Payroll.API.Migrations
{
    /// <summary>
    /// Expenses &amp; travel page: ExpensePolicies (1/tenant), ExpenseCategories, TravelRequests (+ advance),
    /// ExpenseClaims + ExpenseClaimLines. Reimbursement payroll mein PayrollInput (Source = Expense) se jata hai.
    /// Live par SQL Editor se chalta hai (sql/expenses/11_expenses_travel.sql), phir history mein record.
    /// </summary>
    public partial class AddExpensesAndTravel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MaxPerClaim = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReceiptRequired = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                    table.CheckConstraint("CK_ExpenseCategories_Max", "\"MaxPerClaim\" IS NULL OR \"MaxPerClaim\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ExpensePolicies",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TravelEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReceiptRequiredAbove = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SubmitWithinDays = table.Column<short>(type: "smallint", nullable: false),
                    AdvancesEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxAdvancePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ReimbursementComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdvanceRecoveryComponentId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ExpensePolicies", x => x.Id);
                    table.CheckConstraint("CK_ExpensePolicies_Advance", "\"MaxAdvancePercent\" > 0 AND \"MaxAdvancePercent\" <= 100");
                    table.CheckConstraint("CK_ExpensePolicies_Receipt", "\"ReceiptRequiredAbove\" IS NULL OR \"ReceiptRequiredAbove\" >= 0");
                    table.CheckConstraint("CK_ExpensePolicies_Window", "\"SubmitWithinDays\" BETWEEN 1 AND 365");
                    table.ForeignKey(
                        name: "FK_ExpensePolicies_PayComponents_AdvanceRecoveryComponentId",
                        column: x => x.AdvanceRecoveryComponentId,
                        principalSchema: "payroll",
                        principalTable: "PayComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpensePolicies_PayComponents_ReimbursementComponentId",
                        column: x => x.ReimbursementComponentId,
                        principalSchema: "payroll",
                        principalTable: "PayComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TravelRequests",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DepartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TravelMode = table.Column<byte>(type: "smallint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceRequested = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    AdvanceApproved = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceStatus = table.Column<byte>(type: "smallint", nullable: false),
                    AdvancePayoutMethod = table.Column<byte>(type: "smallint", nullable: true),
                    AdvancePayrollInputId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdvancePaidAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TravelRequests", x => x.Id);
                    table.CheckConstraint("CK_TravelRequests_AdvancePaid", "\"AdvanceStatus\" < 2 OR \"AdvancePaidAt\" IS NOT NULL");
                    table.CheckConstraint("CK_TravelRequests_Amounts", "\"EstimatedCost\" > 0 AND \"AdvanceRequested\" BETWEEN 0 AND \"EstimatedCost\" AND \"AdvanceApproved\" BETWEEN 0 AND \"EstimatedCost\"");
                    table.CheckConstraint("CK_TravelRequests_Dates", "\"ReturnDate\" >= \"DepartDate\"");
                    table.ForeignKey(
                        name: "FK_TravelRequests_PayrollEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "payroll",
                        principalTable: "PayrollEmployees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TravelRequests_PayrollInputs_AdvancePayrollInputId",
                        column: x => x.AdvancePayrollInputId,
                        principalSchema: "payroll",
                        principalTable: "PayrollInputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseClaims",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TravelRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AdvanceAdjusted = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayoutMethod = table.Column<byte>(type: "smallint", nullable: true),
                    PayrollInputId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecoveryPayrollInputId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp(3) with time zone", precision: 3, nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ExpenseClaims", x => x.Id);
                    table.CheckConstraint("CK_ExpenseClaims_Amounts", "\"TotalAmount\" > 0 AND \"AdvanceAdjusted\" >= 0 AND (\"ApprovedAmount\" IS NULL OR \"ApprovedAmount\" BETWEEN 0 AND \"TotalAmount\")");
                    table.CheckConstraint("CK_ExpenseClaims_Approved", "\"Status\" NOT IN (2, 5) OR (\"ApprovedAmount\" > 0 AND \"PayoutMethod\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ExpenseClaims_PayrollEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "payroll",
                        principalTable: "PayrollEmployees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseClaims_PayrollInputs_PayrollInputId",
                        column: x => x.PayrollInputId,
                        principalSchema: "payroll",
                        principalTable: "PayrollInputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseClaims_PayrollInputs_RecoveryPayrollInputId",
                        column: x => x.RecoveryPayrollInputId,
                        principalSchema: "payroll",
                        principalTable: "PayrollInputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseClaims_TravelRequests_TravelRequestId",
                        column: x => x.TravelRequestId,
                        principalSchema: "payroll",
                        principalTable: "TravelRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseClaimLines",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Merchant = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HasReceipt = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseClaimLines", x => x.Id);
                    table.CheckConstraint("CK_ExpenseClaimLines_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_ExpenseClaimLines_ExpenseCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "payroll",
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseClaimLines_ExpenseClaims_ExpenseClaimId",
                        column: x => x.ExpenseClaimId,
                        principalSchema: "payroll",
                        principalTable: "ExpenseClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_TenantId_Code",
                schema: "payroll",
                table: "ExpenseCategories",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaimLines_CategoryId",
                schema: "payroll",
                table: "ExpenseClaimLines",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaimLines_ExpenseClaimId",
                schema: "payroll",
                table: "ExpenseClaimLines",
                column: "ExpenseClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaims_EmployeeId_CreatedAt",
                schema: "payroll",
                table: "ExpenseClaims",
                columns: new[] { "EmployeeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaims_PayrollInputId",
                schema: "payroll",
                table: "ExpenseClaims",
                column: "PayrollInputId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaims_RecoveryPayrollInputId",
                schema: "payroll",
                table: "ExpenseClaims",
                column: "RecoveryPayrollInputId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaims_TenantId_Status",
                schema: "payroll",
                table: "ExpenseClaims",
                columns: new[] { "TenantId", "Status" },
                filter: "\"Status\" IN (1, 2) AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseClaims_TravelRequestId",
                schema: "payroll",
                table: "ExpenseClaims",
                column: "TravelRequestId",
                unique: true,
                filter: "\"TravelRequestId\" IS NOT NULL AND \"Status\" IN (1, 2, 5) AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ExpensePolicies_AdvanceRecoveryComponentId",
                schema: "payroll",
                table: "ExpensePolicies",
                column: "AdvanceRecoveryComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpensePolicies_ReimbursementComponentId",
                schema: "payroll",
                table: "ExpensePolicies",
                column: "ReimbursementComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpensePolicies_TenantId",
                schema: "payroll",
                table: "ExpensePolicies",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_TravelRequests_AdvancePayrollInputId",
                schema: "payroll",
                table: "TravelRequests",
                column: "AdvancePayrollInputId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelRequests_EmployeeId_DepartDate",
                schema: "payroll",
                table: "TravelRequests",
                columns: new[] { "EmployeeId", "DepartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TravelRequests_TenantId_Status",
                schema: "payroll",
                table: "TravelRequests",
                columns: new[] { "TenantId", "Status" },
                filter: "\"Status\" = 1 AND \"IsDeleted\" = false");

            // Tenancy (step 03/04 jaisa): composite FKs (TenantId, XId), hr_payroll_app grants, RLS. Idempotent.
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayrollInputs_TenantId_Id"" ON payroll.""PayrollInputs"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_ExpenseCategories_TenantId_Id"" ON payroll.""ExpenseCategories"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_ExpenseClaims_TenantId_Id"" ON payroll.""ExpenseClaims"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_TravelRequests_TenantId_Id"" ON payroll.""TravelRequests"" (""TenantId"", ""Id"");
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ExpensePolicies_ReimbursementComponentId_Tenant' AND conrelid = 'payroll.""ExpensePolicies""'::regclass) THEN
  ALTER TABLE payroll.""ExpensePolicies"" ADD CONSTRAINT ""FK_ExpensePolicies_ReimbursementComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""ReimbursementComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ExpensePolicies_AdvanceRecoveryComponentId_Tenant' AND conrelid = 'payroll.""ExpensePolicies""'::regclass) THEN
  ALTER TABLE payroll.""ExpensePolicies"" ADD CONSTRAINT ""FK_ExpensePolicies_AdvanceRecoveryComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""AdvanceRecoveryComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TravelRequests_EmployeeId_Tenant' AND conrelid = 'payroll.""TravelRequests""'::regclass) THEN
  ALTER TABLE payroll.""TravelRequests"" ADD CONSTRAINT ""FK_TravelRequests_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TravelRequests_AdvancePayrollInputId_Tenant' AND conrelid = 'payroll.""TravelRequests""'::regclass) THEN
  ALTER TABLE payroll.""TravelRequests"" ADD CONSTRAINT ""FK_TravelRequests_AdvancePayrollInputId_Tenant"" FOREIGN KEY (""TenantId"", ""AdvancePayrollInputId"") REFERENCES payroll.""PayrollInputs"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ExpenseClaims_EmployeeId_Tenant' AND conrelid = 'payroll.""ExpenseClaims""'::regclass) THEN
  ALTER TABLE payroll.""ExpenseClaims"" ADD CONSTRAINT ""FK_ExpenseClaims_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ExpenseClaims_TravelRequestId_Tenant' AND conrelid = 'payroll.""ExpenseClaims""'::regclass) THEN
  ALTER TABLE payroll.""ExpenseClaims"" ADD CONSTRAINT ""FK_ExpenseClaims_TravelRequestId_Tenant"" FOREIGN KEY (""TenantId"", ""TravelRequestId"") REFERENCES payroll.""TravelRequests"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ExpenseClaims_PayrollInputId_Tenant' AND conrelid = 'payroll.""ExpenseClaims""'::regclass) THEN
  ALTER TABLE payroll.""ExpenseClaims"" ADD CONSTRAINT ""FK_ExpenseClaims_PayrollInputId_Tenant"" FOREIGN KEY (""TenantId"", ""PayrollInputId"") REFERENCES payroll.""PayrollInputs"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ExpenseClaims_RecoveryPayrollInputId_Tenant' AND conrelid = 'payroll.""ExpenseClaims""'::regclass) THEN
  ALTER TABLE payroll.""ExpenseClaims"" ADD CONSTRAINT ""FK_ExpenseClaims_RecoveryPayrollInputId_Tenant"" FOREIGN KEY (""TenantId"", ""RecoveryPayrollInputId"") REFERENCES payroll.""PayrollInputs"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ExpenseClaimLines_ExpenseClaimId_Tenant' AND conrelid = 'payroll.""ExpenseClaimLines""'::regclass) THEN
  ALTER TABLE payroll.""ExpenseClaimLines"" ADD CONSTRAINT ""FK_ExpenseClaimLines_ExpenseClaimId_Tenant"" FOREIGN KEY (""TenantId"", ""ExpenseClaimId"") REFERENCES payroll.""ExpenseClaims"" (""TenantId"", ""Id"") ON DELETE CASCADE;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ExpenseClaimLines_CategoryId_Tenant' AND conrelid = 'payroll.""ExpenseClaimLines""'::regclass) THEN
  ALTER TABLE payroll.""ExpenseClaimLines"" ADD CONSTRAINT ""FK_ExpenseClaimLines_CategoryId_Tenant"" FOREIGN KEY (""TenantId"", ""CategoryId"") REFERENCES payroll.""ExpenseCategories"" (""TenantId"", ""Id"") ON DELETE RESTRICT;
END IF; END $$;
GRANT SELECT, INSERT, UPDATE, DELETE ON payroll.""ExpensePolicies"", payroll.""ExpenseCategories"", payroll.""TravelRequests"", payroll.""ExpenseClaims"", payroll.""ExpenseClaimLines"" TO hr_payroll_app;
ALTER TABLE payroll.""ExpensePolicies"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""ExpensePolicies"";
CREATE POLICY tenant_isolation ON payroll.""ExpensePolicies"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""ExpenseCategories"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""ExpenseCategories"";
CREATE POLICY tenant_isolation ON payroll.""ExpenseCategories"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""TravelRequests"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""TravelRequests"";
CREATE POLICY tenant_isolation ON payroll.""TravelRequests"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""ExpenseClaims"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""ExpenseClaims"";
CREATE POLICY tenant_isolation ON payroll.""ExpenseClaims"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
ALTER TABLE payroll.""ExpenseClaimLines"" ENABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON payroll.""ExpenseClaimLines"";
CREATE POLICY tenant_isolation ON payroll.""ExpenseClaimLines"" TO hr_payroll_app USING (""TenantId"" = tenancy.current_tenant_id()) WITH CHECK (""TenantId"" = tenancy.current_tenant_id());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS payroll.""UX_PayrollInputs_TenantId_Id"";");

            migrationBuilder.DropTable(
                name: "ExpenseClaimLines",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "ExpensePolicies",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "ExpenseCategories",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "ExpenseClaims",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "TravelRequests",
                schema: "payroll");
        }
    }
}
