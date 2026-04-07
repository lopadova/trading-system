# Skill: Windows Service — Installazione, Lifecycle, Recovery
> Aggiornabile dagli agenti. Controlla knowledge/errors-registry.md per fix noti.

---

## Program.cs Pattern Completo

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddWindowsService(opt =>
        opt.ServiceName = "TradingSupervisorService");

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "TradingSupervisorService")
        .Enrich.WithProperty("TradingMode", ctx.Configuration["TradingMode"] ?? "paper")
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/supervisor-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{Service}] {Message:lj}{NewLine}{Exception}"));

    // Registra tutti i servizi qui
    RegisterServices(builder.Services, builder.Configuration);

    IHost host = builder.Build();

    // Esegui migration DB all'avvio
    await RunMigrationsAsync(host.Services);

    Log.Information("TradingSupervisorService starting. TradingMode={Mode}",
        builder.Configuration["TradingMode"]);

    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "TradingSupervisorService terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

## Script Installazione PowerShell

```powershell
# infra/windows/install-supervisor.ps1
#Requires -RunAsAdministrator
param(
    [string]$InstallPath = "C:\TradingSystem",
    [string]$BinPath     = "C:\TradingSystem\TradingSupervisorService.exe"
)

$ServiceName = "TradingSupervisorService"
$DisplayName = "Trading System Supervisor"
$Description = "Monitora macchina, processi e sincronizza dati verso Cloudflare"

# Rimuovi servizio esistente se presente
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Removing existing service..."
    sc.exe stop  $ServiceName | Out-Null
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Crea directory
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallPath\logs" | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallPath\data" | Out-Null

# Installa servizio
sc.exe create $ServiceName `
    binPath= "`"$BinPath`"" `
    start= delayed-auto `
    DisplayName= $DisplayName | Out-Null

# Descrizione
sc.exe description $ServiceName $Description | Out-Null

# Recovery policy: restart dopo 5s, 10s, 30s
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

# Avvia
Write-Host "Starting $ServiceName..."
sc.exe start $ServiceName | Out-Null

# Verifica
Start-Sleep -Seconds 3
$svc = Get-Service -Name $ServiceName
Write-Host "Service status: $($svc.Status)"
if ($svc.Status -ne 'Running') {
    Write-Error "Service failed to start. Check Event Viewer."
    exit 1
}
Write-Host "Installation complete."
```

## Graceful Shutdown

Il `BackgroundService` deve rispettare il `CancellationToken`:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await RunCycleAsync(stoppingToken);
        // Task.Delay rispetta IsCancellationRequested
        await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
    }
    // Cleanup finale prima dello shutdown
    _logger.LogInformation("Worker stopping gracefully");
}

// In Program.cs — aumenta il timeout shutdown se necessario
builder.Services.Configure<HostOptions>(opt =>
    opt.ShutdownTimeout = TimeSpan.FromSeconds(30));
```

## Publish per Windows Service

```bash
dotnet publish src/TradingSupervisorService \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o publish/supervisor/
```

## Verifica Stato Servizio

```powershell
# Status
sc.exe query TradingSupervisorService

# Avvia / ferma
sc.exe start  TradingSupervisorService
sc.exe stop   TradingSupervisorService

# Vedi log (PowerShell)
Get-Content C:\TradingSystem\logs\supervisor-*.log -Tail 50 -Wait
```
