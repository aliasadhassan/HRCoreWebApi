-- Rollback of 10_identity_schema.sql. Run with Identity API STOPPED, then deploy code from before the H6 PR.
-- The dropped bytea RowVersion columns are re-added with random values (they never held business data).
BEGIN;

DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['Tenants','TenantSettings','TenantSubscriptions','Users','UserExternalLogins','Roles',
                           'Permissions','RolePermissions','UserRoles','RefreshTokens','UserTokens','LoginAudit','__EFMigrationsHistory']
  LOOP
    EXECUTE format('DROP POLICY IF EXISTS identity_app_all ON identity.%I', t);
  END LOOP;
END $$;
REVOKE ALL ON ALL TABLES IN SCHEMA identity FROM hr_identity_app;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA identity FROM hr_identity_app;
REVOKE ALL ON SCHEMA identity FROM hr_identity_app;
DROP ROLE IF EXISTS hr_identity_app;

DROP TABLE identity."__EFMigrationsHistory";

ALTER TABLE identity."LoginAudit"         ALTER COLUMN "OccurredAt"  TYPE timestamp(3) USING "OccurredAt"  AT TIME ZONE 'UTC';
ALTER TABLE identity."RefreshTokens"      ALTER COLUMN "CreatedAt"   TYPE timestamp(3) USING "CreatedAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "ExpiresAt"   TYPE timestamp(3) USING "ExpiresAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "RevokedAt"   TYPE timestamp(3) USING "RevokedAt"   AT TIME ZONE 'UTC';
ALTER TABLE identity."Roles"              ALTER COLUMN "CreatedAt"   DROP DEFAULT,
                                          ALTER COLUMN "CreatedAt"   TYPE timestamp(3) USING "CreatedAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "UpdatedAt"   TYPE timestamp(3) USING "UpdatedAt"   AT TIME ZONE 'UTC';
ALTER TABLE identity."TenantSettings"     ALTER COLUMN "UpdatedAt"   TYPE timestamp(3) USING "UpdatedAt"   AT TIME ZONE 'UTC';
ALTER TABLE identity."Tenants"            ALTER COLUMN "CreatedAt"   DROP DEFAULT,
                                          ALTER COLUMN "CreatedAt"   TYPE timestamp(3) USING "CreatedAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "UpdatedAt"   TYPE timestamp(3) USING "UpdatedAt"   AT TIME ZONE 'UTC';
ALTER TABLE identity."UserExternalLogins" ALTER COLUMN "LastUsedAt"  TYPE timestamp(3) USING "LastUsedAt"  AT TIME ZONE 'UTC',
                                          ALTER COLUMN "LinkedAt"    TYPE timestamp(3) USING "LinkedAt"    AT TIME ZONE 'UTC';
ALTER TABLE identity."UserRoles"          ALTER COLUMN "AssignedAt"  TYPE timestamp(3) USING "AssignedAt"  AT TIME ZONE 'UTC';
ALTER TABLE identity."UserTokens"         ALTER COLUMN "CreatedAt"   TYPE timestamp(3) USING "CreatedAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "ExpiresAt"   TYPE timestamp(3) USING "ExpiresAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "UsedAt"      TYPE timestamp(3) USING "UsedAt"      AT TIME ZONE 'UTC';
ALTER TABLE identity."Users"              ALTER COLUMN "CreatedAt"   DROP DEFAULT,
                                          ALTER COLUMN "CreatedAt"   TYPE timestamp(3) USING "CreatedAt"   AT TIME ZONE 'UTC',
                                          ALTER COLUMN "LastLoginAt" TYPE timestamp(3) USING "LastLoginAt" AT TIME ZONE 'UTC',
                                          ALTER COLUMN "LockoutEnd"  TYPE timestamp(3) USING "LockoutEnd"  AT TIME ZONE 'UTC',
                                          ALTER COLUMN "UpdatedAt"   TYPE timestamp(3) USING "UpdatedAt"   AT TIME ZONE 'UTC';
ALTER TABLE identity."Tenants" ALTER COLUMN "CreatedAt" SET DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC');
ALTER TABLE identity."Users"   ALTER COLUMN "CreatedAt" SET DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC');
ALTER TABLE identity."Roles"   ALTER COLUMN "CreatedAt" SET DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC');

ALTER TABLE identity."Tenants"        ADD COLUMN "RowVersion" bytea NOT NULL DEFAULT extensions.gen_random_bytes(8);
ALTER TABLE identity."TenantSettings" ADD COLUMN "RowVersion" bytea NOT NULL DEFAULT extensions.gen_random_bytes(8);
ALTER TABLE identity."Users"          ADD COLUMN "RowVersion" bytea NOT NULL DEFAULT extensions.gen_random_bytes(8);
ALTER TABLE identity."Roles"          ADD COLUMN "RowVersion" bytea NOT NULL DEFAULT extensions.gen_random_bytes(8);

DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['Tenants','TenantSettings','TenantSubscriptions','Users','UserExternalLogins','Roles',
                           'Permissions','RolePermissions','UserRoles','RefreshTokens','UserTokens','LoginAudit']
  LOOP
    EXECUTE format('ALTER TABLE identity.%I SET SCHEMA public', t);
  END LOOP;
  FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'archive_public_employee' LOOP
    EXECUTE format('ALTER TABLE archive_public_employee.%I SET SCHEMA public', t);
  END LOOP;
END $$;
DROP SCHEMA identity;
DROP SCHEMA archive_public_employee;
ALTER SCHEMA archive_identity_20261001 RENAME TO identity;

COMMIT;
