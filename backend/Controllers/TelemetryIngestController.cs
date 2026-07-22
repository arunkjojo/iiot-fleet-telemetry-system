using FleetTelemetry.Data.Entities;
using FleetTelemetry.Models;
using FleetTelemetry.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetTelemetry.Controllers;

/// <summary>
/// Live telemetry ingestion (BE-002). Thin by design: validates the payload, computes status
/// via VehicleStatusEvaluator, upserts ILiveTelemetryStore (for immediate read-back /
/// broadcast), and enqueues durable writes into ITelemetryIngestQueue. Never calls
/// SaveChangesAsync synchronously — persistence is TelemetryPersistenceService's job, drained
/// in batches, so the number of emitters posting here never translates 1:1 into DB connections.
/// </summary>
[ApiController]
[Route("api/telemetry")]
public class TelemetryIngestController : ControllerBase
{
    private readonly ILiveTelemetryStore _liveStore;
    private readonly ITelemetryIngestQueue _queue;
    private readonly ILogger<TelemetryIngestController> _logger;

    public TelemetryIngestController(
        ILiveTelemetryStore liveStore,
        ITelemetryIngestQueue queue,
        ILogger<TelemetryIngestController> logger)
    {
        _liveStore = liveStore;
        _queue = queue;
        _logger = logger;
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] TelemetryIngestRequest request)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.VehicleId))
        {
            return BadRequest(new { error = "vehicleId is required." });
        }

        // Sanity bounds — reject wildly out-of-range payloads without being overly strict.
        // A small tolerance is allowed on the low end so evaluator edge cases (e.g. exactly 0)
        // still validate.
        if (request.FuelPercent < -1.0 || request.FuelPercent > 101.0)
        {
            return BadRequest(new { error = "fuelPercent out of range (expected 0-100)." });
        }

        if (request.SpeedKph < 0.0 || request.SpeedKph > 300.0)
        {
            return BadRequest(new { error = "speedKph out of range (expected 0-300)." });
        }

        if (request.EngineHealth < -1 || request.EngineHealth > 101)
        {
            return BadRequest(new { error = "engineHealth out of range (expected 0-100)." });
        }

        if (request.TempCelsius < -50 || request.TempCelsius > 150)
        {
            return BadRequest(new { error = "tempCelsius out of range (expected -50 to 150)." });
        }

        if (request.CargoLoad < 0 || request.CargoLoad > 100_000)
        {
            return BadRequest(new { error = "cargoLoad out of range." });
        }

        if (request.Latitude < -90.0 || request.Latitude > 90.0)
        {
            return BadRequest(new { error = "latitude out of range (expected -90 to 90)." });
        }

        if (request.Longitude < -180.0 || request.Longitude > 180.0)
        {
            return BadRequest(new { error = "longitude out of range (expected -180 to 180)." });
        }

        var vehicleId = request.VehicleId.Trim();

        var computedStatus = VehicleStatusEvaluator.Evaluate(
            request.FuelPercent, request.TempCelsius, request.SpeedKph, request.EngineHealth);

        // Read previous state BEFORE upserting, so we can detect the first-ever reading and
        // status-change transitions.
        var hasPrevious = _liveStore.TryGet(vehicleId, out var previous);

        var vehicle = new Vehicle
        {
            Id = vehicleId,
            // BE-009 (QA-003 fix): once a vehicle has prior live-store state, DriverName/
            // DisplayNumber become sticky — only PATCH /api/vehicles/{id} (BE-006) may change
            // them from here on. Without this, every ingest tick (every few seconds) rebuilt a
            // fresh Vehicle from the emitter's immutable roster-seeded DriverName (and an
            // unset, always-empty DisplayNumber), silently reverting any PATCH edit on the very
            // next tick. First-ever ingest (no `previous`) still seeds DriverName from the
            // request, matching original behavior.
            DriverName = previous?.DriverName ?? request.DriverName ?? string.Empty,
            DisplayNumber = previous?.DisplayNumber ?? string.Empty,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            FuelPercent = request.FuelPercent,
            SpeedKph = request.SpeedKph,
            EngineHealth = request.EngineHealth,
            Model = request.Model ?? previous?.Model ?? string.Empty,
            Status = computedStatus,
            Temp = request.TempCelsius,
            CargoLoad = request.CargoLoad,
        };

        _liveStore.Upsert(vehicle);

        var recordedAt = request.RecordedAtUtc ?? DateTime.UtcNow;

        // Always enqueue a snapshot for durable persistence.
        await _queue.EnqueueSnapshot(new TelemetrySnapshotEntity
        {
            VehicleId = vehicleId,
            RecordedAt = recordedAt,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            FuelPercent = request.FuelPercent,
            SpeedKph = request.SpeedKph,
            EngineHealth = request.EngineHealth,
            TempCelsius = request.TempCelsius,
            CargoLoad = request.CargoLoad,
            Status = computedStatus,
        });

        foreach (var (level, message) in BuildLogMessages(hasPrevious, previous, computedStatus, request))
        {
            // Mirror into the live store immediately so LogsController (BE-003) can serve it
            // without waiting for the next DB flush.
            _liveStore.AddLog(vehicleId, level, message);

            await _queue.EnqueueLog(new VehicleLogEntity
            {
                VehicleId = vehicleId,
                LoggedAt = DateTime.UtcNow,
                Level = level,
                Message = message,
            });
        }

        return Accepted(new
        {
            status = "accepted",
            vehicleId,
            computedStatus,
        });
    }

    /// <summary>
    /// Decide which log lines this ingest should produce: first-ever reading, status change,
    /// and/or threshold crossings, so LogsController output stays meaningful.
    /// </summary>
    private static IEnumerable<(string Level, string Message)> BuildLogMessages(
        bool hasPrevious, Vehicle? previous, string computedStatus, TelemetryIngestRequest request)
    {
        if (!hasPrevious || previous is null)
        {
            yield return ("INFO", "Vehicle online — live telemetry started");
        }
        else if (!string.Equals(previous.Status, computedStatus, StringComparison.OrdinalIgnoreCase))
        {
            yield return ("WARN", $"STATUS_CHANGE {previous.Status} -> {computedStatus}");
        }

        if (request.SpeedKph > 80.0)
        {
            yield return ("ERROR", "OVERSPEED detected");
        }
        else if (request.SpeedKph >= 70.0)
        {
            yield return ("WARN", "High speed");
        }

        if (request.FuelPercent < 20.0)
        {
            yield return ("ERROR", "Very low fuel");
        }
        else if (request.FuelPercent < 40.0)
        {
            yield return ("WARN", "Low fuel");
        }

        if (request.TempCelsius > 75)
        {
            yield return ("ERROR", "Critical temperature");
        }
        else if (request.TempCelsius > 65)
        {
            yield return ("WARN", "High temperature");
        }
    }
}
