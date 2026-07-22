using Microsoft.AspNetCore.Mvc;
using FleetTelemetry.Services;

namespace FleetTelemetry.Controllers;

[ApiController]
[Route("api/vehicles/{vehicleId}/logs")]
public class LogsController : ControllerBase
{
    private readonly ILiveTelemetryStore _liveStore;

    public LogsController(ILiveTelemetryStore liveStore)
    {
        _liveStore = liveStore;
    }

    [HttpGet]
    public IActionResult Get(string vehicleId)
    {
        var logs = _liveStore.GetLogs(vehicleId)
            .Select(l => new { ts = l.Ts.ToString("o"), level = l.Level, msg = l.Message })
            .ToArray();

        return Ok(logs);
    }
}
