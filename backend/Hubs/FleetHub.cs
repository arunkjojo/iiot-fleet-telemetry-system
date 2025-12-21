using Microsoft.AspNetCore.SignalR;

namespace FleetTelemetry.Hubs;

public class FleetHub : Hub<IFleetClient>
{
    // Intentionally minimal; server pushes updates via IHubContext<FleetHub, IFleetClient>
}
