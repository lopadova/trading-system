using Microsoft.Extensions.Configuration;

namespace SharedKernel.Configuration;

/// <summary>
/// <see cref="IConfigurationSource"/> that wraps the already-assembled
/// configuration and decrypts any <c>DPAPI:</c>-prefixed values on read.
/// This is deliberately layered LAST (before env vars) so it observes the
/// final, merged value for each key before the service consumes it.
/// </summary>
public sealed class EncryptedConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Snapshot of the upstream configuration, captured at AddEncryptedProvider
    /// time. We do NOT re-read from it during lookups — by the time a consumer
    /// calls GetValue&lt;T&gt;, the base providers have already populated the
    /// merged store this snapshot iterates.
    /// </summary>
    public required IConfiguration UpstreamConfiguration { get; init; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new EncryptedConfigurationProvider(UpstreamConfiguration);
    }
}

/// <summary>
/// <see cref="IConfigurationProvider"/> that intercepts reads for keys whose
/// value starts with <c>DPAPI:</c>, decrypts via
/// <see cref="EncryptedConfigProvider.Unprotect"/>, and surfaces cleartext to
/// the rest of the configuration pipeline.
///
/// Non-DPAPI values are NOT shadowed — the upstream providers still own them,
/// so this provider is effectively read-through plus targeted decryption.
/// </summary>
public sealed class EncryptedConfigurationProvider : ConfigurationProvider
{
    // Captured once at construction. We intentionally pin this reference
    // rather than re-subscribing to change tokens: DPAPI-wrapped values are
    // long-lived (rotation is a manual, supervised procedure — see SECRETS.md),
    // so reloadOnChange semantics would just add risk.
    private readonly IConfiguration _upstream;

    public EncryptedConfigurationProvider(IConfiguration upstream)
    {
        _upstream = upstream ?? throw new ArgumentNullException(nameof(upstream));
    }

    /// <summary>
    /// Called by the configuration root at build time. We walk every key
    /// in the upstream configuration and, for values carrying the DPAPI
    /// marker, decrypt eagerly. Non-DPAPI keys are LEFT OUT of our own
    /// <see cref="ConfigurationProvider.Data"/> so the upstream provider
    /// keeps serving them (avoids double-bookkeeping + accidental shadowing).
    /// </summary>
    public override void Load()
    {
        // Walk the flat key space so we catch nested sections (Cloudflare:ApiKey,
        // Smtp:Password, etc.) without needing to know the shape up front.
        foreach (KeyValuePair<string, string?> kvp in _upstream.AsEnumerable())
        {
            string? raw = kvp.Value;

            // Only intercept DPAPI-wrapped values. Leaves the other ~95%
            // of keys untouched so the upstream provider keeps serving them.
            if (raw is null || !raw.StartsWith(EncryptedConfigProvider.DpapiPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            // ResolveConfigValue handles platform gating (throws clear error
            // on non-Windows) and base64 / scope-mismatch failures.
            string? decrypted = EncryptedConfigProvider.ResolveConfigValue(raw);

            // Store decrypted value under the same key. Because this provider
            // is registered LAST (before env vars), it shadows the upstream
            // cleartext-prefixed value for all downstream consumers.
            Data[kvp.Key] = decrypted;
        }
    }
}
