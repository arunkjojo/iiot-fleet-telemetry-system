namespace FleetTelemetry.Services;

/// <summary>
/// Thread-safe in-memory counter for active `/fleethub` SignalR connections (BE-005).
/// Incremented/decremented from FleetHub's OnConnectedAsync/OnDisconnectedAsync overrides;
/// read by HealthController's GET /api/health/signalr. No DB access, no business logic.
/// </summary>
public class HubConnectionTracker
{
    private int _count;
    private long _lastEventAtUtcTicks;

    /// <summary>Current number of active SignalR connections.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>UTC timestamp of the most recent connect/disconnect event, or null if none yet.</summary>
    public DateTime? LastEventAtUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastEventAtUtcTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    /// <summary>Records a new connection. Call from FleetHub.OnConnectedAsync.</summary>
    public void Increment()
    {
        Interlocked.Increment(ref _count);
        RecordEvent();
    }

    /// <summary>Records a closed connection. Call from FleetHub.OnDisconnectedAsync.</summary>
    public void Decrement()
    {
        Interlocked.Decrement(ref _count);
        RecordEvent();
    }

    private void RecordEvent()
    {
        Interlocked.Exchange(ref _lastEventAtUtcTicks, DateTime.UtcNow.Ticks);
    }
}
