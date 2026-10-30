-- Keep the skipped blank-product row count for workbook history.
-- Existing imports are NULL because this value was not stored before this migration.
BEGIN;

ALTER TABLE public.billing_imports
    ADD COLUMN IF NOT EXISTS rows_without_product_name integer NULL
        CHECK (rows_without_product_name IS NULL OR rows_without_product_name >= 0);

COMMIT;
