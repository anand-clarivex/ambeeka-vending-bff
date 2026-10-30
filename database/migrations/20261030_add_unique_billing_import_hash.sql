-- Prevent importing the exact same workbook more than once.
BEGIN;

CREATE UNIQUE INDEX IF NOT EXISTS uq_billing_imports_content_sha256
    ON public.billing_imports (content_sha256);

COMMIT;
