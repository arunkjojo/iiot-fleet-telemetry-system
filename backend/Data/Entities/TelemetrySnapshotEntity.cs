using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FleetTelemetry.Data.Entities;

[Table("telemetry_snapshots")]
public class TelemetrySnapshotEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("vehicle_id")]
    [MaxLength(20)]
    public string VehicleId { get; set; } = string.Empty;

    [Column("recorded_at")]
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("fuel_percent")]
    public double? FuelPercent { get; set; }

    [Column("speed_kph")]
    public double? SpeedKph { get; set; }

    [Column("engine_health")]
    public int? EngineHealth { get; set; }

    [Column("temp_celsius")]
    public int? TempCelsius { get; set; }

    [Column("cargo_load")]
    public int? CargoLoad { get; set; }

    [Column("status")]
    [MaxLength(10)]
    public string? Status { get; set; }

    [ForeignKey(nameof(VehicleId))]
    public VehicleEntity? Vehicle { get; set; }
}
