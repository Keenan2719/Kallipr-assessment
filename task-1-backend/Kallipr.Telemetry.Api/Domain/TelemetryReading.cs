namespace Kallipr.Telemetry.Api.Domain;

/*
A simple telemetry class representing a row in the DB
Fields were grabbed from the sample data 
*/

public class TelemetryReading
{
    public long Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public int Battery { get; set; }
    public int Signal { get; set; }
    public DateTime RecordedAt { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public DateTime IngestedAt { get; set; }
}
