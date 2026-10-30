-- Stores validated POS import metadata and only the fields required for billing calculations.
-- The original workbook and card-number column are intentionally not persisted.
BEGIN;

CREATE TABLE IF NOT EXISTS public.billing_imports (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    original_file_name varchar(255) NOT NULL,
    content_sha256 varchar(64) NOT NULL,
    status varchar(32) NOT NULL,
    transaction_count integer NOT NULL CHECK (transaction_count >= 0),
    created_at timestamp with time zone NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_billing_imports_created_at
    ON public.billing_imports (created_at DESC);

CREATE TABLE IF NOT EXISTS public.billing_import_transactions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    billing_import_id uuid NOT NULL REFERENCES public.billing_imports(id) ON DELETE CASCADE,
    source_row_number integer NOT NULL CHECK (source_row_number > 0),
    transaction_id varchar(128) NOT NULL,
    site_id varchar(128) NULL,
    currency varchar(8) NULL,
    machine_name varchar(255) NULL,
    settlement_value numeric(12,2) NOT NULL,
    machine_settlement_time timestamp without time zone NOT NULL,
    product_selection_info varchar(1000) NULL,
    CONSTRAINT uq_billing_import_transactions_source_row UNIQUE (billing_import_id, source_row_number)
);

CREATE INDEX IF NOT EXISTS ix_billing_import_transactions_import_time
    ON public.billing_import_transactions (billing_import_id, machine_settlement_time);

COMMIT;
