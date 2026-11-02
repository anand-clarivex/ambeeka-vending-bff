-- Store parsed ignored-row details so Import History can restore processing state.
-- NULL is retained for older imports whose ignored rows were not saved.
BEGIN;

ALTER TABLE public.billing_imports
    ADD COLUMN IF NOT EXISTS ignored_rows_json jsonb NULL;

COMMIT;
