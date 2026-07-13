using FleetTelemetry.Data;
using FleetTelemetry.Models;
using FleetTelemetry.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace FleetTelemetry.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILiveTelemetryStore _liveStore;
    private readonly FleetDbContext _db;

    private const int DriverNameMaxLength = 100;
    private const int DisplayNumberMaxLength = 30;

    public VehiclesController(IConfiguration config, ILiveTelemetryStore liveStore, FleetDbContext db)
    {
        _config = config;
        _liveStore = liveStore;
        _db = db;
    }

    // USE_LIVE_TELEMETRY=true reads from ILiveTelemetryStore (fed by POST /api/telemetry/ingest);
    // false (default) keeps reading from TelemetrySimulationService.Vehicles, unchanged from Sprint 01.
    private bool UseLiveTelemetry => _config.GetValue<bool>("USE_LIVE_TELEMETRY", false);

    [HttpGet("{id}")]
    public IActionResult Get(string id)
    {
        Vehicle? v = null;
        if (UseLiveTelemetry)
        {
            _liveStore.TryGet(id, out v);
        }
        else
        {
            TelemetrySimulationService.Vehicles.TryGetValue(id, out v);
        }

        if (v != null)
        {
            var api = new ApiVehicle
            {
                Id = v.Id,
                Model = string.IsNullOrEmpty(v.Model) ? "NV Cargo" : v.Model,
                Driver = v.DriverName,
                Status = v.Status,
                Fuel = (int)System.Math.Round((double)v.FuelPercent),
                Temp = (int)System.Math.Round((double)v.Temp),
                SpeedKph = (int)System.Math.Round((double)v.SpeedKph),
                CargoLoad = v.CargoLoad,
                Lat = v.Latitude,
                Lng = v.Longitude,
                DisplayNumber = v.DisplayNumber
            };

            // return recent logs from the active data source (live store or simulation service)
            var logs = (UseLiveTelemetry ? _liveStore.GetLogs(id) : TelemetrySimulationService.GetLogs(id))
                .Select(l => new { ts = l.Ts.ToString("o"), level = l.Level, msg = l.Message })
                .ToArray();

            return Ok(new { vehicle = api, logs });
        }

        return NotFound();
    }

    [HttpGet]
    public IActionResult List()
    {
        var vehicles = UseLiveTelemetry ? _liveStore.GetAll() : (IEnumerable<Vehicle>)TelemetrySimulationService.Vehicles.Values;
        var list = vehicles.Select(v => new {
            id = v.Id,
            model = string.IsNullOrEmpty(v.Model) ? "NV Cargo" : v.Model,
            driver = v.DriverName,
            status = v.Status,
                fuel = (int)System.Math.Round((double)v.FuelPercent),
                temp = (int)System.Math.Round((double)v.Temp),
                speedKph = (int)System.Math.Round((double)v.SpeedKph),
            cargoLoad = v.CargoLoad,
            engineHealth = v.EngineHealth,
            lat = v.Latitude,
            lng = v.Longitude,
            displayNumber = v.DisplayNumber
        }).ToArray();

        return Ok(list);
    }

    // BE-006: edits driver_name/display_number only — the {id} route parameter (primary key,
    // FK target for telemetry_snapshots/vehicle_logs, and the exact string the Python emitter
    // sources from GET /api/vehicles/metadata) is NEVER mutated; the request body has no
    // id/vehicleId field. Mutates whichever in-memory store is currently active so the edit is
    // visible immediately, then persists synchronously to Postgres (low-frequency admin action,
    // unlike TelemetryIngestController's buffered pipeline).
    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(string id, [FromBody] PatchVehicleRequest request)
    {
        var driverName = request?.DriverName?.Trim();
        var displayNumber = request?.DisplayNumber?.Trim();

        var hasDriverName = !string.IsNullOrEmpty(driverName);
        var hasDisplayNumber = !string.IsNullOrEmpty(displayNumber);

        if (!hasDriverName && !hasDisplayNumber)
        {
            return BadRequest(new { error = "At least one of driverName or displayNumber must be provided." });
        }

        if (hasDriverName && driverName!.Length > DriverNameMaxLength)
        {
            return BadRequest(new { error = $"driverName must be {DriverNameMaxLength} characters or fewer." });
        }

        if (hasDisplayNumber && displayNumber!.Length > DisplayNumberMaxLength)
        {
            return BadRequest(new { error = $"displayNumber must be {DisplayNumberMaxLength} characters or fewer." });
        }

        Vehicle? v = null;
        if (UseLiveTelemetry)
        {
            _liveStore.TryGet(id, out v);
        }
        else
        {
            TelemetrySimulationService.Vehicles.TryGetValue(id, out v);
        }

        if (v == null)
        {
            return NotFound();
        }

        // Mutate the in-memory Vehicle in place — safe under concurrent access from the
        // simulation loop / live-ingestion writes, consistent with existing reads of these stores.
        if (hasDriverName)
        {
            v.DriverName = driverName!;
        }
        if (hasDisplayNumber)
        {
            v.DisplayNumber = displayNumber!;
        }

        // Postgres write: DbSeeder always seeds all 10,000 rows regardless of USE_LIVE_TELEMETRY,
        // so the row should exist in both modes. Guard defensively in case it doesn't (e.g. a
        // reduced local seed) rather than failing the whole request after the in-memory update.
        var entity = await _db.Vehicles.FindAsync(id);
        if (entity != null)
        {
            if (hasDriverName)
            {
                entity.DriverName = driverName!;
            }
            if (hasDisplayNumber)
            {
                entity.DisplayNumber = displayNumber!;
            }
            await _db.SaveChangesAsync();
        }

        var api = new ApiVehicle
        {
            Id = v.Id,
            Model = string.IsNullOrEmpty(v.Model) ? "NV Cargo" : v.Model,
            Driver = v.DriverName,
            Status = v.Status,
            Fuel = (int)System.Math.Round((double)v.FuelPercent),
            Temp = (int)System.Math.Round((double)v.Temp),
            SpeedKph = (int)System.Math.Round((double)v.SpeedKph),
            CargoLoad = v.CargoLoad,
            Lat = v.Latitude,
            Lng = v.Longitude,
            DisplayNumber = v.DisplayNumber
        };

        return Ok(api);
    }
}
