namespace Kallipr.Telemetry.Api.Features.Telemetry;

public class QueryParams
{
    public string? DeviceId { get; set; }
    public string? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
