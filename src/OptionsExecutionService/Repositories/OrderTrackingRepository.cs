using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OptionsExecutionService.Orders;
using SharedKernel.Data;
using SharedKernel.Domain;

namespace OptionsExecutionService.Repositories;

/// <summary>
/// Repository for tracking orders and executions in the database.
/// Implements audit trail for all order lifecycle events.
/// </summary>
public sealed class OrderTrackingRepository : IOrderTrackingRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<OrderTrackingRepository> _logger;

    public OrderTrackingRepository(IDbConnectionFactory db, ILogger<OrderTrackingRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogOrderAsync(
        string orderId,
        int? ibkrOrderId,
        OrderRequest request,
        OrderStatus status,
        CancellationToken ct = default)
    {
        // Validate inputs (negative-first)
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("OrderId cannot be empty", nameof(orderId));
        }
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        const string sql = """
            INSERT INTO order_tracking (
                order_id, ibkr_order_id, campaign_id, position_id,
                symbol, contract_symbol, side, order_type,
                quantity, limit_price, stop_price, time_in_force,
                status, filled_quantity, avg_fill_price,
                strategy_name, metadata_json,
                created_at, submitted_at, completed_at, updated_at
            ) VALUES (
                @OrderId, @IbkrOrderId, @CampaignId, @PositionId,
                @Symbol, @ContractSymbol, @Side, @OrderType,
                @Quantity, @LimitPrice, @StopPrice, @TimeInForce,
                @Status, 0, NULL,
                @StrategyName, @MetadataJson,
                @CreatedAt, @SubmittedAt, NULL, @UpdatedAt
            )
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new
            {
                OrderId = orderId,
                IbkrOrderId = ibkrOrderId,
                request.CampaignId,
                request.PositionId,
                request.Symbol,
                request.ContractSymbol,
                Side = request.Side.ToString(),
                OrderType = request.Type.ToString(),
                request.Quantity,
                request.LimitPrice,
                request.StopPrice,
                request.TimeInForce,
                Status = status.ToString(),
                request.StrategyName,
                request.MetadataJson,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                SubmittedAt = (status == OrderStatus.Submitted) ? DateTime.UtcNow.ToString("O") : null,
                UpdatedAt = DateTime.UtcNow.ToString("O")
            }, cancellationToken: ct);

            await conn.ExecuteAsync(cmd);

            _logger.LogInformation(
                "Logged order {OrderId} (IBKR: {IbkrOrderId}) with status {Status}",
                orderId, ibkrOrderId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log order {OrderId}", orderId);
            throw;
        }
    }

    public async Task UpdateOrderStatusAsync(
        string orderId,
        OrderStatus status,
        int filledQuantity,
        decimal avgFillPrice,
        CancellationToken ct = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("OrderId cannot be empty", nameof(orderId));
        }

        // Determine if this is a completion event
        bool isCompleted = status is OrderStatus.Filled
            or OrderStatus.Cancelled
            or OrderStatus.Rejected
            or OrderStatus.Failed;

        const string sql = """
            UPDATE order_tracking
            SET status = @Status,
                filled_quantity = @FilledQuantity,
                avg_fill_price = @AvgFillPrice,
                completed_at = @CompletedAt,
                updated_at = @UpdatedAt
            WHERE order_id = @OrderId
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new
            {
                OrderId = orderId,
                Status = status.ToString(),
                FilledQuantity = filledQuantity,
                AvgFillPrice = avgFillPrice > 0 ? avgFillPrice : (decimal?)null,
                CompletedAt = isCompleted ? DateTime.UtcNow.ToString("O") : null,
                UpdatedAt = DateTime.UtcNow.ToString("O")
            }, cancellationToken: ct);

            int rowsAffected = await conn.ExecuteAsync(cmd);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Order {OrderId} not found for status update", orderId);
                return;
            }

            _logger.LogInformation(
                "Updated order {OrderId} to status {Status} (filled: {Filled})",
                orderId, status, filledQuantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update order {OrderId} status", orderId);
            throw;
        }
    }

    public async Task LogExecutionAsync(
        string executionId,
        string orderId,
        int ibkrOrderId,
        OrderRequest request,
        int fillQuantity,
        decimal fillPrice,
        decimal commission,
        DateTime executedAt,
        CancellationToken ct = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new ArgumentException("ExecutionId cannot be empty", nameof(executionId));
        }
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("OrderId cannot be empty", nameof(orderId));
        }
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Log to execution_log table (from database schema)
        const string sql = """
            INSERT OR IGNORE INTO execution_log (
                execution_id, order_id, position_id, campaign_id,
                symbol, contract_symbol, side, quantity,
                fill_price, commission, executed_at, created_at
            ) VALUES (
                @ExecutionId, @IbkrOrderId, @PositionId, @CampaignId,
                @Symbol, @ContractSymbol, @Side, @Quantity,
                @FillPrice, @Commission, @ExecutedAt, @CreatedAt
            )
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new
            {
                ExecutionId = executionId,
                IbkrOrderId = ibkrOrderId.ToString(), // execution_log.order_id is TEXT
                request.PositionId,
                request.CampaignId,
                request.Symbol,
                request.ContractSymbol,
                Side = request.Side.ToString().ToUpperInvariant(), // "BUY" or "SELL"
                Quantity = fillQuantity,
                FillPrice = fillPrice,
                Commission = commission,
                ExecutedAt = executedAt.ToString("O"),
                CreatedAt = DateTime.UtcNow.ToString("O")
            }, cancellationToken: ct);

            await conn.ExecuteAsync(cmd);

            _logger.LogInformation(
                "Logged execution {ExecutionId} for order {OrderId}: {Quantity} @ {Price}",
                executionId, orderId, fillQuantity, fillPrice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log execution {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<OrderRecord?> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("OrderId cannot be empty", nameof(orderId));
        }

        const string sql = """
            SELECT order_id AS OrderId,
                   ibkr_order_id AS IbkrOrderId,
                   campaign_id AS CampaignId,
                   position_id AS PositionId,
                   symbol AS Symbol,
                   contract_symbol AS ContractSymbol,
                   side AS Side,
                   order_type AS Type,
                   quantity AS Quantity,
                   limit_price AS LimitPrice,
                   stop_price AS StopPrice,
                   time_in_force AS TimeInForce,
                   status AS Status,
                   filled_quantity AS FilledQuantity,
                   avg_fill_price AS AvgFillPrice,
                   strategy_name AS StrategyName,
                   metadata_json AS MetadataJson,
                   created_at AS CreatedAt,
                   submitted_at AS SubmittedAt,
                   completed_at AS CompletedAt
            FROM order_tracking
            WHERE order_id = @OrderId
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { OrderId = orderId }, cancellationToken: ct);

            OrderRecord? record = await conn.QuerySingleOrDefaultAsync<OrderRecord>(cmd);
            return record;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<OrderRecord?> GetOrderByIbkrIdAsync(int ibkrOrderId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT order_id AS OrderId,
                   ibkr_order_id AS IbkrOrderId,
                   campaign_id AS CampaignId,
                   position_id AS PositionId,
                   symbol AS Symbol,
                   contract_symbol AS ContractSymbol,
                   side AS Side,
                   order_type AS Type,
                   quantity AS Quantity,
                   limit_price AS LimitPrice,
                   stop_price AS StopPrice,
                   time_in_force AS TimeInForce,
                   status AS Status,
                   filled_quantity AS FilledQuantity,
                   avg_fill_price AS AvgFillPrice,
                   strategy_name AS StrategyName,
                   metadata_json AS MetadataJson,
                   created_at AS CreatedAt,
                   submitted_at AS SubmittedAt,
                   completed_at AS CompletedAt
            FROM order_tracking
            WHERE ibkr_order_id = @IbkrOrderId
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { IbkrOrderId = ibkrOrderId }, cancellationToken: ct);

            OrderRecord? record = await conn.QuerySingleOrDefaultAsync<OrderRecord>(cmd);
            return record;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order by IBKR ID {IbkrOrderId}", ibkrOrderId);
            throw;
        }
    }

    public async Task<List<OrderRecord>> GetFailedOrdersInWindowAsync(int windowMinutes, CancellationToken ct = default)
    {
        if (windowMinutes <= 0)
        {
            throw new ArgumentException("WindowMinutes must be positive", nameof(windowMinutes));
        }

        DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-windowMinutes);

        const string sql = """
            SELECT order_id AS OrderId,
                   ibkr_order_id AS IbkrOrderId,
                   campaign_id AS CampaignId,
                   position_id AS PositionId,
                   symbol AS Symbol,
                   contract_symbol AS ContractSymbol,
                   side AS Side,
                   order_type AS Type,
                   quantity AS Quantity,
                   limit_price AS LimitPrice,
                   stop_price AS StopPrice,
                   time_in_force AS TimeInForce,
                   status AS Status,
                   filled_quantity AS FilledQuantity,
                   avg_fill_price AS AvgFillPrice,
                   strategy_name AS StrategyName,
                   metadata_json AS MetadataJson,
                   created_at AS CreatedAt,
                   submitted_at AS SubmittedAt,
                   completed_at AS CompletedAt
            FROM order_tracking
            WHERE status IN ('Failed', 'Rejected')
              AND created_at >= @CutoffTime
            ORDER BY created_at DESC
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { CutoffTime = cutoffTime.ToString("O") }, cancellationToken: ct);

            IEnumerable<OrderRecord> records = await conn.QueryAsync<OrderRecord>(cmd);
            return records.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get failed orders in window {Minutes}min", windowMinutes);
            throw;
        }
    }

    public async Task<OrderStats> GetOrderStatsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                CAST(COUNT(*) AS INTEGER) as TotalOrders,
                CAST(COALESCE(SUM(CASE WHEN status = 'Filled' THEN 1 ELSE 0 END), 0) AS INTEGER) as FilledOrders,
                CAST(COALESCE(SUM(CASE WHEN status IN ('Failed', 'Rejected', 'ValidationFailed') THEN 1 ELSE 0 END), 0) AS INTEGER) as FailedOrders,
                CAST(COALESCE(SUM(CASE WHEN status IN ('PendingSubmit', 'Submitted', 'Active', 'PartiallyFilled') THEN 1 ELSE 0 END), 0) AS INTEGER) as ActiveOrders,
                CAST(COALESCE(SUM(CASE WHEN status = 'Cancelled' THEN 1 ELSE 0 END), 0) AS INTEGER) as CancelledOrders
            FROM order_tracking
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, cancellationToken: ct);

            OrderStats stats = await conn.QuerySingleAsync<OrderStats>(cmd);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order stats");
            throw;
        }
    }
}
