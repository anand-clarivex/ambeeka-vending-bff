-- Persist immutable billing calculation previews and retain their source imports.
BEGIN;

CREATE TABLE IF NOT EXISTS public.billing_calculations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employer_name varchar(255) NOT NULL,
    year integer NOT NULL CHECK (year BETWEEN 1 AND 9999),
    month integer NOT NULL CHECK (month BETWEEN 1 AND 12),
    status varchar(32) NOT NULL,
    result_json jsonb NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_billing_calculations_employer_period_created
    ON public.billing_calculations (employer_name, year, month, created_at DESC);

CREATE TABLE IF NOT EXISTS public.billing_calculation_imports (
    calculation_id uuid NOT NULL REFERENCES public.billing_calculations(id) ON DELETE CASCADE,
    billing_import_id uuid NOT NULL REFERENCES public.billing_imports(id) ON DELETE RESTRICT,
    PRIMARY KEY (calculation_id, billing_import_id)
);

COMMIT;
