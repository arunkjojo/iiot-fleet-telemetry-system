using FleetTelemetry.Data;
using FleetTelemetry.Hubs;
using FleetTelemetry.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Thread pool tuning (QA-001 fix: /fleethub abnormal disconnects under live-ingestion load) ──
// Root-cause context: under full-scale live ingestion (VEHICLE_COUNT=10000, MAX_CONCURRENCY=300),
// the backend fields a sustained burst of concurrent short-lived async requests/continuations
// (TelemetryIngestController -> channel writes, TelemetryPersistenceService's per-second EF Core
// flush, LiveBroadcastService's 500ms SignalR relay). No blocking (.Result/.Wait()) calls were
// found in this hot path, but the CLR thread pool still starts at Environment.ProcessorCount
// threads and only injects new ones at a throttled rate (~1 every ~500ms-1s) once queued work
// backs up. SignalR's own keep-alive ping is driven by a System.Threading.Timer callback that
// is itself scheduled onto the thread pool — if the pool is busy servicing the ingest burst, that
// callback (and the request-processing work that must run promptly to keep the connection alive)
// can queue behind everything else long enough to blow through the client's fixed timeout,
// producing exactly the abnormal WebSocket closure (code 1006) QA observed roughly every 30-70s.
// Raising the floor removes the injection-throttle delay for this workload's normal burst size.
ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);
var targetMinThreads = Math.Max(Environment.ProcessorCount * 16, 200);
ThreadPool.SetMinThreads(
    Math.Max(minWorkerThreads, targetMinThreads),
    Math.Max(minCompletionPortThreads, targetMinThreads));

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var origin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN") ?? "http://localhost:3000";
        var extras = (Environment.GetEnvironmentVariable("ADDITIONAL_FRONTEND_ORIGINS") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var origins = new List<string> { origin };
        origins.AddRange(extras.Where(x => !string.IsNullOrWhiteSpace(x)));
        policy.WithOrigins(origins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── PostgreSQL / EF Core ──────────────────────────────────────────────────────
// Connection string is read from configuration — never hardcoded here.
// Source (local dev): ConnectionStrings__Fleet in launchSettings.json
// Source (Docker):    ConnectionStrings__Fleet env var in docker-compose.yml
var connectionString = builder.Configuration.GetConnectionString("Fleet")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Fleet is not set. " +
        "Set the ConnectionStrings__Fleet environment variable.");

builder.Services.AddDbContext<FleetDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── Controllers + SignalR ─────────────────────────────────────────────────────
builder.Services.AddControllers();

// QA-001 fix: defaults (KeepAliveInterval=15s server-ping cadence, ClientTimeoutInterval=30s
// server-side patience for the client) are too tight for this workload. The JS client's own
// fixed serverTimeoutInMilliseconds (30s, not configurable from here — frontend is out of
// scope for this fix) trips into an abnormal close (code 1006) if a keep-alive ping is ever
// delayed past that window, which is plausible under full live-ingestion load even with the
// thread-pool floor raised above. Widen both server-side knobs generously: keep
// KeepAliveInterval at the standard 15s cadence (frequent enough that occasional scheduling
// jitter still lands well inside the client's 30s budget) and raise ClientTimeoutInterval to
// 60s (SignalR guidance: at least 2-3x the keep-alive interval) so the server tolerates
// transient delays in receiving client pings under load instead of tearing the connection down.
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
}).AddMessagePackProtocol();

// ── Live telemetry store (always registered; consumed by live-mode services/controllers) ──
builder.Services.AddSingleton<ILiveTelemetryStore, LiveTelemetryStore>();

// ── SignalR connection tracker (BE-005) — always registered; FleetHub increments/decrements
// it on connect/disconnect, HealthController reads it via GET /api/health/signalr regardless
// of USE_LIVE_TELEMETRY, since /fleethub is always mapped below. ──
builder.Services.AddSingleton<HubConnectionTracker>();

// ── Data source toggle: live ingestion pipeline vs. legacy in-memory dummy simulation ──
// USE_LIVE_TELEMETRY=false (default) keeps local `dotnet run` on the dummy simulation.
// USE_LIVE_TELEMETRY=true (set by Docker Compose) defers to the live ingestion pipeline
// (registered in later sprint tasks) and must NOT start TelemetrySimulationService, since
// its constructor seeds 10,000 vehicles and starts ticking immediately.
var useLiveTelemetry = builder.Configuration.GetValue<bool>("USE_LIVE_TELEMETRY", false);
if (!useLiveTelemetry)
{
    builder.Services.AddHostedService<TelemetrySimulationService>();
}
else
{
    // Buffered PostgreSQL writer for POST /api/telemetry/ingest (BE-002). Registered as a
    // singleton so the same instance backs both the ITelemetryIngestQueue the controller
    // enqueues into and the IHostedService drain loop that flushes it — the channels must be
    // shared, not duplicated.
    builder.Services.AddSingleton<TelemetryPersistenceService>();
    builder.Services.AddSingleton<ITelemetryIngestQueue>(sp => sp.GetRequiredService<TelemetryPersistenceService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<TelemetryPersistenceService>());

    // Relays ILiveTelemetryStore changes to connected SignalR clients every ~500ms (BE-003).
    // VehiclesController/LogsController read the same store synchronously on request; this
    // service is what makes the frontend see updates without polling.
    builder.Services.AddHostedService<LiveBroadcastService>();

    // Periodic bounded-batch cleanup of aged telemetry_snapshots/vehicle_logs rows (DB-004,
    // ADR-001 action item #5). Dummy mode never writes to the DB, so retention has nothing to
    // do there — registered only alongside the live ingestion pipeline, like the two services
    // above.
    builder.Services.AddHostedService<TelemetryRetentionService>();
}

var app = builder.Build();

// ── Database: apply migrations + seed on startup ──────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db     = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

    try
    {
        logger.LogInformation("Applying EF Core migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Migrations applied.");

        await DbSeeder.SeedVehiclesAsync(scope, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database startup failed. Check DB_HOST, DB_NAME, DB_USER, DB_PASSWORD env vars.");
        // Do not crash the app — simulation still works without DB
    }
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseCors("frontend");
app.MapControllers();
app.MapHub<FleetHub>("/fleethub");

app.Run();
