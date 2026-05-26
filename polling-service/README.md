# Toolbox Manager — Polling Service

.NET 8 Worker Service that runs on the on-prem Windows host. Long-polls AWS SQS, executes the requested console application, and reports the result back to the API.

This is the **only** component that has access to the on-prem network and the registered executables. The API and UI never touch the .exe files directly.

## Flow

```
ReceiveMessage (long-poll 20s)
  └─ deserialize SqsRunMessage
     ├─ PATCH /api/runs/{id}/status   → Running
     ├─ spawn process (timeout, capture stdout/stderr)
     ├─ PATCH /api/runs/{id}/status   → Succeeded / Failed / TimedOut
     └─ DeleteMessage
```

Concurrency is bounded by `Polling.MaxConcurrentRuns` (default 2). The SQS visibility timeout is sized to comfortably exceed the longest expected execution.

## Security guard rail

`Polling.ExecutablesRoot` is an allow-list prefix. Any `executablePath` not under that root is refused with a `Failed` status — the API can't ask this service to launch `C:\Windows\System32\reg.exe` even if a registered app pointed there.

## gMSA

In production this service runs under a **Group Managed Service Account**. The gMSA is provisioned manually on the host once (see [docs/gmsa-setup.md](../docs/gmsa-setup.md)) and installed with:

```powershell
sc.exe create ToolboxManagerPollingService binPath= "C:\Program Files\ToolboxManager\PollingService\ToolboxManager.PollingService.exe" obj= "DOMAIN\gMSA-Toolbox$" password= "" start= auto
```

Console apps the service spawns inherit the gMSA identity, so they authenticate to on-prem databases under that managed account without any stored passwords.

## Configuration

See [appsettings.json](src/ToolboxManager.PollingService/appsettings.json). Override via environment variables (double-underscore for nesting):

| Setting | Default | Meaning |
|---------|---------|---------|
| `Aws:SqsQueueName`            | `toolbox-run-requests` | Queue to consume from |
| `Aws:SqsServiceUrl`           | `http://localhost:4566` | LocalStack endpoint; leave blank in prod |
| `Polling:ApiBaseUrl`          | `http://localhost:8080` | Where to POST run status callbacks |
| `Polling:WaitTimeSeconds`     | `20` | SQS long-poll wait (max 20) |
| `Polling:MaxConcurrentRuns`   | `2` | Parallel executions on this host |
| `Polling:VisibilityTimeoutSeconds` | `600` | Must exceed longest expected run |
| `Polling:ExecutablesRoot`     | `C:\ToolboxApps` | Allow-list root for executable paths |

## Local development

You need the rest of the stack running (Postgres, LocalStack, API, Seq). The fastest path:

```powershell
# from repo root
./scripts/setup-dev.ps1

# in this directory
cd polling-service/src/ToolboxManager.PollingService
dotnet run
```

The polling service runs natively (not in Docker) because it spawns console apps that live on the host's filesystem and, in production, runs under a gMSA — neither of those work cleanly inside a Linux container.

To trigger a run, register the [EchoTool sample](../sample-apps/EchoTool/) and hit **Run** in the UI.

## Installing as a Windows Service (production)

`Microsoft.Extensions.Hosting.WindowsServices` is wired up in [Program.cs](src/ToolboxManager.PollingService/Program.cs), so the same EXE works as a console app and as a Windows Service.

```powershell
# Publish to a self-contained folder
dotnet publish src/ToolboxManager.PollingService -c Release -r win-x64 --self-contained false -o C:\ProgramFiles\ToolboxManager\PollingService

# Register with the SCM under the gMSA
sc.exe create ToolboxManagerPollingService `
    binPath= "C:\ProgramFiles\ToolboxManager\PollingService\ToolboxManager.PollingService.exe" `
    obj=     "DOMAIN\gMSA-Toolbox$" `
    password= "" `
    start=   auto

sc.exe start ToolboxManagerPollingService
```

Logs land in `%PROGRAMDATA%\ToolboxManager\PollingService\logs\` (you can override the base directory in [nlog.config](src/ToolboxManager.PollingService/nlog.config) for production).

## Logging

NLog targets: Console, File (daily rolling, 14 archives), Seq (local dev). In production New Relic ingests structured stdout from the .NET agent.

Each handler call sets a logging scope with `RunRequestId` and `ApplicationName` so individual runs are easy to follow in Seq / New Relic.

## SQS wire contract

Mirror of [`SqsRunMessage`](src/ToolboxManager.PollingService/Models/SqsRunMessage.cs) on the API side — any change must be applied to both projects.
