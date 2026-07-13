# Sprint 01 — Infrastructure & Application Setup

---

## Note (Operator Prompt)

```
Understand the below modification and bug fix and instruction, if any clarification or doubt, ask me before start the task execution.
```

---

## Sprint Metadata

| Field | Value |
|-------|-------|
| **Sprint ID** | S01 |
| **Branch** | `infra/sprint-01-infrastructure-application-setup` |
| **Base branch** | `main` |
| **PR target** | `main` |
| **Start date** | 2026-06-29 |
| **End date** | 2026-07-06 |
| **Goal** | The full stack runs end-to-end: Next.js dashboard fetches live vehicle data from ASP.NET Core API (with Swagger UI), vehicle metadata and logs persist in PostgreSQL, and the Docker Compose stack starts cleanly with a healthy database. |
| **Success metric** | `docker-compose up --build` starts all services; Swagger UI loads at `/swagger`; `GET /api/vehicles` returns JSON from PostgreSQL-backed seed; SignalR streams live updates to the dashboard. |
| **Target env** | Local (`http://localhost:3000` / `http://localhost:8080`) |
| **Agents involved** | ASP.NET, NEXT, INFRA, QA |
| **Token mode** | caveman (full) |

---

## Context

The backend demo currently runs entirely in-memory — no database, no Swagger, and the Next.js frontend talks directly to the simulation API with no schema validation. Sprint 01 establishes the production-grade infrastructure: add Swagger/OpenAPI documentation to the backend, wire the PostgreSQL connection with EF Core (schema + seed), and ensure the Next.js frontend type-checks cleanly against the real API contract. This sprint does not replace the simulation engine — it adds persistence alongside it so future sprints can query historical data.

**Related documents:**
- `docs/requirements/REQUIREMENTS.md`
- `backend/AGENTS.md`
- `frontend/AGENTS.md`

---

## Branch Setup (run once before any task)

```bash
git fetch origin main
git checkout infra/sprint-01-infrastructure-application-setup
git status    # must be clean
```

---

## Pre-Flight Checklist

- [ ] Branch `infra/sprint-01-infrastructure-application-setup` exists and is clean
- [ ] `cd backend && dotnet build` passes with zero errors on unmodified codebase
- [ ] `cd frontend && npm install` completes with no errors
- [ ] `cd frontend && npm run type-check` passes with zero errors on unmodified codebase
- [ ] `cd frontend && npm run lint` passes with zero warnings on unmodified codebase
- [ ] PostgreSQL is running locally on port 5432
- [ ] Database `fleet_telemetry` exists (or will be created by `dotnet ef database update`)
- [ ] Root `AGENTS.md` read in full
- [ ] `backend/AGENTS.md` read in full
- [ ] `frontend/AGENTS.md` read in full
- [ ] `docs/requirements/REQUIREMENTS.md` read in full

---

## Task Index

- [ ] INFRA-001 — Add Swagger UI to ASP.NET Core backend
- [ ] DB-001 — Add EF Core + PostgreSQL with FleetDbContext and initial schema migration
- [ ] DB-002 — Seed 10,000 vehicle metadata rows into PostgreSQL on startup
- [ ] API-001 — Wire VehiclesController to PostgreSQL (read vehicle metadata from DB)
- [ ] UI-001 — Verify Next.js type-checks against real API response shape
- [ ] INFRA-002 — Add PostgreSQL service to Docker Compose

---

## Dependency Map

```
INFRA-001 (no deps)    DB-001 (no deps)
      |                      |
      |                   DB-002
      |                      |
      +----------+-----------+
                 |
              API-001
                 |
              UI-001
                 |
           INFRA-002
```

---

## Tasks

---

### INFRA-001: Add Swagger UI to ASP.NET Core Backend

**Agent:** ASP.NET
**Depends on:** NONE
**Status:** [ ]

---

**Context:**

The backend (`backend/Program.cs`) currently has no API documentation. Adding Swashbuckle exposes a Swagger UI at `/swagger` that lets developers and AI agents browse all endpoints without reading source code. This is a 3-line change to Program.cs and a NuGet package addition.

---

**Files to read before starting:**

- `backend/Program.cs` — understand current middleware pipeline and where to insert Swagger
- `backend/FleetTelemetry.csproj` — confirm current NuGet dependencies before adding Swashbuckle

---

**Files to modify:**

- `backend/FleetTelemetry.csproj` — add Swashbuckle.AspNetCore package reference
- `backend/Program.cs` — register AddEndpointsApiExplorer, AddSwaggerGen, UseSwagger, UseSwaggerUI

---

**Files to create:**

None.

---

**Do NOT touch:**

- `backend/Hubs/FleetHub.cs` — hub stays untouched
- `backend/Services/TelemetrySimulationService.cs` — simulation service untouched

---

**Sub-task breakdown:**

- [ ] Add `Swashbuckle.AspNetCore` NuGet package to `FleetTelemetry.csproj`
- [ ] Add `builder.Services.AddEndpointsApiExplorer()` and `builder.Services.AddSwaggerGen()` to `Program.cs` (before `builder.Build()`)
- [ ] Add `app.UseSwagger()` and `app.UseSwaggerUI()` to the middleware pipeline (after `app.UseCors`)
- [ ] Run `dotnet build` — must pass with zero errors
- [ ] Open `http://localhost:8080/swagger` and confirm all 4 endpoints appear

---

**Implementation notes:**

1. Insert Swagger services immediately before `builder.Services.AddSignalR()` in `Program.cs`
2. Insert `app.UseSwagger(); app.UseSwaggerUI();` immediately after `app.UseCors("frontend")`
3. No custom Swagger configuration needed — defaults are sufficient for Sprint 01
4. Package version: `Swashbuckle.AspNetCore` version `6.5.0` or latest stable for .NET 8

---

**Acceptance criteria:**

1. `dotnet build` passes with zero errors
2. `http://localhost:8080/swagger` loads the Swagger UI
3. All 4 REST endpoints are listed: `GET /api/vehicles`, `GET /api/vehicles/{id}`, `GET /api/vehicles/{vehicleId}/logs`, `GET /api/vehicles/metadata`
4. Existing SignalR hub at `/fleethub` is unaffected

---

**Verification command:**

```bash
cd backend && dotnet build
dotnet run &
sleep 5
curl -s http://localhost:8080/swagger/v1/swagger.json | grep -c '"operationId"'
# Expected: 4 or more (one per endpoint)
```

---

**Rollback:**

Remove the 3 Swagger lines from `Program.cs` and remove the Swashbuckle package reference from `.csproj`, then `dotnet restore`.

---

### DB-001: Add EF Core + PostgreSQL with FleetDbContext

**Agent:** ASP.NET
**Depends on:** NONE
**Status:** [ ]

---

**Context:**

The backend has no database layer — all vehicle state is in-memory. This task adds Entity Framework Core with the Npgsql provider, creates a `FleetDbContext`, defines the three entity models (Vehicle metadata, TelemetrySnapshot, VehicleLog), and generates the initial migration. The simulation service continues to run in-memory; the DB is an additional persistence layer.

---

**Files to read before starting:**

- `backend/FleetTelemetry.csproj` — current dependencies
- `backend/Program.cs` — where to register DbContext
- `backend/Models/Vehicle.cs` — field names to mirror in DB entities
- `backend/Models/VehicleLog.cs` — field names to mirror in VehicleLog entity
- `backend/AGENTS.md` — PostgreSQL schema spec (vehicles, telemetry_snapshots, vehicle_logs)

---

**Files to modify:**

- `backend/FleetTelemetry.csproj` — add EF Core + Npgsql packages
- `backend/Program.cs` — register FleetDbContext with connection string
- `backend/appsettings.json` — add ConnectionStrings:Fleet placeholder
- `backend/appsettings.Development.json` — add dev connection string

---

**Files to create:**

- `backend/Data/FleetDbContext.cs` — EF Core DbContext with DbSets for all three entities
- `backend/Data/Entities/VehicleEntity.cs` — persistent vehicle metadata entity
- `backend/Data/Entities/TelemetrySnapshotEntity.cs` — point-in-time telemetry snapshot
- `backend/Data/Entities/VehicleLogEntity.cs` — vehicle event log entry
- `backend/Data/Migrations/` — auto-generated by `dotnet ef migrations add InitialSchema`

---

**Do NOT touch:**

- `backend/Models/*.cs` — existing in-memory models stay unchanged
- `backend/Services/TelemetrySimulationService.cs` — simulation stays in-memory

---

**Sub-task breakdown:**

- [ ] Add NuGet packages: `Microsoft.EntityFrameworkCore` 8.x, `Npgsql.EntityFrameworkCore.PostgreSQL` 8.x, `Microsoft.EntityFrameworkCore.Design` 8.x
- [ ] Create `backend/Data/Entities/` directory with three entity classes
- [ ] Create `backend/Data/FleetDbContext.cs` with DbSets and OnModelCreating configuration
- [ ] Register `FleetDbContext` in `Program.cs` using connection string from config
- [ ] Add connection string to `appsettings.json` and `appsettings.Development.json`
- [ ] Run `dotnet ef migrations add InitialSchema` — verify migration files generated
- [ ] Run `dotnet ef database update` — verify tables created in PostgreSQL
- [ ] Run `dotnet build` — must pass with zero errors

---

**Implementation notes:**

1. Entity naming: use `snake_case` table names via `modelBuilder.Entity<T>().ToTable("table_name")`
2. Schema as specified in `backend/AGENTS.md`:
   - `vehicles` table: id (PK varchar 20), driver_name, model, created_at
   - `telemetry_snapshots` table: id (bigserial PK), vehicle_id (FK), recorded_at, lat, lng, fuel_percent, speed_kph, engine_health, temp_celsius, cargo_load, status
   - `vehicle_logs` table: id (bigserial PK), vehicle_id (FK), logged_at, level (varchar 10), message (text)
3. Connection string key: `ConnectionStrings:Fleet` (matches `backend/AGENTS.md`)
4. Dev connection string: `Host=localhost;Database=fleet_telemetry;Username=postgres;Password=postgres`
5. Use `HasDefaultValueSql("NOW()")` for timestamp columns with default values

---

**Acceptance criteria:**

1. `dotnet build` passes with zero errors
2. `dotnet ef database update` creates three tables in PostgreSQL: `vehicles`, `telemetry_snapshots`, `vehicle_logs`
3. `FleetDbContext` is registered in DI and injectable into controllers
4. Existing in-memory simulation continues to run unaffected

---

**Verification command:**

```bash
cd backend
dotnet build
dotnet ef database update
# Connect to PostgreSQL and verify:
# psql -U postgres -d fleet_telemetry -c "\dt"
# Expected: vehicles, telemetry_snapshots, vehicle_logs tables listed
```

---

**Rollback:**

```bash
dotnet ef database update 0   # remove all migrations from DB
# Then delete backend/Data/Migrations/ and backend/Data/ directories
# Remove EF Core packages from .csproj and revert Program.cs changes
```

---

### DB-002: Seed Vehicle Metadata into PostgreSQL on Startup

**Agent:** ASP.NET
**Depends on:** DB-001
**Status:** [ ]

---

**Context:**

After creating the schema (DB-001), the `vehicles` table is empty. This task adds a startup seeder that checks if the table is empty and, if so, inserts all 10,000 vehicle IDs and metadata. This makes the DB the source of truth for vehicle metadata while the simulation continues to manage in-memory state.

---

**Files to read before starting:**

- `backend/Services/TelemetrySimulationService.cs` — lines 1-80 (vehicle seeding logic: ID generation, driver names, models)
- `backend/Data/FleetDbContext.cs` — entity types available
- `backend/Program.cs` — where to call the seeder

---

**Files to modify:**

- `backend/Program.cs` — call seeder after `app` is built but before `app.Run()`

---

**Files to create:**

- `backend/Data/DbSeeder.cs` — static class with `SeedVehiclesAsync(IServiceScope)` method

---

**Do NOT touch:**

- `backend/Services/TelemetrySimulationService.cs` — simulation seeding stays separate; DB seeder mirrors it

---

**Sub-task breakdown:**

- [ ] Create `backend/Data/DbSeeder.cs` with async seed method
- [ ] Mirror vehicle ID generation from `TelemetrySimulationService` (same IDs: VEH-00000 through VEH-09999)
- [ ] Mirror driver name pool: Joy, Rinto, Aisha, Maya, Sam, Liam, Noah, Eva, Zara, Omar, Isha, Kaden (cycled)
- [ ] Mirror model assignment: "NV Cargo" or "Apex Hauler" (alternating by index)
- [ ] Add idempotency check: only seed if `vehicles` table has zero rows
- [ ] Call `DbSeeder.SeedVehiclesAsync(scope)` in `Program.cs` after `app = builder.Build()`
- [ ] Verify 10,000 rows inserted: `SELECT COUNT(*) FROM vehicles;` returns 10000

---

**Implementation notes:**

1. Use `IServiceScopeFactory` in `Program.cs` to resolve scoped `FleetDbContext` before `app.Run()`
2. Insert in batches of 500 to avoid single large transaction timeout
3. `created_at` can use PostgreSQL default (`NOW()`) — do not set it explicitly in the seeder
4. Log seed progress to `ILogger` at INFO level (e.g., "Seeding vehicles: 0/10000", "Seeding complete")

---

**Acceptance criteria:**

1. `dotnet run` completes startup and logs "Seeding complete" (or "Vehicles already seeded — skipping")
2. `SELECT COUNT(*) FROM vehicles` returns 10000
3. Vehicle IDs match the simulation: VEH-00000 through VEH-09999
4. Seeder is idempotent — running `dotnet run` a second time does not insert duplicate rows

---

**Verification command:**

```bash
cd backend && dotnet run &
sleep 10
psql -U postgres -d fleet_telemetry -c "SELECT COUNT(*) FROM vehicles;"
# Expected: 10000
psql -U postgres -d fleet_telemetry -c "SELECT id, driver_name, model FROM vehicles LIMIT 5;"
# Expected: rows with VEH-00000..VEH-00004, valid driver names and models
```

---

**Rollback:**

Remove `DbSeeder.cs`, remove seeder call from `Program.cs`. Run `DELETE FROM vehicles;` to clear the table.

---

### API-001: Wire VehiclesController to Read Metadata from PostgreSQL

**Agent:** ASP.NET
**Depends on:** DB-001, DB-002
**Status:** [ ]

---

**Context:**

Currently `VehiclesController.GET /api/vehicles` returns data from the in-memory `TelemetrySimulationService`. After this task, the controller merges PostgreSQL vehicle metadata (driver name, model) with live in-memory telemetry (lat, lng, fuel, speed, etc.) to produce the API response. This establishes the DB as source of truth for metadata while the simulation owns live metrics.

---

**Files to read before starting:**

- `backend/Controllers/VehiclesController.cs` — current implementation
- `backend/Services/TelemetrySimulationService.cs` — how to access in-memory vehicle state
- `backend/Data/FleetDbContext.cs` — entity types
- `backend/Models/ApiVehicle.cs` — response DTO shape

---

**Files to modify:**

- `backend/Controllers/VehiclesController.cs` — inject `FleetDbContext`, merge DB metadata with live telemetry

---

**Files to create:**

None.

---

**Do NOT touch:**

- `backend/Services/TelemetrySimulationService.cs`
- `backend/Models/ApiVehicle.cs` — response shape must not change (frontend depends on it)

---

**Sub-task breakdown:**

- [ ] Inject `FleetDbContext` into `VehiclesController` constructor
- [ ] On `GET /api/vehicles`: fetch all vehicle metadata from DB, join with in-memory telemetry map, return merged `ApiVehicle[]`
- [ ] On `GET /api/vehicles/{id}`: fetch single vehicle metadata from DB, merge with in-memory telemetry, return merged result
- [ ] Ensure response field names are unchanged (frontend depends on snake_case JSON keys)
- [ ] Run `dotnet build` — zero errors
- [ ] Verify `GET /api/vehicles` returns correct data including driver and model from DB

---

**Implementation notes:**

1. Use `await _db.Vehicles.AsNoTracking().ToListAsync()` for read-only queries
2. Build a `Dictionary<string, VehicleEntity>` keyed by ID, then merge with in-memory simulation state per vehicle
3. If a vehicle exists in DB but not in in-memory state (edge case), skip it in the response
4. If a vehicle exists in in-memory state but not in DB, also skip it (seeder should have covered all IDs)
5. Response time budget: `GET /api/vehicles` should return in under 500ms even with 10k rows

---

**Acceptance criteria:**

1. `dotnet build` passes with zero errors
2. `GET /api/vehicles` returns a JSON array with 10,000 entries
3. Each entry has `driver` and `model` fields matching values seeded in PostgreSQL
4. Each entry has live telemetry fields (`lat`, `lng`, `fuel`, `speedKph`, etc.) from the in-memory simulation
5. Response time under 500ms (measured with `curl -w "%{time_total}"`)

---

**Verification command:**

```bash
cd backend && dotnet run &
sleep 5
curl -s http://localhost:8080/api/vehicles | python -m json.tool | head -30
# Expected: JSON array, first item has id, driver, model, lat, lng, fuel, etc.
curl -w "%{time_total}" -o /dev/null -s http://localhost:8080/api/vehicles
# Expected: under 0.500 seconds
```

---

**Rollback:**

Revert `VehiclesController.cs` to remove `FleetDbContext` injection and revert to pure in-memory implementation.

---

### UI-001: Verify Next.js Type-Checks Against Real API Response Shape

**Agent:** NEXT
**Depends on:** API-001
**Status:** [ ]

---

**Context:**

The frontend `Vehicle` type in `frontend/types/vehicle.ts` was written against the demo API. Now that the backend has a defined schema, this task audits the type definitions against the actual `GET /api/vehicles` JSON response, fixes any mismatches, and confirms `npm run type-check` passes with zero errors. No visual changes — this is a type safety alignment task.

---

**Files to read before starting:**

- `frontend/types/vehicle.ts` — current type definitions
- `frontend/app/page.tsx` — how Vehicle type is used in SignalR updates and API normalization
- `backend/Models/ApiVehicle.cs` — authoritative JSON field names from backend

---

**Files to modify:**

- `frontend/types/vehicle.ts` — update types if any field names or types are mismatched

---

**Files to create:**

None.

---

**Do NOT touch:**

- `frontend/app/page.tsx` — only fix if type errors point here; do not refactor
- `frontend/components/` — no visual changes this sprint

---

**Sub-task breakdown:**

- [ ] Run `GET /api/vehicles` with the backend running and inspect actual JSON field names
- [ ] Compare against `frontend/types/vehicle.ts` — list any mismatches
- [ ] Update `vehicle.ts` to match actual response (field names, optional vs required, types)
- [ ] Run `npm run type-check` — must pass with zero errors
- [ ] Run `npm run lint` — must pass with zero warnings

---

**Acceptance criteria:**

1. `npm run type-check` passes with zero errors
2. `npm run lint` passes with zero warnings
3. `frontend/types/vehicle.ts` matches all fields returned by `GET /api/vehicles`

---

**Verification command:**

```bash
cd frontend
npm run type-check
# Expected: no errors
npm run lint
# Expected: no warnings
```

---

**Rollback:**

Revert `frontend/types/vehicle.ts` to previous version via `git checkout frontend/types/vehicle.ts`.

---

### INFRA-002: Add PostgreSQL Service to Docker Compose

**Agent:** INFRA
**Depends on:** DB-001
**Status:** [ ]

---

**Context:**

The `docker-compose.yml` currently only defines `backend` and `frontend` services. PostgreSQL must be added so the full stack runs with a single `docker-compose up --build`. The backend must wait for PostgreSQL to be healthy before starting.

---

**Files to read before starting:**

- `docker-compose.yml` — current service definitions and network config
- `backend/appsettings.json` — connection string key name
- `DOCKER_README.md` — existing Docker documentation

---

**Files to modify:**

- `docker-compose.yml` — add `db` service (PostgreSQL 16), update `backend` with `depends_on` and `DB_CONNECTION` env var
- `DOCKER_README.md` — add PostgreSQL section to documentation

---

**Files to create:**

None.

---

**Do NOT touch:**

- `backend/Dockerfile` — no changes needed
- `frontend/Dockerfile` — no changes needed

---

**Sub-task breakdown:**

- [ ] Add `db` service to `docker-compose.yml` using `postgres:16-alpine` image
- [ ] Configure `db` with `POSTGRES_DB=fleet_telemetry`, `POSTGRES_USER=postgres`, `POSTGRES_PASSWORD=postgres`
- [ ] Add health check for `db`: `pg_isready -U postgres`
- [ ] Add `depends_on: db: condition: service_healthy` to `backend` service
- [ ] Add `ConnectionStrings__Fleet` env var to `backend` service pointing to `db` container
- [ ] Add `volumes:` for PostgreSQL data persistence
- [ ] Run `docker-compose up --build` — all three services must start and be healthy
- [ ] Update `DOCKER_README.md` with PostgreSQL instructions

---

**Implementation notes:**

1. Connection string in Compose env: `Host=db;Database=fleet_telemetry;Username=postgres;Password=postgres`
2. ASP.NET Core reads `ConnectionStrings__Fleet` env var as `ConnectionStrings:Fleet` in config (double underscore = nesting)
3. PostgreSQL data volume: `postgres_data:/var/lib/postgresql/data`
4. Health check command: `["CMD-SHELL", "pg_isready -U postgres"]`
5. `backend` service health check must also pass after DB migrations run

---

**Acceptance criteria:**

1. `docker-compose up --build` starts all three services without errors
2. `db` service passes health check within 60 seconds
3. `backend` service starts only after `db` is healthy
4. `GET http://localhost:8080/api/vehicles` returns 10,000 vehicles (DB seeded)
5. Frontend dashboard at `http://localhost:3000` loads and shows live updates

---

**Verification command:**

```bash
docker-compose up --build -d
sleep 60
docker-compose ps
# Expected: all three services Up (healthy)
curl -s http://localhost:8080/api/vehicles | python -m json.tool | grep '"id"' | wc -l
# Expected: 10000
curl -s http://localhost:3000 | grep -c "Fleet"
# Expected: 1 or more
```

---

**Rollback:**

Remove the `db` service and volume from `docker-compose.yml`, remove `depends_on` and `ConnectionStrings__Fleet` from `backend` service.

---

## Sprint-End Checklist

- [ ] All 6 task checkboxes above are `[x]`
- [ ] `docker-compose up --build` starts all services cleanly
- [ ] Swagger UI accessible at `http://localhost:8080/swagger`
- [ ] `GET /api/vehicles` returns 10,000 records from PostgreSQL-backed seeder
- [ ] `npm run type-check` passes with zero errors
- [ ] Bump `frontend/package.json` version: `0.1.0` → `0.2.0`
- [ ] Add `## v0.2.0` entry to `CHANGELOG.md`
- [ ] All commits follow format: `IIOT-S01-{TASK-ID}: <one-line summary>`
- [ ] Open PR: `infra/sprint-01-infrastructure-application-setup` → `main`
- [ ] Update `AGENTS.md` `## Current Sprint` to point to `sprint-02.md` once created

---

## Sprint Retrospective

_(fill at sprint end)_

---

## Agent Execution Protocol

```
SESSION START
─────────────
1. Read AGENTS.md (root) in full
2. Read docs/requirements/REQUIREMENTS.md in full
3. Read this sprint file in full
4. Read .claude/skills/sprint/SKILL.md in full
5. Confirm branch: git rev-parse --abbrev-ref HEAD returns infra/sprint-01-infrastructure-application-setup
6. Identify the first task where Status: [ ] and all dependencies are [x]
7. Read every file listed under "Files to read before starting" for that task

TASK EXECUTION
──────────────
8.  Walk the "Sub-task breakdown" list top-to-bottom
9.  Implement following "Implementation notes" exactly
10. Do NOT modify files listed under "Do NOT touch"
11. Run the "Verification command" — do not mark complete until passing
12. Update Status from [ ] to [x]
13. Tick the matching entry in Task Index
14. Commit: git commit -m "IIOT-S01-{TASK-ID}: <one-line summary>"

BLOCKERS
────────
15. If a "Files to read" file does not exist: STOP, report to user
16. If verification fails with unresolvable error: STOP, report to user
```

---

## Glossary

| Term | Definition |
|------|------------|
| **In-memory state** | Vehicle telemetry stored in `ConcurrentDictionary` inside `TelemetrySimulationService` |
| **DB metadata** | Vehicle ID, driver name, and model stored in PostgreSQL `vehicles` table |
| **EF Core** | Entity Framework Core — ORM used to interact with PostgreSQL |
| **Migration** | EF Core generated SQL file that creates/modifies DB schema |
| **Seeder** | `DbSeeder.cs` — inserts initial 10k vehicle rows into PostgreSQL on first startup |
| **SignalR** | WebSocket protocol used to stream `VehicleUpdate[]` from backend to frontend |
| **MessagePack** | Binary serialization format used by SignalR for efficient payload encoding |
