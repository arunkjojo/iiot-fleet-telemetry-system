using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FleetTelemetry.Data.Entities;

[Table("vehicle_logs")]
public class VehicleLogEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("vehicle_id")]
    [MaxLength(20)]
    public string VehicleId { get; set; } = string.Empty;

    [Column("logged_at")]
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    [Column("level")]
    [MaxLength(10)]
    public string Level { get; set; } = "INFO";

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [ForeignKey(nameof(VehicleId))]
    public VehicleEntity? Vehicle { get; set; }
}
