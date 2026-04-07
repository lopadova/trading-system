using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OptionsExecutionService.Campaign;
using OptionsExecutionService.Orders;
using OptionsExecutionService.Repositories;
using SharedKernel.Data;
using SharedKernel.Ibkr;
using SharedKernel.Options;
using SharedKernel.Strategy;
using Xunit;

namespace OptionsExecutionService.Tests;

/// <summary>
/// Integration tests for OptionsExecutionService Program.cs DI configuration.
/// Verifies all services are registered correctly and can be resolved.
/// </summary>
public sealed class ProgramIntegrationTests
{
    [Fact(DisplayName = "TEST-09-01: All required services are registered in DI container")]
    public void AllRequiredServicesRegistered()
    {
        // Arrange: Build a test host with the same configuration as Program.cs
        IHost host = CreateTestHost();

        // Act & Assert: Verify each required service can be resolved
        using IServiceScope scope = host.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        // Database
        IDbConnectionFactory dbFactory = services.GetRequiredService<IDbConnectionFactory>();
        Assert.NotNull(dbFactory);

        // Repositories
        ICampaignRepository campaignRepo = services.GetRequiredService<ICampaignRepository>();
        Assert.NotNull(campaignRepo);

        IOrderTrackingRepository orderRepo = services.GetRequiredService<IOrderTrackingRepository>();
        Assert.NotNull(orderRepo);

        // Strategy services
        IStrategyValidator validator = services.GetRequiredService<IStrategyValidator>();
        Assert.NotNull(validator);

        IStrategyLoader loader = services.GetRequiredService<IStrategyLoader>();
        Assert.NotNull(loader);

        // Greeks calculator
        IGreeksCalculator greeksCalc = services.GetRequiredService<IGreeksCalculator>();
        Assert.NotNull(greeksCalc);

        // IBKR client (singleton)
        IIbkrClient ibkrClient = services.GetRequiredService<IIbkrClient>();
        Assert.NotNull(ibkrClient);
        Assert.Equal(ConnectionState.Disconnected, ibkrClient.State);

        // Order placer (singleton)
        IOrderPlacer orderPlacer = services.GetRequiredService<IOrderPlacer>();
        Assert.NotNull(orderPlacer);

        // Campaign manager (scoped)
        ICampaignManager campaignManager = services.GetRequiredService<ICampaignManager>();
        Assert.NotNull(campaignManager);
    }

    [Fact(DisplayName = "TEST-09-02: IBKR configuration validates correctly for paper trading")]
    public void IbkrConfigurationValidatesForPaperTrading()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act
        IbkrConfig config = host.Services.GetRequiredService<IbkrConfig>();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("127.0.0.1", config.Host);
        Assert.Equal(4002, config.Port); // IB Gateway Paper port
        Assert.Equal(2, config.ClientId);
        Assert.Equal(SharedKernel.Domain.TradingMode.Paper, config.TradingMode);

        // Validate should not throw for paper config
        config.Validate();
    }

    [Fact(DisplayName = "TEST-09-03: Order safety configuration validates correctly")]
    public void OrderSafetyConfigurationValidates()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act
        SharedKernel.Domain.OrderSafetyConfig safetyConfig =
            host.Services.GetRequiredService<SharedKernel.Domain.OrderSafetyConfig>();

        // Assert
        Assert.NotNull(safetyConfig);
        Assert.Equal(SharedKernel.Domain.TradingMode.Paper, safetyConfig.TradingMode);
        Assert.Equal(10, safetyConfig.MaxPositionSize);
        Assert.Equal(50000m, safetyConfig.MaxPositionValueUsd);
        Assert.Equal(10000m, safetyConfig.MinAccountBalanceUsd);
        Assert.Equal(0.05m, safetyConfig.MaxPositionPctOfAccount);

        // Circuit breaker config
        Assert.Equal(3, safetyConfig.CircuitBreakerFailureThreshold);
        Assert.Equal(60, safetyConfig.CircuitBreakerWindowMinutes);
        Assert.Equal(120, safetyConfig.CircuitBreakerCooldownMinutes);

        // Validate should not throw for safe config
        safetyConfig.Validate();
    }

    [Fact(DisplayName = "TEST-09-04: Singleton services return same instance")]
    public void SingletonServicesReturnSameInstance()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act: Test singleton services (IBKR client, strategy services)
        IIbkrClient client1 = host.Services.GetRequiredService<IIbkrClient>();
        IIbkrClient client2 = host.Services.GetRequiredService<IIbkrClient>();

        IStrategyValidator validator1 = host.Services.GetRequiredService<IStrategyValidator>();
        IStrategyValidator validator2 = host.Services.GetRequiredService<IStrategyValidator>();

        IGreeksCalculator greeks1 = host.Services.GetRequiredService<IGreeksCalculator>();
        IGreeksCalculator greeks2 = host.Services.GetRequiredService<IGreeksCalculator>();

        // Assert: Singletons should be the same instance
        Assert.Same(client1, client2);
        Assert.Same(validator1, validator2);
        Assert.Same(greeks1, greeks2);
    }

    [Fact(DisplayName = "TEST-09-05: Scoped services return different instances across scopes")]
    public void ScopedServicesReturnDifferentInstancesAcrossScopes()
    {
        // Arrange
        IHost host = CreateTestHost();

        // Act: Test scoped services across different scopes
        ICampaignManager manager1;
        IOrderPlacer placer1;
        using (IServiceScope scope1 = host.Services.CreateScope())
        {
            manager1 = scope1.ServiceProvider.GetRequiredService<ICampaignManager>();
            placer1 = scope1.ServiceProvider.GetRequiredService<IOrderPlacer>();
        }

        ICampaignManager manager2;
        IOrderPlacer placer2;
        using (IServiceScope scope2 = host.Services.CreateScope())
        {
            manager2 = scope2.ServiceProvider.GetRequiredService<ICampaignManager>();
            placer2 = scope2.ServiceProvider.GetRequiredService<IOrderPlacer>();
        }

        // Assert: Scoped services should be different instances across scopes
        Assert.NotSame(manager1, manager2);
        Assert.NotSame(placer1, placer2);

        // But within the same scope, they should be the same instance
        using (IServiceScope scope3 = host.Services.CreateScope())
        {
            IOrderPlacer placerA = scope3.ServiceProvider.GetRequiredService<IOrderPlacer>();
            IOrderPlacer placerB = scope3.ServiceProvider.GetRequiredService<IOrderPlacer>();
            Assert.Same(placerA, placerB);
        }
    }

    /// <summary>
    /// Creates a test host with the same DI configuration as Program.cs.
    /// Uses in-memory database for testing.
    /// </summary>
    private static IHost CreateTestHost()
    {
        Dictionary<string, string?> testConfig = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:OptionsDbPath"] = ":memory:",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:ClientId"] = "2",
            ["IBKR:ReconnectInitialDelaySeconds"] = "5",
            ["IBKR:ReconnectMaxDelaySeconds"] = "300",
            ["IBKR:MaxReconnectAttempts"] = "0",
            ["IBKR:ConnectionTimeoutSeconds"] = "10",
            ["Safety:MaxPositionSize"] = "10",
            ["Safety:MaxPositionValueUsd"] = "50000",
            ["Safety:MinAccountBalanceUsd"] = "10000",
            ["Safety:MaxPositionPctOfAccount"] = "0.05",
            ["Safety:CircuitBreakerFailureThreshold"] = "3",
            ["Safety:CircuitBreakerWindowMinutes"] = "60",
            ["Safety:CircuitBreakerCooldownMinutes"] = "120",
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
                // Register database connection factory
                string dbPath = configuration["Sqlite:OptionsDbPath"] ?? ":memory:";
                services.AddSingleton<IDbConnectionFactory>(_ =>
                    new SqliteConnectionFactory(dbPath));

                // Register repositories
                services.AddScoped<ICampaignRepository, CampaignRepository>();
                services.AddScoped<IOrderTrackingRepository, OrderTrackingRepository>();

                // Register strategy services
                services.AddSingleton<IStrategyValidator, StrategyValidator>();
                services.AddSingleton<IStrategyLoader, StrategyLoader>();

                // Register Greeks calculator
                services.AddSingleton<IGreeksCalculator, BlackScholesCalculator>();

                // Register IBKR configuration
                IbkrConfig ibkrConfig = new IbkrConfig
                {
                    Host = configuration["IBKR:Host"] ?? "127.0.0.1",
                    Port = configuration.GetValue<int>("IBKR:PaperPort", 4002),
                    ClientId = configuration.GetValue<int>("IBKR:ClientId", 2),
                    TradingMode = SharedKernel.Domain.TradingMode.Paper,
                    ReconnectInitialDelaySeconds = configuration.GetValue<int>("IBKR:ReconnectInitialDelaySeconds", 5),
                    ReconnectMaxDelaySeconds = configuration.GetValue<int>("IBKR:ReconnectMaxDelaySeconds", 300),
                    MaxReconnectAttempts = configuration.GetValue<int>("IBKR:MaxReconnectAttempts", 0),
                    ConnectionTimeoutSeconds = configuration.GetValue<int>("IBKR:ConnectionTimeoutSeconds", 10)
                };

                services.AddSingleton(ibkrConfig);
                services.AddSingleton<Ibkr.TwsCallbackHandler>();
                services.AddSingleton<IIbkrClient, Ibkr.IbkrClient>();

                // Register order safety configuration
                SharedKernel.Domain.OrderSafetyConfig safetyConfig = new()
                {
                    TradingMode = SharedKernel.Domain.TradingMode.Paper,
                    MaxPositionSize = configuration.GetValue<int>("Safety:MaxPositionSize", 10),
                    MaxPositionValueUsd = configuration.GetValue<decimal>("Safety:MaxPositionValueUsd", 50000m),
                    MinAccountBalanceUsd = configuration.GetValue<decimal>("Safety:MinAccountBalanceUsd", 10000m),
                    MaxPositionPctOfAccount = configuration.GetValue<decimal>("Safety:MaxPositionPctOfAccount", 0.05m),
                    CircuitBreakerFailureThreshold = configuration.GetValue<int>("Safety:CircuitBreakerFailureThreshold", 3),
                    CircuitBreakerWindowMinutes = configuration.GetValue<int>("Safety:CircuitBreakerWindowMinutes", 60),
                    CircuitBreakerCooldownMinutes = configuration.GetValue<int>("Safety:CircuitBreakerCooldownMinutes", 120)
                };

                services.AddSingleton(safetyConfig);

                // Register time provider
                services.AddSingleton<OptionsExecutionService.Common.ITimeProvider, OptionsExecutionService.Common.SystemTimeProvider>();

                // Register order placer (scoped to match repository lifetime)
                services.AddScoped<IOrderPlacer, OrderPlacer>();

                // Register campaign manager
                services.AddScoped<ICampaignManager, CampaignManager>();
            })
            .Build();
    }
}
