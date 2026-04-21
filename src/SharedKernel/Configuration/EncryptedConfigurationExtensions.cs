using Microsoft.Extensions.Configuration;

namespace SharedKernel.Configuration;

/// <summary>
/// Wiring helpers for the <see cref="EncryptedConfigurationProvider"/>.
/// Call <see cref="AddEncryptedProvider"/> as the LAST layer before env vars
/// so DPAPI decryption observes the already-merged JSON+env value.
/// </summary>
public static class EncryptedConfigurationExtensions
{
    /// <summary>
    /// Registers the DPAPI-decrypting provider as an additional source on the
    /// given <paramref name="builder"/>. The provider reads from a snapshot of
    /// the upstream configuration built from the same builder's sources so far.
    /// </summary>
    /// <remarks>
    /// Call this AFTER all JSON / user-secrets providers are registered, but
    /// BEFORE <c>AddEnvironmentVariables</c>. This ordering ensures:
    /// <list type="bullet">
    ///   <item>JSON values containing DPAPI: are decrypted transparently.</item>
    ///   <item>Env vars still win — an operator can force-override any key by
    ///         exporting it (useful for emergency secret rotation without
    ///         touching the DPAPI blob).</item>
    /// </list>
    /// </remarks>
    public static IConfigurationBuilder AddEncryptedProvider(this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Build ONE snapshot of the sources registered so far. Our provider
        // walks this snapshot in Load(). It deliberately does NOT include
        // sources registered AFTER AddEncryptedProvider (e.g. env vars) —
        // that ordering is what lets the outer builder still pick up
        // operator overrides at the root after we run.
        //
        // Note: this single .Build() call instantiates the upstream providers
        // a second time at the outer builder's final .Build() (host startup
        // rebuilds every source). The alternative — wrapping in a second
        // ConfigurationBuilder().AddConfiguration(builder.Build()) — doubles
        // the file watchers / reload tokens without buying anything, so we
        // keep the simple single-snapshot form. Rebuild cost is paid once at
        // startup and is negligible for JSON + env-var sources.
        IConfigurationRoot upstreamSnapshot = builder.Build();

        builder.Add(new EncryptedConfigurationSource
        {
            UpstreamConfiguration = upstreamSnapshot
        });

        return builder;
    }
}
