using Microsoft.Extensions.Logging;
using Moq;
using OptionsExecutionService.Ibkr;
using OptionsExecutionService.Repositories;
using SharedKernel.Domain;
using Xunit;

namespace OptionsExecutionService.Tests.Ibkr;

/// <summary>
/// Unit tests for TwsCallbackHandler IBKR callback persistence.
/// Tests the RED phase of TDD - this test WILL FAIL until Task #5 implements the persistence logic.
/// </summary>
/// <remarks>
/// Expected failures:
/// 1. Compilation error: TwsCallbackHandler constructor doesn't accept IOrderEventsRepository yet.
/// 2. Runtime assertion failure: orderStatus() doesn't call repository.InsertOrderStatusAsync() yet.
///
/// These failures are INTENTIONAL - they prove the test is properly written in the RED phase.
/// Task #5 will make this test GREEN by:
/// - Adding IOrderEventsRepository dependency to TwsCallbackHandler constructor
/// - Implementing InsertOrderStatusAsync() call in orderStatus() callback
/// </remarks>
public sealed class TwsCallbackHandlerTests
{
    /// <summary>
    /// Verifies that orderStatus() callback persists to order_events table via IOrderEventsRepository.
    /// </summary>
    [Fact]
    public void InsertOrderStatusAsync_CalledWhenOrderStatusCallbackFired()
    {
        // ============================================================
        // ARRANGE: Setup mocks and test data
        // ============================================================

        // Mock logger
        Mock<ILogger<TwsCallbackHandler>> mockLogger = new();

        // Mock repository (this is what we're verifying gets called)
        Mock<IOrderEventsRepository> mockRepository = new();

        // Create handler with mocked dependencies
        TwsCallbackHandler handler = new(mockLogger.Object, mockRepository.Object);

        // Test parameters matching IBKR orderStatus callback signature
        int ibkrOrderId = 1001;
        string status = "Submitted";
        decimal filled = 0;
        decimal remaining = 2;
        double avgFillPrice = 0.0;  // 0.0 means no fill yet
        long permId = 123456789;
        int parentId = 0;            // 0 means no parent order
        double lastFillPrice = 0.0;  // 0.0 means no fill yet
        int clientId = 0;
        string whyHeld = "";
        double mktCapPrice = 0.0;    // 0.0 means no value

        // ============================================================
        // ACT: Trigger the IBKR callback
        // ============================================================

        handler.orderStatus(
            ibkrOrderId,
            status,
            filled,
            remaining,
            avgFillPrice,
            permId,
            parentId,
            lastFillPrice,
            clientId,
            whyHeld,
            mktCapPrice);

        // No Task.Delay needed: .Wait() in implementation makes persistence synchronous

        // ============================================================
        // ASSERT: Verify repository was called with correct parameters
        // ============================================================

        // Verify InsertOrderStatusAsync was called ONCE
        mockRepository.Verify(
            repo => repo.InsertOrderStatusAsync(
                It.Is<string>(orderId => orderId == ibkrOrderId.ToString()),  // For now, map ibkrOrderId → orderId as string
                It.Is<int?>(id => id == ibkrOrderId),
                It.Is<OrderStatus>(s => s == OrderStatus.Submitted),
                It.Is<int>(f => f == 0),        // filled
                It.Is<int>(r => r == 2),        // remaining
                It.Is<decimal?>(price => price == null),    // lastFillPrice: 0.0 → null (no fill yet)
                It.Is<decimal?>(price => price == null),    // avgFillPrice: 0.0 → null (no fill yet)
                It.Is<int?>(perm => perm == 123456789),     // permId
                It.Is<int?>(parent => parent == null),      // parentId: 0 → null (no parent)
                It.Is<string?>(date => date == null),       // lastTradeDate: not provided in orderStatus callback
                It.Is<string?>(why => why == null),         // whyHeld: empty string → null
                It.Is<decimal?>(cap => cap == null),        // mktCapPrice: 0.0 → null (no value)
                It.IsAny<CancellationToken>()),
            Times.Once,
            "orderStatus callback should persist event to order_events table via InsertOrderStatusAsync");
    }

    /// <summary>
    /// Verifies that orderStatus() callback maps IBKR status strings to OrderStatus enum correctly.
    /// </summary>
    [Theory]
    [InlineData("ApiPending", OrderStatus.PendingSubmit)]       // Order created, not yet submitted
    [InlineData("PendingSubmit", OrderStatus.PendingSubmit)]    // Order submitted but not acknowledged
    [InlineData("PreSubmitted", OrderStatus.PendingSubmit)]     // Order acknowledged but not active (simulated orders)
    [InlineData("Submitted", OrderStatus.Submitted)]            // Order active on exchange
    [InlineData("PartFilled", OrderStatus.PartiallyFilled)]     // Order partially filled
    [InlineData("Filled", OrderStatus.Filled)]                  // Order completely filled
    [InlineData("Cancelled", OrderStatus.Cancelled)]            // Order cancelled
    [InlineData("ApiCancelled", OrderStatus.Cancelled)]         // Order cancelled by API
    [InlineData("Inactive", OrderStatus.Cancelled)]             // Order inactive (mapped to Cancelled)
    [InlineData("PendingCancel", OrderStatus.Cancelled)]        // Cancel request pending
    public void InsertOrderStatusAsync_CorrectlyMapsIbkrStatusToEnum(string ibkrStatus, OrderStatus expectedStatus)
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        Mock<ILogger<TwsCallbackHandler>> mockLogger = new();
        Mock<IOrderEventsRepository> mockRepository = new();

        TwsCallbackHandler handler = new(mockLogger.Object, mockRepository.Object);

        int ibkrOrderId = 1001;

        // ============================================================
        // ACT
        // ============================================================

        handler.orderStatus(
            orderId: ibkrOrderId,
            status: ibkrStatus,
            filled: 0,
            remaining: 2,
            avgFillPrice: 0.0,
            permId: 123456789,
            parentId: 0,
            lastFillPrice: 0.0,
            clientId: 0,
            whyHeld: "",
            mktCapPrice: 0.0);

        // No Task.Delay needed: .Wait() in implementation makes persistence synchronous

        // ============================================================
        // ASSERT
        // ============================================================

        mockRepository.Verify(
            repo => repo.InsertOrderStatusAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.Is<OrderStatus>(s => s == expectedStatus),  // Verify correct enum mapping
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            $"IBKR status '{ibkrStatus}' should map to OrderStatus.{expectedStatus}");
    }

    /// <summary>
    /// Verifies that avgFillPrice 0.0 is stored as NULL (not as 0).
    /// </summary>
    /// <remarks>
    /// According to IBKR API, 0.0 means "no fill yet". We store this as NULL in the database
    /// to distinguish "no fill" from "filled at price 0.00" (unlikely but theoretically possible).
    /// </remarks>
    [Fact]
    public void InsertOrderStatusAsync_StoresZeroPriceAsNull()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        Mock<ILogger<TwsCallbackHandler>> mockLogger = new();
        Mock<IOrderEventsRepository> mockRepository = new();

        TwsCallbackHandler handler = new(mockLogger.Object, mockRepository.Object);

        // ============================================================
        // ACT: Trigger callback with 0.0 prices (meaning "no fill yet")
        // ============================================================

        handler.orderStatus(
            orderId: 1001,
            status: "Submitted",
            filled: 0,
            remaining: 2,
            avgFillPrice: 0.0,      // 0.0 = "no fill yet"
            permId: 123456789,
            parentId: 0,
            lastFillPrice: 0.0,     // 0.0 = "no fill yet"
            clientId: 0,
            whyHeld: "",
            mktCapPrice: 0.0);      // 0.0 = "no value"

        // No Task.Delay needed: .Wait() in implementation makes persistence synchronous

        // ============================================================
        // ASSERT: Verify 0.0 values are stored as NULL
        // ============================================================

        mockRepository.Verify(
            repo => repo.InsertOrderStatusAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<OrderStatus>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.Is<decimal?>(price => price == null),    // lastFillPrice: 0.0 → null
                It.Is<decimal?>(price => price == null),    // avgFillPrice: 0.0 → null
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.Is<decimal?>(cap => cap == null),        // mktCapPrice: 0.0 → null
                It.IsAny<CancellationToken>()),
            Times.Once,
            "0.0 prices should be stored as NULL to distinguish 'no fill' from 'filled at 0.00'");
    }

    /// <summary>
    /// Verifies that parentId 0 is stored as NULL (not as 0).
    /// </summary>
    /// <remarks>
    /// According to IBKR API, parentId=0 means "no parent order" (standalone order).
    /// We store this as NULL to make queries cleaner (WHERE parent_id IS NOT NULL finds child orders).
    /// </remarks>
    [Fact]
    public void InsertOrderStatusAsync_StoresZeroParentIdAsNull()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        Mock<ILogger<TwsCallbackHandler>> mockLogger = new();
        Mock<IOrderEventsRepository> mockRepository = new();

        TwsCallbackHandler handler = new(mockLogger.Object, mockRepository.Object);

        // ============================================================
        // ACT: Trigger callback with parentId=0 (no parent)
        // ============================================================

        handler.orderStatus(
            orderId: 1001,
            status: "Submitted",
            filled: 0,
            remaining: 2,
            avgFillPrice: 0.0,
            permId: 123456789,
            parentId: 0,            // 0 = "no parent order"
            lastFillPrice: 0.0,
            clientId: 0,
            whyHeld: "",
            mktCapPrice: 0.0);

        // No Task.Delay needed: .Wait() in implementation makes persistence synchronous

        // ============================================================
        // ASSERT: Verify parentId=0 is stored as NULL
        // ============================================================

        mockRepository.Verify(
            repo => repo.InsertOrderStatusAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<OrderStatus>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<int?>(),
                It.Is<int?>(parent => parent == null),      // parentId: 0 → null
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "parentId=0 should be stored as NULL to indicate standalone order (no parent)");
    }

    /// <summary>
    /// Verifies that unknown IBKR status values are handled gracefully with fallback to PendingSubmit.
    /// </summary>
    /// <remarks>
    /// When IBKR introduces new status values or sends unexpected values, we should:
    /// 1. Log a warning (captured by mock logger)
    /// 2. Use PendingSubmit as fallback (safest: doesn't claim order is Filled when it's not)
    /// 3. Still persist the event (maintain audit trail)
    /// </remarks>
    [Fact]
    public void InsertOrderStatusAsync_HandlesUnknownStatusWithFallback()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        Mock<ILogger<TwsCallbackHandler>> mockLogger = new();
        Mock<IOrderEventsRepository> mockRepository = new();

        TwsCallbackHandler handler = new(mockLogger.Object, mockRepository.Object);

        string unknownStatus = "UnknownStatusFromFutureIbkrVersion";

        // ============================================================
        // ACT: Trigger callback with unknown status
        // ============================================================

        handler.orderStatus(
            orderId: 1001,
            status: unknownStatus,
            filled: 0,
            remaining: 2,
            avgFillPrice: 0.0,
            permId: 123456789,
            parentId: 0,
            lastFillPrice: 0.0,
            clientId: 0,
            whyHeld: "",
            mktCapPrice: 0.0);

        // ============================================================
        // ASSERT: Verify fallback to PendingSubmit
        // ============================================================

        mockRepository.Verify(
            repo => repo.InsertOrderStatusAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.Is<OrderStatus>(s => s == OrderStatus.PendingSubmit),  // Fallback to PendingSubmit
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Unknown IBKR status should fall back to PendingSubmit and still persist event");

        // Verify warning was logged
        mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(unknownStatus)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Unknown status should trigger a warning log");
    }
}
