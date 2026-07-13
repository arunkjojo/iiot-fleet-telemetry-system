namespace FleetTelemetry.Models;

/// <summary>
/// Request body for PATCH /api/vehicles/{id} (BE-006). Both fields are optional,
/// but at least one must be provided with a non-empty (after trim) value. The
/// route's {id} is the only vehicle identifier accepted — this DTO intentionally
/// has no Id/VehicleId field, so the primary key can never be renamed via PATCH.
/// </summary>
public class PatchVehicleRequest
{
    public string? DriverName { get; set; }
    public string? DisplayNumber { get; set; }
}
