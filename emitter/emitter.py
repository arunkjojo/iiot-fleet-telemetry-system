"""
IIoT Fleet Emitter (INFRA-002)

Simulates up to VEHICLE_COUNT independent IIoT-equipped vehicles, each posting its own
telemetry reading to the backend's POST /api/telemetry/ingest endpoint on an independent
tick loop. Designed to run unattended for the lifetime of the process (see DOCKER_README.md
for the docker-compose wiring added in INFRA-003).

Design constraints (see docs/sprints/sprint-02.md, task INFRA-002):
  - Never hammer the backend with VEHICLE_COUNT simultaneous connections: a single shared
    aiohttp.ClientSession backed by a TCPConnector(limit=MAX_CONCURRENCY), plus an explicit
    asyncio.Semaphore(MAX_CONCURRENCY) around every POST, bound total in-flight requests.
  - Never crash the whole process because one vehicle's HTTP call failed: every POST is
    wrapped in try/except for aiohttp.ClientError / asyncio.TimeoutError; failures are logged
    and the per-vehicle loop continues on the next tick.
  - Only ever emit telemetry for vehicle IDs sourced from GET /api/vehicles/metadata — the
    canonical roster backed by the `vehicles` table — so TelemetrySnapshotEntity / VehicleLogEntity
    foreign keys never fail server-side.
  - VEHICLE_COUNT is a cap, never a hardcoded assumption: the roster is fetched first, then
    sliced to min(VEHICLE_COUNT, len(roster)).
"""

from __future__ import annotations

import asyncio
import logging
import os
import random
import signal
import sys
from dataclasses import dataclass

import aiohttp

# ---------------------------------------------------------------------------
# Configuration — all env vars have sane defaults so `python emitter.py` alone
# works out of the box against a backend on http://localhost:8080.
# ---------------------------------------------------------------------------

BACKEND_URL = os.environ.get("BACKEND_URL", "http://localhost:8080").rstrip("/")
VEHICLE_COUNT = int(os.environ.get("VEHICLE_COUNT", "10000"))
TICK_INTERVAL_SECONDS = float(os.environ.get("TICK_INTERVAL_SECONDS", "3"))
MAX_CONCURRENCY = int(os.environ.get("MAX_CONCURRENCY", "300"))
REQUEST_TIMEOUT_SECONDS = float(os.environ.get("REQUEST_TIMEOUT_SECONDS", "5"))

METADATA_URL = f"{BACKEND_URL}/api/vehicles/metadata"
INGEST_URL = f"{BACKEND_URL}/api/telemetry/ingest"

# San Francisco bounding box (docs/requirements/REQUIREMENTS.md section 5.1) — kept only
# as a defense-in-depth clamp() bound, never as a sampling range (see SF_LAND_WAYPOINTS below).
LAT_MIN, LAT_MAX = 37.70, 37.81
LNG_MIN, LNG_MAX = -122.52, -122.35

# Curated real, confirmed-on-land SF waypoints spanning the documented bbox — avoids
# random.uniform-across-bbox sampling landing vehicles in SF Bay/the Pacific Ocean.
# Positions are only ever chosen from (or interpolated between) this list.
SF_LAND_WAYPOINTS: list[tuple[float, float]] = [
    (37.7749, -122.4194),  # Market St / downtown
    (37.7599, -122.4148),  # Mission District (24th St)
    (37.7935, -122.4399),  # Pacific Heights
    (37.7910, -122.4010),  # Financial District
    (37.7599, -122.4869),  # Sunset District (Irving St)
    (37.7806, -122.4644),  # Richmond District (Clement St)
    (37.7783, -122.4390),  # Hayes Valley
    (37.7609, -122.4350),  # Noe Valley
    (37.7609, -122.4351),  # Castro District
    (37.7692, -122.4481),  # Haight-Ashbury
    (37.8008, -122.4100),  # North Beach
    (37.7827, -122.4090),  # SoMa (5th & Mission)
    (37.7695, -122.3928),  # Dogpatch
    (37.7285, -122.4530),  # Ingleside
    (37.7419, -122.4700),  # Balboa Terrace
    (37.7180, -122.4438),  # Excelsior
    (37.7423, -122.3823),  # Bayview (inland, not waterfront)
    (37.7355, -122.4188),  # Bernal Heights
    (37.7500, -122.4300),  # Glen Park
    (37.7838, -122.4270),  # Western Addition
    (37.7963, -122.4302),  # Russian Hill
    (37.8005, -122.4180),  # Telegraph Hill (inland side)
    (37.7614, -122.4835),  # Parkside
    (37.7444, -122.4864),  # Lakeshore (inland of the lake)
    (37.7869, -122.4324),  # Nob Hill
    (37.7550, -122.4470),  # West Portal
    (37.7686, -122.4300),  # Duboce Triangle
    (37.7280, -122.3823),  # Silver Terrace / Portola (inland)
    (37.7970, -122.3985),  # Embarcadero (inland side, not the pier edge)
    (37.7860, -122.4550),  # Anza Vista
    (37.7515, -122.4160),  # Mission Terrace
    (37.7350, -122.4550),  # Miraloma Park
    (37.7719, -122.4090),  # Rincon Hill
    (37.7204, -122.4468),  # Oceanview
    (37.7645, -122.4187),  # Mission Dolores
]


def random_land_point() -> tuple[float, float]:
    return random.choice(SF_LAND_WAYPOINTS)


# Alternate by roster index — stable for a vehicle's lifetime (never changed tick-to-tick).
MODELS = ("NV Cargo", "Apex Hauler")

SUMMARY_INTERVAL_SECONDS = 10.0
METADATA_FETCH_RETRIES = 30
METADATA_RETRY_DELAY_SECONDS = 2.0

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    stream=sys.stdout,
)
log = logging.getLogger("iiot-emitter")


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


@dataclass
class VehicleState:
    """Per-vehicle state that persists across ticks — values are evolved incrementally,
    never regenerated from scratch, so motion/telemetry looks continuous."""

    vehicle_id: str
    driver_name: str
    model: str
    latitude: float
    longitude: float
    dest_lat: float
    dest_lng: float
    fuel_percent: float
    speed_kph: float
    engine_health: int
    temp_celsius: int
    cargo_load: int


def make_initial_state(vehicle_id: str, driver_name: str, index: int) -> VehicleState:
    model = MODELS[index % len(MODELS)]
    start_lat, start_lng = random_land_point()
    dest_lat, dest_lng = random_land_point()
    while dest_lat == start_lat and dest_lng == start_lng:
        dest_lat, dest_lng = random_land_point()
    return VehicleState(
        vehicle_id=vehicle_id,
        driver_name=driver_name,
        model=model,
        latitude=start_lat,
        longitude=start_lng,
        dest_lat=dest_lat,
        dest_lng=dest_lng,
        fuel_percent=round(random.uniform(45.0, 100.0), 1),
        speed_kph=round(random.uniform(0.0, 60.0), 1),
        engine_health=random.randint(70, 100),
        temp_celsius=random.randint(35, 70),
        cargo_load=random.randint(0, 5000),
    )


def evolve_state(state: VehicleState) -> None:
    """Mutate `state` in place: one tick's worth of bounded, incremental change."""

    # Fuel: mostly decreases; rare refuel event jumps it back up.
    if random.random() < 0.01:
        state.fuel_percent = round(clamp(state.fuel_percent + random.uniform(40.0, 60.0), 0.0, 100.0), 1)
    else:
        state.fuel_percent = round(clamp(state.fuel_percent - random.uniform(0.0, 0.4), 0.0, 100.0), 1)

    # Speed: small bounded random walk, with an occasional full stop (traffic/loading dock).
    if random.random() < 0.03:
        state.speed_kph = 0.0
    else:
        state.speed_kph = round(clamp(state.speed_kph + random.uniform(-8.0, 8.0), 0.0, 120.0), 1)

    # Engine health: small bounded random walk, slight downward bias (wear).
    state.engine_health = int(clamp(state.engine_health + random.randint(-2, 1), 0, 100))

    # Temp: small bounded random walk.
    state.temp_celsius = int(clamp(state.temp_celsius + random.randint(-3, 3), 15, 110))

    # Cargo: mostly static, occasional pickup/drop-off.
    if random.random() < 0.02:
        state.cargo_load = random.randint(0, 8000)

    # Position: waypoint-to-waypoint motion — step toward the current destination each
    # tick; on arrival, pick a new random on-land destination. clamp() stays as a
    # defense-in-depth safety net even though this stepping should never leave the bbox.
    if (
        abs(state.latitude - state.dest_lat) < 0.0005
        and abs(state.longitude - state.dest_lng) < 0.0005
    ):
        state.dest_lat, state.dest_lng = random_land_point()

    step = 0.0015
    d_lat = state.dest_lat - state.latitude
    d_lng = state.dest_lng - state.longitude
    dist = (d_lat ** 2 + d_lng ** 2) ** 0.5
    if dist > 0:
        state.latitude += (d_lat / dist) * min(step, dist)
        state.longitude += (d_lng / dist) * min(step, dist)

    state.latitude = clamp(state.latitude, LAT_MIN, LAT_MAX)
    state.longitude = clamp(state.longitude, LNG_MIN, LNG_MAX)


def build_payload(state: VehicleState) -> dict:
    """Keys must exactly match TelemetryIngestRequest (BE-002) — camelCase JSON."""
    return {
        "vehicleId": state.vehicle_id,
        "driverName": state.driver_name,
        "model": state.model,
        "latitude": round(state.latitude, 6),
        "longitude": round(state.longitude, 6),
        "fuelPercent": state.fuel_percent,
        "speedKph": state.speed_kph,
        "engineHealth": state.engine_health,
        "tempCelsius": state.temp_celsius,
        "cargoLoad": state.cargo_load,
    }


class Stats:
    """Plain counters — safe without locks since asyncio tasks only yield at `await`
    points and increments here are single, non-yielding statements."""

    def __init__(self) -> None:
        self.ticks_sent = 0
        self.errors = 0


async def fetch_roster(session: aiohttp.ClientSession) -> list[dict]:
    """Fetch the canonical vehicle-ID roster. Retries with a fixed backoff since, outside
    Docker Compose's depends_on/service_healthy gating, the backend may not be up yet."""
    timeout = aiohttp.ClientTimeout(total=REQUEST_TIMEOUT_SECONDS)
    last_error: Exception | None = None
    for attempt in range(1, METADATA_FETCH_RETRIES + 1):
        try:
            async with session.get(METADATA_URL, timeout=timeout) as resp:
                resp.raise_for_status()
                roster = await resp.json()
                log.info("Fetched vehicle roster: %d vehicles from %s", len(roster), METADATA_URL)
                return roster
        except (aiohttp.ClientError, asyncio.TimeoutError) as exc:
            last_error = exc
            log.warning(
                "Roster fetch attempt %d/%d failed (%s) — retrying in %.1fs",
                attempt, METADATA_FETCH_RETRIES, exc, METADATA_RETRY_DELAY_SECONDS,
            )
            await asyncio.sleep(METADATA_RETRY_DELAY_SECONDS)
    raise RuntimeError(
        f"Could not fetch vehicle roster from {METADATA_URL} after {METADATA_FETCH_RETRIES} attempts"
    ) from last_error


async def vehicle_loop(
    state: VehicleState,
    session: aiohttp.ClientSession,
    semaphore: asyncio.Semaphore,
    stats: Stats,
    stop_event: asyncio.Event,
) -> None:
    # Staggered startup: sleep a random 0..TICK_INTERVAL_SECONDS delay before the first POST
    # so VEHICLE_COUNT tasks don't all fire in the same instant.
    initial_delay = random.uniform(0.0, TICK_INTERVAL_SECONDS)
    try:
        await asyncio.wait_for(stop_event.wait(), timeout=initial_delay)
        return  # shutdown requested before this vehicle ever ticked
    except asyncio.TimeoutError:
        pass

    timeout = aiohttp.ClientTimeout(total=REQUEST_TIMEOUT_SECONDS)

    while not stop_event.is_set():
        evolve_state(state)
        payload = build_payload(state)

        async with semaphore:
            try:
                async with session.post(INGEST_URL, json=payload, timeout=timeout) as resp:
                    if resp.status >= 400:
                        body = await resp.text()
                        log.warning(
                            "%s: ingest rejected (HTTP %d): %s",
                            state.vehicle_id, resp.status, body[:200],
                        )
                        stats.errors += 1
                    else:
                        stats.ticks_sent += 1
            except (aiohttp.ClientError, asyncio.TimeoutError) as exc:
                # Never let one vehicle's failed tick crash the process — log and continue.
                log.warning("%s: ingest POST failed (%s) — will retry next tick", state.vehicle_id, exc)
                stats.errors += 1

        try:
            await asyncio.wait_for(stop_event.wait(), timeout=TICK_INTERVAL_SECONDS)
        except asyncio.TimeoutError:
            pass


async def summary_loop(stats: Stats, stop_event: asyncio.Event) -> None:
    while not stop_event.is_set():
        try:
            await asyncio.wait_for(stop_event.wait(), timeout=SUMMARY_INTERVAL_SECONDS)
        except asyncio.TimeoutError:
            pass
        if not stop_event.is_set():
            log.info("summary: ticks_sent=%d errors=%d", stats.ticks_sent, stats.errors)


def install_signal_handlers(loop: asyncio.AbstractEventLoop, stop_event: asyncio.Event) -> None:
    def request_stop(sig_name: str) -> None:
        log.info("Received %s — shutting down gracefully...", sig_name)
        stop_event.set()

    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            loop.add_signal_handler(sig, request_stop, sig.name)
        except (NotImplementedError, AttributeError):
            # loop.add_signal_handler isn't available for all signals on all platforms
            # (e.g. SIGTERM on Windows) — fall back to signal.signal so local/dev runs
            # still shut down cleanly on Ctrl+C.
            try:
                signal.signal(sig, lambda *_args, _s=sig: request_stop(_s.name))
            except (ValueError, OSError):
                pass


async def main() -> None:
    log.info(
        "Starting IIoT fleet emitter: backend_url=%s vehicle_count=%d tick_interval=%.1fs "
        "max_concurrency=%d request_timeout=%.1fs",
        BACKEND_URL, VEHICLE_COUNT, TICK_INTERVAL_SECONDS, MAX_CONCURRENCY, REQUEST_TIMEOUT_SECONDS,
    )

    stop_event = asyncio.Event()
    install_signal_handlers(asyncio.get_running_loop(), stop_event)

    connector = aiohttp.TCPConnector(limit=MAX_CONCURRENCY)
    async with aiohttp.ClientSession(connector=connector) as session:
        roster = await fetch_roster(session)
        fleet_size = min(VEHICLE_COUNT, len(roster))
        if fleet_size <= 0:
            log.error(
                "No vehicles to simulate (VEHICLE_COUNT=%d, roster size=%d) — exiting",
                VEHICLE_COUNT, len(roster),
            )
            return

        roster = roster[:fleet_size]
        log.info("Simulating %d vehicles (of %d available in the roster)", fleet_size, len(roster))

        semaphore = asyncio.Semaphore(MAX_CONCURRENCY)
        stats = Stats()

        tasks: list[asyncio.Task] = []
        for index, entry in enumerate(roster):
            vehicle_id = entry.get("id") or entry.get("Id")
            driver_name = entry.get("driverName") or entry.get("DriverName") or ""
            if not vehicle_id:
                log.warning("Skipping roster entry with no id: %r", entry)
                continue
            state = make_initial_state(vehicle_id, driver_name, index)
            tasks.append(asyncio.create_task(vehicle_loop(state, session, semaphore, stats, stop_event)))

        tasks.append(asyncio.create_task(summary_loop(stats, stop_event)))

        await stop_event.wait()
        log.info("Stop requested — waiting for in-flight ticks to wind down...")
        await asyncio.gather(*tasks, return_exceptions=True)

        log.info("Emitter shut down cleanly. ticks_sent=%d errors=%d", stats.ticks_sent, stats.errors)


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        log.info("KeyboardInterrupt — exiting.")
