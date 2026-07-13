# ADR-001: Telemetry Ingestion & Persistence Pipeline

**Status:** Accepted (implemented — Sprint 02, tasks BE-002/BE-003)
**Date:** 2026-07-10 (documented retroactively; decision locked at Sprint 02 kickoff)
**Deciders:** ARCH, ASP.NET, INFRA

## Context

Sprint 02 replaces the in-memory `TelemetrySimulationService` with live telemetry from a Python emitter simulating the full fleet.

Constraints:
- 10,000 vehicles, each an independent emitter task, posting on a per-vehicle tick (1–5s interval per `iiot-emitter` design) — worst case approaches 2,000–10,000 `POST /api/telemetry/ingest` requests/second.
- NF-02: `GET /api/vehicles` must respond under 500ms.
- NF-03: SignalR broadcasts must go out within 500ms of a tick.
- Target datastore is PostgreSQL (`telemetry_snapshots`, `vehicle_logs`) — a single instance, not a distributed cluster.
- Delivery window: Sprint 02, alongside a Python emitter, a tuned Postgres Docker image, and a full `DOCKER_README.md` rewrite — a multi-day, not multi-week, budget.
- The emitter count must never map 1:1 to DB connections — spiking to 10,000 concurrent DB writers was ruled out at the outset as architecturally unacceptable for a single Postgres instance.

## Decision

Decouple request intake from persistence using an in-process buffered/batched writer:

1. `TelemetryIngestController` validates the payload, computes status via `VehicleStatusEvaluator`, upserts the in-memory `ILiveTelemetryStore` (read path + broadcast source), enqueues onto a bounded `System.Threading.Channels.Channel` (capacity 50,000), and returns `202 Accepted` immediately — it never calls `SaveChangesAsync`.
2. `TelemetryPersistenceService`, a singleton `BackgroundService`, drains the channel on a 1000ms timer or at a 2,000-item batch size (whichever comes first), and flushes via `AddRangeAsync` + one `SaveChangesAsync` per entity type, using a scoped `FleetDbContext` per flush (`IServiceScopeFactory`).
3. `LiveBroadcastService` reads only the *dirty* subset of `ILiveTelemetryStore` (`GetAndClearDirty()`) every ~500ms and broadcasts over SignalR — read/broadcast latency is fully decoupled from DB write latency.
4. FK violations (unknown `vehicleId`) are caught per-batch and dropped with a logged error; they do not crash the persistence loop.

## Options Considered

### Option A: Direct synchronous Postgres writes per request

| Dimension | Assessment |
|-----------|------------|
| Complexity | Low — no channel, no background service |
| Cost | Low infra footprint |
| Scalability | Poor at target load — 2k–10k inserts/sec synchronously would exhaust the connection pool and risk lock contention on a single Postgres instance |
| Team familiarity | High |
| Latency impact | Couples ingest response time to DB write time; a slow write stalls the emitter's next tick and risks cascading backpressure across 10,000 concurrent tasks |

**Pros:** Simplest to build and reason about; no buffering logic to get wrong.
**Cons:** Directly violates the "emitter count must not equal DB connection count" constraint; a single slow query or lock could ripple into request timeouts and, indirectly, SignalR broadcast delays (NF-03).

### Option B: In-process buffered/batched writer (chosen)

| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium — bounded channel + timer/size-triggered batch flush |
| Cost | No new infrastructure; runs inside the existing ASP.NET Core process |
| Scalability | Batches 2,000 rows per `SaveChangesAsync`, capping write frequency regardless of emitter count |
| Team familiarity | Medium — standard .NET `Channel<T>` + `BackgroundService` pattern |
| Latency impact | Ingest returns `202` immediately; DB write is asynchronous and bounded |

**Pros:** Meets the connection-count constraint; keeps ingest latency flat under load; isolates DB slowness from the read/broadcast path (`ILiveTelemetryStore` is independent of the write buffer).
**Cons:** Data has bounded staleness (up to ~1s or 2,000 items) before it lands in Postgres; a channel at capacity (50,000) would start blocking writers — not yet load-tested at full 10k-vehicle sustained throughput.

### Option C: External queue (Kafka / Redis Streams)

| Dimension | Assessment |
|-----------|------------|
| Complexity | High — new infra component, new failure mode, new deploy target |
| Cost | Additional container(s), operational burden |
| Scalability | Best long-term ceiling, but far beyond what a single-Postgres, single-API-instance demo needs |
| Team familiarity | Low relative to in-process .NET primitives |

**Pros:** Would scale past a single process/instance; durable buffer survives API restarts.
**Cons:** Disproportionate to a 2-week, single-instance delivery window; adds a Dockerfile, health check, and ops surface for no near-term benefit. Rejected at scope, not on merit.

## Trade-off Analysis

The core tension is durability/simplicity vs. write-path scalability. Option A is simplest but breaks the connection-count constraint outright — it wasn't viable at the stated throughput, not just suboptimal. Option C solves scalability beyond what's needed at the cost of complexity and schedule the sprint doesn't have. Option B is the pragmatic middle: it satisfies every NF requirement (NF-02, NF-03) by keeping the hot read/broadcast path (`ILiveTelemetryStore`) entirely uncoupled from Postgres write latency, at the cost of accepting bounded persistence lag and an unverified upper bound on the channel.

## Consequences

- Read (`GET /api/vehicles`) and broadcast (SignalR) paths are immune to Postgres slowness — they only touch `ILiveTelemetryStore`.
- Persisted data in `telemetry_snapshots`/`vehicle_logs` can lag live state by up to ~1 second under normal load, or longer if the channel is saturated. Acceptable for a live dashboard; would need revisiting for any downstream system that treats Postgres as the real-time source of truth.
- A crash between "upsert to `ILiveTelemetryStore`" and "channel drain" loses that tick's persisted row (not the live/broadcast state). No at-least-once persistence guarantee exists today.
- ~~Known open issue (QA-001)~~ **Resolved.** QA-001 found `/fleethub` closing abnormally (WebSocket code 1006) every 30–70s under full 10k-vehicle load. Root cause: the CLR thread pool's default injection throttle delayed SignalR's keep-alive ping callback under sustained ingest burst, tripping the client's 30s timeout. Fixed in `IIOT-S02-BE-004` (`backend/Program.cs`): thread-pool minimum floor raised to `max(ProcessorCount * 16, 200)`, and `ClientTimeoutInterval` widened from the 30s default to 60s. Re-verified in QA-001's final pass at full `VEHICLE_COUNT=10000` — all 6 acceptance criteria passed, `telemetry_snapshots` grew ~19,000 rows in a 15s window with zero emitter errors. It was not channel-drain contention.
- The 50,000-item channel capacity and the 1000ms/2000-item flush trigger were untuned defaults, not the result of a load test. **Update:** as of this revision they are configurable (`TelemetryPersistence` section in `appsettings.json`) rather than hard-coded, so tuning no longer requires a code change — but a real load test at sustained full throughput still hasn't been run (no Docker/Postgres runtime was available in the environment that made this change).
- **Update — durability improved:** a batch that fails to persist is now retried (`MaxRetryAttempts`, default 2, `RetryDelayMs` apart) before being given up on, and a batch that still fails after retries is written to a dead-letter JSON file (`TelemetryPersistence:DeadLetterDirectory`, default `deadletter/`) instead of being silently discarded, with a running dropped-item counter logged. This narrows — but does not close — the "no at-least-once guarantee" gap: data between the last successful drain and a crash is still lost, and dead-letter files are not yet auto-replayed.

## Action Items

1. [x] Close QA-001 (SignalR disconnects under sustained load) — fixed in `IIOT-S02-BE-004`, confirmed not related to the persistence channel.
2. [ ] Load-test at full 10,000-vehicle sustained throughput to validate the (now configurable) channel capacity and flush-trigger defaults. Tuning knobs exist; the load test itself is still outstanding.
3. [ ] Decide whether persisted-data staleness (up to ~1s) needs an SLA if any consumer other than the live dashboard reads `telemetry_snapshots` directly.
4. [ ] If throughput needs exceed a single Postgres instance in the future, revisit Option C rather than scaling Option B's channel capacity indefinitely.
5. [ ] Build a replay tool for dead-letter files (currently manual-inspection only) and decide a retention/cleanup policy for the `deadletter/` directory so it doesn't grow unbounded under sustained DB outages.
