# Database

PostgreSQL schema and migrations for Toolbox Manager.

## Schema

Three tables drive the system:

| Table | Purpose |
|-------|---------|
| `applications` | Registered console apps — name, executable path, working directory, timeout. |
| `application_parameters` | Parameter definitions per app (name, label, type, required, default). |
| `run_requests` | One row per "Run" click. Holds the parameter values, status, exit code, captured stdout/stderr. |

Each table has an `updated_at` trigger so changes are auto-timestamped. The schema lives in [migrations/V001__initial_schema.sql](migrations/V001__initial_schema.sql).

## Parameter types

`application_parameters.parameter_type` is one of:

| Type | Semantics |
|------|-----------|
| `string` | Free text passed as-is. |
| `number` | Numeric; the API validates it's parseable before queuing the run. |
| `boolean` | Renders as a checkbox; emits `--flag true` / `--flag false`. |
| `flag` | Renders as a checkbox; emits the flag name only when checked (e.g. `--verbose`). |
| `secret` | Same as `string` but the UI masks the input and the run history redacts the value. |

## Local development

`docker compose up` brings up Postgres and applies the migrations automatically. The compose file mounts each migration into `/docker-entrypoint-initdb.d/` with numeric prefixes; Postgres runs them in alphabetical order on first start (when the volume is empty).

Connect with any client:

```
Host:     localhost
Port:     5432
Database: toolbox
User:     toolbox
Password: toolbox_dev_password    # from .env
```

To reset the database, drop the volume:

```powershell
docker compose down -v
docker compose up -d postgres
```

## Migrations

Migrations are versioned `V###__description.sql` and applied in order. To add a new migration:

1. Create `migrations/V003__your_change.sql`.
2. Add a corresponding `- ./database/migrations/V003__...:/docker-entrypoint-initdb.d/03-...` mount in [docker-compose.yml](../docker-compose.yml).
3. For an existing dev DB, either drop the volume (loses data) or apply the new file manually with `psql -f`.

In production, migrations run via the API on startup (see [api/](../api/) — `MigrationRunner`). The same SQL files are baked into the API container so deployment is one artifact.

## Why raw SQL instead of EF Core migrations?

- The schema is small and stable; declarative SQL is clearer than EF's generated migration code.
- The same files seed the Docker container, run in production, and document the schema for new engineers.
- The API still uses EF Core as the ORM at runtime — only the migration mechanism is hand-written.
