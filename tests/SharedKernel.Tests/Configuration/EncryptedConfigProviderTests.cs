using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using SharedKernel.Configuration;
using Xunit;

namespace SharedKernel.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="EncryptedConfigProvider"/> + the custom
/// <see cref="EncryptedConfigurationProvider"/> wiring.
///
/// All DPAPI-backed tests use <see cref="SkippableFactAttribute"/> so they
/// are skipped (not failed) on non-Windows hosts such as Linux CI — DPAPI
/// is a Windows-only API.
///
/// The class is marked <see cref="SupportedOSPlatformAttribute"/>("windows")
/// so the analyzer accepts the Protect/Unprotect calls. At runtime, Skip.IfNot
/// still bails out on non-Windows — this attribute is just to quiet CA1416
/// under TreatWarningsAsErrors.
/// </summary>
[SupportedOSPlatform("windows")]
public class EncryptedConfigProviderTests
{
    private const string SkipMessage = "DPAPI not supported on this OS.";

    // ------------------------------------------------------------
    // Protect / Unprotect round-trip
    // ------------------------------------------------------------

    [SkippableFact]
    public void Protect_Then_Unprotect_Returns_Original_Cleartext()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), SkipMessage);

        const string original = "super-secret-bot-token-12345";

        // Arrange / Act
        string encrypted = EncryptedConfigProvider.Protect(original);
        string roundTripped = EncryptedConfigProvider.Unprotect(encrypted);

        // Assert
        Assert.Equal(original, roundTripped);
        // Encrypted form must differ (sanity check) and be valid base64.
        Assert.NotEqual(original, encrypted);
        Assert.NotEmpty(Convert.FromBase64String(encrypted));
    }

    [SkippableFact]
    public void Protect_Null_Throws_ArgumentNullException()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), SkipMessage);

        Assert.Throws<ArgumentNullException>(() => EncryptedConfigProvider.Protect(null!));
    }

    [SkippableFact]
    public void Protect_EmptyString_RoundTrips()
    {
        // Empty string is a legitimate (if unusual) secret value. Round-trip must preserve it.
        Skip.IfNot(OperatingSystem.IsWindows(), SkipMessage);

        string encrypted = EncryptedConfigProvider.Protect(string.Empty);
        string decrypted = EncryptedConfigProvider.Unprotect(encrypted);

        Assert.Equal(string.Empty, decrypted);
    }

    // ------------------------------------------------------------
    // ResolveConfigValue — prefix-aware behavior
    // ------------------------------------------------------------

    [Fact]
    public void ResolveConfigValue_Null_Returns_Null()
    {
        // Passthrough for null — covered on every OS (no DPAPI call).
        Assert.Null(EncryptedConfigProvider.ResolveConfigValue(null));
    }

    [Fact]
    public void ResolveConfigValue_Empty_Returns_Empty()
    {
        Assert.Equal(string.Empty, EncryptedConfigProvider.ResolveConfigValue(string.Empty));
    }

    [Fact]
    public void ResolveConfigValue_PlainText_Passthrough()
    {
        // No DPAPI: prefix → return unchanged. Works on any OS.
        const string raw = "http://localhost:8787";
        Assert.Equal(raw, EncryptedConfigProvider.ResolveConfigValue(raw));
    }

    [SkippableFact]
    public void ResolveConfigValue_DpapiPrefix_Decrypts()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), SkipMessage);

        const string original = "Bearer abc.def.ghi";
        string wrapped = EncryptedConfigProvider.DpapiPrefix + EncryptedConfigProvider.Protect(original);

        string? resolved = EncryptedConfigProvider.ResolveConfigValue(wrapped);

        Assert.Equal(original, resolved);
    }

    // ------------------------------------------------------------
    // Error handling — corrupt payloads
    // ------------------------------------------------------------

    [SkippableFact]
    public void Unprotect_InvalidBase64_Throws_WithClearMessage()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), SkipMessage);

        // "!!!" is not valid base64 → FormatException inside Unprotect,
        // which we re-wrap as InvalidOperationException with a pointer to SECRETS.md.
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => EncryptedConfigProvider.Unprotect("!!!not-base64!!!"));

        Assert.Contains("not valid base64", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SECRETS.md", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public void Unprotect_ValidBase64ButCorruptBlob_Throws_WithClearMessage()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), SkipMessage);

        // Valid base64, but not a DPAPI blob → CryptographicException inside
        // ProtectedData.Unprotect, re-wrapped as InvalidOperationException.
        string validBase64ButNotDpapi = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => EncryptedConfigProvider.Unprotect(validBase64ButNotDpapi));

        Assert.Contains("decrypt", ex.Message, StringComparison.OrdinalIgnoreCase);
        // The root cause should be a CryptographicException.
        Assert.IsType<CryptographicException>(ex.InnerException);
    }

    // ------------------------------------------------------------
    // EncryptedConfigurationProvider — IConfiguration integration
    // ------------------------------------------------------------

    [SkippableFact]
    public void AddEncryptedProvider_Decrypts_Prefixed_Values_In_Configuration()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), SkipMessage);

        const string secret = "telegram-bot-token-xyz";
        string wrapped = EncryptedConfigProvider.DpapiPrefix + EncryptedConfigProvider.Protect(secret);

        Dictionary<string, string?> initialData = new()
        {
            // Cleartext key — must stay untouched.
            ["Cloudflare:WorkerUrl"] = "https://trading-bot.example.com",
            // Wrapped secret — must be decrypted by the provider.
            ["Telegram:BotToken"] = wrapped,
            // Cleartext sibling under the same section.
            ["Telegram:ChatId"] = "12345",
        };

        IConfigurationBuilder builder = new ConfigurationBuilder()
            .AddInMemoryCollection(initialData);
        builder.AddEncryptedProvider();

        IConfigurationRoot config = builder.Build();

        Assert.Equal("https://trading-bot.example.com", config["Cloudflare:WorkerUrl"]);
        Assert.Equal(secret, config["Telegram:BotToken"]);
        Assert.Equal("12345", config["Telegram:ChatId"]);
    }

    [Fact]
    public void AddEncryptedProvider_Leaves_Cleartext_Only_Config_Unchanged()
    {
        // Platform-independent: no DPAPI values → provider is a no-op.
        Dictionary<string, string?> initialData = new()
        {
            ["Foo:Bar"] = "baz",
            ["Foo:Qux"] = "quux",
        };

        IConfigurationBuilder builder = new ConfigurationBuilder()
            .AddInMemoryCollection(initialData);
        builder.AddEncryptedProvider();

        IConfigurationRoot config = builder.Build();

        Assert.Equal("baz", config["Foo:Bar"]);
        Assert.Equal("quux", config["Foo:Qux"]);
    }
}
