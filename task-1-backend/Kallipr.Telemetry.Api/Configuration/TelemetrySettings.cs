namespace Kallipr.Telemetry.Api.Configuration;

public class TelemetrySettings
{
    public const string SectionName = "Telemetry";

    public int BatteryLowThreshold { get; set; } = 20;
}
