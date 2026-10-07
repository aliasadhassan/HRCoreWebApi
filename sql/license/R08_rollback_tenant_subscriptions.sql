-- R08: undo 08. Drops ONLY the new table (and its license rows). Run only after the PR is reverted,
-- because the new Identity code reads this table at login.
DROP TABLE IF EXISTS public."TenantSubscriptions";
