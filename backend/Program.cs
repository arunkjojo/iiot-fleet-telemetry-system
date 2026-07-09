using FleetTelemetry.Data;
using FleetTelemetry.Hubs;
using FleetTelemetry.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSignalR().AddMessagePackProtocol();

// ── Live telemetry store (always registered; consumed by live-mode services/controllers) ──
builder.Services.AddSingleton<ILiveTelemetryStore, LiveTelemetryStore>();

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
