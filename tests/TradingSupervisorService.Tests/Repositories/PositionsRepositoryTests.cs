using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel.Data;
using SharedKernel.Tests.Helpers;
using TradingSupervisorService.Repositories;
using Xunit;

namespace TradingSupervisorService.Tests.Repositories;

/// <summary>
/// Unit tests for PositionsRepository.
/// Tests reading active_positions data from options.db with Greeks filtering.
/// </summary>
public sealed class PositionsRepositoryTests : IAsyncLifetime
{
    private InMemoryConnectionFactory? _dbFactory;
    private PositionsRepository? _repository;

    public async Task InitializeAsync()
    {
        // Create in-memory database with active_positions schema
        _dbFactory = new InMemoryConnectionFactory();

        // Create active_positions table with Greeks columns
        await using var conn = await _dbFactory.OpenAsync(CancellationToken.None);
        await conn.ExecuteAsync("""
            CREATE TABLE active_positions (
                position_id       TEXT PRIMARY KEY,
                campaign_id       TEXT NOT NULL,
                symbol            TEXT NOT NULL,
                contract_symbol   TEXT NOT NULL,
                strategy_name     TEXT NOT NULL,
                quantity          INTEGER NOT NULL,
                entry_price       REAL NOT NULL,
                current_price     REAL,
                unrealized_pnl    REAL,
                stop_loss         REAL,
                take_profit       REAL,
                delta             REAL,
                gamma             REAL,
                theta             REAL,
                vega              REAL,
                implied_volatility REAL,
                greeks_updated_at TEXT,
                underlying_price  REAL,
                opened_at         TEXT NOT NULL,
                updated_at        TEXT NOT NULL,
                metadata_json     TEXT
            );

            CREATE INDEX idx_positions_campaign ON active_positions(campaign_id);
            CREATE INDEX idx_positions_symbol ON active_positions(symbol);
            CREATE INDEX idx_positions_delta ON active_positions(delta) WHERE delta IS NOT NULL;
            CREATE INDEX idx_positions_gamma ON active_positions(gamma) WHERE gamma IS NOT NULL;
            """);

        Mock<ILogger<PositionsRepository>> loggerMock = new();
        _repository = new PositionsRepository(_dbFactory, loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        if (_dbFactory != null)
        {
            await _dbFactory.DisposeAsync();
        }
    }

    [Fact]
    [Trait("TestId", "TEST-19-11")]
    public async Task GetActivePositionsWithGreeksAsync_WithNoPositions_ReturnsEmptyList()
    {
        // Act
        IReadOnlyList<ActivePositionRecord> positions = await _repository!.GetActivePositionsWithGreeksAsync(CancellationToken.None);

        // Assert
        Assert.Empty(positions);
    }

    [Fact]
    [Trait("TestId", "TEST-19-12")]
    public async Task GetActivePositionsWithGreeksAsync_WithPositionsWithoutGreeks_ReturnsEmptyList()
    {
        // Arrange
        // Insert position WITHOUT Greeks data (delta = NULL)
        await using var conn = await _dbFactory!.OpenAsync(CancellationToken.None);
        await conn.ExecuteAsync("""
            INSERT INTO active_positions
                (position_id, campaign_id, symbol, contract_symbol, strategy_name,
                 quantity, entry_price, opened_at, updated_at)
            VALUES
                ('pos-001', 'camp-001', 'SPY', 'SPY 250101C450', 'TestStrategy',
                 10, 1.50, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z')
            """);

        // Act
        IReadOnlyList<ActivePositionRecord> positions = await _repository!.GetActivePositionsWithGreeksAsync(CancellationToken.None);

        // Assert
        // Should return empty list because delta IS NULL (Greeks not calculated)
        Assert.Empty(positions);
    }

    [Fact]
    [Trait("TestId", "TEST-19-13")]
    public async Task GetActivePositionsWithGreeksAsync_WithPositionsWithGreeks_ReturnsPositions()
    {
        // Arrange
        // Insert position WITH Greeks data
        await using var conn = await _dbFactory!.OpenAsync(CancellationToken.None);
        await conn.ExecuteAsync("""
            INSERT INTO active_positions
                (position_id, campaign_id, symbol, contract_symbol, strategy_name,
                 quantity, entry_price, delta, gamma, theta, vega, implied_volatility,
                 greeks_updated_at, underlying_price, opened_at, updated_at)
            VALUES
                ('pos-001', 'camp-001', 'SPY', 'SPY 250101C450', 'TestStrategy',
                 10, 1.50, 0.65, 0.03, -25.0, 80.0, 0.20,
                 '2025-01-01T10:30:00Z', 450.0, '2025-01-01T10:00:00Z', '2025-01-01T10:30:00Z')
            """);

        // Act
        IReadOnlyList<ActivePositionRecord> positions = await _repository!.GetActivePositionsWithGreeksAsync(CancellationToken.None);

        // Assert
        Assert.Single(positions);
        ActivePositionRecord position = positions[0];
        Assert.Equal("pos-001", position.PositionId);
        Assert.Equal("SPY", position.Symbol);
        Assert.Equal(0.65, position.Delta);
        Assert.Equal(0.03, position.Gamma);
        Assert.Equal(-25.0, position.Theta);
        Assert.Equal(80.0, position.Vega);
        Assert.Equal(0.20, position.ImpliedVolatility);
    }

    [Fact]
    [Trait("TestId", "TEST-19-14")]
    public async Task GetActivePositionsWithGreeksAsync_WithMultiplePositions_ReturnsOrderedBySymbol()
    {
        // Arrange
        // Insert multiple positions with Greeks (different symbols)
        await using var conn = await _dbFactory!.OpenAsync(CancellationToken.None);
        await conn.ExecuteAsync("""
            INSERT INTO active_positions
                (position_id, campaign_id, symbol, contract_symbol, strategy_name,
                 quantity, entry_price, delta, gamma, theta, vega,
                 opened_at, updated_at)
            VALUES
                ('pos-001', 'camp-001', 'TSLA', 'TSLA 250101C200', 'TestStrategy', 5, 2.00, 0.70, 0.04, -30.0, 90.0, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z'),
                ('pos-002', 'camp-001', 'AAPL', 'AAPL 250101C150', 'TestStrategy', 3, 1.80, 0.55, 0.02, -20.0, 70.0, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z'),
                ('pos-003', 'camp-001', 'SPY', 'SPY 250101C450', 'TestStrategy', 10, 1.50, 0.65, 0.03, -25.0, 80.0, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z')
            """);

        // Act
        IReadOnlyList<ActivePositionRecord> positions = await _repository!.GetActivePositionsWithGreeksAsync(CancellationToken.None);

        // Assert
        Assert.Equal(3, positions.Count);
        // Should be ordered by symbol ASC: AAPL, SPY, TSLA
        Assert.Equal("AAPL", positions[0].Symbol);
        Assert.Equal("SPY", positions[1].Symbol);
        Assert.Equal("TSLA", positions[2].Symbol);
    }

    [Fact]
    [Trait("TestId", "TEST-19-15")]
    public async Task GetPositionsByCampaignAsync_WithValidCampaignId_ReturnsPositions()
    {
        // Arrange
        await using var conn = await _dbFactory!.OpenAsync(CancellationToken.None);
        await conn.ExecuteAsync("""
            INSERT INTO active_positions
                (position_id, campaign_id, symbol, contract_symbol, strategy_name,
                 quantity, entry_price, delta, gamma, theta, vega,
                 opened_at, updated_at)
            VALUES
                ('pos-001', 'camp-001', 'SPY', 'SPY 250101C450', 'TestStrategy', 10, 1.50, 0.65, 0.03, -25.0, 80.0, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z'),
                ('pos-002', 'camp-001', 'SPY', 'SPY 250101C455', 'TestStrategy', 5, 1.60, 0.70, 0.04, -30.0, 90.0, '2025-01-01T11:00:00Z', '2025-01-01T11:00:00Z'),
                ('pos-003', 'camp-002', 'AAPL', 'AAPL 250101C150', 'TestStrategy', 3, 1.80, 0.55, 0.02, -20.0, 70.0, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z')
            """);

        // Act
        IReadOnlyList<ActivePositionRecord> positions = await _repository!.GetPositionsByCampaignAsync("camp-001", CancellationToken.None);

        // Assert
        Assert.Equal(2, positions.Count);
        Assert.All(positions, p => Assert.Equal("camp-001", p.CampaignId));
        // Should be ordered by opened_at DESC (most recent first)
        Assert.Equal("pos-002", positions[0].PositionId);
        Assert.Equal("pos-001", positions[1].PositionId);
    }

    [Fact]
    [Trait("TestId", "TEST-19-16")]
    public async Task GetPositionsByCampaignAsync_WithNullCampaignId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _repository!.GetPositionsByCampaignAsync(null!, CancellationToken.None));
    }

    [Fact]
    [Trait("TestId", "TEST-19-17")]
    public async Task GetPositionByIdAsync_WithValidPositionId_ReturnsPosition()
    {
        // Arrange
        await using var conn = await _dbFactory!.OpenAsync(CancellationToken.None);
        await conn.ExecuteAsync("""
            INSERT INTO active_positions
                (position_id, campaign_id, symbol, contract_symbol, strategy_name,
                 quantity, entry_price, delta, gamma, theta, vega,
                 opened_at, updated_at)
            VALUES
                ('pos-001', 'camp-001', 'SPY', 'SPY 250101C450', 'TestStrategy', 10, 1.50, 0.65, 0.03, -25.0, 80.0, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z')
            """);

        // Act
        ActivePositionRecord? position = await _repository!.GetPositionByIdAsync("pos-001", CancellationToken.None);

        // Assert
        Assert.NotNull(position);
        Assert.Equal("pos-001", position.PositionId);
        Assert.Equal("SPY", position.Symbol);
    }

    [Fact]
    [Trait("TestId", "TEST-19-18")]
    public async Task GetPositionByIdAsync_WithNonExistentPositionId_ReturnsNull()
    {
        // Act
        ActivePositionRecord? position = await _repository!.GetPositionByIdAsync("non-existent", CancellationToken.None);

        // Assert
        Assert.Null(position);
    }

    [Fact]
    [Trait("TestId", "TEST-19-19")]
    public async Task GetPositionCountsBySymbolAsync_WithMultipleSymbols_ReturnsCountDictionary()
    {
        // Arrange
        await using var conn = await _dbFactory!.OpenAsync(CancellationToken.None);
        await conn.ExecuteAsync("""
            INSERT INTO active_positions
                (position_id, campaign_id, symbol, contract_symbol, strategy_name,
                 quantity, entry_price, opened_at, updated_at)
            VALUES
                ('pos-001', 'camp-001', 'SPY', 'SPY 250101C450', 'TestStrategy', 10, 1.50, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z'),
                ('pos-002', 'camp-001', 'SPY', 'SPY 250101C455', 'TestStrategy', 5, 1.60, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z'),
                ('pos-003', 'camp-001', 'SPY', 'SPY 250101C460', 'TestStrategy', 3, 1.70, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z'),
                ('pos-004', 'camp-001', 'AAPL', 'AAPL 250101C150', 'TestStrategy', 2, 1.80, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z'),
                ('pos-005', 'camp-001', 'AAPL', 'AAPL 250101C155', 'TestStrategy', 4, 1.90, '2025-01-01T10:00:00Z', '2025-01-01T10:00:00Z')
            """);

        // Act
        Dictionary<string, int> counts = await _repository!.GetPositionCountsBySymbolAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, counts.Count);
        Assert.Equal(3, counts["SPY"]);   // 3 SPY positions
        Assert.Equal(2, counts["AAPL"]);  // 2 AAPL positions
    }

    [Fact]
    [Trait("TestId", "TEST-19-20")]
    public async Task GetPositionCountsBySymbolAsync_WithNoPositions_ReturnsEmptyDictionary()
    {
        // Act
        Dictionary<string, int> counts = await _repository!.GetPositionCountsBySymbolAsync(CancellationToken.None);

        // Assert
        Assert.Empty(counts);
    }
}
