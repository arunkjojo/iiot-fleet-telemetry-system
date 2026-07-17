using FleetTelemetry.Hubs;
using FleetTelemetry.Models;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System;
using System.Linq;
using System.Threading;

namespace FleetTelemetry.Services;

public class TelemetrySimulationService : BackgroundService
{
    private static readonly ConcurrentDictionary<string, Vehicle> _vehicles = new();
    private readonly IHubContext<FleetHub, IFleetClient> _hubContext;
    // use thread-local Random for parallel updates to avoid contention
    private readonly ThreadLocal<Random> _rand = new(() => new Random(Guid.NewGuid().GetHashCode()));
    private const int VehicleCount = 10000;
    // corridor/road model used to snap vehicles to street-like lines
    private record Corridor(double StartLat, double StartLng, double EndLat, double EndLng, double LengthMeters);
    private readonly Corridor[] _corridors;
    // per-vehicle movement state: corridor index, progress [0..1), direction (+1 or -1)
    private readonly ConcurrentDictionary<string, (int corridorIdx, double progress, int dir)> _states = new();

    // in-memory recent logs per vehicle (keep small window)
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<Models.VehicleLog>> _logs = new();
    // track when a vehicle entered offline so we can recover it after a timeout
    private readonly ConcurrentDictionary<string, DateTime> _offlineSince = new();
    private const int OfflineRecoverySeconds = 8;

    public TelemetrySimulationService(IHubContext<FleetHub, IFleetClient> hubContext)
    {
        _hubContext = hubContext;
        _corridors = BuildCorridors();
        SeedVehicles();
    }

    public static ConcurrentDictionary<string, Vehicle> Vehicles => _vehicles;

    public static IEnumerable<Models.VehicleLog> GetLogs(string id)
    {
        if (_logs.TryGetValue(id, out var q)) return q.ToArray();
        return Array.Empty<Models.VehicleLog>();
    }

    private static void AddLog(string id, string level, string msg)
    {
        var q = _logs.GetOrAdd(id, _ => new ConcurrentQueue<Models.VehicleLog>());
        q.Enqueue(new Models.VehicleLog { Ts = DateTime.UtcNow, Level = level, Message = msg });
        // limit size to last 50 entries
        while (q.Count > 50)
        {
            q.TryDequeue(out _);
        }
    }

    // Evaluate status deterministically from metrics (priority: offline > danger > warning > active)
    private static string EvaluateStatus(double fuelPercent, int temp, double speedKph, int engineHealth)
    {
        // offline
        if (fuelPercent < 1 || temp < 5 || engineHealth < 5 || speedKph < 2)
            return "offline";

        // danger
        if (fuelPercent < 10.0 || speedKph > 90.0 || temp > 85 || engineHealth > 90)
            return "danger";

        // warning
        if ((fuelPercent < 30.0 && fuelPercent >= 10.0) ||
            (temp > 60 && temp <= 85) ||
            (engineHealth > 60 && engineHealth <= 90) ||
            (speedKph >= 60.0 && speedKph <= 90.0))
            return "warning";

        // active (default)
        return "active";
    }

    // decide next status from current status using small per-tick transition probabilities
    private static string NextStatus(Random rnd, string current)
    {
        var p = rnd.NextDouble();

        switch (current)
        {
            case "active":
                // make adverse transitions rarer so "active" stays dominant
                if (p < 0.001) return "offline";
                if (p < 0.003) return "danger";
                if (p < 0.03) return "warning";
                return "active";
            case "warning":
                // bias warnings to recover back to active more often
                if (p < 0.001) return "offline";
                if (p < 0.005) return "danger";
                if (p < 0.20) return "active";
                return "warning";
            case "danger":
                // danger recovers to active relatively frequently
                if (p < 0.001) return "offline";
                if (p < 0.01) return "warning";
                if (p < 0.25) return "active";
                return "danger";
            case "offline":
                // offline recovers slowly but can come back to active
                if (p < 0.002) return "danger";
                if (p < 0.006) return "warning";
                if (p < 0.12) return "active";
                return "offline";
            default:
                var choices = new[] { "active", "warning", "danger", "offline" };
                return choices[rnd.Next(choices.Length)];
        }
    }

    // build a set of synthetic corridors (straight line segments) inside SF bbox for demo purposes
    private Corridor[] BuildCorridors()
    {
        var list = new List<Corridor>();
        var rnd = new Random(42);
        // San Francisco bbox
        const double minLat = 37.70, maxLat = 37.81;
        const double minLng = -122.52, maxLng = -122.35;

        // create a mix of horizontal, vertical and diagonal corridors
        for (int i = 0; i < 200; i++)
        {
            double sLat, sLng, eLat, eLng;
            if (i % 3 == 0)
            {
                // horizontal
                sLat = minLat + rnd.NextDouble() * (maxLat - minLat);
                sLng = minLng + rnd.NextDouble() * (maxLng - minLng);
                eLat = sLat;
                eLng = minLng + rnd.NextDouble() * (maxLng - minLng);
            }
            else if (i % 3 == 1)
            {
                // vertical
                sLng = minLng + rnd.NextDouble() * (maxLng - minLng);
                sLat = minLat + rnd.NextDouble() * (maxLat - minLat);
                eLng = sLng;
                eLat = minLat + rnd.NextDouble() * (maxLat - minLat);
            }
            else
            {
                // diagonal
                sLat = minLat + rnd.NextDouble() * (maxLat - minLat);
                sLng = minLng + rnd.NextDouble() * (maxLng - minLng);
                eLat = minLat + rnd.NextDouble() * (maxLat - minLat);
                eLng = minLng + rnd.NextDouble() * (maxLng - minLng);
            }

            var len = DistanceMeters(sLat, sLng, eLat, eLng);
            list.Add(new Corridor(sLat, sLng, eLat, eLng, len));
        }

        return list.ToArray();
    }

    // approximate distance between two lat/lng in meters (haversine)
    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0; // earth radius meters
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    // convert a lateral meters offset into lat/lng offsets (random angle) using base latitude for longitude scaling
    private static (double lat, double lng) MetersToLatLngOffset(double meters, double baseLat, Random rnd)
    {
        var angle = rnd.NextDouble() * (2.0 * Math.PI);
        var metersPerDegLat = 111320.0; // approximate
        var metersPerDegLng = 111320.0 * Math.Cos(ToRad(baseLat));
        var dy = Math.Cos(angle) * meters;
        var dx = Math.Sin(angle) * meters;
        return (dy / metersPerDegLat, dx / (Math.Abs(metersPerDegLng) < 1e-6 ? 1e-6 : metersPerDegLng));
    }

    private void SeedVehicles()
    {
        // assign vehicles to corridors so they appear on street-like lines
        for (int i = 0; i < VehicleCount; i++)
        {
            var rnd = _rand.Value;
            // deterministic VEH-NNNNN id matching live mode / DbSeeder.cs format
            var id = $"VEH-{i:D5}";
            var corridorIdx = rnd.Next(0, _corridors.Length);
            var progress = rnd.NextDouble();
            var dir = rnd.Next(0, 2) == 0 ? 1 : -1;

            // base position along corridor
            var c = _corridors[corridorIdx];
            var lat = c.StartLat + (c.EndLat - c.StartLat) * progress;
            var lng = c.StartLng + (c.EndLng - c.StartLng) * progress;
            // small perpendicular jitter in meters converted to degrees (~ +/- 8-20m)
            var jitterMeters = (rnd.NextDouble() - 0.5) * 40.0;
            var jitter = MetersToLatLngOffset(jitterMeters, lat, rnd);

            // pick a human-like driver name from a small list
            var names = new[] { "Joy", "Rinto", "Aisha", "Maya", "Sam", "Liam", "Noah", "Eva", "Zara", "Omar", "Isha", "Kaden" };
            var driver = names[rnd.Next(names.Length)];
            var veh = new Vehicle
            {
                Id = id,
                DriverName = driver,
                Latitude = lat + jitter.lat,
                Longitude = lng + jitter.lng,
                FuelPercent = 50.0 + rnd.NextDouble() * 50.0,
                SpeedKph = 20.0 + rnd.NextDouble() * 80.0,
                // engine health between 40 and 87 inclusive
                EngineHealth = rnd.Next(40, 88),
                Model = (i % 3) == 0 ? "NV Cargo" : "Apex Hauler",
                // initial status chosen randomly from the set
                Status = "active",
                Temp = 50 + rnd.Next(0, 20),
                CargoLoad = 1000 + rnd.Next(0, 9000)
            };

            _vehicles[id] = veh;
            _states[id] = (corridorIdx, progress, dir);
            // ensure log queue exists and add a seed log
            _logs[id] = new ConcurrentQueue<Models.VehicleLog>();
            AddLog(id, "INFO", "Vehicle seeded");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long tickCounter = 0;
        // rebalanceTicks chosen so rebalance happens roughly every ~20s.
        // With a 1000ms tick, 20 ticks ~= 20s.
        const int rebalanceTicks = 20;
        // target distribution (fractions)
        // target ranges (fractions): offline 8-10%, danger 17-20%, warning 24-30%, active = remainder
        // target ranges (fractions): set by user request
        // // offline: 10-12%, danger: 13-15%, warning: 10-20%, remainder active
        // const int offlineMin = 10, offlineMax = 12;   // 10-12 vehicles
        // const int dangerMin = 13, dangerMax = 15;     // 13-15 vehicles
        // const int warningMin = 10, warningMax = 20;   // 10-20 vehicles
        while (!stoppingToken.IsCancellationRequested)
        {
            var updatedBatch = new List<VehicleUpdate>(_vehicles.Count);
            Parallel.ForEach(_vehicles.Keys, new ParallelOptions { CancellationToken = stoppingToken }, key =>
            {
                var rnd = _rand.Value;
                if (_vehicles.TryGetValue(key, out var v) && _states.TryGetValue(key, out var st))
                {
                    // simulate driving along corridor
                    var corridor = _corridors[st.corridorIdx];
                    // speed -> meters per half-second tick
                    var metersPerSec = v.SpeedKph / 3.6;
                    var metersThisTick = metersPerSec * 0.5; // since updates ~500ms
                    var fracDelta = corridor.LengthMeters > 0 ? metersThisTick / corridor.LengthMeters : 0.0;
                    var newProgress = st.progress + st.dir * fracDelta;
                    // wrap and keep in [0,1)
                    if (newProgress >= 1.0) newProgress -= Math.Floor(newProgress);
                    if (newProgress < 0.0) newProgress += Math.Ceiling(-newProgress);

                    // base lat/lng on corridor interpolation
                    var baseLat = corridor.StartLat + (corridor.EndLat - corridor.StartLat) * newProgress;
                    var baseLng = corridor.StartLng + (corridor.EndLng - corridor.StartLng) * newProgress;

                    // add small perpendicular jitter to keep markers slightly off the exact line
                    var jitterMeters = (rnd.NextDouble() - 0.5) * 10.0; // +/-5m
                    var jitter = MetersToLatLngOffset(jitterMeters, baseLat, rnd);

                    var lat = baseLat + jitter.lat;
                    var lng = baseLng + jitter.lng;

                    var fuel = Math.Max(0.0, v.FuelPercent - rnd.NextDouble() * 0.05);
                    var speed = Math.Max(0.0, v.SpeedKph + (rnd.NextDouble() - 0.5) * 5.0);
                    var health = Math.Max(0, Math.Min(100, v.EngineHealth + rnd.Next(-1, 2)));
                    var temp = Math.Max(-40, Math.Min(120, v.Temp + rnd.Next(-1, 2)));

                    // determine status from the freshly computed metrics
                    var evaluatedStatus = EvaluateStatus(fuel, temp, speed, health);

                    // If vehicle is currently offline, consider automatic recovery after configured timeout
                    if (string.Equals(v.Status, "offline", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!_offlineSince.TryGetValue(v.Id, out var since))
                        {
                            _offlineSince[v.Id] = DateTime.UtcNow;
                        }
                        else
                        {
                            var age = (DateTime.UtcNow - since).TotalSeconds;
                            if (age >= OfflineRecoverySeconds)
                            {
                                // recover to active or warning with some randomized metrics
                                evaluatedStatus = (rnd.NextDouble() < 0.6) ? "active" : "warning";
                                fuel = 20.0 + rnd.NextDouble() * 40.0; // 20-60
                                temp = 50 + rnd.Next(-2, 8); // around 48-58
                                health = Math.Max(40, Math.Min(100, rnd.Next(50, 81)));
                                speed = 10.0 + rnd.NextDouble() * 50.0;
                                AddLog(v.Id, "INFO", $"AUTO_RECOVERY offline -> {evaluatedStatus} after {Math.Round(age)}s");
                                _offlineSince.TryRemove(v.Id, out _);
                            }
                        }
                    }

                    // If evaluated status is offline, force zeroed telemetry
                    if (string.Equals(evaluatedStatus, "offline", StringComparison.OrdinalIgnoreCase))
                    {
                        fuel = 0.0;
                        temp = 0;
                        health = 0;
                        speed = 0.0;
                    }

                    // Apply the metric updates
                    // do not update position/speed/temp if currently offline (preserve last known position)
                    if (!string.Equals(v.Status, "offline", StringComparison.OrdinalIgnoreCase))
                    {
                        v.Latitude = lat;
                        v.Longitude = lng;
                        v.SpeedKph = speed;
                        v.Temp = temp;
                    }

                    v.FuelPercent = fuel;
                    v.EngineHealth = health;

                    // status becomes the evaluated status (metrics drive status)
                    var previousStatus = v.Status;
                    v.Status = evaluatedStatus;
                    _states[key] = (st.corridorIdx, newProgress, st.dir);

                    // produce logs when thresholds crossed or status changed
                    if (!string.Equals(previousStatus, v.Status, StringComparison.OrdinalIgnoreCase))
                    {
                        AddLog(v.Id, "WARN", $"STATUS_CHANGE {previousStatus} -> {v.Status}");
                    }

                    if (speed > 80.0)
                        AddLog(v.Id, "ERROR", "OVERSPEED detected");
                    else if (speed >= 70.0)
                        AddLog(v.Id, "WARN", "High speed");

                    if (fuel < 20.0)
                        AddLog(v.Id, "ERROR", "Very low fuel");
                    else if (fuel < 40.0)
                        AddLog(v.Id, "WARN", "Low fuel");

                    if (temp > 75)
                        AddLog(v.Id, "ERROR", "Critical temperature");
                    else if (temp > 65)
                        AddLog(v.Id, "WARN", "High temperature");

                    if (health > 75)
                        AddLog(v.Id, "WARN", "High engine load/health metric");

                    // occasional random events: driver handover, tracker/vehicle/fuel related
                    if (rnd.NextDouble() < 0.0005)
                    {
                        var names = new[] { "Joy", "Rinto", "Aisha", "Maya", "Sam", "Liam", "Noah", "Eva", "Zara", "Omar", "Isha", "Kaden" };
                        var newDriver = names[rnd.Next(names.Length)];
                        var old = v.DriverName;
                        v.DriverName = newDriver;
                        AddLog(v.Id, "INFO", $"HANDOVER driver {old} -> {newDriver}");
                    }

                    var update = new VehicleUpdate
                    {
                        Id = v.Id,
                        Latitude = v.Latitude,
                        Longitude = v.Longitude,
                        FuelPercent = v.FuelPercent,
                        SpeedKph = v.SpeedKph,
                        EngineHealth = v.EngineHealth
                        ,
                        Status = v.Status,
                        Temp = v.Temp
                    };

                    lock (updatedBatch)
                    {
                        updatedBatch.Add(update);
                    }
                }
            });

            // periodic rebalance to maintain approximate global distribution
            tickCounter++;
            if (tickCounter % rebalanceTicks == 0)
            {
                try
                {
                    var total = _vehicles.Count;
                    if (total > 0)
                    {
                        var counts = _vehicles.Values.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count());
                        int currentActive = counts.ContainsKey("active") ? counts["active"] : 0;
                        int currentWarning = counts.ContainsKey("warning") ? counts["warning"] : 0;
                        int currentDanger = counts.ContainsKey("danger") ? counts["danger"] : 0;
                        int currentOffline = counts.ContainsKey("offline") ? counts["offline"] : 0;

                        // rebalance target ranges (re-rolled every tick): offline 40-100,
                        // danger 100-400, warning 500-800, active = remainder
                        int capOffline = Random.Shared.Next(40, 101);
                        int capDanger = Random.Shared.Next(100, 401);
                        int capWarning = Random.Shared.Next(500, 801);

                        var targetOfflineCount = Math.Min(capOffline, total);
                        var targetDangerCount = Math.Min(capDanger, Math.Max(0, total - targetOfflineCount));
                        var targetWarningCount = Math.Min(capWarning, Math.Max(0, total - targetOfflineCount - targetDangerCount));
                        var targetActiveCount = Math.Max(0, total - (targetOfflineCount + targetDangerCount + targetWarningCount));

                        

                        // helper to move random vehicles from one status to another
                        void MoveRandom(string from, string to, int needed)
                        {
                            if (needed <= 0) return;
                            var candidates = _vehicles.Values.Where(v => v.Status == from).Select(v => v.Id).ToArray();
                            if (candidates.Length == 0) return;
                            var pick = new HashSet<string>();
                            for (int i = 0; i < needed; i++)
                            {
                                var id = candidates[(int)(Random.Shared.NextDouble() * candidates.Length)];
                                pick.Add(id);
                            }
                            foreach (var id in pick)
                            {
                                if (_vehicles.TryGetValue(id, out var vv))
                                {
                                    var prev = vv.Status;
                                    vv.Status = to;
                                    if (string.Equals(to, "offline", StringComparison.OrdinalIgnoreCase))
                                    {
                                        vv.FuelPercent = 0.0;
                                        vv.Temp = 0;
                                        vv.EngineHealth = 0;
                                        vv.SpeedKph = 0.0;
                                        _offlineSince[vv.Id] = DateTime.UtcNow;
                                        AddLog(id, "WARN", "Moved to offline by rebalance");
                                    }
                                    else
                                    {
                                        // if we were offline before, clear timestamp
                                        if (string.Equals(prev, "offline", StringComparison.OrdinalIgnoreCase))
                                            _offlineSince.TryRemove(vv.Id, out _);
                                        AddLog(id, "INFO", $"Moved to {to} by rebalance");
                                    }
                                }
                            }
                        }

                        // adjust counts: if a status is under target, move from others
                        if (currentOffline < targetOfflineCount)
                        {
                            MoveRandom("active", "offline", targetOfflineCount - currentOffline);
                        }
                        if (currentDanger < targetDangerCount)
                        {
                            MoveRandom("warning", "danger", targetDangerCount - currentDanger);
                            MoveRandom("active", "danger", Math.Max(0, targetDangerCount - (_vehicles.Values.Count(x => x.Status == "danger"))));
                        }
                        if (currentWarning < targetWarningCount)
                        {
                            MoveRandom("active", "warning", targetWarningCount - currentWarning);
                        }

                        // if any category is over target, move excess back to active
                        if (currentWarning > targetWarningCount)
                        {
                            MoveRandom("warning", "active", currentWarning - targetWarningCount);
                        }
                        if (currentDanger > targetDangerCount)
                        {
                            MoveRandom("danger", "warning", currentDanger - targetDangerCount);
                        }
                        if (currentOffline > targetOfflineCount)
                        {
                            MoveRandom("offline", "active", currentOffline - targetOfflineCount);
                        }
                    }
                }
                catch { }
            }

            // Broadcast to all connected clients (MessagePack used for low-latency)
            try
            {
                await _hubContext.Clients.All.ReceiveFleetUpdate(updatedBatch);
            }
            catch { /* ignore transient errors */ }

            // wait to target ~1000ms tick
            var elapsed = sw.ElapsedMilliseconds % 1000;
            var delay = 1000 - (int)elapsed;
            await Task.Delay(delay, stoppingToken);
        }
    }
}
