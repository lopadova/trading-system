using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OptionsExecutionService.Ibkr;
using OptionsExecutionService.Repositories;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using Xunit;

namespace OptionsExecutionService.Tests.Ibkr;

/// <summary>
/// RM-01: Tests for order ID reservation in IbkrClient.
/// Verifies atomic increment, reconnect handling, and thread safety.
/// </summary>
public sealed class IbkrClientOrderIdTests : IDisposable
{
    private readonly IbkrClient _client;
    private readonly TwsCallbackHandler _wrapper;
    private readonly IbkrConfig _config;
    private readonly IbkrPortScanner _scanner;

    public IbkrClientOrderIdTests()
    {
        _config = new IbkrConfig
        {
            Host = "127.0.0.1",
            Port = 7497, // Paper trading port
            ClientId = 10,
            TradingMode = TradingMode.Paper,
            ReconnectInitialDelaySeconds = 1,
            ReconnectMaxDelaySeconds = 10,
            MaxReconnectAttempts = 3,
            ConnectionTimeoutSeconds = 5
        };

        Mock<IOrderEventsRepository> mockOrderEventsRepo = new();
        _wrapper = new TwsCallbackHandler(NullLogger<TwsCallbackHandler>.Instance, mockOrderEventsRepo.Object);
        _scanner = new IbkrPortScanner(NullLogger<IbkrPortScanner>.Instance);
        _client = new IbkrClient(NullLogger<IbkrClient>.Instance, _config, _wrapper, _scanner);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    [Fact]
    public void ReserveOrderId_ThrowsInvalidOperationException_WhenNextValidIdNotReceived()
    {
        // Arrange: No nextValidId callback has been fired yet (_localNextOrderId == 0)

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _client.ReserveOrderId());

        Assert.Contains("Cannot reserve order ID", ex.Message);
        Assert.Contains("nextValidId not yet received", ex.Message);
    }

    [Fact]
    public void ReserveOrderId_ReturnsUniqueIds_ForConsecutiveCalls()
    {
        // Arrange: Simulate IBKR sending nextValidId(1001)
        SimulateNextValidId(1001);

        // Act: Reserve 3 order IDs consecutively
        int id1 = _client.ReserveOrderId();
        int id2 = _client.ReserveOrderId();
        int id3 = _client.ReserveOrderId();

        // Assert: Each ID is unique and sequential
        Assert.Equal(1001, id1);
        Assert.Equal(1002, id2);
        Assert.Equal(1003, id3);
    }

    [Fact]
    public void ReserveOrderId_UpdatesCounter_WhenReconnectWithHigherNextValidId()
    {
        // Arrange: Initial connection sends nextValidId(1001), reserve 2 IDs
        SimulateNextValidId(1001);
        _client.ReserveOrderId(); // 1001
        _client.ReserveOrderId(); // 1002
        // Local counter now at 1003

        // Act: Reconnect, IBKR sends higher nextValidId(2000)
        SimulateNextValidId(2000);

        // Reserve next ID
        int id = _client.ReserveOrderId();

        // Assert: Counter jumped to IBKR's higher value
        Assert.Equal(2000, id);
    }

    [Fact]
    public void ReserveOrderId_DoesNotDecrementCounter_WhenReconnectWithLowerNextValidId()
    {
        // Arrange: Initial connection sends nextValidId(1001), reserve 2 IDs
        SimulateNextValidId(1001);
        _client.ReserveOrderId(); // 1001
        _client.ReserveOrderId(); // 1002
        // Local counter now at 1003

        // Act: Reconnect, IBKR sends LOWER nextValidId(500) (edge case: TWS restart)
        SimulateNextValidId(500);

        // Reserve next ID
        int id = _client.ReserveOrderId();

        // Assert: Counter did NOT go backwards (Math.Max protection)
        Assert.Equal(1003, id); // Should still be local counter, not 500
    }

    [Fact]
    public void ReserveOrderId_IsThreadSafe_WhenCalledConcurrently()
    {
        // Arrange: Simulate IBKR sending nextValidId(1001)
        SimulateNextValidId(1001);

        // Act: Reserve 100 IDs from 10 threads concurrently
        const int threadsCount = 10;
        const int reservationsPerThread = 10;
        List<int> allIds = new();
        object listLock = new();

        Parallel.For(0, threadsCount, _ =>
        {
            for (int i = 0; i < reservationsPerThread; i++)
            {
                int id = _client.ReserveOrderId();
                lock (listLock)
                {
                    allIds.Add(id);
                }
            }
        });

        // Assert: All 100 IDs are unique (no collisions)
        Assert.Equal(100, allIds.Count);
        Assert.Equal(100, allIds.Distinct().Count()); // No duplicates

        // Assert: IDs range from 1001 to 1100 (consecutive)
        Assert.Equal(1001, allIds.Min());
        Assert.Equal(1100, allIds.Max());
    }

    [Fact]
    public void ReserveOrderId_MonotonicIncrement_AcrossMultipleReconnects()
    {
        // Arrange: Initial connection
        SimulateNextValidId(1001);
        int id1 = _client.ReserveOrderId(); // 1001

        // Act: Reconnect #1 with higher ID
        SimulateNextValidId(2000);
        int id2 = _client.ReserveOrderId(); // 2000

        // Act: Reconnect #2 with lower ID (Math.Max should ignore)
        SimulateNextValidId(1500);
        int id3 = _client.ReserveOrderId(); // 2001 (local counter, not 1500)

        // Act: Reconnect #3 with much higher ID
        SimulateNextValidId(5000);
        int id4 = _client.ReserveOrderId(); // 5000

        // Assert: IDs are monotonically increasing (never decrease)
        Assert.Equal(1001, id1);
        Assert.Equal(2000, id2);
        Assert.Equal(2001, id3); // Did NOT reset to 1500
        Assert.Equal(5000, id4);

        // Assert: Each ID > previous ID
        Assert.True(id2 > id1);
        Assert.True(id3 > id2);
        Assert.True(id4 > id3);
    }

    /// <summary>
    /// Helper to simulate IBKR sending nextValidId callback.
    /// Directly invokes the wrapper's nextValidId method, which triggers NextValidIdReceived event.
    /// </summary>
    private void SimulateNextValidId(int orderId)
    {
        // Invoke the callback directly (simulating IBKR TWS API call)
        // This will trigger the event subscription in IbkrClient
        _wrapper.GetType()
            .GetMethod("nextValidId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .Invoke(_wrapper, new object[] { orderId });
    }
}
