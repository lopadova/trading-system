using System.Runtime.Versioning;
using System.Security.Cryptography;
using SharedKernel.Configuration;

// ---------------------------------------------------------------------------
// EncryptConfigValue CLI
// ---------------------------------------------------------------------------
// Small operator helper that reads a secret from stdin (or the first CLI arg),
// wraps it with Windows DPAPI via EncryptedConfigProvider, and prints the
// result to stdout in the format used by appsettings.Staging/Production.json:
//
//   DPAPI:<base64-encoded-blob>
//
// Typical use (PowerShell, running on the SAME machine that will host the
// Windows Service):
//
//   dotnet run --project src/Tools/EncryptConfigValue -- <<< "my-secret-value"
//   # or:
//   echo -n "my-secret-value" | dotnet run --project src/Tools/EncryptConfigValue
//
// Then paste the printed DPAPI:... string into the appropriate appsettings
// file. See docs/ops/SECRETS.md for the full rotation procedure.
// ---------------------------------------------------------------------------

[assembly: SupportedOSPlatform("windows")]

if (!OperatingSystem.IsWindows())
{
    // Defensive: the project file already marks this assembly Windows-only,
    // but a user could still try `dotnet run` on Linux. Fail loud and clear.
    Console.Error.WriteLine("ERROR: EncryptConfigValue is Windows-only (DPAPI).");
    return 2;
}

// Optional: --scope=CurrentUser to override the default LocalMachine scope.
// LocalMachine is the default because Windows Services often run under a
// different identity than the operator who wrapped the value — CurrentUser
// scope would make the blob undecryptable by the service at runtime.
DataProtectionScope scope = DataProtectionScope.LocalMachine;
foreach (string arg in args)
{
    if (string.Equals(arg, "--scope=CurrentUser", StringComparison.OrdinalIgnoreCase))
    {
        scope = DataProtectionScope.CurrentUser;
        Console.Error.WriteLine("WARN: using CurrentUser scope. Make sure the Windows Service account matches.");
    }
    else if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
    {
        PrintUsage();
        return 0;
    }
}

// Read the secret from stdin. We use ReadToEnd so multi-line values work, but
// trim the trailing newline the shell adds. If stdin is a terminal (no redirect),
// prompt interactively — operator convenience.
string? cleartext;
if (Console.IsInputRedirected)
{
    cleartext = Console.In.ReadToEnd();
    // Strip a single trailing \r\n / \n that shells typically append.
    cleartext = cleartext.TrimEnd('\n').TrimEnd('\r');
}
else
{
    Console.Error.Write("Enter secret (input hidden): ");
    cleartext = ReadPasswordFromConsole();
    Console.Error.WriteLine();
}

if (string.IsNullOrEmpty(cleartext))
{
    Console.Error.WriteLine("ERROR: empty secret received. Aborting.");
    return 1;
}

try
{
    string wrapped = EncryptedConfigProvider.Protect(cleartext, scope);
    // stdout ONLY carries the final marker — operators typically pipe this to clip.exe
    // or paste directly into an editor. Keep it clean.
    Console.WriteLine($"{EncryptedConfigProvider.DpapiPrefix}{wrapped}");
    return 0;
}
catch (CryptographicException ex)
{
    Console.Error.WriteLine($"ERROR: DPAPI Protect failed: {ex.Message}");
    return 1;
}

// -------------------- helpers --------------------

static void PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  echo -n '<secret>' | dotnet run --project src/Tools/EncryptConfigValue");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options:");
    Console.Error.WriteLine("  --scope=CurrentUser   Use CurrentUser DPAPI scope (default: LocalMachine)");
    Console.Error.WriteLine("  -h, --help            Show this help message");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Prints DPAPI:<base64> to stdout. Paste into appsettings.{Env}.json.");
    Console.Error.WriteLine("See docs/ops/SECRETS.md for the full rotation procedure.");
}

// Minimal interactive prompt that hides input — avoids leaking the secret
// to terminal scrollback when the operator forgets to pipe via echo.
static string ReadPasswordFromConsole()
{
    System.Text.StringBuilder sb = new();
    while (true)
    {
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
        {
            sb.Length -= 1;
            continue;
        }
        if (!char.IsControl(key.KeyChar))
        {
            sb.Append(key.KeyChar);
        }
    }
    return sb.ToString();
}
