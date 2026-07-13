using FleetTelemetry.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetTelemetry.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly HubConnectionTracker _tracker;

    public HealthController(HubConnectionTracker tracker)
    {
        _tracker = tracker;
    }

    // GET /api/health/signalr — connected-client count for the /fleethub hub.
    // Works regardless of USE_LIVE_TELEMETRY, since MapHub<FleetHub>("/fleethub") is
    // always registered in Program.cs.
    [HttpGet("signalr")]
    public IActionResult GetSignalRHealth()
    {
        return Ok(new
        {
            connectedClients = _tracker.Count,
            lastEventAtUtc = _tracker.LastEventAtUtc
        });
    }
}
