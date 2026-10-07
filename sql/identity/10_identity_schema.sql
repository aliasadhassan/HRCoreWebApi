-- HR Cloud H6: move Identity from public to its own schema, xmin, timestamptz, own DB role.
-- Nothing is deleted: the stale identity copy and the old employee tables in public are renamed/moved to archive schemas.
-- Run with Identity API STOPPED, before starting the code from the H6 PR. One transaction: any error = nothing changes.
-- Rollback: R10_rollback_identity_schema.sql
BEGIN;

-- 0) Prechecks: stop if the state is not what this script expects.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='public' AND table_name='Users') THEN
    RAISE EXCEPTION 'public."Users" not found: already moved?';
  END IF;
  IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname IN ('archive_identity_20261001','archive_public_employee')) THEN
    RAISE EXCEPTION 'archive schema already exists';
  END IF;
  IF (SELECT max("CreatedAt") FROM identity."RefreshTokens") >= (SELECT max("CreatedAt") FROM public."RefreshTokens") THEN
    RAISE EXCEPTION 'identity.* is not older than public.*: stop and check which copy is live';
  END IF;
END $$;

-- 1) Archive the stale identity copy (last write 2026-10-01).
ALTER SCHEMA identity RENAME TO archive_identity_20261001;
REVOKE ALL ON SCHEMA archive_identity_20261001 FROM PUBLIC;

-- 2) Archive the old employee-service tables left in public (live ones are in employee.*).
CREATE SCHEMA archive_public_employee;
REVOKE ALL ON SCHEMA archive_public_employee FROM PUBLIC;
ALTER TABLE public."Departments"               SET SCHEMA archive_public_employee;
ALTER TABLE public."Designations"              SET SCHEMA archive_public_employee;
ALTER TABLE public."EmployeeDocuments"         SET SCHEMA archive_public_employee;
ALTER TABLE public."EmployeeEmergencyContacts" SET SCHEMA archive_public_employee;
ALTER TABLE public."EmployeeJobHistory"        SET SCHEMA archive_public_employee;
ALTER TABLE public."Employees"                 SET SCHEMA archive_public_employee;
ALTER TABLE public."Holidays"                  SET SCHEMA archive_public_employee;
ALTER TABLE public."InboxState"                SET SCHEMA archive_public_employee;
ALTER TABLE public."LeaveApprovalSettings"     SET SCHEMA archive_public_employee;
ALTER TABLE public."LeaveBalances"             SET SCHEMA archive_public_employee;
ALTER TABLE public."LeavePolicies"             SET SCHEMA archive_public_employee;
ALTER TABLE public."LeavePolicyRules"          SET SCHEMA archive_public_employee;
ALTER TABLE public."LeaveRequestApprovals"     SET SCHEMA archive_public_employee;
ALTER TABLE public."LeaveRequests"             SET SCHEMA archive_public_employee;
ALTER TABLE public."LeaveTypes"                SET SCHEMA archive_public_employee;
ALTER TABLE public."Locations"                 SET SCHEMA archive_public_employee;
ALTER TABLE public."OutboxMessage"             SET SCHEMA archive_public_employee;
ALTER TABLE public."OutboxState"               SET SCHEMA archive_public_employee;
ALTER TABLE public."__EFMigrationsHistory"     SET SCHEMA archive_public_employee;

-- 3) Move the live Identity tables (metadata only: data, indexes, FKs, identity sequences move with them).
CREATE SCHEMA identity;
REVOKE ALL ON SCHEMA identity FROM PUBLIC;
ALTER TABLE public."Tenants"             SET SCHEMA identity;
ALTER TABLE public."TenantSettings"      SET SCHEMA identity;
ALTER TABLE public."TenantSubscriptions" SET SCHEMA identity;
ALTER TABLE public."Users"               SET SCHEMA identity;
ALTER TABLE public."UserExternalLogins"  SET SCHEMA identity;
ALTER TABLE public."Roles"               SET SCHEMA identity;
ALTER TABLE public."Permissions"         SET SCHEMA identity;
ALTER TABLE public."RolePermissions"     SET SCHEMA identity;
ALTER TABLE public."UserRoles"           SET SCHEMA identity;
ALTER TABLE public."RefreshTokens"       SET SCHEMA identity;
ALTER TABLE public."UserTokens"          SET SCHEMA identity;
ALTER TABLE public."LoginAudit"          SET SCHEMA identity;

-- 4) Concurrency = xmin. The bytea RowVersion columns hold random bytes only (no business data).
ALTER TABLE identity."Tenants"        DROP COLUMN "RowVersion";
ALTER TABLE identity."TenantSettings" DROP COLUMN "RowVersion";
ALTER TABLE identity."Users"          DROP COLUMN "RowVersion";
ALTER TABLE identity."Roles"          DROP COLUMN "RowVersion";

-- 5) timestamp -> timestamptz(3). Values are UTC today, so every instant stays identical (same as script 05).
ALTER TABLE identity."Tenants" ALTER COLUMN "CreatedAt" DROP DEFAULT;
ALTER TABLE identity."Users"   ALTER COLUMN "CreatedAt" DROP DEFAULT;
ALTER TABLE identity."Roles"   ALTER COLUMN "CreatedAt" DROP DEFAULT;
ALTER TABLE identity."LoginAudit"         ALTER COLUMN "OccurredAt"  TYPE timestamptz(3) USING "OccurredAt"  AT TIME ZONE 'UTC';
ALTER TABLE identity."RefreshTokens"      ALTER COLUMN "CreatedAt"   TYPE timestamptz(3) USING "CreatedAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "ExpiresAt"   TYPE timestamptz(3) USING "ExpiresAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "RevokedAt"   TYPE timestamptz(3) USING "RevokedAt"   AT TIME ZONE 'UTC';
ALTER TABLE identity."Roles"              ALTER COLUMN "CreatedAt"   TYPE timestamptz(3) USING "CreatedAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "UpdatedAt"   TYPE timestamptz(3) USING "UpdatedAt"   AT TIME ZONE 'UTC';
ALTER TABLE identity."TenantSettings"     ALTER COLUMN "UpdatedAt"   TYPE timestamptz(3) USING "UpdatedAt"   AT TIME ZONE 'UTC';
ALTER TABLE identity."Tenants"            ALTER COLUMN "CreatedAt"   TYPE timestamptz(3) USING "CreatedAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "UpdatedAt"   TYPE timestamptz(3) USING "UpdatedAt"   AT TIME ZONE 'UTC';
ALTER TABLE identity."UserExternalLogins" ALTER COLUMN "LastUsedAt"  TYPE timestamptz(3) USING "LastUsedAt"  AT TIME ZONE 'UTC',
                                          ALTER COLUMN "LinkedAt"    TYPE timestamptz(3) USING "LinkedAt"    AT TIME ZONE 'UTC';
ALTER TABLE identity."UserRoles"          ALTER COLUMN "AssignedAt"  TYPE timestamptz(3) USING "AssignedAt"  AT TIME ZONE 'UTC';
ALTER TABLE identity."UserTokens"         ALTER COLUMN "CreatedAt"   TYPE timestamptz(3) USING "CreatedAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "ExpiresAt"   TYPE timestamptz(3) USING "ExpiresAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "UsedAt"      TYPE timestamptz(3) USING "UsedAt"      AT TIME ZONE 'UTC';
ALTER TABLE identity."Users"              ALTER COLUMN "CreatedAt"   TYPE timestamptz(3) USING "CreatedAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "LastLoginAt" TYPE timestamptz(3) USING "LastLoginAt" AT TIME ZONE 'UTC',
                                          ALTER COLUMN "LockoutEnd"  TYPE timestamptz(3) USING "LockoutEnd"  AT TIME ZONE 'UTC',
                                          ALTER COLUMN "UpdatedAt"   TYPE timestamptz(3) USING "UpdatedAt"   AT TIME ZONE 'UTC';
ALTER TABLE identity."Tenants" ALTER COLUMN "CreatedAt" SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE identity."Users"   ALTER COLUMN "CreatedAt" SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE identity."Roles"   ALTER COLUMN "CreatedAt" SET DEFAULT CURRENT_TIMESTAMP;

-- 6) EF history: the baseline migration describes exactly this structure, so record it as applied.
CREATE TABLE identity."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" character varying(32) NOT NULL);
INSERT INTO identity."__EFMigrationsHistory" VALUES ('20261007133835_IdentityBaseline', '8.0.26');

-- 7) Own DB role: identity schema only, no employee/payroll access. Password is set separately by ali.
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'hr_identity_app') THEN
    CREATE ROLE hr_identity_app LOGIN NOINHERIT NOBYPASSRLS;
  END IF;
END $$;
GRANT USAGE ON SCHEMA identity TO hr_identity_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO hr_identity_app;
REVOKE INSERT, UPDATE, DELETE ON identity."__EFMigrationsHistory" FROM hr_identity_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA identity TO hr_identity_app;

-- RLS stays enabled on every Identity table (script 01 lockdown). hr_identity_app gets one policy per table;
-- anon/authenticated still get nothing. Company isolation stays in the EF filters (login is cross-company by nature).
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['Tenants','TenantSettings','TenantSubscriptions','Users','UserExternalLogins','Roles',
                           'Permissions','RolePermissions','UserRoles','RefreshTokens','UserTokens','LoginAudit','__EFMigrationsHistory']
  LOOP
    EXECUTE format('ALTER TABLE identity.%I ENABLE ROW LEVEL SECURITY', t);
    EXECUTE format('CREATE POLICY identity_app_all ON identity.%I FOR ALL TO hr_identity_app USING (true) WITH CHECK (true)', t);
  END LOOP;
END $$;

COMMIT;

-- After COMMIT, in the SQL Editor (not saved anywhere):
-- ALTER ROLE hr_identity_app PASSWORD '<generated>';
