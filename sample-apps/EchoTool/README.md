# EchoTool

Trivial console app for verifying the Toolbox Manager end-to-end. Echoes its `--message` `--count` times to stdout. Pass `--message FAIL` to simulate a non-zero exit.

This README also serves as the **discovery manifest** that the Polling Service uses to auto-register the tool. When you publish EchoTool to the polling host, the README is copied alongside the `.exe` (see `EchoTool.csproj`'s `<Content Include="README.md">`), and on its next scan the Polling Service finds it and POSTs the application to the API.

## Build + deploy

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o C:\ToolboxApps\EchoTool
```

Result on the host:

```
C:\ToolboxApps\EchoTool\
├── EchoTool.exe
├── EchoTool.dll
├── README.md       <-- the file you're reading; picked up by the scanner
└── ...
```

Within `AppScanIntervalSeconds` (default 60s), the Polling Service registers EchoTool in the API and it appears in the UI.

## Application Settings

```json
{
  "name": "EchoTool",
  "description": "Sample console app that echoes its arguments. Used to verify the toolbox end-to-end.",
  "executable": "EchoTool.exe",
  "timeoutSeconds": 60,
  "parameters": [
    {
      "name": "--message",
      "label": "Message",
      "type": "string",
      "required": true,
      "default": "hello",
      "description": "Text that will be echoed back.",
      "order": 1
    },
    {
      "name": "--count",
      "label": "Repeat",
      "type": "number",
      "default": "1",
      "description": "How many times to repeat the message.",
      "order": 2
    },
    {
      "name": "--verbose",
      "label": "Verbose",
      "type": "flag",
      "description": "Include extra diagnostic output.",
      "order": 3
    }
  ]
}
```

## Notes

- If an application with the same `name` is already registered in the API, the scanner skips it. To change something on an already-registered app, edit it in the UI.
- The scanner only registers tools whose declared `executable` actually exists on disk in the same directory — broken manifests log a warning and are skipped.
- Parameter `type` is one of: `string`, `number`, `boolean`, `flag`, `secret` (case-insensitive).
