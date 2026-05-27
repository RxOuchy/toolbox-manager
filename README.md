# Toolbox Manager

A universal web UI for running custom-built C# console applications through a parameterized, browser-driven workflow. Define an application once, declare its arguments, and run it on demand against a controlled Windows host without ever touching the command line.

## What it does

1. A developer publishes a console application to the host (e.g. `C:\ToolboxApps\MyTool\`) with a `README.md` containing an `## Application Settings` JSON block. Within a minute the **Polling Service** discovers it and registers it via the API — no manual setup. (Apps can also be registered by hand through the UI if you prefer.)
2. A user opens the **Web UI**, sees the registered app, fills the declared parameters in a generated form, and clicks **Run**.
3. The **API** persists the run request and publishes a message to **AWS SQS**.
4. The **Polling Service** running on the host Windows machine consumes the SQS message, executes the registered executable under a manually-provisioned **gMSA service account**, captures stdout/stderr, and reports the result back via the API.
5. All components emit structured logs through **NLog**. In local dev these are aggregated in a **Seq** container with search; in production they ship to **New Relic**.

## Architecture

```
┌──────────┐    HTTPS    ┌──────────┐    SQL    ┌─────────────┐
│  Web UI  │────────────▶│   API    │──────────▶│  PostgreSQL │
└──────────┘             └────┬─────┘           └─────────────┘
                              │
                              │ SQS Publish
                              ▼
                         ┌─────────┐
                         │ AWS SQS │
                         └────┬────┘
                              │ Long-poll
                              ▼
                   ┌──────────────────────────┐
                   │   Polling Service        │
                   │   (Windows, gMSA)        │
                   │                          │
                   │   Spawns: console apps   │
                   └────┬─────────────────────┘
                        │ Status / output
                        ▼
                   ┌─────────┐
                   │   API   │  (writes run results to PostgreSQL)
                   └─────────┘
```

Everything except the database lives in containers locally. In production, the API and UI ship to AWS, the PostgreSQL DB is managed (RDS or on-prem), and the Polling Service installs as a Windows Service on the on-prem host that also stores the executables.

## Repository layout

| Directory | Purpose |
|-----------|---------|
| [api/](api/) | ASP.NET Core 8 Web API. CRUD for applications + parameters, SQS publisher, run status webhook. |
| [ui/](ui/) | React + TypeScript + Vite + Tailwind frontend. |
| [polling-service/](polling-service/) | .NET 8 Worker Service. Consumes SQS, executes registered console apps, reports back. |
| [database/](database/) | PostgreSQL schema, migrations, and the Docker init script. |
| [log-viewer/](log-viewer/) | Seq container for local log aggregation and search. |
| [sample-apps/](sample-apps/) | A trivial `EchoTool` console app you can register to test the system end-to-end. |
| [docs/](docs/) | Architecture, deployment, and gMSA provisioning guides. |
| [.github/workflows/](.github/workflows/) | CI/CD pipelines for the API, UI, and Polling Service. |
| [scripts/](scripts/) | Local dev helpers (LocalStack queue creation, etc.). |

Each top-level directory has its own README explaining the component in detail.

## Quick start (local dev)

Prerequisites: Docker Desktop, .NET 8 SDK, Node.js 20+, PowerShell.

```powershell
# 1. Copy the env template and edit if needed
Copy-Item .env.example .env

# 2. Bring up Postgres, LocalStack (SQS), Seq, API, and UI
docker compose up --build

# 3. In a separate terminal, create the SQS queue inside LocalStack
./scripts/setup-localstack.ps1

# 4. Build and run the polling service on your Windows host
cd polling-service/src/ToolboxManager.PollingService
dotnet run
```

Then browse to:

- **UI**: http://localhost:5173
- **API (Swagger)**: http://localhost:8080/swagger
- **Seq logs**: http://localhost:5341
- **LocalStack SQS**: http://localhost:4566

To register the included [sample app](sample-apps/EchoTool/), follow the walkthrough in [docs/architecture.md](docs/architecture.md#end-to-end-walkthrough).

## Production

- **CI/CD**: GitHub Actions. See [.github/workflows/](.github/workflows/).
- **API + UI**: Containerized, deployed to AWS (ECS or App Runner) behind your existing ingress.
- **Polling Service**: Installed as a Windows Service on the on-prem host that stores the executables. Runs under a gMSA; see [docs/gmsa-setup.md](docs/gmsa-setup.md).
- **Database**: PostgreSQL — either managed (RDS) or on-prem. The API applies migrations on startup.
- **Logs**: NLog targets switch from Seq/file to **New Relic** via the `NEWRELIC_LICENSE_KEY` env var. See [docs/deployment.md](docs/deployment.md).

## Why this design

- **SQS decouples** the API (cloud) from the executor (on-prem) without requiring inbound firewall holes — the on-prem host only needs outbound HTTPS to AWS.
- **gMSA** lets the executor authenticate to on-prem databases under a managed AD identity without storing passwords.
- **One execution host** keeps the operational model simple: any console app you want to run goes on that machine, and the Polling Service is the only thing that can launch it.
- **Auto-discovery via README manifest** — drop a tool onto the host with an `## Application Settings` block in its README and the polling service registers it automatically. No deploy-then-manually-configure step. See [polling-service/README.md](polling-service/README.md#auto-discovery).
- **NLog + Seq locally, New Relic in prod** — same log calls, different sinks, no code change.

## License

Internal — Listrak.
