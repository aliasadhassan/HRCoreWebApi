using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR.Payroll.API.Migrations
{
    /// <summary>
    /// Tenant-aware relationships (tenancy hardening step 03, live par 2026-10-06 ko SQL Editor se chal chuka hai).
    /// 1) UNIQUE ("TenantId", "Id") parents par + composite FKs (TenantId, XId) -> Parent(TenantId, Id).
    ///    Employee references PayrollEmployees ("TenantId", "EmployeeId") par jaate hain.
    /// 2) Tenant-aware business uniques: PayComponents/PayGroups code, ek PayrollSettings per tenant, PayPeriods start.
    /// 3) Payslip number index non-unique: cancelled run apni payslips rakhta hai aur redo wahi number banata hai.
    /// FK/unique statements idempotent hain (IF NOT EXISTS). EF model composite FKs track nahi karta, isliye raw SQL.
    /// </summary>
    public partial class TenantCompositeKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payslips_TenantId_PayslipNumber",
                schema: "payroll",
                table: "Payslips");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_TenantId_PayslipNumber",
                schema: "payroll",
                table: "Payslips",
                columns: new[] { "TenantId", "PayslipNumber" });

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_EmployeeLoans_TenantId_Id"" ON payroll.""EmployeeLoans"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayComponents_TenantId_Id"" ON payroll.""PayComponents"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayGroups_TenantId_Id"" ON payroll.""PayGroups"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayPeriods_TenantId_Id"" ON payroll.""PayPeriods"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PaymentBatches_TenantId_Id"" ON payroll.""PaymentBatches"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayrollEmployees_TenantId_EmployeeId"" ON payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayrollRuns_TenantId_Id"" ON payroll.""PayrollRuns"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Payslips_TenantId_Id"" ON payroll.""Payslips"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_SalaryGrades_TenantId_Id"" ON payroll.""SalaryGrades"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_SalaryTemplates_TenantId_Id"" ON payroll.""SalaryTemplates"" (""TenantId"", ""Id"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayComponents_TenantId_Code"" ON payroll.""PayComponents"" (""TenantId"", ""Code"") WHERE ""IsDeleted"" = false;
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayComponents_TenantId_SystemCode"" ON payroll.""PayComponents"" (""TenantId"", ""SystemCode"") WHERE ""SystemCode"" IS NOT NULL AND ""IsDeleted"" = false;
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayGroups_TenantId_Code"" ON payroll.""PayGroups"" (""TenantId"", ""Code"") WHERE ""IsDeleted"" = false;
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayrollSettings_TenantId"" ON payroll.""PayrollSettings"" (""TenantId"") WHERE ""IsDeleted"" = false;
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_PayPeriods_PayGroupId_PeriodStart"" ON payroll.""PayPeriods"" (""PayGroupId"", ""PeriodStart"") WHERE ""IsDeleted"" = false;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_ContributionRules_GlobalNoComponents') THEN
  ALTER TABLE payroll.""ContributionRules"" ADD CONSTRAINT ""CK_ContributionRules_GlobalNoComponents""
    CHECK (""TenantId"" IS NOT NULL OR (""EmployeeComponentId"" IS NULL AND ""EmployerComponentId"" IS NULL)) NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ContributionRules_EmployeeComponentId_Tenant' AND conrelid = 'payroll.""ContributionRules""'::regclass) THEN
  ALTER TABLE payroll.""ContributionRules"" ADD CONSTRAINT ""FK_ContributionRules_EmployeeComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ContributionRules_EmployerComponentId_Tenant' AND conrelid = 'payroll.""ContributionRules""'::regclass) THEN
  ALTER TABLE payroll.""ContributionRules"" ADD CONSTRAINT ""FK_ContributionRules_EmployerComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployerComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeLoans_DeductionComponentId_Tenant' AND conrelid = 'payroll.""EmployeeLoans""'::regclass) THEN
  ALTER TABLE payroll.""EmployeeLoans"" ADD CONSTRAINT ""FK_EmployeeLoans_DeductionComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""DeductionComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeLoans_EmployeeId_Tenant' AND conrelid = 'payroll.""EmployeeLoans""'::regclass) THEN
  ALTER TABLE payroll.""EmployeeLoans"" ADD CONSTRAINT ""FK_EmployeeLoans_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeSalaries_EmployeeId_Tenant' AND conrelid = 'payroll.""EmployeeSalaries""'::regclass) THEN
  ALTER TABLE payroll.""EmployeeSalaries"" ADD CONSTRAINT ""FK_EmployeeSalaries_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeSalaries_SalaryGradeId_Tenant' AND conrelid = 'payroll.""EmployeeSalaries""'::regclass) THEN
  ALTER TABLE payroll.""EmployeeSalaries"" ADD CONSTRAINT ""FK_EmployeeSalaries_SalaryGradeId_Tenant"" FOREIGN KEY (""TenantId"", ""SalaryGradeId"") REFERENCES payroll.""SalaryGrades"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeSalaries_SalaryTemplateId_Tenant' AND conrelid = 'payroll.""EmployeeSalaries""'::regclass) THEN
  ALTER TABLE payroll.""EmployeeSalaries"" ADD CONSTRAINT ""FK_EmployeeSalaries_SalaryTemplateId_Tenant"" FOREIGN KEY (""TenantId"", ""SalaryTemplateId"") REFERENCES payroll.""SalaryTemplates"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeTaxOpeningBalances_EmployeeId_Tenant' AND conrelid = 'payroll.""EmployeeTaxOpeningBalances""'::regclass) THEN
  ALTER TABLE payroll.""EmployeeTaxOpeningBalances"" ADD CONSTRAINT ""FK_EmployeeTaxOpeningBalances_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeUnpaidLeaveDays_EmployeeId_Tenant' AND conrelid = 'payroll.""EmployeeUnpaidLeaveDays""'::regclass) THEN
  ALTER TABLE payroll.""EmployeeUnpaidLeaveDays"" ADD CONSTRAINT ""FK_EmployeeUnpaidLeaveDays_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LoanPolicies_AdvanceDeductionComponentId_Tenant' AND conrelid = 'payroll.""LoanPolicies""'::regclass) THEN
  ALTER TABLE payroll.""LoanPolicies"" ADD CONSTRAINT ""FK_LoanPolicies_AdvanceDeductionComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""AdvanceDeductionComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LoanPolicies_LoanDeductionComponentId_Tenant' AND conrelid = 'payroll.""LoanPolicies""'::regclass) THEN
  ALTER TABLE payroll.""LoanPolicies"" ADD CONSTRAINT ""FK_LoanPolicies_LoanDeductionComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""LoanDeductionComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LoanRepayments_EmployeeLoanId_Tenant' AND conrelid = 'payroll.""LoanRepayments""'::regclass) THEN
  ALTER TABLE payroll.""LoanRepayments"" ADD CONSTRAINT ""FK_LoanRepayments_EmployeeLoanId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeLoanId"") REFERENCES payroll.""EmployeeLoans"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LoanRepayments_PayslipId_Tenant' AND conrelid = 'payroll.""LoanRepayments""'::regclass) THEN
  ALTER TABLE payroll.""LoanRepayments"" ADD CONSTRAINT ""FK_LoanRepayments_PayslipId_Tenant"" FOREIGN KEY (""TenantId"", ""PayslipId"") REFERENCES payroll.""Payslips"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LoanRequests_EmployeeId_Tenant' AND conrelid = 'payroll.""LoanRequests""'::regclass) THEN
  ALTER TABLE payroll.""LoanRequests"" ADD CONSTRAINT ""FK_LoanRequests_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LoanRequests_EmployeeLoanId_Tenant' AND conrelid = 'payroll.""LoanRequests""'::regclass) THEN
  ALTER TABLE payroll.""LoanRequests"" ADD CONSTRAINT ""FK_LoanRequests_EmployeeLoanId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeLoanId"") REFERENCES payroll.""EmployeeLoans"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PaymentBatches_PayrollRunId_Tenant' AND conrelid = 'payroll.""PaymentBatches""'::regclass) THEN
  ALTER TABLE payroll.""PaymentBatches"" ADD CONSTRAINT ""FK_PaymentBatches_PayrollRunId_Tenant"" FOREIGN KEY (""TenantId"", ""PayrollRunId"") REFERENCES payroll.""PayrollRuns"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Payments_PaymentBatchId_Tenant' AND conrelid = 'payroll.""Payments""'::regclass) THEN
  ALTER TABLE payroll.""Payments"" ADD CONSTRAINT ""FK_Payments_PaymentBatchId_Tenant"" FOREIGN KEY (""TenantId"", ""PaymentBatchId"") REFERENCES payroll.""PaymentBatches"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Payments_PayslipId_Tenant' AND conrelid = 'payroll.""Payments""'::regclass) THEN
  ALTER TABLE payroll.""Payments"" ADD CONSTRAINT ""FK_Payments_PayslipId_Tenant"" FOREIGN KEY (""TenantId"", ""PayslipId"") REFERENCES payroll.""Payslips"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PayrollEmployees_PayGroupId_Tenant' AND conrelid = 'payroll.""PayrollEmployees""'::regclass) THEN
  ALTER TABLE payroll.""PayrollEmployees"" ADD CONSTRAINT ""FK_PayrollEmployees_PayGroupId_Tenant"" FOREIGN KEY (""TenantId"", ""PayGroupId"") REFERENCES payroll.""PayGroups"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PayrollInputs_EmployeeId_Tenant' AND conrelid = 'payroll.""PayrollInputs""'::regclass) THEN
  ALTER TABLE payroll.""PayrollInputs"" ADD CONSTRAINT ""FK_PayrollInputs_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PayrollInputs_PayComponentId_Tenant' AND conrelid = 'payroll.""PayrollInputs""'::regclass) THEN
  ALTER TABLE payroll.""PayrollInputs"" ADD CONSTRAINT ""FK_PayrollInputs_PayComponentId_Tenant"" FOREIGN KEY (""TenantId"", ""PayComponentId"") REFERENCES payroll.""PayComponents"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PayrollInputs_PayPeriodId_Tenant' AND conrelid = 'payroll.""PayrollInputs""'::regclass) THEN
  ALTER TABLE payroll.""PayrollInputs"" ADD CONSTRAINT ""FK_PayrollInputs_PayPeriodId_Tenant"" FOREIGN KEY (""TenantId"", ""PayPeriodId"") REFERENCES payroll.""PayPeriods"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PayrollRuns_PayGroupId_Tenant' AND conrelid = 'payroll.""PayrollRuns""'::regclass) THEN
  ALTER TABLE payroll.""PayrollRuns"" ADD CONSTRAINT ""FK_PayrollRuns_PayGroupId_Tenant"" FOREIGN KEY (""TenantId"", ""PayGroupId"") REFERENCES payroll.""PayGroups"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PayrollRuns_PayPeriodId_Tenant' AND conrelid = 'payroll.""PayrollRuns""'::regclass) THEN
  ALTER TABLE payroll.""PayrollRuns"" ADD CONSTRAINT ""FK_PayrollRuns_PayPeriodId_Tenant"" FOREIGN KEY (""TenantId"", ""PayPeriodId"") REFERENCES payroll.""PayPeriods"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Payslips_EmployeeId_Tenant' AND conrelid = 'payroll.""Payslips""'::regclass) THEN
  ALTER TABLE payroll.""Payslips"" ADD CONSTRAINT ""FK_Payslips_EmployeeId_Tenant"" FOREIGN KEY (""TenantId"", ""EmployeeId"") REFERENCES payroll.""PayrollEmployees"" (""TenantId"", ""EmployeeId"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Payslips_PayrollRunId_Tenant' AND conrelid = 'payroll.""Payslips""'::regclass) THEN
  ALTER TABLE payroll.""Payslips"" ADD CONSTRAINT ""FK_Payslips_PayrollRunId_Tenant"" FOREIGN KEY (""TenantId"", ""PayrollRunId"") REFERENCES payroll.""PayrollRuns"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryTemplates_SalaryGradeId_Tenant' AND conrelid = 'payroll.""SalaryTemplates""'::regclass) THEN
  ALTER TABLE payroll.""SalaryTemplates"" ADD CONSTRAINT ""FK_SalaryTemplates_SalaryGradeId_Tenant"" FOREIGN KEY (""TenantId"", ""SalaryGradeId"") REFERENCES payroll.""SalaryGrades"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PayPeriods_PayGroupId_Tenant' AND conrelid = 'payroll.""PayPeriods""'::regclass) THEN
  ALTER TABLE payroll.""PayPeriods"" ADD CONSTRAINT ""FK_PayPeriods_PayGroupId_Tenant"" FOREIGN KEY (""TenantId"", ""PayGroupId"") REFERENCES payroll.""PayGroups"" (""TenantId"", ""Id"") ON DELETE RESTRICT NOT VALID;
END IF; END $$;
ALTER TABLE payroll.""ContributionRules"" VALIDATE CONSTRAINT ""FK_ContributionRules_EmployeeComponentId_Tenant"";
ALTER TABLE payroll.""ContributionRules"" VALIDATE CONSTRAINT ""FK_ContributionRules_EmployerComponentId_Tenant"";
ALTER TABLE payroll.""EmployeeLoans"" VALIDATE CONSTRAINT ""FK_EmployeeLoans_DeductionComponentId_Tenant"";
ALTER TABLE payroll.""EmployeeLoans"" VALIDATE CONSTRAINT ""FK_EmployeeLoans_EmployeeId_Tenant"";
ALTER TABLE payroll.""EmployeeSalaries"" VALIDATE CONSTRAINT ""FK_EmployeeSalaries_EmployeeId_Tenant"";
ALTER TABLE payroll.""EmployeeSalaries"" VALIDATE CONSTRAINT ""FK_EmployeeSalaries_SalaryGradeId_Tenant"";
ALTER TABLE payroll.""EmployeeSalaries"" VALIDATE CONSTRAINT ""FK_EmployeeSalaries_SalaryTemplateId_Tenant"";
ALTER TABLE payroll.""EmployeeTaxOpeningBalances"" VALIDATE CONSTRAINT ""FK_EmployeeTaxOpeningBalances_EmployeeId_Tenant"";
ALTER TABLE payroll.""EmployeeUnpaidLeaveDays"" VALIDATE CONSTRAINT ""FK_EmployeeUnpaidLeaveDays_EmployeeId_Tenant"";
ALTER TABLE payroll.""LoanPolicies"" VALIDATE CONSTRAINT ""FK_LoanPolicies_AdvanceDeductionComponentId_Tenant"";
ALTER TABLE payroll.""LoanPolicies"" VALIDATE CONSTRAINT ""FK_LoanPolicies_LoanDeductionComponentId_Tenant"";
ALTER TABLE payroll.""LoanRepayments"" VALIDATE CONSTRAINT ""FK_LoanRepayments_EmployeeLoanId_Tenant"";
ALTER TABLE payroll.""LoanRepayments"" VALIDATE CONSTRAINT ""FK_LoanRepayments_PayslipId_Tenant"";
ALTER TABLE payroll.""LoanRequests"" VALIDATE CONSTRAINT ""FK_LoanRequests_EmployeeId_Tenant"";
ALTER TABLE payroll.""LoanRequests"" VALIDATE CONSTRAINT ""FK_LoanRequests_EmployeeLoanId_Tenant"";
ALTER TABLE payroll.""PaymentBatches"" VALIDATE CONSTRAINT ""FK_PaymentBatches_PayrollRunId_Tenant"";
ALTER TABLE payroll.""Payments"" VALIDATE CONSTRAINT ""FK_Payments_PaymentBatchId_Tenant"";
ALTER TABLE payroll.""Payments"" VALIDATE CONSTRAINT ""FK_Payments_PayslipId_Tenant"";
ALTER TABLE payroll.""PayrollEmployees"" VALIDATE CONSTRAINT ""FK_PayrollEmployees_PayGroupId_Tenant"";
ALTER TABLE payroll.""PayrollInputs"" VALIDATE CONSTRAINT ""FK_PayrollInputs_EmployeeId_Tenant"";
ALTER TABLE payroll.""PayrollInputs"" VALIDATE CONSTRAINT ""FK_PayrollInputs_PayComponentId_Tenant"";
ALTER TABLE payroll.""PayrollInputs"" VALIDATE CONSTRAINT ""FK_PayrollInputs_PayPeriodId_Tenant"";
ALTER TABLE payroll.""PayrollRuns"" VALIDATE CONSTRAINT ""FK_PayrollRuns_PayGroupId_Tenant"";
ALTER TABLE payroll.""PayrollRuns"" VALIDATE CONSTRAINT ""FK_PayrollRuns_PayPeriodId_Tenant"";
ALTER TABLE payroll.""Payslips"" VALIDATE CONSTRAINT ""FK_Payslips_EmployeeId_Tenant"";
ALTER TABLE payroll.""Payslips"" VALIDATE CONSTRAINT ""FK_Payslips_PayrollRunId_Tenant"";
ALTER TABLE payroll.""SalaryTemplates"" VALIDATE CONSTRAINT ""FK_SalaryTemplates_SalaryGradeId_Tenant"";
ALTER TABLE payroll.""PayPeriods"" VALIDATE CONSTRAINT ""FK_PayPeriods_PayGroupId_Tenant"";
ALTER TABLE payroll.""ContributionRules"" VALIDATE CONSTRAINT ""CK_ContributionRules_GlobalNoComponents"";
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE payroll.""ContributionRules"" DROP CONSTRAINT IF EXISTS ""CK_ContributionRules_GlobalNoComponents"";
ALTER TABLE payroll.""ContributionRules"" DROP CONSTRAINT IF EXISTS ""FK_ContributionRules_EmployeeComponentId_Tenant"";
ALTER TABLE payroll.""ContributionRules"" DROP CONSTRAINT IF EXISTS ""FK_ContributionRules_EmployerComponentId_Tenant"";
ALTER TABLE payroll.""EmployeeLoans"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeLoans_DeductionComponentId_Tenant"";
ALTER TABLE payroll.""EmployeeLoans"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeLoans_EmployeeId_Tenant"";
ALTER TABLE payroll.""EmployeeSalaries"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeSalaries_EmployeeId_Tenant"";
ALTER TABLE payroll.""EmployeeSalaries"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeSalaries_SalaryGradeId_Tenant"";
ALTER TABLE payroll.""EmployeeSalaries"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeSalaries_SalaryTemplateId_Tenant"";
ALTER TABLE payroll.""EmployeeTaxOpeningBalances"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeTaxOpeningBalances_EmployeeId_Tenant"";
ALTER TABLE payroll.""EmployeeUnpaidLeaveDays"" DROP CONSTRAINT IF EXISTS ""FK_EmployeeUnpaidLeaveDays_EmployeeId_Tenant"";
ALTER TABLE payroll.""LoanPolicies"" DROP CONSTRAINT IF EXISTS ""FK_LoanPolicies_AdvanceDeductionComponentId_Tenant"";
ALTER TABLE payroll.""LoanPolicies"" DROP CONSTRAINT IF EXISTS ""FK_LoanPolicies_LoanDeductionComponentId_Tenant"";
ALTER TABLE payroll.""LoanRepayments"" DROP CONSTRAINT IF EXISTS ""FK_LoanRepayments_EmployeeLoanId_Tenant"";
ALTER TABLE payroll.""LoanRepayments"" DROP CONSTRAINT IF EXISTS ""FK_LoanRepayments_PayslipId_Tenant"";
ALTER TABLE payroll.""LoanRequests"" DROP CONSTRAINT IF EXISTS ""FK_LoanRequests_EmployeeId_Tenant"";
ALTER TABLE payroll.""LoanRequests"" DROP CONSTRAINT IF EXISTS ""FK_LoanRequests_EmployeeLoanId_Tenant"";
ALTER TABLE payroll.""PaymentBatches"" DROP CONSTRAINT IF EXISTS ""FK_PaymentBatches_PayrollRunId_Tenant"";
ALTER TABLE payroll.""Payments"" DROP CONSTRAINT IF EXISTS ""FK_Payments_PaymentBatchId_Tenant"";
ALTER TABLE payroll.""Payments"" DROP CONSTRAINT IF EXISTS ""FK_Payments_PayslipId_Tenant"";
ALTER TABLE payroll.""PayrollEmployees"" DROP CONSTRAINT IF EXISTS ""FK_PayrollEmployees_PayGroupId_Tenant"";
ALTER TABLE payroll.""PayrollInputs"" DROP CONSTRAINT IF EXISTS ""FK_PayrollInputs_EmployeeId_Tenant"";
ALTER TABLE payroll.""PayrollInputs"" DROP CONSTRAINT IF EXISTS ""FK_PayrollInputs_PayComponentId_Tenant"";
ALTER TABLE payroll.""PayrollInputs"" DROP CONSTRAINT IF EXISTS ""FK_PayrollInputs_PayPeriodId_Tenant"";
ALTER TABLE payroll.""PayrollRuns"" DROP CONSTRAINT IF EXISTS ""FK_PayrollRuns_PayGroupId_Tenant"";
ALTER TABLE payroll.""PayrollRuns"" DROP CONSTRAINT IF EXISTS ""FK_PayrollRuns_PayPeriodId_Tenant"";
ALTER TABLE payroll.""Payslips"" DROP CONSTRAINT IF EXISTS ""FK_Payslips_EmployeeId_Tenant"";
ALTER TABLE payroll.""Payslips"" DROP CONSTRAINT IF EXISTS ""FK_Payslips_PayrollRunId_Tenant"";
ALTER TABLE payroll.""SalaryTemplates"" DROP CONSTRAINT IF EXISTS ""FK_SalaryTemplates_SalaryGradeId_Tenant"";
ALTER TABLE payroll.""PayPeriods"" DROP CONSTRAINT IF EXISTS ""FK_PayPeriods_PayGroupId_Tenant"";
DROP INDEX IF EXISTS payroll.""UX_EmployeeLoans_TenantId_Id"";
DROP INDEX IF EXISTS payroll.""UX_PayComponents_TenantId_Id"";
DROP INDEX IF EXISTS payroll.""UX_PayGroups_TenantId_Id"";
DROP INDEX IF EXISTS payroll.""UX_PayPeriods_TenantId_Id"";
DROP INDEX IF EXISTS payroll.""UX_PaymentBatches_TenantId_Id"";
DROP INDEX IF EXISTS payroll.""UX_PayrollEmployees_TenantId_EmployeeId"";
DROP INDEX IF EXISTS payroll.""UX_PayrollRuns_TenantId_Id"";
DROP INDEX IF EXISTS payroll.""UX_Payslips_TenantId_Id"";
DROP INDEX IF EXISTS payroll.""UX_SalaryGrades_TenantId_Id"";
DROP INDEX IF EXISTS payroll.""UX_SalaryTemplates_TenantId_Id"";
DROP INDEX IF EXISTS payroll.""UX_PayComponents_TenantId_Code"";
DROP INDEX IF EXISTS payroll.""UX_PayComponents_TenantId_SystemCode"";
DROP INDEX IF EXISTS payroll.""UX_PayGroups_TenantId_Code"";
DROP INDEX IF EXISTS payroll.""UX_PayrollSettings_TenantId"";
DROP INDEX IF EXISTS payroll.""UX_PayPeriods_PayGroupId_PeriodStart"";
");

            migrationBuilder.DropIndex(
                name: "IX_Payslips_TenantId_PayslipNumber",
                schema: "payroll",
                table: "Payslips");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_TenantId_PayslipNumber",
                schema: "payroll",
                table: "Payslips",
                columns: new[] { "TenantId", "PayslipNumber" },
                unique: true);
        }
    }
}
