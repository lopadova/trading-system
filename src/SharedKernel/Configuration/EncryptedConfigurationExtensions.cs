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

        // Build an intermediate IConfiguration from the sources registered so
        // far. This snapshot is what our provider walks in Load(). It does NOT
        // include sources registered AFTER AddEncryptedProvider — which is
        // exactly the point: env vars added later still win at the root.
        IConfigurationRoot upstreamSnapshot = new ConfigurationBuilder()
            .AddConfiguration(builder.Build())
            .Build();

        builder.Add(new EncryptedConfigurationSource
        {
            UpstreamConfiguration = upstreamSnapshot
        });

        return builder;
    }
}
