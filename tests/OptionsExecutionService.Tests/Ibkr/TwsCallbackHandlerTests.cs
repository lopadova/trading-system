using IBApi;
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
    [InlineData("Active", OrderStatus.Active)]                  // Order accepted by IBKR and is active
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

    #region execDetails() Callback Tests

    /// <summary>
    /// Verifies that execDetails() callback persists execution data to order_events table via IOrderEventsRepository.
    /// </summary>
    /// <remarks>
    /// This test is in the RED phase of TDD - it WILL FAIL because:
    /// 1. The execDetails() callback in TwsCallbackHandler doesn't call InsertExecutionAsync() yet.
    /// 2. Task #8 will implement the persistence logic to make this test GREEN.
    ///
    /// Expected failure: Mock verification will fail because InsertExecutionAsync is never called.
    /// </remarks>
    [Fact]
    public void InsertExecutionAsync_CalledWhenExecDetailsCallbackFired()
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

        // Test parameters matching IBKR execDetails callback signature
        int reqId = 1;

        // Create mock Contract representing SPY 20260515 550C option
        Contract contract = new()
        {
            Symbol = "SPY",
            SecType = "OPT",
            LastTradeDateOrContractMonth = "20260515",
            Strike = 550.0,
            Right = "C",
            Exchange = "SMART",
            Currency = "USD"
        };

        // Create mock Execution representing a fill of 2 contracts at $5.50
        Execution execution = new()
        {
            OrderId = 1001,
            ExecId = "0001f4e8.65a1b2c3.01.01",
            Time = "20260430  09:30:15",
            Side = "BOT",
            Shares = 2,
            Price = 5.50,
            Exchange = "CBOE",
            PermId = 123456789,
            ClientId = 0
        };

        // ============================================================
        // ACT: Trigger the IBKR callback
        // ============================================================

        handler.execDetails(reqId, contract, execution);

        // No Task.Delay needed: .Wait() in implementation makes persistence synchronous (when implemented)

        // ============================================================
        // ASSERT: Verify repository was called with correct parameters
        // ============================================================

        // Verify InsertExecutionAsync was called ONCE with correct data
        mockRepository.Verify(
            repo => repo.InsertExecutionAsync(
                // NOTE: This test assumes orderId == execution.OrderId.ToString() for simplicity (RED phase).
                // Task #8 implementation must resolve orderId via order_tracking lookup:
                //   SELECT order_id FROM order_tracking WHERE ibkr_order_id = execution.OrderId
                // If no match found, the execution is orphaned (order placed before crash).
                It.Is<string>(orderId => orderId == execution.OrderId.ToString()),
                It.Is<int?>(id => id == execution.OrderId),                        // ibkrOrderId
                It.Is<string>(execId => execId == execution.ExecId),               // execId (unique per fill)
                It.Is<string>(time => time == execution.Time),                     // execTime (IBKR format)
                It.Is<string>(side => side == execution.Side),                     // side (BOT/SLD)
                It.Is<decimal>(shares => shares == execution.Shares),              // shares executed
                It.Is<decimal>(price => price == (decimal)execution.Price),        // execution price (cast from double)
                It.Is<string>(exchange => exchange == execution.Exchange),         // exchange
                It.Is<int?>(permId => permId == (execution.PermId > 0 ? (int)execution.PermId : null)),  // permId (cast from long)
                It.Is<string>(symbol => symbol == contract.Symbol),                // contract symbol
                It.Is<string>(secType => secType == contract.SecType),             // contract secType
                It.IsAny<CancellationToken>()),
            Times.Once,
            "execDetails callback should persist execution event to order_events table via InsertExecutionAsync");
    }

    /// <summary>
    /// Verifies that execDetails() callback correctly handles multiple executions for the same order.
    /// </summary>
    /// <remarks>
    /// An order can have multiple partial fills (executions). Each execution should create a separate
    /// immutable event row in order_events with a unique execId.
    ///
    /// This test verifies:
    /// 1. Each execDetails() call creates a new event (append-only audit trail)
    /// 2. Each execution has a unique execId
    /// 3. Repository is called once per execution (no deduplication at callback level)
    ///
    /// Deduplication (if needed) is the repository's responsibility via unique constraint on execId.
    /// </remarks>
    [Fact]
    public void InsertExecutionAsync_CreatesNewEventForEachExecution()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        Mock<ILogger<TwsCallbackHandler>> mockLogger = new();
        Mock<IOrderEventsRepository> mockRepository = new();

        TwsCallbackHandler handler = new(mockLogger.Object, mockRepository.Object);

        int reqId = 1;

        // Same contract for all executions
        Contract contract = new()
        {
            Symbol = "SPY",
            SecType = "OPT",
            LastTradeDateOrContractMonth = "20260515",
            Strike = 550.0,
            Right = "C",
            Exchange = "SMART",
            Currency = "USD"
        };

        // First execution: partial fill of 1 contract at $5.50
        Execution execution1 = new()
        {
            OrderId = 1001,
            ExecId = "0001f4e8.65a1b2c3.01.01",  // Unique execId #1
            Time = "20260430  09:30:15",
            Side = "BOT",
            Shares = 1,
            Price = 5.50,
            Exchange = "CBOE",
            PermId = 123456789,
            ClientId = 0
        };

        // Second execution: another partial fill of 1 contract at $5.55 (price improved)
        Execution execution2 = new()
        {
            OrderId = 1001,                      // Same order
            ExecId = "0001f4e8.65a1b2c3.01.02",  // Different execId #2
            Time = "20260430  09:30:16",         // 1 second later
            Side = "BOT",
            Shares = 1,
            Price = 5.55,                        // Different price
            Exchange = "CBOE",
            PermId = 123456789,
            ClientId = 0
        };

        // ============================================================
        // ACT: Trigger execDetails twice for the same order
        // ============================================================

        handler.execDetails(reqId, contract, execution1);
        handler.execDetails(reqId, contract, execution2);

        // ============================================================
        // ASSERT: Verify repository was called TWICE (once per execution)
        // ============================================================

        // Verify first execution was persisted
        mockRepository.Verify(
            repo => repo.InsertExecutionAsync(
                It.Is<string>(orderId => orderId == "1001"),
                It.Is<int?>(id => id == 1001),
                It.Is<string>(execId => execId == "0001f4e8.65a1b2c3.01.01"),  // First execId
                It.Is<string>(time => time == "20260430  09:30:15"),
                It.Is<string>(side => side == "BOT"),
                It.Is<decimal>(shares => shares == 1),
                It.Is<decimal>(price => price == 5.50m),
                It.Is<string>(exchange => exchange == "CBOE"),
                It.Is<int?>(permId => permId == 123456789),
                It.Is<string>(symbol => symbol == "SPY"),
                It.Is<string>(secType => secType == "OPT"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "First execution should be persisted");

        // Verify second execution was persisted
        mockRepository.Verify(
            repo => repo.InsertExecutionAsync(
                It.Is<string>(orderId => orderId == "1001"),
                It.Is<int?>(id => id == 1001),
                It.Is<string>(execId => execId == "0001f4e8.65a1b2c3.01.02"),  // Second execId
                It.Is<string>(time => time == "20260430  09:30:16"),
                It.Is<string>(side => side == "BOT"),
                It.Is<decimal>(shares => shares == 1),
                It.Is<decimal>(price => price == 5.55m),
                It.Is<string>(exchange => exchange == "CBOE"),
                It.Is<int?>(permId => permId == 123456789),
                It.Is<string>(symbol => symbol == "SPY"),
                It.Is<string>(secType => secType == "OPT"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Second execution should be persisted");

        // Verify total calls = 2
        mockRepository.Verify(
            repo => repo.InsertExecutionAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "execDetails should create separate events for each execution (append-only audit trail)");
    }

    #endregion

    #region Error Callback Persistence Tests

    /// <summary>
    /// Verifies that order-specific error callbacks trigger InsertErrorAsync() persistence.
    /// This is TDD RED phase - test will fail until Task #11 implements persistence.
    /// </summary>
    /// <remarks>
    /// Expected failures:
    /// 1. Compilation error: OrderEventsRepository doesn't implement InsertErrorAsync yet.
    /// 2. Runtime error: TwsCallbackHandler.error() doesn't call repository yet.
    /// </remarks>
    [Fact]
    public void InsertErrorAsync_CalledWhenOrderErrorCallbackFired()
    {
        // ARRANGE
        Mock<ILogger<TwsCallbackHandler>> mockLogger = new();
        Mock<IOrderEventsRepository> mockRepository = new();
        TwsCallbackHandler handler = new(mockLogger.Object, mockRepository.Object);

        // IBKR error callback parameters for order-specific error
        int orderId = 1001;
        long errorTime = 1735603200; // Unix timestamp
        int errorCode = 201; // Order-specific error (200+)
        string errorMsg = "Order rejected - invalid contract";
        string advancedOrderRejectJson = "";

        // ACT
        handler.error(orderId, errorTime, errorCode, errorMsg, advancedOrderRejectJson);

        // ASSERT
        // Verify InsertErrorAsync called once with correct parameters
        mockRepository.Verify(
            repo => repo.InsertErrorAsync(
                // NOTE: This test assumes orderId == id.ToString() for simplicity (RED phase).
                // Task #11 implementation must resolve orderId via order_tracking lookup.
                It.Is<string>(oid => oid == orderId.ToString()),
                It.Is<int?>(id => id == orderId),
                It.Is<int>(code => code == errorCode),
                It.Is<string>(msg => msg == errorMsg),
                It.Is<long>(time => time == errorTime),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Order-specific error callback should persist error event to order_events table");
    }

    /// <summary>
    /// Verifies that connection errors and info messages are NOT persisted.
    /// Only order-specific errors (id > 0 and errorCode >= 200) should be saved.
    /// </summary>
    [Theory]
    [InlineData(0, 2104, "Market data farm connection OK")]        // Connection status (id=0)
    [InlineData(-1, 502, "Connection refused")]                    // Connection error (id=-1)
    [InlineData(1001, 165, "Historical market data service OK")]   // Info message (errorCode < 200)
    public void InsertErrorAsync_NotCalledForNonOrderErrors(int orderId, int errorCode, string errorMsg)
    {
        // ARRANGE
        Mock<ILogger<TwsCallbackHandler>> mockLogger = new();
        Mock<IOrderEventsRepository> mockRepository = new();
        TwsCallbackHandler handler = new(mockLogger.Object, mockRepository.Object);

        // ACT
        handler.error(orderId, 0L, errorCode, errorMsg, "");

        // ASSERT
        // Verify InsertErrorAsync NOT called for connection/info messages
        mockRepository.Verify(
            repo => repo.InsertErrorAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "Connection errors and info messages should NOT be persisted to order_events");
    }

    #endregion
}
