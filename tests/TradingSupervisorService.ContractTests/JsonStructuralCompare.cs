using System.Text.Json;

namespace TradingSupervisorService.ContractTests;

/// <summary>
/// Order-independent structural JSON comparison. Two documents are equivalent
/// when:
/// <list type="bullet">
///   <item><description>They have the same TYPE (object, array, string, number, bool, null).</description></item>
///   <item><description>Objects have the same set of keys, with recursively equivalent values.
///   Key order is irrelevant.</description></item>
///   <item><description>Arrays have the same length and positionally equivalent elements.
///   (Array order IS relevant — reordering an array is a contract change.)</description></item>
///   <item><description>Scalars compare equal after JSON parsing (so <c>1</c>, <c>1.0</c>, and
///   <c>1e0</c> are equivalent; whitespace outside strings is ignored).</description></item>
/// </list>
///
/// This is NOT a string compare — two equivalent documents serialized by
/// different JSON writers will still compare equal. This matters because
/// <see cref="JsonSerializer"/> produces numbers as <c>5410.25</c> while a
/// hand-edited fixture might have <c>5410.25</c> without trailing precision;
/// we want both to match.
///
/// Nullable-field handling: <c>null</c> and "missing" are treated as
/// DIFFERENT. If a fixture has <c>"close_normalized": null</c> but the
/// producer emits no such key, this comparator fails — and correctly so,
/// because a downstream Zod schema sees them differently.
/// </summary>
internal static class JsonStructuralCompare
{
    /// <summary>
    /// Returns a list of human-readable differences. Empty list means
    /// equivalent. Path format is JSON Pointer (RFC 6901) without the
    /// leading slash: e.g. <c>"account_value"</c> or <c>"indicators/2/status"</c>.
    /// </summary>
    public static List<string> Diff(string expectedJson, string actualJson)
    {
        using JsonDocument expected = JsonDocument.Parse(expectedJson);
        using JsonDocument actual = JsonDocument.Parse(actualJson);

        List<string> differences = new();
        CompareElements(expected.RootElement, actual.RootElement, "", differences);
        return differences;
    }

    private static void CompareElements(
        JsonElement expected,
        JsonElement actual,
        string path,
        List<string> diffs)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            diffs.Add($"{PathOrRoot(path)}: type mismatch (expected {expected.ValueKind}, actual {actual.ValueKind})");
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                CompareObjects(expected, actual, path, diffs);
                return;

            case JsonValueKind.Array:
                CompareArrays(expected, actual, path, diffs);
                return;

            case JsonValueKind.String:
                // Comparing as .NET strings — exact value equality.
                string? expectedStr = expected.GetString();
                string? actualStr = actual.GetString();
                if (expectedStr != actualStr)
                {
                    diffs.Add($"{PathOrRoot(path)}: string mismatch (expected \"{expectedStr}\", actual \"{actualStr}\")");
                }
                return;

            case JsonValueKind.Number:
                // Compare as decimal for exactness where possible; fall back
                // to double. Using decimal preserves 5410.25 exactly but
                // rejects values outside decimal range — fine for our domain.
                if (!expected.TryGetDecimal(out decimal expectedDec) ||
                    !actual.TryGetDecimal(out decimal actualDec))
                {
                    // Try double as fallback
                    double expectedDbl = expected.GetDouble();
                    double actualDbl = actual.GetDouble();
                    if (Math.Abs(expectedDbl - actualDbl) > 1e-9)
                    {
                        diffs.Add($"{PathOrRoot(path)}: number mismatch (expected {expectedDbl}, actual {actualDbl})");
                    }
                    return;
                }
                if (expectedDec != actualDec)
                {
                    diffs.Add($"{PathOrRoot(path)}: number mismatch (expected {expectedDec}, actual {actualDec})");
                }
                return;

            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                // ValueKind equality above was sufficient — they're primitive.
                return;

            default:
                diffs.Add($"{PathOrRoot(path)}: unsupported JSON value kind {expected.ValueKind}");
                return;
        }
    }

    private static void CompareObjects(
        JsonElement expected,
        JsonElement actual,
        string path,
        List<string> diffs)
    {
        HashSet<string> expectedKeys = new();
        HashSet<string> actualKeys = new();

        foreach (JsonProperty prop in expected.EnumerateObject())
        {
            expectedKeys.Add(prop.Name);
        }
        foreach (JsonProperty prop in actual.EnumerateObject())
        {
            actualKeys.Add(prop.Name);
        }

        foreach (string missing in expectedKeys.Except(actualKeys))
        {
            diffs.Add($"{PathOrRoot(path)}: missing key \"{missing}\"");
        }
        foreach (string extra in actualKeys.Except(expectedKeys))
        {
            diffs.Add($"{PathOrRoot(path)}: unexpected extra key \"{extra}\"");
        }

        foreach (string sharedKey in expectedKeys.Intersect(actualKeys))
        {
            JsonElement expectedChild = expected.GetProperty(sharedKey);
            JsonElement actualChild = actual.GetProperty(sharedKey);
            CompareElements(expectedChild, actualChild, Join(path, sharedKey), diffs);
        }
    }

    private static void CompareArrays(
        JsonElement expected,
        JsonElement actual,
        string path,
        List<string> diffs)
    {
        int expectedLen = expected.GetArrayLength();
        int actualLen = actual.GetArrayLength();
        if (expectedLen != actualLen)
        {
            diffs.Add($"{PathOrRoot(path)}: array length mismatch (expected {expectedLen}, actual {actualLen})");
            return;
        }
        for (int i = 0; i < expectedLen; i++)
        {
            CompareElements(expected[i], actual[i], Join(path, i.ToString()), diffs);
        }
    }

    private static string Join(string basePath, string segment) =>
        string.IsNullOrEmpty(basePath) ? segment : $"{basePath}/{segment}";

    private static string PathOrRoot(string path) => string.IsNullOrEmpty(path) ? "<root>" : path;
}
