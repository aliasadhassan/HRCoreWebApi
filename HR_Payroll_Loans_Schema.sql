START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payroll."__EFMigrationsHistory" WHERE "MigrationId" = '20261005105859_AddLoanRequestsAndPolicy') THEN
    CREATE TABLE payroll."LoanPolicies" (
        "Id" uuid NOT NULL,
        "LoansEnabled" boolean NOT NULL,
        "MaxLoanAmount" numeric(18,2),
        "MaxLoanSalaryMultiple" smallint,
        "MaxLoanInstallments" smallint NOT NULL,
        "MinServiceMonths" smallint NOT NULL,
        "AdvancesEnabled" boolean NOT NULL,
        "MaxAdvancePercent" numeric(5,2) NOT NULL,
        "MaxAdvanceInstallments" smallint NOT NULL,
        "AllowMultipleActive" boolean NOT NULL,
        "LoanDeductionComponentId" uuid,
        "AdvanceDeductionComponentId" uuid,
        "CreatedAt" timestamp(3) with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "CreatedBy" uuid,
        "UpdatedAt" timestamp(3) with time zone,
        "UpdatedBy" uuid,
        "IsDeleted" boolean NOT NULL,
        "RowVersion" bytea NOT NULL DEFAULT gen_random_bytes(8),
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_LoanPolicies" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_LoanPolicies_Advance" CHECK ("MaxAdvancePercent" > 0 AND "MaxAdvancePercent" <= 100),
        CONSTRAINT "CK_LoanPolicies_Installments" CHECK ("MaxLoanInstallments" BETWEEN 1 AND 120 AND "MaxAdvanceInstallments" BETWEEN 1 AND 12),
        CONSTRAINT "FK_LoanPolicies_PayComponents_AdvanceDeductionComponentId" FOREIGN KEY ("AdvanceDeductionComponentId") REFERENCES payroll."PayComponents" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_LoanPolicies_PayComponents_LoanDeductionComponentId" FOREIGN KEY ("LoanDeductionComponentId") REFERENCES payroll."PayComponents" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payroll."__EFMigrationsHistory" WHERE "MigrationId" = '20261005105859_AddLoanRequestsAndPolicy') THEN
    CREATE TABLE payroll."LoanRequests" (
        "Id" uuid NOT NULL,
        "EmployeeId" uuid NOT NULL,
        "LoanType" smallint NOT NULL,
        "CurrencyCode" character(3) NOT NULL,
        "RequestedAmount" numeric(18,2) NOT NULL,
        "RequestedInstallments" smallint NOT NULL,
        "PreferredStartDate" date NOT NULL,
        "Reason" character varying(1000) NOT NULL,
        "Status" smallint NOT NULL,
        "ApprovedAmount" numeric(18,2),
        "ApprovedInstallmentAmount" numeric(18,2),
        "EmployeeLoanId" uuid,
        "DecidedByUserId" uuid,
        "DecidedAt" timestamp(3) with time zone,
        "DecisionComment" character varying(500),
        "CreatedAt" timestamp(3) with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "CreatedBy" uuid,
        "UpdatedAt" timestamp(3) with time zone,
        "UpdatedBy" uuid,
        "IsDeleted" boolean NOT NULL,
        "RowVersion" bytea NOT NULL DEFAULT gen_random_bytes(8),
        "TenantId" uuid NOT NULL,
        CONSTRAINT "PK_LoanRequests" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_LoanRequests_Amounts" CHECK ("RequestedAmount" > 0 AND "RequestedInstallments" BETWEEN 1 AND 120),
        CONSTRAINT "CK_LoanRequests_Approved" CHECK ("Status" <> 2 OR ("EmployeeLoanId" IS NOT NULL AND "ApprovedAmount" > 0 AND "ApprovedInstallmentAmount" > 0)),
        CONSTRAINT "FK_LoanRequests_EmployeeLoans_EmployeeLoanId" FOREIGN KEY ("EmployeeLoanId") REFERENCES payroll."EmployeeLoans" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_LoanRequests_PayrollEmployees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES payroll."PayrollEmployees" ("EmployeeId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payroll."__EFMigrationsHistory" WHERE "MigrationId" = '20261005105859_AddLoanRequestsAndPolicy') THEN
    CREATE INDEX "IX_LoanPolicies_AdvanceDeductionComponentId" ON payroll."LoanPolicies" ("AdvanceDeductionComponentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payroll."__EFMigrationsHistory" WHERE "MigrationId" = '20261005105859_AddLoanRequestsAndPolicy') THEN
    CREATE INDEX "IX_LoanPolicies_LoanDeductionComponentId" ON payroll."LoanPolicies" ("LoanDeductionComponentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payroll."__EFMigrationsHistory" WHERE "MigrationId" = '20261005105859_AddLoanRequestsAndPolicy') THEN
    CREATE UNIQUE INDEX "IX_LoanPolicies_TenantId" ON payroll."LoanPolicies" ("TenantId") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payroll."__EFMigrationsHistory" WHERE "MigrationId" = '20261005105859_AddLoanRequestsAndPolicy') THEN
    CREATE INDEX "IX_LoanRequests_EmployeeId_CreatedAt" ON payroll."LoanRequests" ("EmployeeId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payroll."__EFMigrationsHistory" WHERE "MigrationId" = '20261005105859_AddLoanRequestsAndPolicy') THEN
    CREATE UNIQUE INDEX "IX_LoanRequests_EmployeeLoanId" ON payroll."LoanRequests" ("EmployeeLoanId") WHERE "EmployeeLoanId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payroll."__EFMigrationsHistory" WHERE "MigrationId" = '20261005105859_AddLoanRequestsAndPolicy') THEN
    CREATE INDEX "IX_LoanRequests_TenantId_Status" ON payroll."LoanRequests" ("TenantId", "Status") WHERE "Status" = 1 AND "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payroll."__EFMigrationsHistory" WHERE "MigrationId" = '20261005105859_AddLoanRequestsAndPolicy') THEN
    INSERT INTO payroll."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261005105859_AddLoanRequestsAndPolicy', '8.0.26');
    END IF;
END $EF$;
COMMIT;

