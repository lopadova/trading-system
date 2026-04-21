using Serilog;
using SharedKernel.Configuration;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using TradingSupervisorService.Bot;
using TradingSupervisorService.Collectors;
using TradingSupervisorService.Configuration;
using TradingSupervisorService.Ibkr;
using TradingSupervisorService.Migrations;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Services;
using TradingSupervisorService.Workers;

// Bootstrap Serilog early to capture startup errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/supervisor-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("TradingSupervisorService starting up");

    // Build configuration for early validation
    IConfiguration configForValidation = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
            optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)  // Developer overrides (not in git)
        .AddEnvironmentVariables()
        .AddCommandLine(args)
        .Build();

    // Validate configuration before building host
    Log.Information("Validating configuration...");
    SupervisorConfigurationValidator configValidator = new(
        configForValidation,
        new LoggerFactory().CreateLogger<SupervisorConfigurationValidator>());

    ValidationResult validationResult = configValidator.Validate();

    // Log warnings (non-blocking)
    foreach (string warning in validationResult.Warnings)
    {
        Log.Warning("Configuration warning: {Warning}", warning);
    }

    // Fail fast on critical errors
    if (!validationResult.IsValid)
    {
        Log.Fatal("Configuration validation failed with {Count} critical error(s):", validationResult.CriticalErrors.Count);
        foreach (string error in validationResult.CriticalErrors)
        {
            Log.Fatal("  - {Error}", error);
        }
        throw new InvalidOperationException(
            $"Configuration validation failed. Critical errors: {validationResult.CriticalErrors.Count}. " +
            "Service cannot start safely. Check logs for details.");
    }

    Log.Information("Configuration validation passed");

    IHost host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            // CRITICAL: Explicitly add appsettings.Local.json to override base settings
            // CreateDefaultBuilder doesn't include .Local.json by default
            config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
        })
        .UseWindowsService(options =>
        {
            options.ServiceName = "TradingSupervisorService";
        })
        .UseSerilog((context, services, loggerConfig) => loggerConfig
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "TradingSupervisorService")
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/supervisor-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30))
        .ConfigureServices((context, services) =>
        {
            IConfiguration config = context.Configuration;

            // Database connection factories
            // NOTE: Most repositories use supervisor.db, but PositionsRepository needs options.db
            string supervisorDbPath = config.GetValue<string>("Sqlite:SupervisorDbPath")
                ?? throw new InvalidOperationException("Sqlite:SupervisorDbPath not configured");
            string optionsDbPath = config.GetValue<string>("OptionsDb:OptionsDbPath", "data/options.db")
                ?? "data/options.db";

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
            services.AddSingleton<TwsCallbackHandler>(sp =>
            {
                ILogger<TwsCallbackHandler> handlerLogger = sp.GetRequiredService<ILogger<TwsCallbackHandler>>();

                // Connection state callback - logs state changes
                Action<ConnectionState> onConnectionStateChanged = state =>
                {
                    handlerLogger.LogInformation("IBKR connection state changed: {State}", state);
                };

                return new TwsCallbackHandler(handlerLogger, onConnectionStateChanged);
            });
            services.AddSingleton<IIbkrClient>(sp =>
            {
                ILogger<IbkrClient> logger = sp.GetRequiredService<ILogger<IbkrClient>>();
                TwsCallbackHandler wrapper = sp.GetRequiredService<TwsCallbackHandler>();

                // Read IBKR configuration
                string host = config.GetValue<string>("IBKR:Host") ?? "127.0.0.1";
                int paperPort = config.GetValue<int>("IBKR:PaperPort", 4002);
                int livePort = config.GetValue<int>("IBKR:LivePort", 4001);
                int clientId = config.GetValue<int>("IBKR:ClientId", 1);
                string tradingModeStr = config.GetValue<string>("TradingMode", "paper") ?? "paper";

                // Parse trading mode (safety: default to Paper on any error)
                if (!Enum.TryParse<TradingMode>(tradingModeStr, ignoreCase: true, out TradingMode tradingMode))
                {
                    tradingMode = TradingMode.Paper;
                    Log.Warning("Invalid TradingMode value '{Mode}', defaulting to Paper", tradingModeStr);
                }

                // Determine port based on trading mode (safety: default to paper)
                int port = tradingMode == TradingMode.Live ? livePort : paperPort;

                IbkrConfig ibkrConfig = new()
                {
                    Host = host,
                    Port = port,
                    ClientId = clientId,
                    TradingMode = tradingMode,
                    ConnectionTimeoutSeconds = 30,
                    ReconnectInitialDelaySeconds = 5,
                    ReconnectMaxDelaySeconds = 60,
                    MaxReconnectAttempts = 10
                };

                IbkrPortScanner portScanner = sp.GetRequiredService<IbkrPortScanner>();
                return new IbkrClient(logger, ibkrConfig, wrapper, portScanner);
            });

            // Register port scanner for IBKR diagnostics
            services.AddSingleton<IbkrPortScanner>();

            // Telegram alerting service
            services.AddSingleton<ITelegramAlerter, TelegramAlerter>();

            // Bot configuration
            services.Configure<BotOptions>(config.GetSection("Bots"));

            // HttpClient factory for OutboxSyncWorker and BotWebhookRegistrar
            services.AddHttpClient();

            // Workers (hosted services)
            services.AddHostedService<HeartbeatWorker>();
            services.AddHostedService<OutboxSyncWorker>();
            services.AddHostedService<TelegramWorker>();
            services.AddHostedService<LogReaderWorker>();
            services.AddHostedService<IvtsMonitorWorker>();
            services.AddHostedService<GreeksMonitorWorker>();
            services.AddHostedService<MarketDataCollector>();
            services.AddHostedService<BenchmarkCollector>();

            // Bot webhook registrar (runs at startup)
            services.AddHostedService<BotWebhookRegistrar>();
        })
        .Build();

    // Run database migrations before starting services
    Log.Information("Running database migrations...");
    IDbConnectionFactory dbFactory = host.Services.GetRequiredService<IDbConnectionFactory>();
    ILogger<MigrationRunner> migrationLogger = host.Services.GetRequiredService<ILogger<MigrationRunner>>();
    MigrationRunner migrationRunner = new(dbFactory, migrationLogger);
    await migrationRunner.RunAsync(SupervisorMigrations.All, CancellationToken.None);
    Log.Information("Database migrations completed");

    await host.RunAsync();
    Log.Information("TradingSupervisorService stopped cleanly");
}
catch (Exception ex)
{
    Log.Fatal(ex, "TradingSupervisorService terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
