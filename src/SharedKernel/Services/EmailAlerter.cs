using System.Globalization;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
using SharedKernel.Observability;

namespace SharedKernel.Services;

/// <summary>
/// SMTP configuration for <see cref="EmailAlerter"/>. Pulled from <c>Smtp:*</c> config keys.
/// Gmail-friendly defaults (port 587, StartTLS).
/// </summary>
public sealed record EmailAlerterConfig
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public bool EnableSsl { get; init; } = true;

    /// <summary>Validates the config; returns a human-readable reason or null if OK.</summary>
    public string? Validate()
    {
        if (!Enabled)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(Host))
        {
            return "Smtp:Host is required when Smtp:Enabled=true";
        }
        if (Port <= 0 || Port > 65535)
        {
            return "Smtp:Port must be in 1..65535";
        }
        if (string.IsNullOrWhiteSpace(From))
        {
            return "Smtp:From is required when Smtp:Enabled=true";
        }
        if (string.IsNullOrWhiteSpace(To))
        {
            return "Smtp:To is required when Smtp:Enabled=true";
        }
        return null;
    }
}

/// <summary>
/// Testable wrapper around <see cref="SmtpClient"/>. Production binds to <see cref="SystemSmtpClient"/>.
/// Tests use <see cref="FakeSmtpClient"/> (or a Moq) to assert calls without opening network sockets.
/// </summary>
public interface ISmtpClient
{
    Task SendMailAsync(MailMessage message, CancellationToken ct);
}

/// <summary>
/// Thin real-world wrapper around <see cref="SmtpClient"/>. Disposes the client after each send
/// because <see cref="SmtpClient"/> is not thread-safe for concurrent SendAsync calls.
/// Acceptable here: Critical alerts are rare (minutes/hours between).
/// </summary>
public sealed class SystemSmtpClient : ISmtpClient
{
    private readonly EmailAlerterConfig _config;

    public SystemSmtpClient(EmailAlerterConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task SendMailAsync(MailMessage message, CancellationToken ct)
    {
        using SmtpClient client = new(_config.Host, _config.Port)
        {
            EnableSsl = _config.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(_config.Username))
        {
            client.Credentials = new NetworkCredential(_config.Username, _config.Password);
        }
        // CancellationToken: SmtpClient doesn't accept one on SendMailAsync(MailMessage) directly,
        // but we can race the send against the token via Task.WhenAny. Simpler: short SMTP timeout (30s).
        client.Timeout = 30_000;
        await client.SendMailAsync(message).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
    }
}

/// <summary>
/// Email alerter — last-resort channel. ONLY sends on <see cref="AlertSeverity.Critical"/>.
/// Everything else is a no-op (or a log line).
/// <para>
/// Why so restrictive: email is noisy, slow, and easy to ignore. We reserve it for
/// "page Lorenzo at 3am" class events. Telegram handles Error/Warning/Digest.
/// </para>
/// </summary>
public sealed class EmailAlerter : IAlerter
{
    private readonly EmailAlerterConfig _config;
    private readonly ISmtpClient _smtp;
    private readonly ILogger<EmailAlerter> _logger;

    public EmailAlerter(IConfiguration configuration, ISmtpClient smtp, ILogger<EmailAlerter> logger)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        _smtp = smtp ?? throw new ArgumentNullException(nameof(smtp));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _config = new EmailAlerterConfig
        {
            Enabled = configuration.GetValue<bool>("Smtp:Enabled", false),
            Host = configuration.GetValue<string>("Smtp:Host") ?? string.Empty,
            Port = configuration.GetValue<int>("Smtp:Port", 587),
            Username = configuration.GetValue<string>("Smtp:Username") ?? string.Empty,
            Password = configuration.GetValue<string>("Smtp:Password") ?? string.Empty,
            From = configuration.GetValue<string>("Smtp:From") ?? string.Empty,
            To = configuration.GetValue<string>("Smtp:To") ?? string.Empty,
            EnableSsl = configuration.GetValue<bool>("Smtp:EnableSsl", true)
        };

        string? err = _config.Validate();
        if (err != null)
        {
            _logger.LogWarning("EmailAlerter disabled: {Reason}", err);
            _config = _config with { Enabled = false };
        }
    }

    /// <summary>
    /// Constructor overload for tests — skips IConfiguration binding.
    /// </summary>
    public EmailAlerter(EmailAlerterConfig config, ISmtpClient smtp, ILogger<EmailAlerter> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _smtp = smtp ?? throw new ArgumentNullException(nameof(smtp));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        string? err = _config.Validate();
        if (err != null)
        {
            _logger.LogWarning("EmailAlerter disabled: {Reason}", err);
            _config = _config with { Enabled = false };
        }
    }

    /// <summary>
    /// Sends the alert via SMTP only when severity == Critical. Other severities are no-ops
    /// (we log them at Debug so we can audit later but avoid channel noise).
    /// </summary>
    public async Task SendImmediateAsync(AlertSeverity severity, string title, string message, CancellationToken ct)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("EmailAlerter disabled — skipping severity={Severity} title={Title}", severity, title);
            return;
        }

        if (severity != AlertSeverity.Critical)
        {
            _logger.LogDebug("EmailAlerter only ships Critical — skipping severity={Severity} title={Title}",
                severity, title);
            return;
        }

        await SendMailInternalAsync(title, message, count: 1, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a digest only when the severity is Critical — consistent with the no-noise policy.
    /// </summary>
    public async Task SendDigestAsync(AlertSeverity severity, IReadOnlyList<(string title, string message)> entries, CancellationToken ct)
    {
        if (!_config.Enabled)
        {
            return;
        }
        if (severity != AlertSeverity.Critical)
        {
            _logger.LogDebug("EmailAlerter only ships Critical digests — skipping severity={Severity} count={Count}",
                severity, entries?.Count ?? 0);
            return;
        }
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        // Build a single email whose body lists all entries. `SendMailInternalAsync`
        // centralizes the "[Trading-System]" prefix, so pass the raw subject here
        // (prefixing twice would yield "[Trading-System] [Trading-System] …").
        string subject = string.Format(CultureInfo.InvariantCulture, "Critical digest ({0} items)", entries.Count);
        System.Text.StringBuilder body = new();
        body.AppendLine(string.Format(CultureInfo.InvariantCulture, "Critical alert digest — {0} items", entries.Count));
        body.AppendLine();
        foreach ((string t, string m) in entries)
        {
            body.AppendLine(string.Format(CultureInfo.InvariantCulture, "• {0}: {1}", t, m));
        }
        await SendMailInternalAsync(subject, body.ToString(), entries.Count, ct).ConfigureAwait(false);
    }

    private async Task SendMailInternalAsync(string subject, string body, int count, CancellationToken ct)
    {
        try
        {
            using MailMessage msg = new(_config.From, _config.To)
            {
                Subject = string.Format(CultureInfo.InvariantCulture, "[Trading-System] {0}", subject),
                Body = body,
                IsBodyHtml = false
            };

            await _smtp.SendMailAsync(msg, ct).ConfigureAwait(false);
            _logger.LogInformation("EmailAlerter sent. subject={Subject} count={Count}", subject, count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailAlerter failed to send. subject={Subject}", subject);
            // Swallow — the caller is typically a fire-and-forget safety worker.
        }
    }
}
