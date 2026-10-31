-- Add user-friendly labels for imported workbooks and immutable calculation snapshots.
-- Keep the columns nullable during deployment so older running API tasks can still write rows.
BEGIN;

ALTER TABLE public.billing_imports
    ADD COLUMN IF NOT EXISTS display_name varchar(255);

UPDATE public.billing_imports
SET display_name = regexp_replace(original_file_name, '\.[^.]*$', '')
WHERE display_name IS NULL;

ALTER TABLE public.billing_calculations
    ADD COLUMN IF NOT EXISTS calculation_name varchar(255);

UPDATE public.billing_calculations
SET calculation_name = left(
    employer_name,
    GREATEST(0, 255 - length(' · ' || to_char(make_date(year, month, 1), 'Mon YYYY')))
) || ' · ' || to_char(make_date(year, month, 1), 'Mon YYYY')
WHERE calculation_name IS NULL;

COMMIT;
