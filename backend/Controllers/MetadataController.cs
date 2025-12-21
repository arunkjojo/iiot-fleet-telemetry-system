using FleetTelemetry.Models;
using Microsoft.AspNetCore.Mvc;

namespace FleetTelemetry.Controllers;

[ApiController]
[Route("api/vehicles/metadata")]
public class MetadataController : ControllerBase
{
    // Returns static list of vehicle ids and driver names
    [HttpGet]
    public IActionResult Get()
    {
        var list = Enumerable.Range(0, 10000).Select(i => new {
            Id = $"VEH-{i:D5}",
            DriverName = $"Driver {i % 500}"
        });

        return Ok(list);
    }
}
