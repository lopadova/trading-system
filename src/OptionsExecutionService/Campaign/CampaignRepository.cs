namespace OptionsExecutionService.Campaign;

using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using SharedKernel.Domain;

/// <summary>
/// SQLite-based repository for campaign persistence.
/// Uses strategy_state table to store campaign metadata and state.
/// </summary>
public sealed class CampaignRepository : ICampaignRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<CampaignRepository> _logger;

    public CampaignRepository(IDbConnectionFactory db, ILogger<CampaignRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SaveCampaignAsync(Campaign campaign, CancellationToken ct = default)
    {
        if (campaign == null)
        {
            throw new ArgumentNullException(nameof(campaign));
        }

        // Serialize campaign metadata to JSON for storage in strategy_state table
        CampaignMetadata metadata = new()
        {
            CampaignId = campaign.CampaignId,
            StrategyName = campaign.Strategy.StrategyName,
            State = campaign.State.ToString(),
            CreatedAt = campaign.CreatedAt.ToString("O"),
            ActivatedAt = campaign.ActivatedAt?.ToString("O"),
            ClosedAt = campaign.ClosedAt?.ToString("O"),
            CloseReason = campaign.CloseReason,
            RealizedPnL = campaign.RealizedPnL,
            StrategyDefinitionJson = JsonSerializer.Serialize(campaign.Strategy),
            StateJson = campaign.StateJson
        };

        string metadataJson = JsonSerializer.Serialize(metadata);

        const string sql = """
            INSERT INTO strategy_state (campaign_id, strategy_name, state_json, updated_at)
            VALUES (@CampaignId, @StrategyName, @StateJson, @UpdatedAt)
            ON CONFLICT(campaign_id) DO UPDATE SET
                strategy_name = excluded.strategy_name,
                state_json = excluded.state_json,
                updated_at = excluded.updated_at
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new
            {
                CampaignId = campaign.CampaignId,
                StrategyName = campaign.Strategy.StrategyName,
                StateJson = metadataJson,
                UpdatedAt = DateTime.UtcNow.ToString("O")
            }, cancellationToken: ct);

            await conn.ExecuteAsync(cmd);
            _logger.LogDebug("Saved campaign {CampaignId} (state={State})",
                campaign.CampaignId, campaign.State);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save campaign {CampaignId}", campaign.CampaignId);
            throw;
        }
    }

    public async Task<Campaign?> GetCampaignAsync(string campaignId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            throw new ArgumentException("Campaign ID cannot be null or empty", nameof(campaignId));
        }

        const string sql = """
            SELECT state_json
            FROM strategy_state
            WHERE campaign_id = @CampaignId
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { CampaignId = campaignId }, cancellationToken: ct);

            string? stateJson = await conn.QuerySingleOrDefaultAsync<string?>(cmd);

            if (stateJson == null)
            {
                return null;
            }

            CampaignMetadata? metadata = JsonSerializer.Deserialize<CampaignMetadata>(stateJson);

            if (metadata == null)
            {
                _logger.LogWarning("Failed to deserialize campaign metadata for {CampaignId}", campaignId);
                return null;
            }

            return HydrateCampaign(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get campaign {CampaignId}", campaignId);
            throw;
        }
    }

    public async Task<IReadOnlyList<Campaign>> GetCampaignsByStateAsync(CampaignState state, CancellationToken ct = default)
    {
        const string sql = """
            SELECT state_json
            FROM strategy_state
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, cancellationToken: ct);

            IEnumerable<string> rows = await conn.QueryAsync<string>(cmd);

            List<Campaign> campaigns = new();

            foreach (string stateJson in rows)
            {
                CampaignMetadata? metadata = JsonSerializer.Deserialize<CampaignMetadata>(stateJson);

                if (metadata == null)
                {
                    _logger.LogWarning("Skipping campaign with invalid metadata JSON");
                    continue;
                }

                if (metadata.State != state.ToString())
                {
                    continue;
                }

                Campaign campaign = HydrateCampaign(metadata);
                campaigns.Add(campaign);
            }

            return campaigns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get campaigns by state {State}", state);
            throw;
        }
    }

    public async Task<IReadOnlyList<Campaign>> GetCampaignsByStrategyAsync(string strategyName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(strategyName))
        {
            throw new ArgumentException("Strategy name cannot be null or empty", nameof(strategyName));
        }

        const string sql = """
            SELECT state_json
            FROM strategy_state
            WHERE strategy_name = @StrategyName
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { StrategyName = strategyName }, cancellationToken: ct);

            IEnumerable<string> rows = await conn.QueryAsync<string>(cmd);

            List<Campaign> campaigns = new();

            foreach (string stateJson in rows)
            {
                CampaignMetadata? metadata = JsonSerializer.Deserialize<CampaignMetadata>(stateJson);

                if (metadata == null)
                {
                    _logger.LogWarning("Skipping campaign with invalid metadata JSON");
                    continue;
                }

                Campaign campaign = HydrateCampaign(metadata);
                campaigns.Add(campaign);
            }

            return campaigns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get campaigns by strategy {StrategyName}", strategyName);
            throw;
        }
    }

    public async Task DeleteCampaignAsync(string campaignId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            throw new ArgumentException("Campaign ID cannot be null or empty", nameof(campaignId));
        }

        const string sql = """
            DELETE FROM strategy_state
            WHERE campaign_id = @CampaignId
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { CampaignId = campaignId }, cancellationToken: ct);

            await conn.ExecuteAsync(cmd);
            _logger.LogDebug("Deleted campaign {CampaignId}", campaignId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete campaign {CampaignId}", campaignId);
            throw;
        }
    }

    private Campaign HydrateCampaign(CampaignMetadata metadata)
    {
        // Deserialize strategy definition from JSON
        StrategyDefinition? strategy = JsonSerializer.Deserialize<StrategyDefinition>(metadata.StrategyDefinitionJson);

        if (strategy == null)
        {
            throw new InvalidOperationException($"Failed to deserialize strategy definition for campaign {metadata.CampaignId}");
        }

        // Parse campaign state
        if (!Enum.TryParse<CampaignState>(metadata.State, out CampaignState state))
        {
            throw new InvalidOperationException($"Invalid campaign state: {metadata.State}");
        }

        return new Campaign
        {
            CampaignId = metadata.CampaignId,
            Strategy = strategy,
            State = state,
            CreatedAt = DateTime.Parse(metadata.CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            ActivatedAt = metadata.ActivatedAt != null ? DateTime.Parse(metadata.ActivatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind) : null,
            ClosedAt = metadata.ClosedAt != null ? DateTime.Parse(metadata.ClosedAt, null, System.Globalization.DateTimeStyles.RoundtripKind) : null,
            CloseReason = metadata.CloseReason,
            RealizedPnL = metadata.RealizedPnL,
            StateJson = metadata.StateJson
        };
    }

    /// <summary>
    /// Internal DTO for serializing campaign data to strategy_state.state_json.
    /// </summary>
    private sealed class CampaignMetadata
    {
        public required string CampaignId { get; init; }
        public required string StrategyName { get; init; }
        public required string State { get; init; }
        public required string CreatedAt { get; init; }
        public string? ActivatedAt { get; init; }
        public string? ClosedAt { get; init; }
        public string? CloseReason { get; init; }
        public decimal? RealizedPnL { get; init; }
        public required string StrategyDefinitionJson { get; init; }
        public string? StateJson { get; init; }
    }
}
