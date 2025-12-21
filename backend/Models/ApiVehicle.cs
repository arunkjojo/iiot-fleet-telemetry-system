using System.Text.Json.Serialization;

namespace FleetTelemetry.Models;

public class ApiVehicle
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
    [JsonPropertyName("driver")] public string Driver { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("fuel")] public int Fuel { get; set; }
    [JsonPropertyName("temp")] public int Temp { get; set; }
    [JsonPropertyName("speedKph")] public int SpeedKph { get; set; }
    [JsonPropertyName("cargoLoad")] public int CargoLoad { get; set; }
    [JsonPropertyName("lat")] public double Lat { get; set; }
    [JsonPropertyName("lng")] public double Lng { get; set; }
}
