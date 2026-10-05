-- Payroll: bytea "RowVersion" hata kar Postgres ka xmin system column (Employee API jaisa).
-- Koi trigger, function ya gen_random_bytes nahi. xmin har table mein pehle se hota hai.
-- Supabase SQL Editor mein ek dafa chalayen.
BEGIN;

-- 20261005105858_UseXminRowVersion (18 purane payroll tables)
ALTER TABLE payroll."ContributionRules"          DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."EmployeeLoans"              DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."EmployeeSalaries"           DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."EmployeeTaxOpeningBalances" DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."LoanRepayments"             DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."PayComponents"              DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."PayGroups"                  DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."PayPeriods"                 DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."PaymentBatches"             DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."Payments"                   DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."PayrollEmployees"           DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."PayrollInputs"              DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."PayrollRuns"                DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."PayrollSettings"            DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."Payslips"                   DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."SalaryGrades"               DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."SalaryTemplates"            DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."TaxRegimes"                 DROP COLUMN IF EXISTS "RowVersion";

-- 20261005105859_AddLoanRequestsAndPolicy pehle bytea + gen_random_bytes(8) ke saath chal chuka hai;
-- corrected migration mein RowVersion column hai hi nahi, isliye yahan bhi drop.
ALTER TABLE payroll."LoanPolicies" DROP COLUMN IF EXISTS "RowVersion";
ALTER TABLE payroll."LoanRequests" DROP COLUMN IF EXISTS "RowVersion";

INSERT INTO payroll."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261005105858_UseXminRowVersion', '8.0.26')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

-- InboxState/OutboxState (MassTransit) ko nahi chheda, Employee API mein bhi nahi chheda gaya.
