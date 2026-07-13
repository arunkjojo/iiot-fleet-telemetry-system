using FleetTelemetry.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetTelemetry.Data;

public class FleetDbContext : DbContext
{
    public FleetDbContext(DbContextOptions<FleetDbContext> options) : base(options) { }

    public DbSet<VehicleEntity> Vehicles => Set<VehicleEntity>();
    public DbSet<VehicleLogEntity> VehicleLogs => Set<VehicleLogEntity>();
    public DbSet<TelemetrySnapshotEntity> TelemetrySnapshots => Set<TelemetrySnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // vehicles
        modelBuilder.Entity<VehicleEntity>(e =>
        {
            e.ToTable("vehicles");
            e.Property(v => v.CreatedAt)
             .HasDefaultValueSql("NOW()");
        });

        // vehicle_logs
        modelBuilder.Entity<VehicleLogEntity>(e =>
        {
            e.ToTable("vehicle_logs");
            e.Property(l => l.LoggedAt)
             .HasDefaultValueSql("NOW()");
            e.HasIndex(l => new { l.VehicleId, l.LoggedAt })
             .HasDatabaseName("idx_logs_vehicle_time")
             .IsDescending(false, true);
        });

        // telemetry_snapshots
        modelBuilder.Entity<TelemetrySnapshotEntity>(e =>
        {
            e.ToTable("telemetry_snapshots");
            e.Property(s => s.RecordedAt)
             .HasDefaultValueSql("NOW()");
            e.HasIndex(s => new { s.VehicleId, s.RecordedAt })
             .HasDatabaseName("idx_telemetry_vehicle_time")
             .IsDescending(false, true);
        });
    }
}
