-- R09: undo 09. Only drops the new global index; the per-tenant index stays.
DROP INDEX IF EXISTS public."UX_Users_NormalizedEmail";
