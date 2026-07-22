using FleetTelemetry.Hubs;
using FleetTelemetry.Models;
using Microsoft.AspNetCore.SignalR;

namespace FleetTelemetry.Services;

/// <summary>
/// Relays vehicles that changed in <see cref="ILiveTelemetryStore"/> to all connected SignalR
/// clients roughly every 500ms (BE-003). Always registered (live-only mode).
///
/// Purely a store-to-hub relay — it performs no simulation work itself. Each tick it drains
/// only the vehicles that were upserted since the previous tick via
/// <see cref="ILiveTelemetryStore.GetAndClearDirty"/>, so payloads stay proportional to actual
/// ingest traffic instead of re-broadcasting the full fleet every 500ms. If nothing changed on
/// a given tick, the broadcast call is skipped entirely.
/// </summary>
public class LiveBroadcastService : BackgroundService
{
    private const int TickMilliseconds = 500;

    private readonly IHubContext<FleetHub, IFleetClient> _hubContext;
    private readonly ILiveTelemetryStore _store;

    public LiveBroadcastService(IHubContext<FleetHub, IFleetClient> hubContext, ILiveTelemetryStore store)
    {
        _hubContext = hubContext;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Drift-corrected tick loop, targeting a steady ~500ms cadence.
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (!stoppingToken.IsCancellationRequested)
        {
            var dirty = _store.GetAndClearDirty();

            var updates = dirty
                .Select(v => new VehicleUpdate
                {
                    Id = v.Id,
                    Latitude = v.Latitude,
                    Longitude = v.Longitude,
                    FuelPercent = v.FuelPercent,
                    SpeedKph = v.SpeedKph,
                    EngineHealth = v.EngineHealth,
                    Status = v.Status,
                    Temp = v.Temp
                })
                .ToList();

            // No empty broadcasts — only push when something actually changed since last tick.
            if (updates.Count > 0)
            {
                try
                {
                    await _hubContext.Clients.All.ReceiveFleetUpdate(updates);
                }
                catch
                {
                    // ignore transient errors — best-effort broadcast, next tick will retry with fresh data
                }
            }

            // wait to target ~500ms tick
            var elapsed = sw.ElapsedMilliseconds % TickMilliseconds;
            var delay = TickMilliseconds - (int)elapsed;
            await Task.Delay(delay, stoppingToken);
        }
    }
}
