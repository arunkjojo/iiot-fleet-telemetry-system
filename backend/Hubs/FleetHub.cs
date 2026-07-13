using FleetTelemetry.Services;
using Microsoft.AspNetCore.SignalR;

namespace FleetTelemetry.Hubs;

public class FleetHub : Hub<IFleetClient>
{
    // Intentionally minimal; server pushes updates via IHubContext<FleetHub, IFleetClient>
    private readonly HubConnectionTracker _tracker;

    public FleetHub(HubConnectionTracker tracker)
    {
        _tracker = tracker;
    }

    public override async Task OnConnectedAsync()
    {
        _tracker.Increment();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _tracker.Decrement();
        await base.OnDisconnectedAsync(exception);
    }
}
