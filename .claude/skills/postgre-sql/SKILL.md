---
name: postgre-sql
description: PostgreSQL conventions and EF Core migration patterns for the IIoT Fleet Telemetry System. Activates for database schema tasks, migrations, query optimization, and seeding.
---

# PostgreSQL Skill — IIoT Fleet Telemetry Database

## Connection

```
Host: localhost (or `db` in Docker Compose)
Port: 5432
Database: fleet_telemetry
User: postgres
```

**Connection string (EF Core / appsettings):**
```json
{
  "ConnectionStrings": {
    "Fleet": "Host=localhost;Database=fleet_telemetry;Username=postgres;Password=postgres"
  }
}
```

**Environment variable (Docker Compose):**
```
ConnectionStrings__Fleet=Host=db;Database=fleet_telemetry;Username=postgres;Password=postgres
```

## Schema

### vehicles

```sql
CREATE TABLE vehicles (
    id          VARCHAR(20) PRIMARY KEY,
    driver_name VARCHAR(100) NOT NULL,
    model       VARCHAR(50)  NOT NULL,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
```

### telemetry_snapshots

```sql
CREATE TABLE telemetry_snapshots (
    id            BIGSERIAL    PRIMARY KEY,
    vehicle_id    VARCHAR(20)  NOT NULL REFERENCES vehicles(id),
    recorded_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    latitude      DOUBLE PRECISION,
    longitude     DOUBLE PRECISION,
    fuel_percent  DOUBLE PRECISION,
    speed_kph     DOUBLE PRECISION,
    engine_health INT,
    temp_celsius  INT,
    cargo_load    INT,
    status        VARCHAR(10)
);

CREATE INDEX idx_telemetry_vehicle_time
  ON telemetry_snapshots(vehicle_id, recorded_at DESC);
```

### vehicle_logs

```sql
CREATE TABLE vehicle_logs (
    id         BIGSERIAL   PRIMARY KEY,
    vehicle_id VARCHAR(20) NOT NULL REFERENCES vehicles(id),
    logged_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    level      VARCHAR(10),
    message    TEXT
);

CREATE INDEX idx_logs_vehicle_time
  ON vehicle_logs(vehicle_id, logged_at DESC);
```

## Naming Conventions

| Object | Convention | Example |
|--------|-----------|---------|
| Table names | snake_case, plural | `vehicles`, `vehicle_logs` |
| Column names | snake_case | `vehicle_id`, `fuel_percent` |
| Index names | `idx_{table}_{columns}` | `idx_logs_vehicle_time` |
| PK on lookup tables | `VARCHAR(20)` (business key) | `vehicles.id` |
| PK on log/time-series tables | `BIGSERIAL` | `vehicle_logs.id` |
| Timestamp columns | `TIMESTAMPTZ` (always UTC) | `logged_at`, `recorded_at` |

## EF Core Entity Pattern

```csharp
// backend/Data/Entities/VehicleEntity.cs
[Table("vehicles")]
public class VehicleEntity {
    [Key]
    [Column("id")]
    public string Id { get; set; } = "";

    [Column("driver_name")]
    public string DriverName { get; set; } = "";

    [Column("model")]
    public string Model { get; set; } = "";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public ICollection<VehicleLogEntity> Logs { get; set; } = [];
}
```

Use `[Column("snake_case_name")]` on every property to map C# PascalCase to DB snake_case.

## Seeding Pattern (10,000 vehicles)

```csharp
// backend/Data/DbSeeder.cs
public static class DbSeeder {
    public static async Task SeedVehiclesAsync(IServiceScope scope) {
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        if (await db.Vehicles.AnyAsync()) return; // idempotent

        var drivers = new[] { "Joy","Rinto","Aisha","Maya","Sam","Liam","Noah","Eva","Zara","Omar","Isha","Kaden" };
        var models  = new[] { "NV Cargo", "Apex Hauler" };

        var batch = new List<VehicleEntity>(500);
        for (int i = 0; i < 10_000; i++) {
            batch.Add(new VehicleEntity {
                Id         = $"VEH-{i:D5}",
                DriverName = drivers[i % drivers.Length],
                Model      = models[i % 2],
            });
            if (batch.Count == 500) {
                await db.Vehicles.AddRangeAsync(batch);
                await db.SaveChangesAsync();
                batch.Clear();
            }
        }
        if (batch.Count > 0) {
            await db.Vehicles.AddRangeAsync(batch);
            await db.SaveChangesAsync();
        }
    }
}
```

## Useful psql Commands

```bash
# Connect
psql -U postgres -d fleet_telemetry

# Verify tables
\dt

# Check vehicle count
SELECT COUNT(*) FROM vehicles;

# Check recent logs
SELECT vehicle_id, level, message, logged_at
FROM vehicle_logs
ORDER BY logged_at DESC
LIMIT 10;

# Check status distribution (from telemetry_snapshots)
SELECT status, COUNT(*) FROM telemetry_snapshots
WHERE recorded_at > NOW() - INTERVAL '1 minute'
GROUP BY status;
```

## Migration Workflow

```bash
cd backend

# Add new migration after entity/context changes
dotnet ef migrations add <MigrationName>

# Apply to DB
dotnet ef database update

# Verify in psql
psql -U postgres -d fleet_telemetry -c "\dt"

# Rollback if needed
dotnet ef database update 0
```

## Query Performance Rules

1. Always add indexes for `(vehicle_id, timestamp DESC)` on time-series tables
2. Use `AsNoTracking()` for all read-only queries in controllers
3. Use `AnyAsync()` not `CountAsync() > 0` for existence checks
4. Batch inserts in chunks of 500 to avoid statement timeouts on 10k rows
5. Use `ToDictionaryAsync(v => v.Id)` for O(1) join with in-memory vehicle state
