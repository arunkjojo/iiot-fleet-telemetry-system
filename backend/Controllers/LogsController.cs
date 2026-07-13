using Microsoft.AspNetCore.Mvc;
using FleetTelemetry.Services;

namespace FleetTelemetry.Controllers;

[ApiController]
[Route("api/vehicles/{vehicleId}/logs")]
public class LogsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILiveTelemetryStore _liveStore;

    public LogsController(IConfiguration config, ILiveTelemetryStore liveStore)
    {
        _config = config;
        _liveStore = liveStore;
    }

    // Same live/dummy branching as VehiclesController — see USE_LIVE_TELEMETRY.
    private bool UseLiveTelemetry => _config.GetValue<bool>("USE_LIVE_TELEMETRY", false);

    [HttpGet]
    public IActionResult Get(string vehicleId)
    {
        var logs = (UseLiveTelemetry ? _liveStore.GetLogs(vehicleId) : TelemetrySimulationService.GetLogs(vehicleId))
            .Select(l => new { ts = l.Ts.ToString("o"), level = l.Level, msg = l.Message })
            .ToArray();

        return Ok(logs);
    }
}
