using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace SharedKernel.Configuration;

/// <summary>
/// Windows DPAPI helper that wraps sensitive configuration values so they
/// never live on disk in cleartext. Callers Protect a secret once (during
/// operator-driven provisioning), store the resulting <c>DPAPI:&lt;base64&gt;</c>
/// marker inside appsettings.Staging.json / appsettings.Production.json, and
/// the runtime Unprotects transparently via <see cref="EncryptedConfigurationProvider"/>.
///
/// Scope defaults to <see cref="DataProtectionScope.LocalMachine"/> because
/// the Windows Service account is typically different from the operator
/// account that wrote the value — a CurrentUser-scoped blob would be
/// undecryptable by the service.
/// </summary>
public static class EncryptedConfigProvider
{
    /// <summary>
    /// Prefix used inside appsettings.json values to signal a DPAPI-wrapped
    /// payload. Raw values without this prefix are returned unchanged by
    /// <see cref="ResolveConfigValue"/> so non-secret keys stay trivially editable.
    /// </summary>
    public const string DpapiPrefix = "DPAPI:";

    /// <summary>
    /// Encrypts <paramref name="cleartext"/> with Windows DPAPI and returns
    /// a base64-encoded string. DOES NOT prepend the <c>DPAPI:</c> marker —
    /// callers (typically the EncryptConfigValue CLI) are responsible for
    /// that so the stored format stays explicit.
    /// </summary>
    /// <param name="cleartext">Non-null secret to encrypt.</param>
    /// <param name="scope">DPAPI scope. LocalMachine (default) so any local
    /// process — including Windows Services running under LocalSystem /
    /// NetworkService — can decrypt.</param>
    /// <exception cref="PlatformNotSupportedException">Thrown on non-Windows OSes.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cleartext"/> is null.</exception>
    [SupportedOSPlatform("windows")]
    public static string Protect(
        string cleartext,
        DataProtectionScope scope = DataProtectionScope.LocalMachine)
    {
        ArgumentNullException.ThrowIfNull(cleartext);
        // DPAPI operates on byte arrays; UTF-8 is stable for any string.
        byte[] plainBytes = Encoding.UTF8.GetBytes(cleartext);
        byte[] encrypted = ProtectedData.Protect(plainBytes, optionalEntropy: null, scope: scope);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Decrypts a base64-encoded DPAPI blob back to cleartext. The input
    /// string MUST NOT include the <c>DPAPI:</c> prefix — use
    /// <see cref="ResolveConfigValue"/> if you want prefix-aware handling.
    /// </summary>
    /// <param name="protectedBase64">Base64 DPAPI-encrypted payload.</param>
    /// <param name="scope">Must match the scope used during Protect.</param>
    /// <exception cref="PlatformNotSupportedException">Thrown on non-Windows OSes.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the blob is malformed or was encrypted under a different scope /
    /// different machine. Wraps the underlying <see cref="CryptographicException"/>
    /// with a clear operator-facing message (see SECRETS.md recovery section).
    /// </exception>
    [SupportedOSPlatform("windows")]
    public static string Unprotect(
        string protectedBase64,
        DataProtectionScope scope = DataProtectionScope.LocalMachine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedBase64);

        // Try to parse the base64 payload — CryptographicException from
        // DPAPI is opaque, so we catch and re-throw with a friendlier message.
        byte[] encryptedBytes;
        try
        {
            encryptedBytes = Convert.FromBase64String(protectedBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Failed to decode DPAPI payload: value is not valid base64. " +
                "Re-generate the encrypted value via the EncryptConfigValue CLI. " +
                "See docs/ops/SECRETS.md § Recovery.",
                ex);
        }

        try
        {
            byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, optionalEntropy: null, scope: scope);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            // Most common causes: (a) blob written under CurrentUser scope
            // but Unprotect called under LocalMachine (or vice versa);
            // (b) blob written on a different machine (LocalMachine keys
            // are tied to the physical Windows install).
            throw new InvalidOperationException(
                "Failed to decrypt DPAPI payload. Likely causes: blob was " +
                "created under a different DataProtectionScope, on a different " +
                "machine, or has been corrupted. See docs/ops/SECRETS.md § Recovery.",
                ex);
        }
    }

    /// <summary>
    /// Prefix-aware resolver used by <see cref="EncryptedConfigurationProvider"/>.
    /// Values starting with <c>DPAPI:</c> are decrypted; everything else is
    /// returned unchanged (including null / empty). This keeps non-secret keys
    /// trivially editable and allows mixing wrapped + cleartext entries in the
    /// same appsettings file.
    /// </summary>
    public static string? ResolveConfigValue(string? rawValueFromJson)
    {
        if (string.IsNullOrEmpty(rawValueFromJson))
        {
            // Passthrough: null stays null, "" stays "".
            return rawValueFromJson;
        }

        if (!rawValueFromJson.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            // Cleartext value — nothing to decrypt.
            return rawValueFromJson;
        }

        // Strip the prefix before handing off to Unprotect.
        string payload = rawValueFromJson.Substring(DpapiPrefix.Length);

        if (!OperatingSystem.IsWindows())
        {
            // DPAPI is Windows-only. On non-Windows (e.g. Linux CI), we
            // fail loudly rather than returning the encrypted blob as if
            // it were cleartext — that would almost certainly lead to
            // bad auth attempts against upstream services.
            throw new PlatformNotSupportedException(
                "DPAPI-wrapped configuration values can only be decrypted on Windows. " +
                "For non-Windows hosts, provide cleartext values via environment variables " +
                "or a different secret provider (e.g. appsettings.Local.json, user secrets).");
        }

        return Unprotect(payload);
    }
}
