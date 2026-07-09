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

    public VehiclesController(IConfiguration config, ILiveTelemetryStore liveStore)
    {
        _config = config;
        _liveStore = liveStore;
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
                Lng = v.Longitude
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
            lng = v.Longitude
        }).ToArray();

        return Ok(list);
    }
}
