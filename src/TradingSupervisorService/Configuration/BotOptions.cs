namespace TradingSupervisorService.Configuration;

/// <summary>
/// Bot configuration options
/// </summary>
public class BotOptions
{
    /// <summary>
    /// Active bot type: "telegram", "discord", or "none"
    /// </summary>
    public string ActiveBot { get; init; } = "none";

    /// <summary>
    /// Webhook URL (Cloudflare Worker base URL + /api/bot)
    /// Example: https://trading-system.your-account.workers.dev/api/bot
    /// </summary>
    public string WebhookUrl { get; init; } = string.Empty;

    /// <summary>
    /// Telegram bot token (from @BotFather)
    /// </summary>
    public string TelegramBotToken { get; init; } = string.Empty;

    /// <summary>
    /// Discord bot token
    /// </summary>
    public string DiscordBotToken { get; init; } = string.Empty;

    /// <summary>
    /// Discord application public key (for webhook signature verification)
    /// </summary>
    public string DiscordPublicKey { get; init; } = string.Empty;

    /// <summary>
    /// Comma-separated list of whitelisted user IDs
    /// </summary>
    public string Whitelist { get; init; } = string.Empty;
}
