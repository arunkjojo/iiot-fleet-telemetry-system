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
    /// Active band (terminal default, not re-checked): 30.0 &lt;= fuelPercent &lt;= 100.0 ||
    /// 5 &lt;= temp &lt;= 60 || 5 &lt;= engineHealth &lt;= 60 || 2 &lt;= speedKph &lt;= 60.0.
    /// </summary>
    public static string Evaluate(double fuelPercent, int temp, double speedKph, int engineHealth)
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
}
