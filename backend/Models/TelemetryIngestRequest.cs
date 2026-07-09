namespace FleetTelemetry.Models;

/// <summary>
/// Request DTO for POST /api/telemetry/ingest. Bound from JSON using ASP.NET Core's
/// default camelCase System.Text.Json policy — the Python emitter (INFRA-002) sends
/// camelCase keys (vehicleId, driverName, ...), so no [JsonPropertyName] overrides
/// are needed here (contrast with ApiVehicle, which controls *outbound* naming).
/// </summary>
public class TelemetryIngestRequest
{
    public string VehicleId { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public string? Model { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double FuelPercent { get; set; }
    public double SpeedKph { get; set; }
    public int EngineHealth { get; set; }
    public int TempCelsius { get; set; }
    public int CargoLoad { get; set; }

    /// <summary>Optional. Server defaults to DateTime.UtcNow when omitted.</summary>
    public DateTime? RecordedAtUtc { get; set; }
}
