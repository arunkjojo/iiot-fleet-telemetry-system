---
name: asp-dot-net-core
description: ASP.NET Core 8 patterns and conventions for the IIoT Fleet Telemetry backend. Activates for controller work, SignalR hub changes, MessagePack model definitions, EF Core migrations, and background service tasks.
---

# ASP.NET Core 8 Skill — IIoT Fleet Telemetry Backend

## Project Structure Pattern

```
backend/
├── Controllers/    # HTTP endpoints — thin, no business logic
├── Hubs/           # SignalR hub (FleetHub + IFleetClient)
├── Models/         # MessagePack models + API DTOs
├── Services/       # Background services (TelemetrySimulationService)
├── Data/           # EF Core (FleetDbContext + Entities + Migrations)
└── Program.cs      # DI, middleware pipeline
```

## Program.cs Pattern

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. CORS
builder.Services.AddCors(options => {
    options.AddPolicy("frontend", policy => {
        policy.WithOrigins(
            Environment.GetEnvironmentVariable("FRONTEND_ORIGIN") ?? "http://localhost:3000"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();  // required for SignalR
    });
});

// 2. Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. SignalR + MessagePack
builder.Services.AddSignalR().AddMessagePackProtocol();

// 4. EF Core + PostgreSQL
builder.Services.AddDbContext<FleetDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Fleet")));

// 5. Background services
builder.Services.AddHostedService<TelemetrySimulationService>();

var app = builder.Build();

app.UseCors("frontend");
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHub<FleetHub>("/fleethub");  // path must not change

app.Run();
```

## SignalR Hub Pattern

```csharp
// backend/Hubs/FleetHub.cs — stays minimal
public class FleetHub : Hub<IFleetClient> { }

// backend/Hubs/IFleetClient.cs
public interface IFleetClient {
    Task ReceiveFleetUpdate(IEnumerable<VehicleUpdate> updates);
}

// Sending updates from background service:
await _hubContext.Clients.All.ReceiveFleetUpdate(updates);
```

## MessagePack Model Pattern

```csharp
[MessagePackObject]
public class VehicleUpdate {
    [Key(0)] public string Id { get; set; } = "";
    [Key(1)] public double Latitude { get; set; }
    [Key(2)] public double Longitude { get; set; }
    [Key(3)] public double FuelPercent { get; set; }
    [Key(4)] public double SpeedKph { get; set; }
    [Key(5)] public int EngineHealth { get; set; }
    [Key(6)] public string Status { get; set; } = "active";
    [Key(7)] public int Temp { get; set; }
}
```

Rules: every property needs `[Key(N)]`. Keys are sequential from 0. Never skip a key index.

## API DTO Pattern (REST responses)

```csharp
public class ApiVehicle {
    [JsonPropertyName("id")]           public string Id { get; set; } = "";
    [JsonPropertyName("model")]        public string Model { get; set; } = "";
    [JsonPropertyName("driver")]       public string Driver { get; set; } = "";
    [JsonPropertyName("status")]       public string Status { get; set; } = "active";
    [JsonPropertyName("fuel")]         public double Fuel { get; set; }
    [JsonPropertyName("temp")]         public int Temp { get; set; }
    [JsonPropertyName("speedKph")]     public double SpeedKph { get; set; }
    [JsonPropertyName("cargoLoad")]    public int CargoLoad { get; set; }
    [JsonPropertyName("engineHealth")] public int EngineHealth { get; set; }
    [JsonPropertyName("lat")]          public double Lat { get; set; }
    [JsonPropertyName("lng")]          public double Lng { get; set; }
}
```

Use `[JsonPropertyName]` on all DTO properties to control JSON output naming.

## EF Core Pattern

```csharp
// backend/Data/FleetDbContext.cs
public class FleetDbContext : DbContext {
    public FleetDbContext(DbContextOptions<FleetDbContext> options) : base(options) { }

    public DbSet<VehicleEntity> Vehicles => Set<VehicleEntity>();
    public DbSet<VehicleLogEntity> VehicleLogs => Set<VehicleLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<VehicleEntity>().ToTable("vehicles");
        modelBuilder.Entity<VehicleLogEntity>().ToTable("vehicle_logs");
        modelBuilder.Entity<VehicleLogEntity>()
            .HasIndex(l => new { l.VehicleId, l.LoggedAt })
            .IsDescending(false, true);
    }
}
```

Always use `AsNoTracking()` for read-only queries:
```csharp
var vehicles = await _db.Vehicles.AsNoTracking().ToListAsync();
```

## Controller Pattern

```csharp
[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase {
    private readonly TelemetrySimulationService _sim;
    private readonly FleetDbContext _db;

    public VehiclesController(TelemetrySimulationService sim, FleetDbContext db) {
        _sim = sim;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() {
        var dbVehicles = await _db.Vehicles.AsNoTracking()
            .ToDictionaryAsync(v => v.Id);
        var result = _sim.GetAllVehicles()
            .Select(v => new ApiVehicle {
                Id = v.Id,
                Driver = dbVehicles.TryGetValue(v.Id, out var dv) ? dv.DriverName : v.DriverName,
                // ... merge in-memory telemetry + DB metadata
            });
        return Ok(result);
    }
}
```

## EF Core Migration Commands

```bash
cd backend

# Create new migration
dotnet ef migrations add <MigrationName>

# Apply migrations to DB
dotnet ef database update

# Rollback all migrations
dotnet ef database update 0

# List migrations
dotnet ef migrations list
```

## Connection String in Docker / Env

```bash
# Environment variable (double underscore = config nesting)
ConnectionStrings__Fleet="Host=db;Database=fleet_telemetry;Username=postgres;Password=postgres"

# Reads as: builder.Configuration.GetConnectionString("Fleet")
```

## Verification

```bash
cd backend
dotnet build     # zero errors
dotnet run       # starts on http://localhost:8080
# Then: curl http://localhost:8080/swagger to verify Swagger UI
# Then: curl http://localhost:8080/api/vehicles | python -m json.tool | head -20
```
