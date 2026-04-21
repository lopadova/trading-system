using System.Reflection;

namespace TradingSupervisorService.ContractTests;

/// <summary>
/// Resolves fixture paths under <c>tests/Contract/fixtures/</c> regardless of
/// where the assembly is running from. The output directory in a typical
/// <c>bin/Release/net10.0</c> build lives 4 levels deep relative to the repo
/// root, but we don't assume the exact depth — we walk up until we find the
/// fixtures directory.
/// </summary>
internal static class FixtureLoader
{
    private const string FixturesRelative = "tests/Contract/fixtures";

    /// <summary>
    /// Returns the absolute path to the fixtures root, throwing if it
    /// cannot be found. Cached so repeated lookups don't hit the filesystem.
    /// </summary>
    private static readonly Lazy<string> s_fixturesRoot = new(ResolveFixturesRoot);

    public static string ReadOutboxEventFixture(string eventType)
    {
        string path = Path.Combine(s_fixturesRoot.Value, "outbox-events", $"{eventType}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Contract fixture not found for outbox event '{eventType}'. " +
                $"Expected at: {path}. See tests/Contract/README.md for the procedure.");
        }
        return File.ReadAllText(path);
    }

    public static string ReadWorkerResponseFixture(string name)
    {
        string path = Path.Combine(s_fixturesRoot.Value, "worker-responses", $"{name}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Contract fixture not found for worker response '{name}'. Expected at: {path}.");
        }
        return File.ReadAllText(path);
    }

    private static string ResolveFixturesRoot()
    {
        // Start from the assembly's containing directory and walk up.
        string? dir = Path.GetDirectoryName(
            typeof(FixtureLoader).Assembly.Location) ?? Directory.GetCurrentDirectory();

        // Max 10 levels up is more than enough for any sane bin layout.
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            string candidate = Path.Combine(dir, FixturesRelative);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException(
            $"Contract fixtures root not found. Walked up from {typeof(FixtureLoader).Assembly.Location} " +
            $"looking for '{FixturesRelative}'. Run tests from the repo (or a descendant) where this dir exists.");
    }
}
