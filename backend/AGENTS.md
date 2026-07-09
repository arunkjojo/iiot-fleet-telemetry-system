# Backend Subsystem — Agent Guide (ASP.NET)

Read this file in full before touching any file under `backend/`.

---

## Stack

| Technology | Version | Purpose |
|-----------|---------|---------|
| ASP.NET Core | 8.0 | Web API framework |
| SignalR | 8.0 | Real-time WebSocket hub |
| MessagePack | 2.6.90 | Binary serialization for SignalR payloads |
| Entity Framework Core | 8.0 (Sprint 01) | PostgreSQL ORM + migrations |
| Npgsql | 8.0 (Sprint 01) | EF Core PostgreSQL provider |
| Swagger / Swashbuckle | 6.x (Sprint 01) | API documentation UI |

---

## Directory Map

```
backend/
├── Controllers/
│   ├── VehiclesController.cs       # GET /api/vehicles, GET /api/vehicles/{id}
│   ├── LogsController.cs           # GET /api/vehicles/{vehicleId}/logs
│   ├── MetadataController.cs       # GET /api/vehicles/metadata
│   └── TelemetryIngestController.cs # POST /api/telemetry/ingest (live mode ingestion, BE-002)
├── Hubs/
│   ├── FleetHub.cs                 # SignalR hub (intentionally minimal)
│   └── IFleetClient.cs             # Client interface — ReceiveFleetUpdate()
├── Models/
│   ├── Vehicle.cs                  # Internal state model (MessagePackObject)
│   ├── VehicleUpdate.cs            # SignalR broadcast payload (MessagePackObject)
│   ├── ApiVehicle.cs               # REST response DTO (JsonPropertyName)
│   ├── VehicleLog.cs               # Telemetry log entry
│   └── TelemetryIngestRequest.cs   # POST /api/telemetry/ingest request DTO (camelCase JSON binding, BE-002)
├── Services/
│   ├── TelemetrySimulationService.cs  # BackgroundService — 10k vehicle simulation
│   ├── VehicleStatusEvaluator.cs      # Static status evaluator (live-mode canonical rules, REQUIREMENTS.md 4.1)
│   ├── LiveTelemetryStore.cs          # ILiveTelemetryStore — in-memory current-state cache + last-50-logs cache for live mode
│   ├── TelemetryPersistenceService.cs # ITelemetryIngestQueue + BackgroundService — buffered batched writer draining into PostgreSQL (telemetry_snapshots, vehicle_logs), decouples emitter/request count from DB connections (BE-002)
│   └── LiveBroadcastService.cs        # BackgroundService — relays ILiveTelemetryStore.GetAndClearDirty() to SignalR every ~500ms; skips empty ticks (BE-003)
├── Data/                           # (Sprint 01) EF Core DbContext + migrations
│   ├── FleetDbContext.cs
│   └── Migrations/
├── Program.cs                      # App startup, DI, CORS, SignalR, Swagger
├── appsettings.json                # Default config (no secrets)
├── appsettings.Development.json    # Dev overrides
├── FleetTelemetry.csproj
├── fleet-telemetry-system.sln
└── Dockerfile
```

---

## API Endpoint Map

| Method | Route | Response | Description |
|--------|-------|----------|-------------|
| GET | `/api/vehicles` | `ApiVehicle[]` | All 10,000 vehicles with current telemetry |
| GET | `/api/vehicles/{id}` | `{ vehicle, logs }` | Single vehicle detail + last 50 log entries |
| GET | `/api/vehicles/{vehicleId}/logs` | `VehicleLog[]` | Last 50 log entries for a vehicle |
| GET | `/api/vehicles/metadata` | `{ id, driver }[]` | Static metadata list for all 10k vehicles |
| POST | `/api/telemetry/ingest` | `202 { status, vehicleId, computedStatus }` | Live telemetry ingestion (BE-002) — validates payload, computes status via `VehicleStatusEvaluator`, upserts `ILiveTelemetryStore`, enqueues a buffered/batched write via `ITelemetryIngestQueue`. Only registered when `USE_LIVE_TELEMETRY=true`. `400` on missing `vehicleId` or out-of-range numeric fields. |
| WS | `/fleethub` | SignalR | Hub for `ReceiveFleetUpdate` broadcasts |

---

## Read-Path Data Source Branching (BE-003)

`VehiclesController` (`GET /api/vehicles`, `GET /api/vehicles/{id}`) and `LogsController` (`GET /api/vehicles/{vehicleId}/logs`) both inject `IConfiguration` and `ILiveTelemetryStore` and branch per-request on `IConfiguration.GetValue<bool>("USE_LIVE_TELEMETRY", false)`:

- `false` (default, local `dotnet run`): read from `TelemetrySimulationService.Vehicles` / `.GetLogs(id)` — unchanged from Sprint 01.
- `true` (Docker Compose): read from `ILiveTelemetryStore.GetAll()` / `.TryGet(id, out v)` / `.GetLogs(id)`, fed by `POST /api/telemetry/ingest` (BE-002).

`ApiVehicle` field mapping (rounding, `driver`/`model` names) is byte-for-byte identical across both branches — the frontend type contract needs no changes. `MetadataController` is source-independent and untouched; it always returns the static `VEH-00000..VEH-09999` roster.

---

## Data Models

### Vehicle (internal, MessagePack)

```csharp
[MessagePackObject]
public class Vehicle {
    [Key(0)]  public string Id { get; set; }
    [Key(1)]  public string DriverName { get; set; }
    [Key(2)]  public double Latitude { get; set; }
    [Key(3)]  public double Longitude { get; set; }
    [Key(4)]  public double FuelPercent { get; set; }
    [Key(5)]  public double SpeedKph { get; set; }
    [Key(6)]  public int EngineHealth { get; set; }
    [Key(7)]  public string Model { get; set; }
    [Key(8)]  public string Status { get; set; }  // active|warning|danger|offline
    [Key(9)]  public int Temp { get; set; }
    [Key(10)] public int CargoLoad { get; set; }
}
```

### VehicleUpdate (SignalR broadcast)

Subset of Vehicle fields sent every ~500ms to all connected clients via `ReceiveFleetUpdate`.

### ApiVehicle (REST response DTO)

Uses `[JsonPropertyName("snake_case")]` for all properties to match frontend expectations.

### VehicleLog

```csharp
public class VehicleLog {
    public DateTime Ts { get; set; }       // UTC timestamp
    public string Level { get; set; }      // INFO | WARN | ERROR
    public string Message { get; set; }
}
```

---

## TelemetrySimulationService Behavior

The `TelemetrySimulationService` (514 lines) is the core of the demo:

- Seeds 10,000 vehicles across 200 synthetic SF corridors on startup
- Runs a 500ms loop using `Parallel.ForEach` to update all vehicle metrics
- Status classification priority: **offline > danger > warning > active**
- Enforces distribution caps every ~20 ticks: offline ≤12, danger ≤14, warning ≤24
- Broadcasts `VehicleUpdate[]` via SignalR to all connected clients
- Keeps last 50 log entries per vehicle in `ConcurrentQueue<VehicleLog>`

**Do not add HTTP clients or database calls inside this service** — it is designed to be fully in-memory for the simulation.

---

## CORS Configuration

CORS is configured in `Program.cs` via environment variables:

| Env Var | Purpose | Default |
|---------|---------|---------|
| `FRONTEND_ORIGIN` | Primary allowed origin | `http://localhost:3000` |
| `ADDITIONAL_FRONTEND_ORIGINS` | Comma-separated additional origins | (none) |

---

## SignalR / MessagePack Rules

1. Hub class `FleetHub` stays minimal — server pushes via `IHubContext<FleetHub, IFleetClient>`
2. Hub URL `/fleethub` is immutable — never change without coordinating frontend env var update
3. All models sent over SignalR MUST have `[MessagePackObject]` and `[Key(N)]` attributes
4. Do not mix JSON and MessagePack on the same connection — the frontend negotiates the protocol

---

## PostgreSQL Integration (Sprint 01)

**Connection string** (from environment / appsettings):

```json
{
  "ConnectionStrings": {
    "Fleet": "Host=localhost;Database=fleet_telemetry;Username=postgres;Password=yourpassword"
  }
}
```

**Planned schema:**

```sql
-- vehicles table (persistent metadata)
CREATE TABLE vehicles (
    id VARCHAR(20) PRIMARY KEY,
    driver_name VARCHAR(100) NOT NULL,
    model VARCHAR(50) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- telemetry_snapshots table (time-series)
CREATE TABLE telemetry_snapshots (
    id BIGSERIAL PRIMARY KEY,
    vehicle_id VARCHAR(20) REFERENCES vehicles(id),
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    fuel_percent DOUBLE PRECISION,
    speed_kph DOUBLE PRECISION,
    engine_health INT,
    temp_celsius INT,
    cargo_load INT,
    status VARCHAR(10)
);

-- vehicle_logs table (event log)
CREATE TABLE vehicle_logs (
    id BIGSERIAL PRIMARY KEY,
    vehicle_id VARCHAR(20) REFERENCES vehicles(id),
    logged_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    level VARCHAR(10),
    message TEXT
);
```

**EF Core workflow:**
```bash
cd backend
dotnet ef migrations add InitialSchema
dotnet ef database update
```

---

## Swagger / Swashbuckle Setup (Sprint 01)

Add to `Program.cs`:
```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In app pipeline (Development + Production):
app.UseSwagger();
app.UseSwaggerUI();
```

Swagger UI available at: `http://localhost:8080/swagger`

---

## Coding Conventions

- **Controllers:** thin — parse request, call service, return result. No business logic.
- **Naming:** PascalCase for all C# identifiers; snake_case for SQL columns; camelCase for JSON properties via `[JsonPropertyName]`
- **Nullable:** enabled project-wide — all string properties must be initialized or marked nullable
- **No static state** in controllers — all shared state via DI services
- **appsettings.json:** no secrets — use environment variables or user secrets for connection strings

---

## Build & Run Commands

```bash
cd backend

dotnet restore                        # restore NuGet packages
dotnet build                          # must succeed with zero errors
dotnet run                            # start at http://localhost:8080
dotnet ef migrations add <Name>       # create new migration
dotnet ef database update             # apply pending migrations
dotnet test                           # run tests (when test project exists)
```

---

## Pre-Commit Checklist (ASP.NET agent)

- [ ] `dotnet build` succeeds with zero errors
- [ ] No hardcoded connection strings in source files
- [ ] No unused `using` statements
- [ ] SignalR hub path `/fleethub` unchanged
- [ ] New MessagePack models have `[MessagePackObject]` + `[Key(N)]` on all properties
- [ ] New EF Core migration added if schema changed

---

## Do NOT Touch

- `backend/Services/TelemetrySimulationService.cs` — do not add DB calls or HTTP clients; in-memory only
- `backend/Hubs/FleetHub.cs` — hub stays minimal; hub path stays `/fleethub`
- `backend/fleet-telemetry-system.sln` — solution file managed by dotnet tooling
