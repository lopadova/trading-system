using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SharedKernel.Configuration;
using SharedKernel.Data;
using SharedKernel.Ibkr;
using SharedKernel.Observability;
using SharedKernel.Options;
using SharedKernel.Safety;
using SharedKernel.Services;
using SharedKernel.Strategy;
using OptionsExecutionService.Campaign;
using OptionsExecutionService.Configuration;
using OptionsExecutionService.Ibkr;
using OptionsExecutionService.Migrations;
using OptionsExecutionService.Orders;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Services;
using OptionsExecutionService.Workers;
using SharedKernel.Domain;

// Bootstrap Serilog early to capture startup errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/options-execution-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("OptionsExecutionService starting up");

    // Build configuration for early validation.
    // Layer order: appsettings.json → appsettings.{Env}.json → appsettings.Local.json
    // → EncryptedProvider (decrypts DPAPI:<blob> from earlier layers)
    // → environment variables (still win for emergency override)
    // → command line. See Supervisor Program.cs + docs/ops/SECRETS.md.
    IConfiguration configForValidation = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
            optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)  // Developer overrides (not in git)
        .AddEncryptedProvider()
        .AddEnvironmentVariables()
        .AddCommandLine(args)
        .Build();

    // Validate configuration before building host
    Log.Information("Validating configuration...");
    OptionsConfigurationValidator configValidator = new(
        configForValidation,
        new LoggerFactory().CreateLogger<OptionsConfigurationValidator>());

    SharedKernel.Configuration.ValidationResult validationResult = configValidator.Validate();

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

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    // Mirror the DPAPI decryption layer on the host's own IConfiguration so
    // code paths reading from builder.Configuration (e.g. Serilog, IBKR
    // config below) see decrypted values too. CreateApplicationBuilder
    // already registered the standard JSON + env-var sources.
    builder.Configuration.AddEncryptedProvider();

    // Configure Windows Service
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "OptionsExecutionService";
    });

    // Configure Serilog
    HttpSinkOptions optionsSinkOpts = ObservabilityConfig.ReadOptions(builder.Configuration, "options-execution");
    builder.Services.AddSerilog((services, loggerConfig) => loggerConfig
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "OptionsExecutionService")
        .Enrich.WithProperty("TradingMode", builder.Configuration["TradingMode"] ?? "paper")
        .MinimumLevel.Information()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/options-execution-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}")
        // HTTP sink: Warning+ only, batched, streams to Worker /api/v1/logs. File sink keeps full history locally.
        .AddLogShipping(optionsSinkOpts));

    // Register database connection factory
    string dbPath = builder.Configuration["Sqlite:OptionsDbPath"] ?? "data/options-execution.db";
    builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
        new SqliteConnectionFactory(dbPath));

    // Register repositories
    builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
    builder.Services.AddScoped<IOrderTrackingRepository, OrderTrackingRepository>();
    builder.Services.AddScoped<IOrderEventsRepository, OrderEventsRepository>();

    // Register strategy services
    builder.Services.AddSingleton<IStrategyValidator, StrategyValidator>();
    builder.Services.AddSingleton<IStrategyLoader, StrategyLoader>();

    // Register Greeks calculator
    builder.Services.AddSingleton<IGreeksCalculator, BlackScholesCalculator>();

    // Register IBKR client configuration
    string tradingModeStr = builder.Configuration["TradingMode"] ?? "paper";
    TradingMode tradingMode = Enum.Parse<TradingMode>(tradingModeStr, ignoreCase: true);

    int port = tradingMode == TradingMode.Paper
        ? builder.Configuration.GetValue<int>("IBKR:PaperPort", 4002)
        : builder.Configuration.GetValue<int>("IBKR:LivePort", 4001);

    IbkrConfig ibkrConfig = new IbkrConfig
    {
        Host = builder.Configuration["IBKR:Host"] ?? "127.0.0.1",
        Port = port,
        ClientId = builder.Configuration.GetValue<int>("IBKR:ClientId", 2),
        TradingMode = tradingMode,
        ReconnectInitialDelaySeconds = builder.Configuration.GetValue<int>("IBKR:ReconnectInitialDelaySeconds", 5),
        ReconnectMaxDelaySeconds = builder.Configuration.GetValue<int>("IBKR:ReconnectMaxDelaySeconds", 300),
        MaxReconnectAttempts = builder.Configuration.GetValue<int>("IBKR:MaxReconnectAttempts", 0),
        ConnectionTimeoutSeconds = builder.Configuration.GetValue<int>("IBKR:ConnectionTimeoutSeconds", 10)
    };

    // Validate IBKR configuration at startup (fails fast if invalid)
    ibkrConfig.Validate();

    builder.Services.AddSingleton(ibkrConfig);
    builder.Services.AddSingleton<TwsCallbackHandler>();
    builder.Services.AddSingleton<IbkrPortScanner>();
    builder.Services.AddSingleton<IIbkrClient, IbkrClient>();

    // Register order safety configuration
    OrderSafetyConfig safetyConfig = new OrderSafetyConfig
    {
        TradingMode = tradingMode,
        MaxPositionSize = builder.Configuration.GetValue<int>("Safety:MaxPositionSize", 10),
        MaxPositionValueUsd = builder.Configuration.GetValue<decimal>("Safety:MaxPositionValueUsd", 50000m),
        MinAccountBalanceUsd = builder.Configuration.GetValue<decimal>("Safety:MinAccountBalanceUsd", 10000m),
        MaxPositionPctOfAccount = builder.Configuration.GetValue<decimal>("Safety:MaxPositionPctOfAccount", 0.05m),
        CircuitBreakerFailureThreshold = builder.Configuration.GetValue<int>("Safety:CircuitBreakerFailureThreshold", 3),
        CircuitBreakerWindowMinutes = builder.Configuration.GetValue<int>("Safety:CircuitBreakerWindowMinutes", 60),
        CircuitBreakerCooldownMinutes = builder.Configuration.GetValue<int>("Safety:CircuitBreakerCooldownMinutes", 120)
    };

    // Validate safety config at startup (fails fast if invalid)
    safetyConfig.Validate();

    builder.Services.AddSingleton(safetyConfig);

    // Register time provider
    builder.Services.AddSingleton<OptionsExecutionService.Common.ITimeProvider, OptionsExecutionService.Common.SystemTimeProvider>();

    // ============================================================
    // Phase 7.4 — Safety & risk gates wiring
    // ============================================================

    // Bind Cloudflare + Safety option records.
    builder.Services.Configure<CloudflareOptions>(builder.Configuration.GetSection(CloudflareOptions.SectionName));
    builder.Services.Configure<SafetyOptions>(builder.Configuration.GetSection(SafetyOptions.SectionName));

    // HttpClient factory for SemaphoreGate, DailyPnLWatcher, OutboxOrderAuditSink.
    builder.Services.AddHttpClient();

    // Email alerter — last-resort channel. EmailAlerter gracefully disables when SMTP is not configured.
    builder.Services.AddSingleton<ISmtpClient>(sp =>
    {
        EmailAlerterConfig cfg = new()
        {
            Enabled = builder.Configuration.GetValue<bool>("Smtp:Enabled", false),
            Host = builder.Configuration.GetValue<string>("Smtp:Host") ?? string.Empty,
            Port = builder.Configuration.GetValue<int>("Smtp:Port", 587),
            Username = builder.Configuration.GetValue<string>("Smtp:Username") ?? string.Empty,
            Password = builder.Configuration.GetValue<string>("Smtp:Password") ?? string.Empty,
            From = builder.Configuration.GetValue<string>("Smtp:From") ?? string.Empty,
            To = builder.Configuration.GetValue<string>("Smtp:To") ?? string.Empty,
            EnableSsl = builder.Configuration.GetValue<bool>("Smtp:EnableSsl", true)
        };
        return new SystemSmtpClient(cfg);
    });
    builder.Services.AddSingleton<IAlerter, EmailAlerter>();

    // SemaphoreGate (Gate #1) — composite market-risk indicator fetched from Worker.
    builder.Services.AddSingleton<SemaphoreGate>();

    // SafetyFlagStore (Gate #2) — durable KV for trading_paused.
    builder.Services.AddSingleton<ISafetyFlagStore, SqliteSafetyFlagStore>();

    // Order-audit sink — local mirror + Worker ingest.
    builder.Services.AddSingleton<IOrderAuditSink, OutboxOrderAuditSink>();

    // DailyPnLWatcher — BackgroundService polling Worker for daily drawdown.
    builder.Services.AddHostedService<DailyPnLWatcher>();

    // Startup banner for semaphore override — CRITICAL visibility if ever enabled.
    bool overrideSemaphore = builder.Configuration.GetValue<bool>("Safety:OverrideSemaphore", false);
    if (overrideSemaphore)
    {
        EmitSemaphoreOverrideBanner();
    }

    // Register order placer service (scoped to match repository lifetime).
    builder.Services.AddScoped<IOrderPlacer, OrderPlacer>();

    // Register campaign manager
    builder.Services.AddScoped<ICampaignManager, CampaignManager>();

    // Register background workers
    builder.Services.AddHostedService<IbkrConnectionWorker>();
    builder.Services.AddHostedService<CampaignMonitorWorker>();

    // Observability — health-state tracker; exposed via /health HTTP endpoint below.
    builder.Services.AddSingleton<IHealthState>(sp =>
    {
        IIbkrClient ibkr = sp.GetRequiredService<IIbkrClient>();
        IDbConnectionFactory db = sp.GetRequiredService<IDbConnectionFactory>();
        ILogger<HealthState> healthLogger = sp.GetRequiredService<ILogger<HealthState>>();
        return new HealthState("options-execution", ibkr, db, healthLogger);
    });

    // Configure shutdown timeout (allow 30s for graceful shutdown)
    builder.Services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromSeconds(30);
    });

    IHost host = builder.Build();

    // Start the /health HTTP endpoint on port 5089 (convention) as a side-car.
    int healthPort = builder.Configuration.GetValue<int>("Observability:Health:Port", 5089);
    IHealthState healthState = host.Services.GetRequiredService<IHealthState>();
    IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    ILoggerFactory healthLoggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
    Microsoft.Extensions.Logging.ILogger healthStartupLogger = healthLoggerFactory.CreateLogger("HealthEndpointHost");
    HealthEndpointHost.StartAlongside(lifetime, healthState, healthPort, healthStartupLogger);

    // Run database migrations before starting workers
    await RunMigrationsAsync(host.Services);

    Log.Information(
        "OptionsExecutionService configured. TradingMode={Mode} IBKR={Host}:{Port} ClientId={ClientId}",
        tradingMode, ibkrConfig.Host, ibkrConfig.Port, ibkrConfig.ClientId);

    // If the override banner fired above, also post a Critical alert after DI is
    // ready — pre-host we don't have an IAlerter instance, so we do it here.
    if (overrideSemaphore)
    {
        try
        {
            IAlerter alerter = host.Services.GetRequiredService<IAlerter>();
            await alerter.SendImmediateAsync(
                AlertSeverity.Critical,
                "Safety:OverrideSemaphore is ACTIVE",
                "Orders will execute even when the Cloudflare semaphore reports RED. " +
                "This bypasses the primary operating-risk gate. Set Safety:OverrideSemaphore=false " +
                "and restart to re-enable the gate. No auto-expiry — this persists across restarts.",
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Alert failed — bootstrap log already recorded the banner so operator knowledge is preserved.
            Log.Error(ex, "Failed to dispatch Safety:OverrideSemaphore startup alert");
        }
    }

    await host.RunAsync();
    Log.Information("OptionsExecutionService stopped cleanly");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "OptionsExecutionService terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Runs all database migrations on startup.
/// </summary>
static async Task RunMigrationsAsync(IServiceProvider services)
{
    try
    {
        Log.Information("Running database migrations...");

        IDbConnectionFactory dbFactory = services.GetRequiredService<IDbConnectionFactory>();
        ILogger<MigrationRunner> logger = services.GetRequiredService<ILogger<MigrationRunner>>();
        MigrationRunner runner = new(dbFactory, logger);

        await runner.RunAsync(OptionsMigrations.All, CancellationToken.None);

        Log.Information("Database migrations completed successfully");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database migration failed");
        throw;
    }
}

/// <summary>
/// Emit a very-loud multi-line banner when Safety:OverrideSemaphore=true so the
/// operator CAN'T miss it scrolling through logs. Logged at Fatal level (which
/// ships through the HTTP sink to the Worker + lights up the dashboard) AND
/// written to console so the Windows-service console-viewer also surfaces it.
/// We accept the "Fatal" misnomer (nothing actually fatal happened) in exchange
/// for the visibility — this IS the loudest level available.
/// </summary>
static void EmitSemaphoreOverrideBanner()
{
    const string line = "================================================================================";
    string[] banner = new[]
    {
        line,
        "",
        "    SAFETY:OVERRIDESEMAPHORE = TRUE",
        "",
        "    The SemaphoreGate is BYPASSED. Orders will execute even when the composite",
        "    operating-risk indicator is RED. This is an ADMIN OVERRIDE and is NOT",
        "    time-limited — it persists across restarts until the config is flipped back.",
        "",
        "    To disable: set Safety:OverrideSemaphore=false in appsettings*.json or env",
        "    vars (Safety__OverrideSemaphore=false) and restart the service.",
        "",
        line
    };

    // Use CurrentCulture.InvariantCulture here: log banner text is mechanical,
    // operator-readable English — no numbers to localize.
    foreach (string row in banner)
    {
        Log.Fatal("{BannerRow}", row.ToString(CultureInfo.InvariantCulture));
    }
}
