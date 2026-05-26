# Architecture

## Components

```
┌────────────┐   HTTPS    ┌────────────┐   SQL    ┌──────────────┐
│   Web UI   │ ─────────► │    API     │ ───────► │  PostgreSQL  │
│  (Nginx +  │   (CORS)   │  (ASP.NET) │  (EF)    │              │
│   React)   │            │            │          └──────────────┘
└────────────┘            └─────┬──────┘
                                │ SendMessage
                                ▼
                          ┌──────────┐
                          │ AWS SQS  │
                          └─────┬────┘
                                │ Long-poll
                                ▼
              ┌──────────────────────────────────────┐
              │       Polling Service                │
              │   (Windows host, gMSA)               │
              │                                      │
              │   ▸ ReceiveMessage                   │
              │   ▸ PATCH /api/runs/{id}/status      │
              │   ▸ Process.Start(executable)        │
              │   ▸ Capture stdout / stderr          │
              │   ▸ PATCH /api/runs/{id}/status      │
              │   ▸ DeleteMessage                    │
              └────┬─────────────────────────────────┘
                   │ child process
                   ▼
              ┌──────────────────────────────────┐
              │  Registered console apps         │
              │  C:\ToolboxApps\<AppName>\…exe   │
              └──────────────────────────────────┘
```

## Why this shape

### SQS in the middle

The execution host is on-prem; the UI and API run in AWS. Inserting SQS between them means:

- **No inbound firewall holes** into the on-prem network. The polling service makes outbound HTTPS to AWS only.
- **Decoupling**: the API is up even if the polling service is restarting / down. Run requests queue and drain when the consumer comes back.
- **Retry & DLQ for free**: visibility timeout + max-receives + DLQ are SQS primitives — no custom retry logic needed.

### One execution host

Per the user spec, all registered apps live on a single Windows machine and run there. Benefits:

- Simple ops model — one machine to harden, one filesystem to manage, one gMSA to provision.
- A single allow-list root (`Polling.ExecutablesRoot`) is enough to prevent the API from asking the service to run anything off-list.

### gMSA

Console apps that hit on-prem databases need a domain identity to authenticate. A gMSA gives that without storing a password anywhere:

- Provisioned once by an AD admin (see [gmsa-setup.md](gmsa-setup.md)).
- The polling service Windows Service runs as the gMSA; any child process it spawns inherits that identity.
- Rotated automatically by AD; no secrets in the API or repo.

### NLog → Seq locally, New Relic in prod

The same `logger.LogInformation(...)` calls produce structured events. The NLog target wired into the runtime decides where they land:

- **Local**: Console + rolling file + Seq.
- **Production**: Console + rolling file (kept for forensic purposes) — New Relic ingests structured stdout via the .NET agent. No code change.

## End-to-end walkthrough

Using the seed data + `EchoTool` sample:

1. `docker compose up --build`
2. `./scripts/setup-localstack.ps1`  *(creates the SQS queue inside LocalStack)*
3. Publish [EchoTool](../sample-apps/EchoTool/) to `C:\ToolboxApps\EchoTool\` on this machine.
4. `cd polling-service/src/ToolboxManager.PollingService && dotnet run`
5. Open http://localhost:5173.
6. Click **EchoTool** → **Run** → fill `--message` → **Run**.
7. You land on the run detail page; status flips `Queued → Running → Succeeded` (auto-polls every 2s while non-terminal).
8. Cross-check Seq (http://localhost:5341) — you'll see paired log lines from `ToolboxManager.Api` (queued) and `ToolboxManager.PollingService` (executed).

## Data model

Three tables. See [database/migrations/V001__initial_schema.sql](../database/migrations/V001__initial_schema.sql) for the authoritative schema:

- `applications` — one row per registered .exe
- `application_parameters` — N rows per app describing each argument
- `run_requests` — one row per "Run" click with parameter snapshot, status, output

`run_requests.parameter_values` is `jsonb` — the snapshot is taken at run time so renaming a parameter on the application later doesn't rewrite history.

## Wire contracts

There are two wire contracts that span service boundaries:

1. **REST**: documented at `/swagger` when the API is running. The UI is the only typed consumer; the wire types are mirrored in [ui/src/api/types.ts](../ui/src/api/types.ts).
2. **SQS message**: [`SqsRunMessage`](../api/src/ToolboxManager.Api/Dtos/SqsRunMessage.cs) on the producer side, [`SqsRunMessage`](../polling-service/src/ToolboxManager.PollingService/Models/SqsRunMessage.cs) on the consumer side. Keep them in sync — any field rename requires changing both.

## Non-goals

These are intentionally **not** in scope for the scaffold:

- **Auth/Z**: no login on the UI/API yet. The system reminders show this is a Listrak-internal tool — bolt SSO on at the ingress (Cloudflare Access, AWS ALB authentication, etc.) before opening it up.
- **Real-time log streaming**: the run detail page polls every 2s. If you need true streaming, add an SSE endpoint that tails the polling service's local log file for that `RunRequestId`.
- **Per-app permission scopes**: every authenticated user can run every registered app.
- **Multi-host execution**: one polling service, one host. To scale out, run multiple polling services on different hosts pointed at the same queue — SQS will load-balance, but you'd need per-host executable allow-lists and a host selector.
