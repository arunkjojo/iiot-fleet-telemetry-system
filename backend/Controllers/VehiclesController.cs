using FleetTelemetry.Models;
using FleetTelemetry.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace FleetTelemetry.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Get(string id)
    {
        if (TelemetrySimulationService.Vehicles.TryGetValue(id, out var v))
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

            // return recent generated logs from simulation service
            var logs = TelemetrySimulationService.GetLogs(id)
                .Select(l => new { ts = l.Ts.ToString("o"), level = l.Level, msg = l.Message })
                .ToArray();

            return Ok(new { vehicle = api, logs });
        }

        return NotFound();
    }

    [HttpGet]
    public IActionResult List()
    {
            var list = TelemetrySimulationService.Vehicles.Values.Select(v => new {
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
