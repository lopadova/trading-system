using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Observability;

/// <summary>
/// Side-car minimal-API web host that exposes GET /health and GET /healthz on a dedicated port.
/// Lives for the lifetime of the main <see cref="IHost"/> — use
/// <see cref="StartAlongside(IHostApplicationLifetime, IHealthState, int, ILogger)"/> from Program.cs
/// AFTER the main host has been built so we can tie its cancellation to the main lifetime.
/// <para>
/// We intentionally do NOT use a BackgroundService here — the health endpoint must be
/// available even if the main host is still initializing workers, so it runs as a
/// separate WebApplication on its own Kestrel binding.
/// </para>
/// </summary>
public static class HealthEndpointHost
{
    /// <summary>
    /// Builds and starts a minimal WebApplication bound to <c>http://localhost:{port}</c>
    /// exposing:
    /// <list type="bullet">
    ///   <item><description>GET /health — full JSON HealthReport (200 if ok, 503 if down).</description></item>
    ///   <item><description>GET /healthz — compact { status } echo for load-balancer probes.</description></item>
    /// </list>
    /// Returns immediately — the returned <see cref="WebApplication"/> runs in background.
    /// </summary>
    /// <param name="lifetime">Host lifetime so the health app shuts down with the main service.</param>
    /// <param name="healthState">The live <see cref="IHealthState"/> implementation to query.</param>
    /// <param name="port">TCP port to bind. 5088 supervisor, 5089 options by convention.</param>
    /// <param name="logger">Logger used for startup/shutdown telemetry.</param>
    /// <returns>The running <see cref="WebApplication"/> (caller can dispose on shutdown; normally lifetime handles it).</returns>
    public static WebApplication StartAlongside(
        IHostApplicationLifetime lifetime,
        IHealthState healthState,
        int port,
        ILogger logger)
    {
        if (lifetime == null)
        {
            throw new ArgumentNullException(nameof(lifetime));
        }
        if (healthState == null)
        {
            throw new ArgumentNullException(nameof(healthState));
        }
        if (port <= 0 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Strip default logging providers — the main host owns Serilog; we don't want
        // the health app double-logging request traces to the console.
        builder.Logging.ClearProviders();

        // Keep Kestrel minimal — localhost binding only; no HTTPS (trusted local loopback).
        builder.WebHost.UseUrls($"http://localhost:{port}");

        // Inject the live health-state instance as a singleton.
        builder.Services.AddSingleton<IHealthState>(healthState);

        WebApplication app = builder.Build();

        // Wire endpoints. We serialize HealthReport manually because Checks is an
        // IReadOnlyDictionary, which System.Text.Json handles natively but we want
        // control over shape + status-code mapping.
        app.MapGet("/health", (HttpContext ctx, IHealthState state) =>
        {
            HealthReport report = state.Current();
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = report.Status == "down" ? 503 : 200;
            return JsonSerializer.Serialize(new
            {
                service = report.Service,
                status = report.Status,
                version = report.Version,
                uptime_seconds = (long)report.Uptime.TotalSeconds,
                now = report.Now.ToString("O"),
                checks = report.Checks
            });
        });

        app.MapGet("/healthz", (HttpContext ctx, IHealthState state) =>
        {
            HealthReport report = state.Current();
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = report.Status == "down" ? 503 : 200;
            return JsonSerializer.Serialize(new { status = report.Status });
        });

        // Tie health-app shutdown to the main host shutdown — we don't want the endpoint
        // lingering after the main worker exits.
        lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                logger.LogInformation("Stopping /health endpoint on port {Port}", port);
                // Fire and forget: StopAsync will block the shutdown hook if awaited synchronously.
                _ = app.StopAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Exception while stopping /health endpoint");
            }
        });

        // Launch the web app on the thread pool. RunAsync binds the port and blocks until
        // ApplicationStopping triggers, so this Task completes cleanly at shutdown.
        _ = Task.Run(async () =>
        {
            try
            {
                logger.LogInformation("Starting /health endpoint on http://localhost:{Port}", port);
                await app.RunAsync().ConfigureAwait(false);
                logger.LogInformation("/health endpoint on port {Port} stopped", port);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "/health endpoint on port {Port} crashed", port);
            }
        });

        return app;
    }
}
