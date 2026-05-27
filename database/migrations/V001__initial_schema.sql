-- =========================================================================
-- V001 — initial schema
-- =========================================================================
-- Tables:
--   applications            Registered console apps and their executable paths
--   application_parameters  Parameter definitions per application
--   run_requests            One row per "Run" button click, with status/output
-- =========================================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ──────────── applications ────────────
CREATE TABLE IF NOT EXISTS applications (
    id                  uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    name                varchar(200) NOT NULL UNIQUE,
    description         text         NOT NULL DEFAULT '',
    executable_path     varchar(1000) NOT NULL,
    working_directory   varchar(1000),
    timeout_seconds     integer      NOT NULL DEFAULT 300 CHECK (timeout_seconds > 0),
    is_active           boolean      NOT NULL DEFAULT true,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_applications_active ON applications (is_active);

-- ──────────── application_parameters ────────────
CREATE TABLE IF NOT EXISTS application_parameters (
    id              uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    application_id  uuid         NOT NULL REFERENCES applications(id) ON DELETE CASCADE,
    parameter_name  varchar(200) NOT NULL,           -- the argument flag, e.g. --config or -v
    display_label   varchar(200) NOT NULL,           -- shown in the UI form
    parameter_type  varchar(20)  NOT NULL CHECK (parameter_type IN ('string','number','boolean','flag','secret')),
    is_required     boolean      NOT NULL DEFAULT false,
    default_value   text,
    description     text         NOT NULL DEFAULT '',
    display_order   integer      NOT NULL DEFAULT 0,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),

    UNIQUE (application_id, parameter_name)
);

CREATE INDEX IF NOT EXISTS idx_parameters_application ON application_parameters (application_id, display_order);

-- ──────────── run_requests ────────────
CREATE TABLE IF NOT EXISTS run_requests (
    id                uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    application_id    uuid         NOT NULL REFERENCES applications(id) ON DELETE RESTRICT,
    requested_by      varchar(320) NOT NULL,           -- email
    parameter_values  jsonb        NOT NULL DEFAULT '{}'::jsonb,
    status            varchar(20)  NOT NULL DEFAULT 'queued'
                        CHECK (status IN ('queued','running','succeeded','failed','cancelled','timed_out')),
    sqs_message_id    varchar(200),
    exit_code         integer,
    stdout            text,
    stderr            text,
    error_message     text,
    queued_at         timestamptz  NOT NULL DEFAULT now(),
    started_at        timestamptz,
    completed_at      timestamptz,
    created_at        timestamptz  NOT NULL DEFAULT now(),
    updated_at        timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_runs_application_queued ON run_requests (application_id, queued_at DESC);
CREATE INDEX IF NOT EXISTS idx_runs_status            ON run_requests (status);

-- ──────────── updated_at trigger ────────────
CREATE OR REPLACE FUNCTION touch_updated_at() RETURNS trigger AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_applications_touch ON applications;
CREATE TRIGGER trg_applications_touch       BEFORE UPDATE ON applications
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

DROP TRIGGER IF EXISTS trg_parameters_touch ON application_parameters;
CREATE TRIGGER trg_parameters_touch         BEFORE UPDATE ON application_parameters
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

DROP TRIGGER IF EXISTS trg_runs_touch ON run_requests;
CREATE TRIGGER trg_runs_touch               BEFORE UPDATE ON run_requests
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();
