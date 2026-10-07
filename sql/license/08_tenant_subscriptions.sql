-- 08_tenant_subscriptions.sql  (audit M4, part 1)
-- Adds the license table for the Identity service (schema public, where Identity runs).
-- Safe to re-run. Changes NO existing table and deletes NOTHING.
-- What it does:
--   1. creates public."TenantSubscriptions" (one row per subscription period; history is kept)
--   2. at most one "current" row per tenant (Status Trial/Active/PastDue)
--   3. RLS on + no grants to anon/authenticated (same lockdown as script 01; Identity connects as postgres)
--   4. gives every existing tenant that has no current row an open-ended Active row on its current
--      Tenants."Plan", no seat limit, amount 0, so nobody is locked out when the new code ships
-- Status:        1 Trial, 2 Active, 3 PastDue, 4 Expired, 5 Cancelled
-- BillingCycle:  0 None, 1 Monthly, 2 Yearly
-- EndDate NULL = no end date. GraceUntil NULL = no grace (access ends after EndDate).
-- Concurrency = Postgres xmin (no RowVersion column).

BEGIN;

CREATE TABLE IF NOT EXISTS public."TenantSubscriptions" (
    "Id"           uuid          NOT NULL DEFAULT gen_random_uuid(),
    "TenantId"     uuid          NOT NULL,
    "PlanCode"     varchar(50)   NOT NULL,
    "Status"       smallint      NOT NULL,
    "StartDate"    date          NOT NULL,
    "EndDate"      date          NULL,
    "GraceUntil"   date          NULL,
    "SeatLimit"    integer       NULL,
    "Amount"       numeric(18,2) NOT NULL DEFAULT 0,
    "CurrencyCode" char(3)       NOT NULL,
    "BillingCycle" smallint      NOT NULL DEFAULT 0,
    "AutoRenew"    boolean       NOT NULL DEFAULT false,
    "RenewedFromId" uuid         NULL,
    "Notes"        varchar(500)  NULL,
    "CreatedAt"    timestamptz(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "CreatedBy"    uuid          NULL,
    "UpdatedAt"    timestamptz(3) NULL,
    "UpdatedBy"    uuid          NULL,
    CONSTRAINT "PK_TenantSubscriptions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TenantSubscriptions_Tenants_TenantId"
        FOREIGN KEY ("TenantId") REFERENCES public."Tenants" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_TenantSubscriptions_TenantSubscriptions_RenewedFromId"
        FOREIGN KEY ("RenewedFromId") REFERENCES public."TenantSubscriptions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "CK_TenantSubscriptions_Status"       CHECK ("Status" BETWEEN 1 AND 5),
    CONSTRAINT "CK_TenantSubscriptions_BillingCycle" CHECK ("BillingCycle" BETWEEN 0 AND 2),
    CONSTRAINT "CK_TenantSubscriptions_Dates"        CHECK ("EndDate" IS NULL OR "EndDate" >= "StartDate"),
    CONSTRAINT "CK_TenantSubscriptions_Grace"        CHECK ("GraceUntil" IS NULL OR ("EndDate" IS NOT NULL AND "GraceUntil" >= "EndDate")),
    CONSTRAINT "CK_TenantSubscriptions_SeatLimit"    CHECK ("SeatLimit" IS NULL OR "SeatLimit" > 0),
    CONSTRAINT "CK_TenantSubscriptions_Amount"       CHECK ("Amount" >= 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_TenantSubscriptions_Current"
    ON public."TenantSubscriptions" ("TenantId") WHERE "Status" IN (1, 2, 3);
CREATE INDEX IF NOT EXISTS "IX_TenantSubscriptions_TenantId_StartDate"
    ON public."TenantSubscriptions" ("TenantId", "StartDate");
CREATE INDEX IF NOT EXISTS "IX_TenantSubscriptions_RenewedFromId"
    ON public."TenantSubscriptions" ("RenewedFromId");

ALTER TABLE public."TenantSubscriptions" ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public."TenantSubscriptions" FROM anon, authenticated;

INSERT INTO public."TenantSubscriptions"
    ("TenantId", "PlanCode", "Status", "StartDate", "CurrencyCode", "Notes")
SELECT t."Id",
       t."Plan",
       2,
       t."CreatedAt"::date,
       COALESCE(upper(s."Currency"), 'PKR'),
       'Created by 08_tenant_subscriptions.sql for an existing tenant'
FROM public."Tenants" t
LEFT JOIN public."TenantSettings" s ON s."TenantId" = t."Id"
WHERE NOT t."IsDeleted"
  AND NOT EXISTS (SELECT 1 FROM public."TenantSubscriptions" x
                  WHERE x."TenantId" = t."Id" AND x."Status" IN (1, 2, 3));

COMMIT;
