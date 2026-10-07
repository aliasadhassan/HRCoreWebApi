-- 09_users_unique_email.sql  (audit H5)
-- One email = one company. Login finds the user by email alone, so the email must be unique
-- across all tenants (not only within one tenant).
-- Safe to re-run. Changes NO data and drops nothing (the per-tenant index stays as it is).
-- If any active email is used twice, the script stops with an error listing it; nothing is fixed silently.

DO $$
DECLARE dupes text;
BEGIN
    SELECT string_agg("NormalizedEmail" || ' (' || n || ')', ', ')
    INTO dupes
    FROM (SELECT "NormalizedEmail", count(*) n
          FROM public."Users" WHERE NOT "IsDeleted"
          GROUP BY 1 HAVING count(*) > 1) d;
    IF dupes IS NOT NULL THEN
        RAISE EXCEPTION 'H5: same email used by more than one active user: %. Resolve these first.', dupes;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Users_NormalizedEmail"
    ON public."Users" ("NormalizedEmail") WHERE "IsDeleted" = false;
