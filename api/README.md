# Toolbox Manager — API

ASP.NET Core 8 Web API. Owns the database, publishes run requests to SQS, and accepts status callbacks from the Polling Service.

## Routes

| Verb | Path | Purpose |
|------|------|---------|
| GET    | `/api/applications`                       | List registered apps (`?includeInactive=true` for soft-deleted) |
| GET    | `/api/applications/{id}`                  | Get one app with its parameters |
| POST   | `/api/applications`                       | Register a new app (optionally with parameters in one shot) |
| PUT    | `/api/applications/{id}`                  | Update app metadata |
| DELETE | `/api/applications/{id}`                  | Soft delete (sets `is_active = false`) |
| POST   | `/api/applications/{appId}/parameters`    | Add a parameter to an app |
| GET    | `/api/parameters/{id}`                    | Get one parameter |
| PUT    | `/api/parameters/{id}`                    | Update a parameter |
| DELETE | `/api/parameters/{id}`                    | Remove a parameter |
| POST   | `/api/applications/{appId}/run`           | Trigger a run — validates inputs, persists `run_requests` row, publishes to SQS |
| GET    | `/api/runs`                               | List runs, newest first (`?applicationId=&status=&limit=50`) |
| GET    | `/api/runs/{id}`                          | Get one run |
| PATCH  | `/api/runs/{id}/status`                   | **Polling-Service callback**: update status / exit code / stdout / stderr |
| GET    | `/health`                                 | Liveness + DB health check |
| GET    | `/swagger`                                | OpenAPI explorer |

Full schema is in Swagger UI when the API is running locally — http://localhost:8080/swagger.

## Architecture

```
Controllers ─► IRunRequestService ─► (ToolboxDbContext + ISqsPublisher)
                  │                          │
                  │                          └─► AWS SQS
                  └─► validates parameter values, builds argv,
                      persists run_requests, publishes message
```

- **Controllers** are thin — model validation + dispatch to services.
- **`RunRequestService`** is the only place that knows how to translate `(application, values)` into a real argv. Centralizing that means the wire contract to the Polling Service stays narrow: it just executes what we send.
- **`MigrationRunner`** applies SQL migration files baked into the build output. Tracking lives in a `schema_migrations` table.
- **`SqsPublisher`** caches the queue URL on first use and auto-creates the queue if it doesn't exist (helpful for LocalStack on first boot).

## Logging

NLog is configured in [src/ToolboxManager.Api/nlog.config](src/ToolboxManager.Api/nlog.config) with three sinks:

- **Console** — picked up by Docker / `dotnet run`
- **File** — rolling daily logs in `./logs/toolbox-api-YYYY-MM-DD.log`, 14 archives kept
- **Seq** — buffered async target, points at `Seq.ServerUrl` from config

In production, New Relic ingests the structured stdout via the .NET agent (no code change needed — see [docs/deployment.md](../docs/deployment.md)).

## Local development

### Inside docker compose (recommended)

```powershell
docker compose up --build api
```

Open http://localhost:8080/swagger.

### As a standalone dotnet process

You still need Postgres + LocalStack + Seq running. Easiest: start *only* the dependencies in compose, then `dotnet run`:

```powershell
docker compose up -d postgres localstack seq
./scripts/setup-localstack.ps1

cd api/src/ToolboxManager.Api
dotnet run
```

Override appsettings.json values via environment variables (double-underscore for nesting):

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=toolbox;Username=toolbox;Password=toolbox_dev_password"
$env:Aws__SqsServiceUrl         = "http://localhost:4566"
dotnet run
```

## SQS wire contract

Producer (API) and consumer (Polling Service) share [SqsRunMessage](src/ToolboxManager.Api/Dtos/SqsRunMessage.cs). The shape is mirrored in the Polling Service; if you change one, change both.

```jsonc
{
  "runRequestId":   "f1e0d2…",
  "applicationId":  "11111111-…",
  "applicationName":"EchoTool",
  "executablePath": "C:\\ToolboxApps\\EchoTool\\EchoTool.exe",
  "workingDirectory":"C:\\ToolboxApps\\EchoTool",
  "timeoutSeconds": 60,
  "arguments":      ["--message","hi","--count","3","--verbose"],
  "requestedBy":    "user@example.com",
  "queuedAt":       "2025-05-25T14:22:00Z"
}
```

## Tests

A `ToolboxManager.Api.Tests` project will live in `src/` alongside the API project when added — wire it into the solution and run with `dotnet test`. (Out of scope for the initial scaffold.)

## Production

- Built into a container by [.github/workflows/api.yml](../.github/workflows/api.yml) on every push to `main`.
- Deploys to AWS (ECS or App Runner) behind the existing ingress.
- Migrations run on startup. The same SQL files are baked into the image, so you don't need a separate migration job.
