using System;

namespace FleetTelemetry.Models;

public class VehicleLog
{
    public DateTime Ts { get; set; }
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
}
