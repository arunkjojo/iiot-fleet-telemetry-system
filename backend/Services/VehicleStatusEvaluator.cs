namespace FleetTelemetry.Services;

/// <summary>
/// Canonical, stateless status evaluator for live-telemetry mode.
/// Implements REQUIREMENTS.md section 4.1 (Vehicle Status Thresholds) exactly.
/// This intentionally does not need to match TelemetrySimulationService's private
/// EvaluateStatus bit-for-bit — that one is legacy/dummy-mode-only and stays untouched.
/// </summary>
public static class VehicleStatusEvaluator
{
    /// <summary>
    /// Evaluate vehicle status from raw metrics. Priority order (highest wins):
    /// offline > danger > warning > active.
    /// </summary>
    public static string Evaluate(double fuelPercent, int temp, double speedKph, int engineHealth)
    {
        // offline: fuel depleted, or all core metrics explicitly zeroed
        if (fuelPercent <= 0.0 || (temp == 0 && engineHealth == 0 && speedKph == 0.0))
            return "offline";

        // danger conditions
        if (speedKph > 90.0 || fuelPercent < 10.0 || temp > 85 || engineHealth > 90)
            return "danger";

        // warning conditions
        if ((fuelPercent >= 10.0 && fuelPercent < 30.0) ||
            (temp >= 65 && temp <= 85) ||
            (speedKph >= 80.0 && speedKph <= 90.0))
            return "warning";

        // default
        return "active";
    }
}
