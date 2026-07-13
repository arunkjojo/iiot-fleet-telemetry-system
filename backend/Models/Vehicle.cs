using MessagePack;

namespace FleetTelemetry.Models;

[MessagePackObject]
public class Vehicle
{
    [Key(0)] public string Id { get; set; } = string.Empty;
    [Key(1)] public string DriverName { get; set; } = string.Empty;
    [Key(2)] public double Latitude { get; set; }
    [Key(3)] public double Longitude { get; set; }
    [Key(4)] public double FuelPercent { get; set; }
    [Key(5)] public double SpeedKph { get; set; }
    [Key(6)] public int EngineHealth { get; set; }
    [Key(7)] public string Model { get; set; } = string.Empty;
    [Key(8)] public string Status { get; set; } = "active";
    [Key(9)] public int Temp { get; set; }
    [Key(10)] public int CargoLoad { get; set; }
    [Key(11)] public string DisplayNumber { get; set; } = string.Empty;
}
