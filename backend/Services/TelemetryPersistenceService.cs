using System.Threading.Channels;
using FleetTelemetry.Data;
using FleetTelemetry.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetTelemetry.Services;

/// <summary>
/// Enqueue-only surface exposed to controllers. Kept separate from the BackgroundService
/// contract so callers (TelemetryIngestController) only depend on what they need — writing
/// into the buffer — and never see the drain/flush loop.
/// </summary>
public interface ITelemetryIngestQueue
{
    /// <summary>Buffer a telemetry snapshot for durable persistence. Never blocks on the DB.</summary>
    ValueTask EnqueueSnapshot(TelemetrySnapshotEntity snapshot);

    /// <summary>Buffer a vehicle log entry for durable persistence. Never blocks on the DB.</summary>
    ValueTask EnqueueLog(VehicleLogEntity log);
}

/// <summary>
/// Buffered PostgreSQL writer for live telemetry ingestion (BE-002).
///
/// Up to 10,000 vehicles may POST to /api/telemetry/ingest every few seconds. The controller
/// must never call SaveChangesAsync synchronously inside the HTTP request — instead it writes
/// into two bounded in-process channels here, and this single BackgroundService drains both in
/// batches on a fixed cadence. This decouples emitter/request count from DB connection count:
/// no matter how many vehicles are posting, there is exactly one writer loop and one scoped
/// FleetDbContext per flush.
///
/// Registered as both ITelemetryIngestQueue (for controllers) and IHostedService (for the
/// drain loop) pointing at the *same* singleton instance, so the channels are shared.
/// </summary>
public class TelemetryPersistenceService : BackgroundService, ITelemetryIngestQueue
{
    private const int ChannelCapacity = 50_000;
    private const int FlushIntervalMs = 1000;
    private const int MaxBatchSize = 2000;
    private const int PollDelayMs = 50;

    private readonly Channel<TelemetrySnapshotEntity> _snapshotChannel;
    private readonly Channel<VehicleLogEntity> _logChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryPersistenceService> _logger;

    public TelemetryPersistenceService(IServiceScopeFactory scopeFactory, ILogger<TelemetryPersistenceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // DropOldest: under sustained overload, prefer dropping the oldest buffered item over
        // blocking the HTTP request thread that's trying to write into the channel.
        var options = new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        };

        _snapshotChannel = Channel.CreateBounded<TelemetrySnapshotEntity>(options);
        _logChannel = Channel.CreateBounded<VehicleLogEntity>(options);
    }

    public ValueTask EnqueueSnapshot(TelemetrySnapshotEntity snapshot) =>
        _snapshotChannel.Writer.WriteAsync(snapshot);

    public ValueTask EnqueueLog(VehicleLogEntity log) =>
        _logChannel.Writer.WriteAsync(log);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelemetryPersistenceService starting drain loop (flush every {Ms}ms or {Batch} items).",
            FlushIntervalMs, MaxBatchSize);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var (snapshots, logs) = await DrainAsync(stoppingToken);
                if (snapshots.Count > 0 || logs.Count > 0)
                {
                    await FlushAsync(snapshots, logs, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        finally
        {
            // Final best-effort flush so anything buffered at shutdown isn't silently lost.
            // Uses CancellationToken.None since stoppingToken is already cancelled here.
            var finalSnapshots = DrainAllSync(_snapshotChannel);
            var finalLogs = DrainAllSync(_logChannel);
            if (finalSnapshots.Count > 0 || finalLogs.Count > 0)
            {
                try
                {
                    await FlushAsync(finalSnapshots, finalLogs, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Final shutdown flush failed; {SnapCount} snapshots and {LogCount} logs dropped.",
                        finalSnapshots.Count, finalLogs.Count);
                }
            }
        }
    }

    /// <summary>
    /// Drain both channels until either FlushIntervalMs has elapsed or a batch reaches
    /// MaxBatchSize items, whichever comes first.
    /// </summary>
    private async Task<(List<TelemetrySnapshotEntity> snapshots, List<VehicleLogEntity> logs)> DrainAsync(
        CancellationToken stoppingToken)
    {
        var snapshots = new List<TelemetrySnapshotEntity>(MaxBatchSize);
        var logs = new List<VehicleLogEntity>(MaxBatchSize);
        var cycleStart = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested &&
               (DateTime.UtcNow - cycleStart).TotalMilliseconds < FlushIntervalMs &&
               snapshots.Count < MaxBatchSize &&
               logs.Count < MaxBatchSize)
        {
            var drainedAny = false;

            while (snapshots.Count < MaxBatchSize && _snapshotChannel.Reader.TryRead(out var snap))
            {
                snapshots.Add(snap);
                drainedAny = true;
            }

            while (logs.Count < MaxBatchSize && _logChannel.Reader.TryRead(out var log))
            {
                logs.Add(log);
                drainedAny = true;
            }

            if (!drainedAny)
            {
                try
                {
                    await Task.Delay(PollDelayMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        return (snapshots, logs);
    }

    private static List<T> DrainAllSync<T>(Channel<T> channel)
    {
        var items = new List<T>();
        while (channel.Reader.TryRead(out var item))
        {
            items.Add(item);
        }
        return items;
    }

    /// <summary>
    /// One AddRangeAsync + SaveChangesAsync per entity type, in a fresh scoped FleetDbContext.
    /// FK violations (vehicle_id not present in vehicles table — e.g. an emitter misconfigured
    /// with a stale/unknown vehicle ID) or any other DB error are logged and the batch is
    /// dropped; the drain loop must never crash.
    /// </summary>
    private async Task FlushAsync(List<TelemetrySnapshotEntity> snapshots, List<VehicleLogEntity> logs, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        if (snapshots.Count > 0)
        {
            try
            {
                await db.TelemetrySnapshots.AddRangeAsync(snapshots, ct);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist {Count} telemetry snapshots (dropping batch).", snapshots.Count);
                db.ChangeTracker.Clear();
            }
        }

        if (logs.Count > 0)
        {
            try
            {
                await db.VehicleLogs.AddRangeAsync(logs, ct);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist {Count} vehicle logs (dropping batch).", logs.Count);
                db.ChangeTracker.Clear();
            }
        }
    }
}
