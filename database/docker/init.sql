-- =========================================================================
-- Standalone init script (NOT used by docker compose — see note below).
--
-- docker compose mounts each migration directly into the Postgres
-- /docker-entrypoint-initdb.d/ directory with numeric prefixes so they run
-- in order. This file exists for manual onboarding against an external
-- Postgres instance:
--
--    psql -h <host> -U <user> -d <db> -f database/docker/init.sql
-- =========================================================================

\echo 'Applying V001 — initial schema'
\i ../migrations/V001__initial_schema.sql

\echo 'Applying V002 — seed data'
\i ../migrations/V002__seed_data.sql
