using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FleetTelemetry.Data.Entities;

[Table("vehicles")]
public class VehicleEntity
{
    [Key]
    [Column("id")]
    [MaxLength(20)]
    public string Id { get; set; } = string.Empty;

    [Column("driver_name")]
    [MaxLength(100)]
    public string DriverName { get; set; } = string.Empty;

    [Column("model")]
    [MaxLength(50)]
    public string Model { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<VehicleLogEntity> Logs { get; set; } = [];
    public ICollection<TelemetrySnapshotEntity> Snapshots { get; set; } = [];
}
