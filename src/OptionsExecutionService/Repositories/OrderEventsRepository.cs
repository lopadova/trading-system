using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using SharedKernel.Domain;

namespace OptionsExecutionService.Repositories;

/// <summary>
/// Repository for persisting IBKR order callback events to the database.
/// Provides immutable append-only audit trail for crash recovery.
/// </summary>
public sealed class OrderEventsRepository : IOrderEventsRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<OrderEventsRepository> _logger;

    public OrderEventsRepository(IDbConnectionFactory db, ILogger<OrderEventsRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InsertOrderStatusAsync(
        string orderId,
        int? ibkrOrderId,
        OrderStatus status,
        int filled,
        int remaining,
        decimal? lastFillPrice,
        decimal? avgFillPrice,
        int? permId,
        int? parentId,
        string? lastTradeDate,
        string? whyHeld,
        decimal? mktCapPrice,
        CancellationToken ct = default)
    {
        // Validate inputs (negative-first pattern)
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("OrderId cannot be empty", nameof(orderId));
        }

        // SQL: Insert event with auto-generated event_id and timestamp
        // NOTE: event_id uses AUTOINCREMENT for monotonic ordering (prevents timestamp flakes)
        const string sql = """
            INSERT INTO order_events (
                order_id, ibkr_order_id, status, filled, remaining,
                last_fill_price, avg_fill_price, perm_id, parent_id,
                last_trade_date, why_held, mkt_cap_price
            ) VALUES (
                @OrderId, @IbkrOrderId, @Status, @Filled, @Remaining,
                @LastFillPrice, @AvgFillPrice, @PermId, @ParentId,
                @LastTradeDate, @WhyHeld, @MktCapPrice
            )
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new
            {
                OrderId = orderId,
                IbkrOrderId = ibkrOrderId,
                Status = status.ToString(),
                Filled = filled,
                Remaining = remaining,
                LastFillPrice = lastFillPrice,
                AvgFillPrice = avgFillPrice,
                PermId = permId,
                ParentId = parentId,
                LastTradeDate = lastTradeDate,
                WhyHeld = whyHeld,
                MktCapPrice = mktCapPrice
            }, cancellationToken: ct);

            await conn.ExecuteAsync(cmd);

            _logger.LogInformation(
                "Persisted order event: OrderId={OrderId} IbkrOrderId={IbkrOrderId} Status={Status} Filled={Filled}/{Total}",
                orderId, ibkrOrderId, status, filled, filled + remaining);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist order event: OrderId={OrderId} Status={Status}",
                orderId, status);
            throw;
        }
    }

    public async Task<OrderEventRecord?> GetLatestOrderEventAsync(string orderId, CancellationToken ct = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("OrderId cannot be empty", nameof(orderId));
        }

        // SQL: Get latest event by event_id (monotonic, not timestamp)
        const string sql = """
            SELECT
                event_id AS EventId,
                order_id AS OrderId,
                ibkr_order_id AS IbkrOrderId,
                status AS Status,
                filled AS Filled,
                remaining AS Remaining,
                last_fill_price AS LastFillPrice,
                avg_fill_price AS AvgFillPrice,
                perm_id AS PermId,
                parent_id AS ParentId,
                last_trade_date AS LastTradeDate,
                why_held AS WhyHeld,
                mkt_cap_price AS MktCapPrice,
                event_timestamp AS EventTimestamp
            FROM order_events
            WHERE order_id = @OrderId
            ORDER BY event_id DESC
            LIMIT 1
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { OrderId = orderId }, cancellationToken: ct);

            OrderEventRecord? result = await conn.QuerySingleOrDefaultAsync<OrderEventRecord>(cmd);

            if (result is null)
            {
                _logger.LogDebug("No events found for order {OrderId}", orderId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest event for order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<IReadOnlyList<OrderEventRecord>> GetOrderEventsAsync(string orderId, CancellationToken ct = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("OrderId cannot be empty", nameof(orderId));
        }

        // SQL: Get all events ordered by event_id (monotonic, not timestamp)
        // NOTE: ORDER BY event_id per Task #1 requirement to prevent timestamp-based flakes
        const string sql = """
            SELECT
                event_id AS EventId,
                order_id AS OrderId,
                ibkr_order_id AS IbkrOrderId,
                status AS Status,
                filled AS Filled,
                remaining AS Remaining,
                last_fill_price AS LastFillPrice,
                avg_fill_price AS AvgFillPrice,
                perm_id AS PermId,
                parent_id AS ParentId,
                last_trade_date AS LastTradeDate,
                why_held AS WhyHeld,
                mkt_cap_price AS MktCapPrice,
                event_timestamp AS EventTimestamp
            FROM order_events
            WHERE order_id = @OrderId
            ORDER BY event_id
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { OrderId = orderId }, cancellationToken: ct);

            IEnumerable<OrderEventRecord> results = await conn.QueryAsync<OrderEventRecord>(cmd);
            List<OrderEventRecord> list = results.ToList();

            _logger.LogDebug("Retrieved {Count} events for order {OrderId}", list.Count, orderId);

            return list.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events for order {OrderId}", orderId);
            throw;
        }
    }
}
