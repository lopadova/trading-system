using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Tests.Data;
using OptionsExecutionService.Migrations;
using OptionsExecutionService.Repositories;
using Xunit;

namespace OptionsExecutionService.Tests.Repositories;

/// <summary>
/// Integration tests for OptionsExecutionService repositories with SQLite.
/// Verifies repositories persist and retrieve data correctly with real database.
/// TEST-22-31 through TEST-22-35
/// </summary>
public sealed class RepositoryIntegrationTests : IAsyncLifetime
{
    private InMemoryConnectionFactory _factory = default!;
    private ICampaignRepository _campaignRepo = default!;
    private IOrderTrackingRepository _orderRepo = default!;

    public async Task InitializeAsync()
    {
        _factory = new InMemoryConnectionFactory();

        // Run migrations
        MigrationRunner runner = new(_factory, NullLogger<MigrationRunner>.Instance);
        await runner.RunAsync(OptionsMigrations.All, CancellationToken.None);

        // Create repositories
        _campaignRepo = new CampaignRepository(_factory, NullLogger<CampaignRepository>.Instance);
        _orderRepo = new OrderTrackingRepository(_factory, NullLogger<OrderTrackingRepository>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact(DisplayName = "TEST-22-31: CampaignRepository creates and retrieves campaigns")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-31")]
    public async Task TEST_22_31_CampaignRepositoryCreatesAndRetrievesCampaigns()
    {
        // Arrange
        Campaign campaign = new()
        {
            CampaignId = Guid.NewGuid().ToString(),
            StrategyName = "test-strategy",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            StrategyParams = "{\"param1\":\"value1\"}"
        };

        // Act: Insert campaign
        await _campaignRepo.InsertAsync(campaign, CancellationToken.None);

        // Retrieve by ID
        Campaign? retrieved = await _campaignRepo.GetByIdAsync(campaign.CampaignId, CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(campaign.CampaignId, retrieved.CampaignId);
        Assert.Equal("test-strategy", retrieved.StrategyName);
        Assert.Equal("active", retrieved.Status);
        Assert.Equal("{\"param1\":\"value1\"}", retrieved.StrategyParams);
    }

    [Fact(DisplayName = "TEST-22-32: CampaignRepository lists active campaigns")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-32")]
    public async Task TEST_22_32_CampaignRepositoryListsActiveCampaigns()
    {
        // Arrange: Create multiple campaigns
        Campaign active1 = new()
        {
            CampaignId = Guid.NewGuid().ToString(),
            StrategyName = "strategy-1",
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        Campaign active2 = new()
        {
            CampaignId = Guid.NewGuid().ToString(),
            StrategyName = "strategy-2",
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        Campaign closed = new()
        {
            CampaignId = Guid.NewGuid().ToString(),
            StrategyName = "strategy-3",
            Status = "closed",
            CreatedAt = DateTime.UtcNow
        };

        // Act: Insert all campaigns
        await _campaignRepo.InsertAsync(active1, CancellationToken.None);
        await _campaignRepo.InsertAsync(active2, CancellationToken.None);
        await _campaignRepo.InsertAsync(closed, CancellationToken.None);

        // Get active campaigns
        List<Campaign> activeCampaigns = await _campaignRepo.ListActiveAsync(CancellationToken.None);

        // Assert: Only active campaigns should be returned
        Assert.Equal(2, activeCampaigns.Count);
        Assert.All(activeCampaigns, c => Assert.Equal("active", c.Status));
    }

    [Fact(DisplayName = "TEST-22-33: CampaignRepository updates campaign status")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-33")]
    public async Task TEST_22_33_CampaignRepositoryUpdatesCampaignStatus()
    {
        // Arrange
        Campaign campaign = new()
        {
            CampaignId = Guid.NewGuid().ToString(),
            StrategyName = "test-strategy",
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        await _campaignRepo.InsertAsync(campaign, CancellationToken.None);

        // Act: Update status
        await _campaignRepo.UpdateStatusAsync(campaign.CampaignId, "closed", CancellationToken.None);

        // Retrieve updated campaign
        Campaign? updated = await _campaignRepo.GetByIdAsync(campaign.CampaignId, CancellationToken.None);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("closed", updated.Status);
    }

    [Fact(DisplayName = "TEST-22-34: OrderTrackingRepository creates and tracks orders")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-34")]
    public async Task TEST_22_34_OrderTrackingRepositoryCreatesAndTracksOrders()
    {
        // Arrange
        OrderTracking order = new()
        {
            OrderId = Guid.NewGuid().ToString(),
            IbkrOrderId = 12345,
            CampaignId = Guid.NewGuid().ToString(),
            Status = "submitted",
            Symbol = "SPY",
            Quantity = 10,
            OrderType = "LMT",
            LimitPrice = 450.0m,
            CreatedAt = DateTime.UtcNow
        };

        // Act: Insert order
        await _orderRepo.InsertAsync(order, CancellationToken.None);

        // Retrieve by ID
        OrderTracking? retrieved = await _orderRepo.GetByIdAsync(order.OrderId, CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(order.OrderId, retrieved.OrderId);
        Assert.Equal(12345, retrieved.IbkrOrderId);
        Assert.Equal("submitted", retrieved.Status);
        Assert.Equal("SPY", retrieved.Symbol);
        Assert.Equal(10, retrieved.Quantity);
        Assert.Equal(450.0m, retrieved.LimitPrice);
    }

    [Fact(DisplayName = "TEST-22-35: OrderTrackingRepository updates order status")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-35")]
    public async Task TEST_22_35_OrderTrackingRepositoryUpdatesOrderStatus()
    {
        // Arrange
        OrderTracking order = new()
        {
            OrderId = Guid.NewGuid().ToString(),
            IbkrOrderId = 67890,
            CampaignId = Guid.NewGuid().ToString(),
            Status = "submitted",
            Symbol = "SPY",
            Quantity = 5,
            OrderType = "MKT",
            CreatedAt = DateTime.UtcNow
        };

        await _orderRepo.InsertAsync(order, CancellationToken.None);

        // Act: Update status to filled
        await _orderRepo.UpdateStatusAsync(order.OrderId, "filled", DateTime.UtcNow, CancellationToken.None);

        // Retrieve updated order
        OrderTracking? updated = await _orderRepo.GetByIdAsync(order.OrderId, CancellationToken.None);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("filled", updated.Status);
        Assert.NotNull(updated.FilledAt);
    }
}
