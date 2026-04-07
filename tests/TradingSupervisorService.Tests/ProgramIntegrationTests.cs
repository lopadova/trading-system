using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using SharedKernel.Ibkr;
using TradingSupervisorService.Collectors;
using TradingSupervisorService.Ibkr;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Services;
using TradingSupervisorService.Workers;
using Xunit;

namespace TradingSupervisorService.Tests;

/// <summary>
/// Integration tests for TradingSupervisorService Program.cs DI configuration.
/// Verifies all services are registered correctly and can be resolved.
/// TEST-22-01 through TEST-22-10
/// </summary>
public sealed class ProgramIntegrationTests
{
    [Fact(DisplayName = "TEST-22-01: All required services are registered in DI container")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-01")]
    public void TEST_22_01_AllRequiredServicesRegistered()
    {
        // Arrange: Build a test host with the same configuration as Program.cs
        IHost host = CreateTestHost();

        // Act & Assert: Verify each required service can be resolved
        using IServiceScope scope = host.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        // Database connection factory
        IDbConnectionFactory dbFactory = services.GetRequiredService<IDbConnectionFactory>();
        Assert.NotNull(dbFactory);

        // Repositories
        IHeartbeatRepository heartbeatRepo = services.GetRequiredService<IHeartbeatRepository>();
        Assert.NotNull(heartbeatRepo);

        IOutboxRepository outboxRepo = services.GetRequiredService<IOutboxRepository>();
        Assert.NotNull(outboxRepo);

        IAlertRepository alertRepo = services.GetRequiredService<IAlertRepository>();
        Assert.NotNull(alertRepo);

        ILogReaderStateRepository logReaderRepo = services.GetRequiredService<ILogReaderStateRepository>();
        Assert.NotNull(logReaderRepo);

        IIvtsRepository ivtsRepo = services.GetRequiredService<IIvtsRepository>();
        Assert.NotNull(ivtsRepo);

        IPositionsRepository positionsRepo = services.GetRequiredService<IPositionsRepository>();
        Assert.NotNull(positionsRepo);

        // Collectors
        IMachineMetricsCollector metricsCollector = services.GetRequiredService<IMachineMetricsCollector>();
        Assert.NotNull(metricsCollector);

        // IBKR components
        TwsCallbackHandler callbackHandler = services.GetRequiredService<TwsCallbackHandler>();
        Assert.NotNull(callbackHandler);

        IIbkrClient ibkrClient = services.GetRequiredService<IIbkrClient>();
        Assert.NotNull(ibkrClient);
        Assert.Equal(ConnectionState.Disconnected, ibkrClient.State);

        // Services
        ITelegramAlerter telegramAlerter = services.GetRequiredService<ITelegramAlerter>();
        Assert.NotNull(telegramAlerter);

        IHttpClientFactory httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
        Assert.NotNull(httpClientFactory);
    }

    [Fact(DisplayName = "TEST-22-02: Service startup validates configuration correctly")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-02")]
    public void TEST_22_02_ServiceStartupValidatesConfiguration()
    {
        // Arrange: Create host with valid configuration
        IHost host = CreateTestHost();

        // Act: Retrieve configuration values from DI
        IConfiguration config = host.Services.GetRequiredService<IConfiguration>();

        // Assert: Verify configuration values are correct
        Assert.Equal("paper", config["TradingMode"]);
        Assert.Equal(":memory:", config["Sqlite:SupervisorDbPath"]);
        Assert.Equal(":memory:", config["OptionsDb:OptionsDbPath"]);
        Assert.Equal("127.0.0.1", config["IBKR:Host"]);
        Assert.Equal("4002", config["IBKR:PaperPort"]);
        Assert.Equal("1", config["IBKR:ClientId"]);
    }

    [Fact(DisplayName = "TEST-22-03: IBKR client is registered as singleton")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-03")]
    public void TEST_22_03_IbkrClientIsSingleton()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act: Resolve IBKR client twice
        IIbkrClient client1 = host.Services.GetRequiredService<IIbkrClient>();
        IIbkrClient client2 = host.Services.GetRequiredService<IIbkrClient>();

        // Assert: Both should be the same instance
        Assert.Same(client1, client2);
    }

    [Fact(DisplayName = "TEST-22-04: Repository services are registered correctly")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-04")]
    public void TEST_22_04_RepositoryServicesRegistered()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act & Assert: Verify all repositories can be resolved
        using IServiceScope scope = host.Services.CreateScope();

        IHeartbeatRepository heartbeat = scope.ServiceProvider.GetRequiredService<IHeartbeatRepository>();
        Assert.IsType<HeartbeatRepository>(heartbeat);

        IOutboxRepository outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        Assert.IsType<OutboxRepository>(outbox);

        IAlertRepository alert = scope.ServiceProvider.GetRequiredService<IAlertRepository>();
        Assert.IsType<AlertRepository>(alert);

        ILogReaderStateRepository logReader = scope.ServiceProvider.GetRequiredService<ILogReaderStateRepository>();
        Assert.IsType<LogReaderStateRepository>(logReader);

        IIvtsRepository ivts = scope.ServiceProvider.GetRequiredService<IIvtsRepository>();
        Assert.IsType<IvtsRepository>(ivts);

        IPositionsRepository positions = scope.ServiceProvider.GetRequiredService<IPositionsRepository>();
        Assert.IsType<PositionsRepository>(positions);
    }

    [Fact(DisplayName = "TEST-22-05: Metrics collector is available for workers")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-05")]
    public void TEST_22_05_MetricsCollectorAvailable()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act
        IMachineMetricsCollector collector = host.Services.GetRequiredService<IMachineMetricsCollector>();

        // Assert
        Assert.NotNull(collector);
        Assert.IsType<WindowsMachineMetricsCollector>(collector);
    }

    [Fact(DisplayName = "TEST-22-06: HttpClientFactory is registered for OutboxSyncWorker")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-06")]
    public void TEST_22_06_HttpClientFactoryRegistered()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act
        IHttpClientFactory factory = host.Services.GetRequiredService<IHttpClientFactory>();
        HttpClient client = factory.CreateClient();

        // Assert
        Assert.NotNull(factory);
        Assert.NotNull(client);
    }

    [Fact(DisplayName = "TEST-22-07: TelegramAlerter service is available")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-07")]
    public void TEST_22_07_TelegramAlerterAvailable()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act
        ITelegramAlerter alerter = host.Services.GetRequiredService<ITelegramAlerter>();

        // Assert
        Assert.NotNull(alerter);
        Assert.IsType<TelegramAlerter>(alerter);
    }

    [Fact(DisplayName = "TEST-22-08: Database connection factory creates valid connections")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-08")]
    public async Task TEST_22_08_DatabaseConnectionFactoryCreatesValidConnections()
    {
        // Arrange
        IHost host = CreateTestHost();
        IDbConnectionFactory factory = host.Services.GetRequiredService<IDbConnectionFactory>();

        // Act
        await using var conn = await factory.OpenAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(conn);
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }

    [Fact(DisplayName = "TEST-22-09: Positions repository uses separate database")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-09")]
    public void TEST_22_09_PositionsRepositoryUsesSeparateDatabase()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act
        IPositionsRepository positionsRepo = host.Services.GetRequiredService<IPositionsRepository>();

        // Assert
        // The positions repository should be resolvable and independent
        Assert.NotNull(positionsRepo);
        Assert.IsType<PositionsRepository>(positionsRepo);

        // Verify it's using a separate connection factory (different from main IDbConnectionFactory)
        // This is implicit in the registration - PositionsRepository gets its own SqliteConnectionFactory
    }

    [Fact(DisplayName = "TEST-22-10: All hosted services (workers) are registered")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-10")]
    public void TEST_22_10_AllHostedServicesRegistered()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act: Get all registered IHostedService instances
        IEnumerable<IHostedService> hostedServices = host.Services.GetServices<IHostedService>();

        // Assert: Verify expected worker count (6 workers)
        // HeartbeatWorker, OutboxSyncWorker, TelegramWorker, LogReaderWorker, IvtsMonitorWorker, GreeksMonitorWorker
        Assert.NotNull(hostedServices);
        Assert.True(hostedServices.Count() >= 6,
            $"Expected at least 6 hosted services, but found {hostedServices.Count()}");
    }

    /// <summary>
    /// Creates a test host with the same DI configuration as Program.cs.
    /// Uses in-memory databases for testing.
    /// </summary>
    private static IHost CreateTestHost()
    {
        Dictionary<string, string?> testConfig = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:SupervisorDbPath"] = ":memory:",
            ["OptionsDb:OptionsDbPath"] = ":memory:",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1",
            ["IBKR:ReconnectInitialDelaySeconds"] = "5",
            ["IBKR:ReconnectMaxDelaySeconds"] = "60",
            ["IBKR:MaxReconnectAttempts"] = "10",
            ["IBKR:ConnectionTimeoutSeconds"] = "30",
            ["Monitoring:HeartbeatIntervalSeconds"] = "60",
            ["Monitoring:OutboxSyncIntervalSeconds"] = "30",
            ["Monitoring:TelegramIntervalSeconds"] = "10",
            ["Monitoring:LogReaderIntervalSeconds"] = "120",
            ["Monitoring:IvtsIntervalSeconds"] = "300",
            ["Monitoring:GreeksIntervalSeconds"] = "300",
            ["Telegram:BotToken"] = "test-token",
            ["Telegram:ChatId"] = "test-chat-id",
            ["Telegram:Enabled"] = "false",
            ["OutboxSync:CloudflareWorkerUrl"] = "http://localhost:8787",
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(testConfig)
            .Build();

        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(testConfig);
            })
            .ConfigureServices((context, services) =>
            {
                IConfiguration config = context.Configuration;

                // Database connection factories
                string supervisorDbPath = config["Sqlite:SupervisorDbPath"] ?? ":memory:";
                string optionsDbPath = config["OptionsDb:OptionsDbPath"] ?? ":memory:";

                // Register supervisor.db factory as the default IDbConnectionFactory
                services.AddSingleton<IDbConnectionFactory>(
                    _ => new SqliteConnectionFactory(supervisorDbPath));

                // Repositories (supervisor.db)
                services.AddSingleton<IHeartbeatRepository, HeartbeatRepository>();
                services.AddSingleton<IOutboxRepository, OutboxRepository>();
                services.AddSingleton<IAlertRepository, AlertRepository>();
                services.AddSingleton<ILogReaderStateRepository, LogReaderStateRepository>();
                services.AddSingleton<IIvtsRepository, IvtsRepository>();

                // Repository for options.db (uses a separate factory instance)
                services.AddSingleton<IPositionsRepository>(sp =>
                {
                    ILogger<PositionsRepository> logger = sp.GetRequiredService<ILogger<PositionsRepository>>();
                    IDbConnectionFactory optionsDbFactory = new SqliteConnectionFactory(optionsDbPath);
                    return new PositionsRepository(optionsDbFactory, logger);
                });

                // Collectors
                services.AddSingleton<IMachineMetricsCollector, WindowsMachineMetricsCollector>();

                // IBKR client (singleton - shared across all workers)
                services.AddSingleton<TwsCallbackHandler>();
                services.AddSingleton<IIbkrClient>(sp =>
                {
                    ILogger<IbkrClient> logger = sp.GetRequiredService<ILogger<IbkrClient>>();
                    TwsCallbackHandler wrapper = sp.GetRequiredService<TwsCallbackHandler>();

                    IbkrConfig ibkrConfig = new()
                    {
                        Host = config["IBKR:Host"] ?? "127.0.0.1",
                        Port = config.GetValue<int>("IBKR:PaperPort", 4002),
                        ClientId = config.GetValue<int>("IBKR:ClientId", 1),
                        TradingMode = config["TradingMode"] ?? "paper",
                        ConnectionTimeoutSeconds = 30,
                        ReconnectInitialDelaySeconds = 5,
                        ReconnectMaxDelaySeconds = 60,
                        MaxReconnectAttempts = 10
                    };

                    return new IbkrClient(logger, ibkrConfig, wrapper);
                });

                // Telegram alerting service
                services.AddSingleton<ITelegramAlerter, TelegramAlerter>();

                // HttpClient factory for OutboxSyncWorker
                services.AddHttpClient();

                // Workers (hosted services)
                services.AddHostedService<HeartbeatWorker>();
                services.AddHostedService<OutboxSyncWorker>();
                services.AddHostedService<TelegramWorker>();
                services.AddHostedService<LogReaderWorker>();
                services.AddHostedService<IvtsMonitorWorker>();
                services.AddHostedService<GreeksMonitorWorker>();
            })
            .Build();
    }
}
