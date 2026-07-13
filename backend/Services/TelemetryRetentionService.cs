using FleetTelemetry.Data;
using Microsoft.EntityFrameworkCore;

namespace FleetTelemetry.Services;

/// <summary>
/// Periodic bounded-batch deletion of aged telemetry_snapshots/vehicle_logs rows
/// (ADR-001 action item #5 — "no retention/cleanup policy yet, both tables grow forever
/// under sustained live ingestion").
///
/// Mirrors TelemetryPersistenceService's scoped-FleetDbContext-per-cycle pattern: this
/// service itself is registered with singleton (hosted-service) lifetime, but FleetDbContext
/// is scoped, so a fresh IServiceScope/DbContext is created per sweep rather than held for the
/// service's lifetime.
///
/// Each sweep deletes rows older than RetentionDays in DeleteBatchSize-row chunks, capped at
/// MaxChunksPerSweep chunks per table, so a single sweep against a large backlog cannot run
/// unbounded — any rows left over are picked up on the next sweep interval. Only
/// telemetry_snapshots/vehicle_logs retention is in scope here; dead-letter JSON file cleanup
/// (ADR-001 action item #5's other half) is explicitly out of scope for this service.
/// </summary>
public class TelemetryRetentionService : BackgroundService
{
    private const int DefaultRetentionDays = 30;
    private const int DefaultSweepIntervalMinutes = 60;
    private const int DefaultDeleteBatchSize = 5000;
    private const int DefaultMaxChunksPerSweep = 20;

    private readonly int _retentionDays;
    private readonly TimeSpan _sweepInterval;
    private readonly int _deleteBatchSize;
    private readonly int _maxChunksPerSweep;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryRetentionService> _logger;

    public TelemetryRetentionService(
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryRetentionService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var section = configuration.GetSection("TelemetryRetention");
        _retentionDays = section.GetValue("RetentionDays", DefaultRetentionDays);
        _sweepInterval = TimeSpan.FromMinutes(section.GetValue("SweepIntervalMinutes", DefaultSweepIntervalMinutes));
        _deleteBatchSize = section.GetValue("DeleteBatchSize", DefaultDeleteBatchSize);
        _maxChunksPerSweep = section.GetValue("MaxChunksPerSweep", DefaultMaxChunksPerSweep);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TelemetryRetentionService starting (retention {Days}d, sweep every {Interval}, batch {Batch}, max {Chunks} chunks/table/sweep).",
            _retentionDays, _sweepInterval, _deleteBatchSize, _maxChunksPerSweep);

        // Sweep once immediately on startup, then on the configured interval — mirrors
        // TelemetryPersistenceService's "don't wait a full cycle before doing anything useful"
        // behavior. A failed sweep is caught and logged so it never crashes the host; the next
        // interval simply tries again.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention sweep failed; will retry on the next interval.");
            }

            try
            {
                await Task.Delay(_sweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunSweepAsync(CancellationToken ct)
    {
        // UTC throughout: recorded_at/logged_at are TIMESTAMPTZ columns written via
        // DateTime.UtcNow elsewhere in the pipeline (TelemetryIngestController), and Npgsql
        // requires DateTimeKind.Utc for timestamptz comparisons — DateTime.Now here would
        // silently compare against the wrong offset.
        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        var deletedSnapshots = await DeleteOldSnapshotsAsync(db, cutoff, ct);
        var deletedLogs = await DeleteOldLogsAsync(db, cutoff, ct);

        _logger.LogInformation(
            "Retention sweep: deleted {Snapshots} snapshots, {Logs} logs older than {Cutoff:o}.",
            deletedSnapshots, deletedLogs, cutoff);
    }

    /// <summary>
    /// Deletes telemetry_snapshots rows older than cutoff in DeleteBatchSize-row chunks, up to
    /// MaxChunksPerSweep chunks. ExecuteDeleteAsync does not reliably translate when combined
    /// directly with Take() across providers, so each chunk first selects a bounded page of ids
    /// (ordered for determinism) and then issues a single ExecuteDeleteAsync filtered on that id
    /// set — two round-trips per chunk instead of one, but a translatable, provider-safe query.
    /// </summary>
    private async Task<int> DeleteOldSnapshotsAsync(FleetDbContext db, DateTime cutoff, CancellationToken ct)
    {
        var totalDeleted = 0;

        for (var chunk = 0; chunk < _maxChunksPerSweep; chunk++)
        {
            ct.ThrowIfCancellationRequested();

            var ids = await db.TelemetrySnapshots
                .Where(s => s.RecordedAt < cutoff)
                .OrderBy(s => s.Id)
                .Select(s => s.Id)
                .Take(_deleteBatchSize)
                .ToListAsync(ct);

            if (ids.Count == 0)
            {
                break;
            }

            totalDeleted += await db.TelemetrySnapshots
                .Where(s => ids.Contains(s.Id))
                .ExecuteDeleteAsync(ct);

            if (ids.Count < _deleteBatchSize)
            {
                // Fewer rows matched than a full batch — nothing left older than cutoff.
                break;
            }
        }

        return totalDeleted;
    }

    /// <summary>Same bounded-chunk pattern as DeleteOldSnapshotsAsync, for vehicle_logs.</summary>
    private async Task<int> DeleteOldLogsAsync(FleetDbContext db, DateTime cutoff, CancellationToken ct)
    {
        var totalDeleted = 0;

        for (var chunk = 0; chunk < _maxChunksPerSweep; chunk++)
        {
            ct.ThrowIfCancellationRequested();

            var ids = await db.VehicleLogs
                .Where(l => l.LoggedAt < cutoff)
                .OrderBy(l => l.Id)
                .Select(l => l.Id)
                .Take(_deleteBatchSize)
                .ToListAsync(ct);

            if (ids.Count == 0)
            {
                break;
            }

            totalDeleted += await db.VehicleLogs
                .Where(l => ids.Contains(l.Id))
                .ExecuteDeleteAsync(ct);

            if (ids.Count < _deleteBatchSize)
            {
                break;
            }
        }

        return totalDeleted;
    }
}
