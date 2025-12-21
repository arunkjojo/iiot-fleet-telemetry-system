This backend is a minimal .NET 8 SignalR telemetry simulator used by the frontend fleet dashboard.

Quick start:

1. Install .NET 8 SDK
2. From this folder run:

```bash
dotnet restore
dotnet run
```

Endpoints:
- SignalR Hub: `/fleethub` (MessagePack protocol enabled)
- Metadata: `GET /api/vehicles/metadata`
- Logs: `GET /api/vehicles/{vehicleId}/logs`
### 📂 Backend: Fleet-Telemetry-Engine
**Audience:** System Architects, Backend Engineers

```markdown
# ⚙️ Fleet-Telemetry-Engine
**Real-Time Data Streaming & Simulation Microservice**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet)](https://dotnet.microsoft.com/)
[![SignalR](https://img.shields.io/badge/SignalR-Enabled-orange)](https://dotnet.microsoft.com/en-us/apps/aspnet/signalr)

## 📖 The "What"
The core engine responsible for simulating, processing, and broadcasting telemetry data for 10,000 unique vehicle entities. It serves as the high-throughput bridge between raw asset data and the monitoring UI.

## 🎯 The "Why"
Simulating 10,000 entities in real-time requires a backend that can handle high concurrency without I/O blocking. This engine was implemented to provide a **sub-500ms latency** broadcast loop to all connected clients.

## 💻 Technical Terms & Tech
- **ASP.NET Core 8:** The high-performance runtime.
- **SignalR (WebSockets):** For full-duplex, low-latency communication.
- **ConcurrentDictionary:** For thread-safe, in-memory management of the 10k vehicle states.
- **IHostedService:** A long-running background worker that handles the 500ms "Heartbeat" simulation.
- **MessagePack:** (Optional) Used for binary serialization to reduce WebSocket payload size by ~60%.

## 🚀 Backend Design
1. **Simulation Loop:** A background task iterates through the 10k entities, mutating GPS/Fuel/RPM values based on a delta-algorithm.
2. **Push-Model:** Instead of the UI polling, the Hub pushes updates to `Clients.All` every 500ms.
3. **Memory Safety:** Designed to avoid Garbage Collection (GC) pressure by reusing objects within the simulation loop.

## 🛠️ Setup
```bash
dotnet restore
dotnet run --project Fleet.Api