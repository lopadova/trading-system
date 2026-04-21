using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel.Data;
using SharedKernel.Ibkr;
using SharedKernel.Observability;
using Xunit;

namespace SharedKernel.Tests.Observability;

/// <summary>
/// Unit tests for <see cref="HealthState"/>. Covers the three subsystem-status axes:
/// IBKR (null / connected / disconnected / throws), DB (null / ok / error), and
/// the overall status-aggregation logic.
/// </summary>
public sealed class HealthStateTests : IDisposable
{
    private readonly string _tempDbPath;

    public HealthStateTests()
    {
        // Temp SQLite file — SqliteConnectionFactory creates it on first OpenAsync().
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"healthstate-test-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        // Best-effort cleanup; ignore errors if the file is locked.
        try
        {
            if (File.Exists(_tempDbPath))
            {
                File.Delete(_tempDbPath);
            }
        }
        catch
        {
            // Intentionally empty — test cleanup should never fail CI.
        }
    }

    [Fact]
    public void Current_WithAllSubsystemsHealthy_ReportsOk()
    {
        // Arrange — IBKR mock returns connected, DB factory points at a writable temp file.
        Mock<IIbkrClient> ibkr = new();
        ibkr.Setup(x => x.IsConnected).Returns(true);
        SqliteConnectionFactory db = new(_tempDbPath);

        HealthState sut = new("supervisor", ibkr.Object, db, NullLogger<HealthState>.Instance);

        // Act
        HealthReport report = sut.Current();

        // Assert
        Assert.Equal("supervisor", report.Service);
        Assert.Equal("ok", report.Status);
        Assert.Equal("connected", report.Checks["ibkr"]);
        Assert.Equal("ok", report.Checks["db"]);
        Assert.True(report.Uptime >= TimeSpan.Zero);
        Assert.False(string.IsNullOrWhiteSpace(report.Version));
    }

    [Fact]
    public void Current_WithIbkrDisconnected_ReportsDegraded()
    {
        // IBKR down but DB ok = degraded (partial outage).
        Mock<IIbkrClient> ibkr = new();
        ibkr.Setup(x => x.IsConnected).Returns(false);
        SqliteConnectionFactory db = new(_tempDbPath);

        HealthState sut = new("supervisor", ibkr.Object, db, NullLogger<HealthState>.Instance);

        HealthReport report = sut.Current();

        Assert.Equal("degraded", report.Status);
        Assert.Equal("disconnected", report.Checks["ibkr"]);
        Assert.Equal("ok", report.Checks["db"]);
    }

    [Fact]
    public void Current_WithIbkrThrowing_ReportsDown()
    {
        // A throw from IBKR.IsConnected is treated as "error" → overall down.
        Mock<IIbkrClient> ibkr = new();
        ibkr.Setup(x => x.IsConnected).Throws(new InvalidOperationException("boom"));
        SqliteConnectionFactory db = new(_tempDbPath);

        HealthState sut = new("supervisor", ibkr.Object, db, NullLogger<HealthState>.Instance);

        HealthReport report = sut.Current();

        Assert.Equal("down", report.Status);
        Assert.Equal("error", report.Checks["ibkr"]);
    }

    [Fact]
    public void Current_WithUnreachableDb_ReportsDown()
    {
        // Invalid DB path that the factory can create but probe can fail on —
        // we simulate by using a path inside a non-writable directory.
        Mock<IIbkrClient> ibkr = new();
        ibkr.Setup(x => x.IsConnected).Returns(true);

        // Pick a path inside a directory that does not exist and whose parent is writable.
        // SqliteConnectionFactory creates the dir, so we instead pass a file where the path
        // is a directory name — that forces a failure on open. Use a subfolder with an
        // already-existing directory to fake it: create a directory with the same name as
        // the intended DB file so Sqlite cannot open it.
        string fakeDbDir = Path.Combine(Path.GetTempPath(), $"healthstate-fakedir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fakeDbDir);
        try
        {
            // Passing a directory path as the DB file triggers an IO error during open.
            SqliteConnectionFactory db = new(fakeDbDir);

            HealthState sut = new("supervisor", ibkr.Object, db, NullLogger<HealthState>.Instance);
            HealthReport report = sut.Current();

            Assert.Equal("down", report.Status);
            Assert.Equal("error", report.Checks["db"]);
        }
        finally
        {
            try { Directory.Delete(fakeDbDir, recursive: true); } catch { /* cleanup only */ }
        }
    }

    [Fact]
    public void Current_WithNullSubsystems_ReportsOk()
    {
        // A service that doesn't wire IBKR or DB should still emit a valid 'ok' report.
        HealthState sut = new("dashboard-probe", ibkr: null, db: null, NullLogger<HealthState>.Instance);

        HealthReport report = sut.Current();

        Assert.Equal("ok", report.Status);
        Assert.Equal("not-configured", report.Checks["ibkr"]);
        Assert.Equal("not-configured", report.Checks["db"]);
        Assert.Equal("dashboard-probe", report.Service);
    }

    [Fact]
    public void Constructor_WithBlankServiceName_Throws()
    {
        // Explicit contract test: the service-name is load-bearing (it labels every shipped log line).
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            new HealthState("", ibkr: null, db: null, NullLogger<HealthState>.Instance));
        Assert.Contains("serviceName", ex.Message);
    }
}
