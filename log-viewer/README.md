# Log Viewer (local development only)

A [Seq](https://datalust.co/seq) container that aggregates structured logs from every component (API + Polling Service) for local development. Search, filter by property (RunRequestId, ApplicationName, level), and tail in real time.

> **Production note**: this container is **not** deployed to production. Production uses New Relic via the .NET agent — the NLog config in each service is environment-aware.

## Already wired up

Seq is part of [docker-compose.yml](../docker-compose.yml) at the repo root. There is no separate compose file in this directory — every service that emits logs already points at `seq:5341` for ingestion.

```yaml
seq:
  image: datalust/seq:latest
  ports:
    - "5341:80"
  environment:
    ACCEPT_EULA: "Y"
```

## Use it

```powershell
docker compose up -d seq
```

Open http://localhost:5341 in your browser.

In the UI you can:

- **Search**: full-text across the message + properties (e.g. `RunRequestId = '11111111-…'`)
- **Filter**: by `Application` (`ToolboxManager.Api` vs `ToolboxManager.PollingService`), `Level`, `MachineName`
- **Save searches**: pin frequent queries like "all Failed runs in the last hour"
- **Signal**: trigger a notification (email, Slack webhook) when a query starts matching

## Properties enriched on every event

Set in [api/.../nlog.config](../api/src/ToolboxManager.Api/nlog.config) and [polling-service/.../nlog.config](../polling-service/src/ToolboxManager.PollingService/nlog.config):

| Property        | Source                                                |
|-----------------|-------------------------------------------------------|
| `Application`   | Constant per service (`ToolboxManager.Api`, etc.)      |
| `MachineName`   | NLog `${machinename}`                                  |
| `RunRequestId`  | Scope-pushed in the polling Worker before each run    |
| `ApplicationName` | Scope-pushed alongside `RunRequestId` for app context |

## Persistence

Seq stores its index in a Docker named volume (`seq-data`). Nuke it any time:

```powershell
docker compose down seq
docker volume rm toolbox-manager_seq-data
```

## Why Seq for local

- Single container, zero config.
- Excellent property-based filtering — matches the structured logs NLog produces.
- Free for single-user / dev use.
- Mirrors what New Relic gives you in prod (saved queries, alerts, structured search) so the mental model is the same.
