# Sample Apps

Trivial console applications used to validate the toolbox end-to-end. Build one of these, place it under `C:\ToolboxApps\` (or wherever your polling service's `Polling:ExecutablesRoot` points), register it through the UI, and click Run.

## EchoTool

A self-contained sanity check. Echoes its `--message` `--count` times, optionally with `--verbose` diagnostics to stderr. Pass `--message FAIL` to simulate a non-zero exit.

### Build + place it on the polling host

```powershell
cd sample-apps/EchoTool
dotnet publish -c Release -r win-x64 --self-contained false -o C:\ToolboxApps\EchoTool
```

### Register it in the UI

The dev seed migration ([V002__seed_data.sql](../database/migrations/V002__seed_data.sql)) already inserts EchoTool with three parameters:

| Flag         | Type    | Required | Default |
|--------------|---------|----------|---------|
| `--message`  | string  | yes      | `hello` |
| `--count`    | number  | no       | `1`     |
| `--verbose`  | flag    | no       | —       |

So a fresh `docker compose up` boots into a working demo — just publish the EXE to the expected path and you can Run it from the UI.

### Verify

1. `docker compose up`
2. `./scripts/setup-localstack.ps1`
3. Publish EchoTool to `C:\ToolboxApps\EchoTool\`
4. `cd polling-service/src/ToolboxManager.PollingService && dotnet run`
5. Open http://localhost:5173, click **EchoTool**, click **Run**, fill in any message, **Run**.
6. The run detail page should show `Succeeded`, exit code `0`, and stdout containing your message.

If something doesn't work, check Seq at http://localhost:5341 — the API and Polling Service both stream structured logs there.
