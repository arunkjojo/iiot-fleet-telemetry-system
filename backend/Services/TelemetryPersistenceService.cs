using System.Text.Json;
using System.Text.Json.Serialization;
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
    // Defaults preserved from the original hard-coded values (ADR-001, action item #2 — these
    // are still untuned/not load-tested at full 10k-vehicle sustained throughput; they are now
    // configurable via the "TelemetryPersistence" section in appsettings so future tuning does
    // not require a code change).
    private const int DefaultChannelCapacity = 50_000;
    private const int DefaultFlushIntervalMs = 1000;
    private const int DefaultMaxBatchSize = 2000;
    private const int DefaultMaxRetryAttempts = 2;
    private const int DefaultRetryDelayMs = 250;
    private const string DefaultDeadLetterDirectory = "deadletter";
    private const int PollDelayMs = 50;

    private readonly int _flushIntervalMs;
    private readonly int _maxBatchSize;
    private readonly int _maxRetryAttempts;
    private readonly int _retryDelayMs;
    private readonly string _deadLetterDirectory;
    private readonly JsonSerializerOptions _deadLetterJsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false,
    };

    private readonly Channel<TelemetrySnapshotEntity> _snapshotChannel;
    private readonly Channel<VehicleLogEntity> _logChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryPersistenceService> _logger;

    // Running counters for dropped-after-retry items (ADR-001 "no at-least-once guarantee"
    // consequence) — surfaced in logs so silent data loss is at least observable.
    private long _droppedSnapshotCount;
    private long _droppedLogCount;

    public TelemetryPersistenceService(
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryPersistenceService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var section = configuration.GetSection("TelemetryPersistence");
        var channelCapacity = section.GetValue("ChannelCapacity", DefaultChannelCapacity);
        _flushIntervalMs = section.GetValue("FlushIntervalMs", DefaultFlushIntervalMs);
        _maxBatchSize = section.GetValue("MaxBatchSize", DefaultMaxBatchSize);
        _maxRetryAttempts = section.GetValue("MaxRetryAttempts", DefaultMaxRetryAttempts);
        _retryDelayMs = section.GetValue("RetryDelayMs", DefaultRetryDelayMs);
        _deadLetterDirectory = section.GetValue("DeadLetterDirectory", DefaultDeadLetterDirectory)
            ?? DefaultDeadLetterDirectory;

        // DropOldest: under sustained overload, prefer dropping the oldest buffered item over
        // blocking the HTTP request thread that's trying to write into the channel.
        var options = new BoundedChannelOptions(channelCapacity)
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
            _flushIntervalMs, _maxBatchSize);

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
    /// Drain both channels until either the configured flush interval has elapsed or a batch
    /// reaches the configured max batch size, whichever comes first.
    /// </summary>
    private async Task<(List<TelemetrySnapshotEntity> snapshots, List<VehicleLogEntity> logs)> DrainAsync(
        CancellationToken stoppingToken)
    {
        var snapshots = new List<TelemetrySnapshotEntity>(_maxBatchSize);
        var logs = new List<VehicleLogEntity>(_maxBatchSize);
        var cycleStart = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested &&
               (DateTime.UtcNow - cycleStart).TotalMilliseconds < _flushIntervalMs &&
               snapshots.Count < _maxBatchSize &&
               logs.Count < _maxBatchSize)
        {
            var drainedAny = false;

            while (snapshots.Count < _maxBatchSize && _snapshotChannel.Reader.TryRead(out var snap))
            {
                snapshots.Add(snap);
                drainedAny = true;
            }

            while (logs.Count < _maxBatchSize && _logChannel.Reader.TryRead(out var log))
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
    ///
    /// Durability (ADR-001 follow-up — "no at-least-once guarantee" was a known gap): a batch
    /// that fails to persist (e.g. a transient connection blip, not a permanent FK violation) is
    /// retried a few times with a short delay before being given up on. If it still fails after
    /// all attempts, the batch is written to a dead-letter JSON file on disk instead of being
    /// silently discarded, and a running dropped-item counter is logged so the loss is at least
    /// observable. The drain loop itself must never crash regardless of outcome.
    /// </summary>
    private async Task FlushAsync(List<TelemetrySnapshotEntity> snapshots, List<VehicleLogEntity> logs, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        if (snapshots.Count > 0)
        {
            var persisted = await TryPersistWithRetryAsync(
                () => db.TelemetrySnapshots.AddRangeAsync(snapshots, ct),
                db, ct, "telemetry snapshots", snapshots.Count);

            if (!persisted)
            {
                _droppedSnapshotCount += snapshots.Count;
                await WriteDeadLetterAsync("telemetry_snapshots", snapshots, ct);
                _logger.LogError(
                    "Dropped {Count} telemetry snapshots after {Attempts} attempt(s); written to dead-letter file. Running total dropped: {Total}.",
                    snapshots.Count, _maxRetryAttempts + 1, _droppedSnapshotCount);
            }
        }

        if (logs.Count > 0)
        {
            var persisted = await TryPersistWithRetryAsync(
                () => db.VehicleLogs.AddRangeAsync(logs, ct),
                db, ct, "vehicle logs", logs.Count);

            if (!persisted)
            {
                _droppedLogCount += logs.Count;
                await WriteDeadLetterAsync("vehicle_logs", logs, ct);
                _logger.LogError(
                    "Dropped {Count} vehicle logs after {Attempts} attempt(s); written to dead-letter file. Running total dropped: {Total}.",
                    logs.Count, _maxRetryAttempts + 1, _droppedLogCount);
            }
        }
    }

    /// <summary>
    /// Attempts AddRangeAsync (already queued via the caller's closure) + SaveChangesAsync, retrying
    /// up to _maxRetryAttempts additional times on failure with a short delay in between. Returns
    /// false (without throwing) if every attempt fails — callers are responsible for the dead-letter
    /// fallback and dropped-count bookkeeping. Note: an FK violation (unknown vehicle_id) will fail
    /// identically on every retry — retries mainly help with transient connection issues, but the
    /// entity is still eventually dead-lettered rather than infinitely retried.
    /// </summary>
    private async Task<bool> TryPersistWithRetryAsync(
        Func<Task> addRangeAsync, FleetDbContext db, CancellationToken ct, string label, int count)
    {
        for (var attempt = 0; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                await addRangeAsync();
                await db.SaveChangesAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                db.ChangeTracker.Clear();

                if (attempt < _maxRetryAttempts)
                {
                    _logger.LogWarning(ex,
                        "Attempt {Attempt}/{Max} failed persisting {Count} {Label}; retrying in {Delay}ms.",
                        attempt + 1, _maxRetryAttempts + 1, count, label, _retryDelayMs);
                    try
                    {
                        await Task.Delay(_retryDelayMs, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }
                }
                else
                {
                    _logger.LogError(ex, "Final attempt {Attempt}/{Max} failed persisting {Count} {Label}.",
                        attempt + 1, _maxRetryAttempts + 1, count, label);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Best-effort fallback so a batch that can't reach Postgres isn't lost outright — appends a
    /// single JSON line (one file per failed flush) to _deadLetterDirectory. This is deliberately
    /// simple (no rotation, no automatic replay) since it exists to bound data loss and give an
    /// operator something to inspect/replay manually, not to be a second durable queue.
    /// </summary>
    private async Task WriteDeadLetterAsync<T>(string kind, List<T> items, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(_deadLetterDirectory);
            var fileName = $"{kind}_{DateTime.UtcNow:yyyyMMddTHHmmss.fffZ}_{Guid.NewGuid():N}.json";
            var path = Path.Combine(_deadLetterDirectory, fileName);
            var json = JsonSerializer.Serialize(items, _deadLetterJsonOptions);
            await File.WriteAllTextAsync(path, json, ct);
        }
        catch (Exception ex)
        {
            // Dead-lettering itself failed (e.g. disk full/read-only volume) — this is now a
            // true, unrecoverable drop. Log loudly; do not throw, the drain loop must survive.
            _logger.LogCritical(ex,
                "Failed to write dead-letter file for {Count} {Kind}; data is unrecoverably lost.",
                items.Count, kind);
        }
    }
}
