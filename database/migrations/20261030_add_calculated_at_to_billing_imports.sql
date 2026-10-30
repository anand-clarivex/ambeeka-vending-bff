-- Record when an import is used by a completed calculation so it can be protected from deletion.
BEGIN;

ALTER TABLE public.billing_imports
    ADD COLUMN IF NOT EXISTS calculated_at timestamp with time zone NULL;

COMMIT;
