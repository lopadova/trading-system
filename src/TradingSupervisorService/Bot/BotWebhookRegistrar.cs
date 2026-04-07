using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingSupervisorService.Configuration;

namespace TradingSupervisorService.Bot;

/// <summary>
/// Registers bot webhook URL with Telegram or Discord on service startup
/// </summary>
public class BotWebhookRegistrar : IHostedService
{
    private readonly ILogger<BotWebhookRegistrar> _logger;
    private readonly BotOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public BotWebhookRegistrar(
        ILogger<BotWebhookRegistrar> logger,
        IOptions<BotOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Early return if bot is disabled
        if (_options.ActiveBot == "none")
        {
            _logger.LogInformation("[BOT] Bot disabled (ActiveBot = none)");
            return;
        }

        // Validate configuration
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogWarning("[BOT] Webhook URL not configured — skipping bot webhook registration");
            return;
        }

        // Register Telegram webhook
        if (_options.ActiveBot == "telegram")
        {
            await RegisterTelegramWebhookAsync(cancellationToken);
            return;
        }

        // Discord webhooks are registered manually via Discord Developer Portal
        if (_options.ActiveBot == "discord")
        {
            _logger.LogInformation("[BOT] Discord bot configured — slash commands must be registered manually via Discord Developer Portal");
            _logger.LogInformation("[BOT] Webhook URL: {WebhookUrl}", _options.WebhookUrl);
            return;
        }

        // Unknown bot type
        _logger.LogWarning("[BOT] Unknown ActiveBot value: {ActiveBot}", _options.ActiveBot);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No cleanup needed
        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers Telegram webhook URL
    /// </summary>
    private async Task RegisterTelegramWebhookAsync(CancellationToken cancellationToken)
    {
        // Validate Telegram bot token
        if (string.IsNullOrWhiteSpace(_options.TelegramBotToken))
        {
            _logger.LogError("[BOT] Telegram bot token not configured — cannot register webhook");
            return;
        }

        try
        {
            // Generate secret token from bot token hash (same algorithm as in auth.ts)
            var secretToken = GenerateSecretToken(_options.TelegramBotToken);

            // Construct setWebhook URL
            var webhookUrl = _options.WebhookUrl.TrimEnd('/') + "/webhook/telegram";
            var setWebhookUrl = $"https://api.telegram.org/bot{_options.TelegramBotToken}/setWebhook" +
                                $"?url={Uri.EscapeDataString(webhookUrl)}" +
                                $"&secret_token={Uri.EscapeDataString(secretToken)}";

            _logger.LogInformation("[BOT] Registering Telegram webhook: {WebhookUrl}", webhookUrl);

            // Send HTTP request
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.GetAsync(setWebhookUrl, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // Check response
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[BOT] Failed to register Telegram webhook: {StatusCode} {Body}",
                    response.StatusCode, responseBody);
                return;
            }

            _logger.LogInformation("[BOT] Telegram webhook registered successfully: {Response}", responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BOT] Exception while registering Telegram webhook");
        }
    }

    /// <summary>
    /// Generate secret token from bot token (SHA-256 hash, first 32 chars)
    /// Matches the algorithm in infra/cloudflare/worker/src/bot/auth.ts
    /// </summary>
    private static string GenerateSecretToken(string botToken)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(botToken));
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hashHex.Substring(0, 32);
    }
}
