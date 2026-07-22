using System.Collections.Concurrent;
using FleetTelemetry.Models;

namespace FleetTelemetry.Services;

/// <summary>
/// In-memory "current live state" cache fed by the telemetry ingestion pipeline.
/// Holds no DB dependency and performs no persistence itself — that responsibility
/// belongs to TelemetryPersistenceService (BE-002). Read by VehiclesController /
/// LogsController and drained by LiveBroadcastService (BE-003).
/// </summary>
public interface ILiveTelemetryStore
{
    /// <summary>Insert or replace the current state for a vehicle. Marks the vehicle dirty.</summary>
    void Upsert(Vehicle v);

    /// <summary>Append a log entry for a vehicle, keeping only the last 50 entries.</summary>
    void AddLog(string id, string level, string message);

    /// <summary>Attempt to read the current state for a vehicle.</summary>
    bool TryGet(string id, out Vehicle? v);

    /// <summary>Attempt to read the UTC timestamp of the last upsert for a vehicle.</summary>
    bool TryGetLastSeenUtc(string id, out DateTime lastSeenUtc);

    /// <summary>All vehicles currently known to the store.</summary>
    IEnumerable<Vehicle> GetAll();

    /// <summary>Last (up to) 50 log entries for a vehicle, oldest first.</summary>
    IEnumerable<VehicleLog> GetLogs(string id);

    /// <summary>
    /// Atomically return and clear the set of vehicles that have been upserted
    /// since the last call. Used by LiveBroadcastService (BE-003) to broadcast
    /// only what changed since the previous tick.
    /// </summary>
    IEnumerable<Vehicle> GetAndClearDirty();
}

public class LiveTelemetryStore : ILiveTelemetryStore
{
    private readonly ConcurrentDictionary<string, Vehicle> _vehicles = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<VehicleLog>> _logs = new();
    private readonly ConcurrentDictionary<string, bool> _dirty = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastSeenUtc = new();

    private const int MaxLogEntries = 50;

    public void Upsert(Vehicle v)
    {
        _vehicles[v.Id] = v;
        _dirty[v.Id] = true;
        _lastSeenUtc[v.Id] = DateTime.UtcNow;
    }

    public void AddLog(string id, string level, string message)
    {
        var q = _logs.GetOrAdd(id, _ => new ConcurrentQueue<VehicleLog>());
        q.Enqueue(new VehicleLog { Ts = DateTime.UtcNow, Level = level, Message = message });
        while (q.Count > MaxLogEntries)
        {
            q.TryDequeue(out _);
        }
    }

    public bool TryGet(string id, out Vehicle? v) => _vehicles.TryGetValue(id, out v);

    public bool TryGetLastSeenUtc(string id, out DateTime lastSeenUtc) => _lastSeenUtc.TryGetValue(id, out lastSeenUtc);

    public IEnumerable<Vehicle> GetAll() => _vehicles.Values;

    public IEnumerable<VehicleLog> GetLogs(string id)
    {
        if (_logs.TryGetValue(id, out var q)) return q.ToArray();
        return Array.Empty<VehicleLog>();
    }

    public IEnumerable<Vehicle> GetAndClearDirty()
    {
        var ids = _dirty.Keys.ToArray();
        var result = new List<Vehicle>(ids.Length);
        foreach (var id in ids)
        {
            _dirty.TryRemove(id, out _);
            if (_vehicles.TryGetValue(id, out var v))
            {
                result.Add(v);
            }
        }
        return result;
    }
}
