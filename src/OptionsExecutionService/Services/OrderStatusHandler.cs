using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OptionsExecutionService.Repositories;
using SharedKernel.Domain;
using SharedKernel.Ibkr;

namespace OptionsExecutionService.Services;

/// <summary>
/// Background service that subscribes to IBKR order status callbacks and updates order_tracking table.
/// Phase 3: Broker callback persistence P1 - Task RM-04
///
/// Solves the problem: after PlaceOrderAsync marks order as Submitted, IBKR callbacks arrive but
/// order_tracking table is never updated with Filled/Cancelled/Rejected status.
/// </summary>
public sealed class OrderStatusHandler : BackgroundService
{
    private readonly IIbkrClient _ibkrClient;
    private readonly IOrderTrackingRepository _orderRepo;
    private readonly ILogger<OrderStatusHandler> _logger;

    public OrderStatusHandler(
        IIbkrClient ibkrClient,
        IOrderTrackingRepository orderRepo,
        ILogger<OrderStatusHandler> logger)
    {
        _ibkrClient = ibkrClient ?? throw new ArgumentNullException(nameof(ibkrClient));
        _orderRepo = orderRepo ?? throw new ArgumentNullException(nameof(orderRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to IBKR callbacks
        _ibkrClient.OrderStatusChanged += OnOrderStatusChanged;
        _ibkrClient.OrderError += OnOrderError;

        _logger.LogInformation("OrderStatusHandler started - subscribed to IBKR order callbacks");

        // This service runs for the lifetime of the application, no active loop needed
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // Unsubscribe from events
        _ibkrClient.OrderStatusChanged -= OnOrderStatusChanged;
        _ibkrClient.OrderError -= OnOrderError;

        _logger.LogInformation("OrderStatusHandler stopped - unsubscribed from IBKR order callbacks");
        return base.StopAsync(cancellationToken);
    }

    private async void OnOrderStatusChanged(object? sender, (int OrderId, string Status, int Filled, int Remaining, double AvgFillPrice) args)
    {
        try
        {
            // Lookup internal order ID from IBKR order ID
            OrderRecord? order = await _orderRepo.GetOrderByIbkrIdAsync(args.OrderId, CancellationToken.None);

            if (order is null)
            {
                _logger.LogWarning(
                    "Received orderStatus callback for unknown IBKR orderId={IbkrOrderId}. " +
                    "This can happen for orders placed by other clients or manual TWS orders.",
                    args.OrderId);
                return;
            }

            // Map IBKR status string to enum
            OrderStatus mappedStatus = MapIbkrStatus(args.Status);

            // Update order_tracking table
            await _orderRepo.UpdateOrderStatusAsync(
                order.OrderId,
                mappedStatus,
                args.Filled,
                (decimal)args.AvgFillPrice,
                CancellationToken.None);

            _logger.LogInformation(
                "Updated order status: OrderId={OrderId} IbkrId={IbkrOrderId} Status={Status} Filled={Filled}/{Total}",
                order.OrderId, args.OrderId, mappedStatus, args.Filled, order.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process orderStatus callback for IbkrOrderId={IbkrOrderId}",
                args.OrderId);
            // Don't rethrow - event handler must not crash the service
        }
    }

    private async void OnOrderError(object? sender, (int OrderId, int ErrorCode, string ErrorMessage) args)
    {
        try
        {
            // Lookup internal order ID from IBKR order ID
            OrderRecord? order = await _orderRepo.GetOrderByIbkrIdAsync(args.OrderId, CancellationToken.None);

            if (order is null)
            {
                _logger.LogWarning(
                    "Received order error callback for unknown IBKR orderId={IbkrOrderId} errorCode={ErrorCode}",
                    args.OrderId, args.ErrorCode);
                return;
            }

            // Critical errors that should mark order as Failed
            if (IsCriticalError(args.ErrorCode))
            {
                await _orderRepo.UpdateOrderStatusAsync(
                    order.OrderId,
                    OrderStatus.Failed,
                    0, // filled
                    0m, // avgPrice
                    CancellationToken.None);

                _logger.LogError(
                    "Marked order as Failed due to critical IBKR error: OrderId={OrderId} IbkrId={IbkrOrderId} ErrorCode={ErrorCode} Message={ErrorMessage}",
                    order.OrderId, args.OrderId, args.ErrorCode, args.ErrorMessage);
            }
            else
            {
                _logger.LogWarning(
                    "Order error (non-critical): OrderId={OrderId} IbkrId={IbkrOrderId} ErrorCode={ErrorCode} Message={ErrorMessage}",
                    order.OrderId, args.OrderId, args.ErrorCode, args.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process order error callback for IbkrOrderId={IbkrOrderId} ErrorCode={ErrorCode}",
                args.OrderId, args.ErrorCode);
            // Don't rethrow - event handler must not crash the service
        }
    }

    /// <summary>
    /// Maps IBKR order status string to OrderStatus enum.
    /// </summary>
    private static OrderStatus MapIbkrStatus(string ibkrStatus) => ibkrStatus switch
    {
        // Pending states
        "ApiPending" => OrderStatus.PendingSubmit,
        "PendingSubmit" => OrderStatus.PendingSubmit,
        "PreSubmitted" => OrderStatus.PendingSubmit,

        // Active states
        "Submitted" => OrderStatus.Submitted,
        "Active" => OrderStatus.Active,
        "PartFilled" => OrderStatus.PartiallyFilled,
        "Filled" => OrderStatus.Filled,

        // Terminal states
        "Cancelled" => OrderStatus.Cancelled,
        "ApiCancelled" => OrderStatus.Cancelled,
        "Inactive" => OrderStatus.Cancelled,
        "PendingCancel" => OrderStatus.Cancelled,

        // Unknown: use PendingSubmit as safe fallback
        _ => OrderStatus.PendingSubmit
    };

    /// <summary>
    /// Determines if an IBKR error code represents a critical failure that should mark order as Failed.
    /// </summary>
    /// <remarks>
    /// IBKR error codes: https://interactivebrokers.github.io/tws-api/message_codes.html
    /// Critical errors (200-299, 400-499): Order rejected, invalid parameters, insufficient margin, etc.
    /// Non-critical (2100+): Informational messages, market data warnings, etc.
    /// </remarks>
    private static bool IsCriticalError(int errorCode)
    {
        return errorCode switch
        {
            // 200-299: Order rejection errors
            >= 200 and < 300 => true,

            // 400-499: Order validation errors
            >= 400 and < 500 => true,

            // 10000+: TWS-specific order errors
            >= 10000 and < 11000 => true,

            // All others: informational or connection-related
            _ => false
        };
    }
}
