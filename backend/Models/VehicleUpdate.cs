using MessagePack;

namespace FleetTelemetry.Models;

[MessagePackObject]
public class VehicleUpdate
{
    [Key(0)] public string Id { get; set; } = string.Empty;
    [Key(1)] public double Latitude { get; set; }
    [Key(2)] public double Longitude { get; set; }
    [Key(3)] public double FuelPercent { get; set; }
    [Key(4)] public double SpeedKph { get; set; }
    [Key(5)] public int EngineHealth { get; set; }
    [Key(6)] public string Status { get; set; } = string.Empty;
    [Key(7)] public int Temp { get; set; }
}
