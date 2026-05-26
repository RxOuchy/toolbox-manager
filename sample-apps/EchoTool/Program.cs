// EchoTool — a trivial console app for verifying the toolbox end-to-end.
//
// Args:
//   --message <text>   Required. The text to echo.
//   --count   <n>      Optional, default 1. Repeat count.
//   --verbose          Optional flag. Emit diagnostics to stderr.
//
// Exit codes:
//   0   success
//   1   missing required argument or bad number
//   2   simulated failure (set --message to "FAIL")

using System.Globalization;

var parsed = ParseArgs(args);

if (parsed.Verbose)
{
    Console.Error.WriteLine($"[EchoTool] PID={Environment.ProcessId} User={Environment.UserName} OS={Environment.OSVersion}");
    Console.Error.WriteLine($"[EchoTool] CWD={Environment.CurrentDirectory}");
    Console.Error.WriteLine($"[EchoTool] Args={string.Join(' ', args)}");
}

if (parsed.Message is null)
{
    Console.Error.WriteLine("error: --message is required");
    return 1;
}

if (string.Equals(parsed.Message, "FAIL", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Simulated failure (message was 'FAIL').");
    return 2;
}

for (var i = 0; i < parsed.Count; i++)
{
    Console.WriteLine($"[{i + 1}/{parsed.Count}] {parsed.Message}");
}

if (parsed.Verbose)
    Console.Error.WriteLine("[EchoTool] done.");

return 0;

static ParsedArgs ParseArgs(string[] args)
{
    string? message = null;
    var count   = 1;
    var verbose = false;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--message" when i + 1 < args.Length:
                message = args[++i];
                break;
            case "--count" when i + 1 < args.Length:
                if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1)
                {
                    Console.Error.WriteLine($"error: --count must be a positive integer, got '{args[i]}'");
                    Environment.Exit(1);
                }
                break;
            case "--verbose":
                verbose = true;
                break;
            default:
                Console.Error.WriteLine($"warning: ignoring unknown arg '{args[i]}'");
                break;
        }
    }

    return new ParsedArgs(message, count, verbose);
}

internal readonly record struct ParsedArgs(string? Message, int Count, bool Verbose);
