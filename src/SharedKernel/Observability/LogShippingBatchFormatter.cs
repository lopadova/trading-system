using System.Globalization;
using System.IO;
using System.Text.Json;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Http;

namespace SharedKernel.Observability;

/// <summary>
/// Custom batch formatter for Serilog.Sinks.Http that produces the exact payload shape
/// consumed by the Cloudflare Worker's /api/v1/logs endpoint:
/// <code>
/// {
///   "batch": [
///     { "ts": "...", "level": "error", "service": "supervisor", "message": "...",
///       "properties": {...}, "source_context": "Namespace.Class",
///       "exception": { "type": "...", "message": "...", "stackTrace": "..." } }
///   ]
/// }
/// </code>
/// <para>
/// The default ArrayBatchFormatter emits a raw Serilog-shaped JSON array, which is NOT
/// what our Worker consumes. We render each LogEvent manually to keep control of naming.
/// </para>
/// </summary>
public sealed class LogShippingBatchFormatter : IBatchFormatter
{
    private readonly string _serviceName;

    /// <summary>
    /// Creates a batch formatter. The logical service name is embedded in every record
    /// so the Worker can group logs without parsing Serilog's Service enricher property.
    /// </summary>
    /// <param name="serviceName">Stable short name, e.g. "supervisor", "options-execution".</param>
    public LogShippingBatchFormatter(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("serviceName required", nameof(serviceName));
        }
        _serviceName = serviceName;
    }

    /// <summary>
    /// Non-throwing fallback for the textFormatter path of Serilog.Sinks.Http — unused by
    /// this formatter (we format everything in <see cref="Format(IEnumerable{string}, TextWriter)"/>)
    /// but exposed in case a downstream integration calls into it.
    /// </summary>
    public static ITextFormatter PassthroughTextFormatter { get; } = new NullTextFormatter();

    /// <summary>
    /// Serilog.Sinks.Http hands us pre-serialized log events (each is a Serilog JSON line
    /// produced by the textFormatter). We re-parse them, reshape, and wrap in a { "batch": [...] } envelope.
    /// </summary>
    public void Format(IEnumerable<string> logEvents, TextWriter output)
    {
        if (logEvents == null)
        {
            throw new ArgumentNullException(nameof(logEvents));
        }
        if (output == null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        // Use a pooled StringBuilder via MemoryStream → Utf8JsonWriter would be more efficient
        // but TextWriter output locks us into string-based rendering. Simplicity wins here.
        output.Write("{\"batch\":[");

        bool first = true;
        foreach (string logEventJson in logEvents)
        {
            if (string.IsNullOrWhiteSpace(logEventJson))
            {
                continue;
            }

            if (!first)
            {
                output.Write(',');
            }
            first = false;

            // Reshape the Serilog JSON line into our Worker schema.
            // We parse defensively — any malformed event is dropped, not allowed to crash the sink.
            string reshaped = ReshapeEvent(logEventJson, _serviceName);
            output.Write(reshaped);
        }

        output.Write("]}");
    }

    /// <summary>
    /// Parses a single Serilog JSON line (NormalRenderedTextFormatter shape) and re-emits
    /// it in the Worker's expected schema. On any error, returns "null" (valid JSON) so the
    /// batch envelope stays well-formed and the bad event is silently dropped.
    /// </summary>
    private static string ReshapeEvent(string logEventJson, string serviceName)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(logEventJson);
            JsonElement root = doc.RootElement;

            // NormalRenderedTextFormatter emits:
            // { "Timestamp":"...", "Level":"Information", "MessageTemplate":"...", "RenderedMessage":"...",
            //   "Exception":"stringified stack", "Properties": {...} }

            string ts = root.TryGetProperty("Timestamp", out JsonElement tsEl) && tsEl.ValueKind == JsonValueKind.String
                ? (tsEl.GetString() ?? DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                : DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            string level = root.TryGetProperty("Level", out JsonElement lvlEl) && lvlEl.ValueKind == JsonValueKind.String
                ? (lvlEl.GetString() ?? "information").ToLowerInvariant()
                : "information";

            string message = root.TryGetProperty("RenderedMessage", out JsonElement msgEl) && msgEl.ValueKind == JsonValueKind.String
                ? (msgEl.GetString() ?? string.Empty)
                : (root.TryGetProperty("MessageTemplate", out JsonElement tmplEl) && tmplEl.ValueKind == JsonValueKind.String
                    ? (tmplEl.GetString() ?? string.Empty)
                    : string.Empty);

            string? sourceContext = null;
            JsonElement propertiesElement = default;
            bool hasProperties = false;
            if (root.TryGetProperty("Properties", out JsonElement propsEl) && propsEl.ValueKind == JsonValueKind.Object)
            {
                propertiesElement = propsEl;
                hasProperties = true;
                if (propsEl.TryGetProperty("SourceContext", out JsonElement srcEl) && srcEl.ValueKind == JsonValueKind.String)
                {
                    sourceContext = srcEl.GetString();
                }
            }

            // Exception is rendered as a single string by NormalRenderedTextFormatter.
            // We split first line as "type: message" and the rest as the stack trace.
            string? excType = null;
            string? excMessage = null;
            string? excStack = null;
            if (root.TryGetProperty("Exception", out JsonElement excEl) && excEl.ValueKind == JsonValueKind.String)
            {
                string excStr = excEl.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(excStr))
                {
                    int newlineIdx = excStr.IndexOf('\n');
                    string firstLine = newlineIdx < 0 ? excStr : excStr[..newlineIdx].Trim();
                    string rest = newlineIdx < 0 ? string.Empty : excStr[(newlineIdx + 1)..];

                    int colonIdx = firstLine.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        excType = firstLine[..colonIdx].Trim();
                        excMessage = firstLine[(colonIdx + 1)..].Trim();
                    }
                    else
                    {
                        excType = firstLine;
                        excMessage = string.Empty;
                    }
                    excStack = rest;
                }
            }

            // Emit the reshaped record using Utf8JsonWriter for safe escaping.
            using MemoryStream ms = new();
            using (Utf8JsonWriter writer = new(ms, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteString("ts", ts);
                writer.WriteString("level", level);
                writer.WriteString("service", serviceName);
                writer.WriteString("message", message);

                if (hasProperties)
                {
                    writer.WritePropertyName("properties");
                    propertiesElement.WriteTo(writer);
                }
                else
                {
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                }

                if (!string.IsNullOrWhiteSpace(sourceContext))
                {
                    writer.WriteString("source_context", sourceContext);
                }

                if (excType != null)
                {
                    writer.WritePropertyName("exception");
                    writer.WriteStartObject();
                    writer.WriteString("type", excType);
                    writer.WriteString("message", excMessage ?? string.Empty);
                    writer.WriteString("stackTrace", excStack ?? string.Empty);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            // Any parse error → drop the event; return valid JSON so the batch stays well-formed.
            return "null";
        }
    }

    /// <summary>
    /// No-op text formatter used as a placeholder when the caller doesn't supply one.
    /// The actual rendering happens in <see cref="LogShippingBatchFormatter.Format(IEnumerable{string}, TextWriter)"/>.
    /// </summary>
    private sealed class NullTextFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            // Intentionally empty — NormalRenderedTextFormatter is the default in Serilog.Sinks.Http.
        }
    }
}
