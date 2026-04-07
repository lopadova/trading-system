using Dapper;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Tests.Helpers;
using TradingSupervisorService.Repositories;
using Xunit;

namespace TradingSupervisorService.Tests.Repositories;

/// <summary>
/// Unit tests for AlertRepository.
/// Uses in-memory SQLite database for fast, isolated tests.
/// </summary>
public sealed class AlertRepositoryTests : IAsyncDisposable
{
    private readonly InMemoryConnectionFactory _dbFactory;
    private readonly ILogger<AlertRepository> _logger;
    private readonly AlertRepository _sut;  // System Under Test

    public AlertRepositoryTests()
    {
        _dbFactory = new InMemoryConnectionFactory();
        _logger = new LoggerFactory().CreateLogger<AlertRepository>();
        _sut = new AlertRepository(_dbFactory, _logger);

        // Create the alert_history table schema
        InitializeSchemaAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeSchemaAsync()
    {
        await using var conn = await _dbFactory.OpenAsync(CancellationToken.None);
        await conn.ExecuteAsync("""
            CREATE TABLE alert_history (
                alert_id       TEXT PRIMARY KEY,
                alert_type     TEXT NOT NULL,
                severity       TEXT NOT NULL,
                message        TEXT NOT NULL,
                details_json   TEXT,
                source_service TEXT NOT NULL,
                created_at     TEXT NOT NULL DEFAULT (datetime('now')),
                resolved_at    TEXT,
                resolved_by    TEXT
            );
            CREATE INDEX idx_alerts_unresolved ON alert_history(resolved_at) WHERE resolved_at IS NULL;
            CREATE INDEX idx_alerts_type_severity ON alert_history(alert_type, severity);
            CREATE INDEX idx_alerts_created ON alert_history(created_at DESC);
            """);
    }

    [Fact]
    [Trait("TestId", "TEST-08-01")]
    public async Task InsertAsync_ValidAlert_InsertsSuccessfully()
    {
        // Arrange
        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "HeartbeatMissing",
            Severity = "warning",
            Message = "Service X has not sent heartbeat in 5 minutes",
            DetailsJson = "{\"service\":\"TradingSupervisor\",\"last_seen\":\"2026-04-05T10:00:00Z\"}",
            SourceService = "TradingSupervisor",
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        // Act
        await _sut.InsertAsync(alert, CancellationToken.None);

        // Assert
        IReadOnlyList<AlertRecord> unresolved = await _sut.GetUnresolvedAsync(10, CancellationToken.None);
        Assert.Single(unresolved);
        Assert.Equal(alert.AlertId, unresolved[0].AlertId);
        Assert.Equal(alert.Message, unresolved[0].Message);
        Assert.Equal(alert.Severity, unresolved[0].Severity);
    }

    [Fact]
    [Trait("TestId", "TEST-08-02")]
    public async Task InsertAsync_NullAlert_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _sut.InsertAsync(null!, CancellationToken.None);
        });
    }

    [Fact]
    [Trait("TestId", "TEST-08-03")]
    public async Task GetUnresolvedAsync_MultipleAlerts_ReturnsOnlyUnresolved()
    {
        // Arrange
        AlertRecord resolved = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "Test",
            Severity = "info",
            Message = "Resolved alert",
            SourceService = "Test",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            ResolvedAt = DateTime.UtcNow.ToString("O"),
            ResolvedBy = "auto"
        };

        AlertRecord unresolved = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "Test",
            Severity = "warning",
            Message = "Unresolved alert",
            SourceService = "Test",
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _sut.InsertAsync(resolved, CancellationToken.None);
        await _sut.InsertAsync(unresolved, CancellationToken.None);

        // Act
        IReadOnlyList<AlertRecord> result = await _sut.GetUnresolvedAsync(10, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(unresolved.AlertId, result[0].AlertId);
    }

    [Fact]
    [Trait("TestId", "TEST-08-04")]
    public async Task GetBySeverityAsync_FiltersCorrectly()
    {
        // Arrange
        DateTime since = DateTime.UtcNow.AddHours(-1);

        AlertRecord critical = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "Test",
            Severity = "critical",
            Message = "Critical alert",
            SourceService = "Test",
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        AlertRecord warning = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "Test",
            Severity = "warning",
            Message = "Warning alert",
            SourceService = "Test",
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _sut.InsertAsync(critical, CancellationToken.None);
        await _sut.InsertAsync(warning, CancellationToken.None);

        // Act
        IReadOnlyList<AlertRecord> criticalResults = await _sut.GetBySeverityAsync(
            AlertSeverity.Critical, since, 10, CancellationToken.None);

        // Assert
        Assert.Single(criticalResults);
        Assert.Equal(critical.AlertId, criticalResults[0].AlertId);
        Assert.Equal("critical", criticalResults[0].Severity);
    }

    [Fact]
    [Trait("TestId", "TEST-08-05")]
    public async Task ResolveAsync_ExistingAlert_MarksAsResolved()
    {
        // Arrange
        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "Test",
            Severity = "warning",
            Message = "Test alert",
            SourceService = "Test",
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _sut.InsertAsync(alert, CancellationToken.None);

        // Act
        await _sut.ResolveAsync(alert.AlertId, "manual", CancellationToken.None);

        // Assert
        IReadOnlyList<AlertRecord> unresolved = await _sut.GetUnresolvedAsync(10, CancellationToken.None);
        Assert.Empty(unresolved);
    }

    [Fact]
    [Trait("TestId", "TEST-08-06")]
    public async Task GetUnresolvedCountsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        await _sut.InsertAsync(new AlertRecord
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "Test",
            Severity = "critical",
            Message = "Critical 1",
            SourceService = "Test",
            CreatedAt = DateTime.UtcNow.ToString("O")
        }, CancellationToken.None);

        await _sut.InsertAsync(new AlertRecord
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "Test",
            Severity = "critical",
            Message = "Critical 2",
            SourceService = "Test",
            CreatedAt = DateTime.UtcNow.ToString("O")
        }, CancellationToken.None);

        await _sut.InsertAsync(new AlertRecord
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "Test",
            Severity = "warning",
            Message = "Warning 1",
            SourceService = "Test",
            CreatedAt = DateTime.UtcNow.ToString("O")
        }, CancellationToken.None);

        // Act
        Dictionary<AlertSeverity, int> counts = await _sut.GetUnresolvedCountsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, counts[AlertSeverity.Critical]);
        Assert.Equal(1, counts[AlertSeverity.Warning]);
        Assert.Equal(0, counts[AlertSeverity.Info]);
        Assert.Equal(0, counts[AlertSeverity.Error]);
    }

    [Fact]
    [Trait("TestId", "TEST-08-07")]
    public async Task ResolveAsync_NonExistentAlert_LogsWarningButDoesNotThrow()
    {
        // Act & Assert (should not throw)
        await _sut.ResolveAsync("non-existent-id", "manual", CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbFactory.DisposeAsync();
    }
}
