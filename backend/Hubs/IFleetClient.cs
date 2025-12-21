using FleetTelemetry.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FleetTelemetry.Hubs;

public interface IFleetClient
{
    Task ReceiveFleetUpdate(IEnumerable<VehicleUpdate> updates);
}
