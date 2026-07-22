---
name: iiot-emitter
description: IIoT Emitter patterns and conventions for realistic vehicle telemetry simulation. Activates for tasks touching vehicle position realism, motion/route simulation, or telemetry-evolution rules in the emitter.
---

# IIoT Emitter Skill — Realistic Vehicle Telemetry Simulation

## Purpose

`emitter/emitter.py` simulates a fleet of real IIoT-equipped vehicles posting telemetry to `POST /api/telemetry/ingest`. This skill activates for any task touching vehicle position realism, motion/route simulation, or telemetry-evolution rules in the emitter.

## Core Problem This Skill Guards Against

`docs/requirements/REQUIREMENTS.md` §5.1 documents a lat/lng **bounding box** for San Francisco:

```
LAT_MIN, LAT_MAX = 37.70, 37.81
LNG_MIN, LNG_MAX = -122.52, -122.35
```

A bounding box is a rectangle; San Francisco's coastline is not. `random.uniform(LAT_MIN, LAT_MAX)` / `random.uniform(LNG_MIN, LNG_MAX)` will place vehicles in San Francisco Bay, the Pacific Ocean, and other water/non-drivable areas at a rate proportional to how much of the rectangle is water (a large fraction, for this bbox). This is the root cause of dashboard markers appearing to float outside land/road boundaries.

**Rule: never sample a raw lat/lng uniformly from a bounding box for a vehicle position.** Always constrain to a known-land point set.

## Land-Constrained Position Pattern

Prefer a small, bundled, curated list of real waypoints over a runtime OSM fetch (keeps the emitter dependency-light and offline-friendly):

```python
# emitter/waypoints.py (or inline in emitter.py) — curated real-street coordinates
# within the SF bbox, hand-picked or exported once from OpenStreetMap/OSMnx and
# committed as static data (no runtime network call to a map provider).
SF_LAND_WAYPOINTS: list[tuple[float, float]] = [
    (37.7749, -122.4194),  # Market St / downtown
    (37.7599, -122.4148),  # Mission District
    (37.8080, -122.4177),  # Fisherman's Wharf
    (37.7910, -122.3990),  # Financial District / Embarcadero
    # ... dozens more, all confirmed on land
]

def random_land_point() -> tuple[float, float]:
    return random.choice(SF_LAND_WAYPOINTS)
```

If a routing-quality result is needed (vehicles that actually follow streets turn-by-turn), use `osmnx`+`networkx` to build a road graph once at startup and snap positions to graph nodes/edges — but treat this as an upgrade path, not a requirement, given the project's simulation-fidelity bar. Justify the added dependency weight before introducing it.

## Waypoint-to-Waypoint Motion Pattern

Replace unconstrained random-walk position updates with destination-seeking movement so vehicles look like they're driving somewhere, not vibrating in place:

```python
@dataclass
class VehicleState:
    ...
    dest_lat: float
    dest_lng: float

def maybe_pick_new_destination(state: VehicleState) -> None:
    # Reached destination (within ~50m) or no destination yet — pick a new one.
    if abs(state.latitude - state.dest_lat) < 0.0005 and abs(state.longitude - state.dest_lng) < 0.0005:
        state.dest_lat, state.dest_lng = random_land_point()

def step_toward_destination(state: VehicleState, step: float = 0.0015) -> None:
    d_lat = state.dest_lat - state.latitude
    d_lng = state.dest_lng - state.longitude
    dist = (d_lat ** 2 + d_lng ** 2) ** 0.5
    if dist > 0:
        state.latitude += (d_lat / dist) * min(step, dist)
        state.longitude += (d_lng / dist) * min(step, dist)
```

Call `maybe_pick_new_destination` then `step_toward_destination` each tick in place of the old `clamp(lat + uniform(-0.003, 0.003), ...)` random-walk.

## Telemetry Realism Correlations (optional upgrade, don't over-engineer)

Real IIoT fleets show correlated signals, not independent random walks:
- Fuel burn rate should scale with `speed_kph` and `cargo_load` (a loaded, fast-moving truck burns more fuel per tick than an idling one).
- `temp_celsius` should trend upward with sustained high `speed_kph` (engine load), not just an independent random walk.
- `engine_health` decay should accelerate slightly when `temp_celsius` is in the danger band (>85°C per `REQUIREMENTS.md` §4.1).

Only add these correlations if the task explicitly asks for telemetry realism, not just position realism — don't scope-creep a position-fix task into a full physics model.

## Verification

```bash
cd emitter
python -m py_compile emitter.py
python emitter.py   # requires backend running; watch for ticks_sent growing, errors near 0
```

Manually spot-check a sample of emitted `(latitude, longitude)` pairs against the waypoint list or a map — every point must resolve to land within the documented bbox, never open water.

## Constraints Carried From `iiot-emitter` Agent

- Only touch `emitter/**`.
- Never hardcode vehicle IDs — always source from `GET /api/vehicles/metadata`.
- Payload keys must stay camelCase matching `TelemetryIngestRequest` exactly.
- Keep new dependencies minimal; justify anything beyond `aiohttp` + stdlib + a small bundled waypoint list.
