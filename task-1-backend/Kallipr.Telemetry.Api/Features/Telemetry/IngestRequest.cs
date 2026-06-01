using System.ComponentModel.DataAnnotations;

namespace Kallipr.Telemetry.Api.Features.Telemetry;

public class IngestRequest
{
    [Required]   //validate required fields
    [MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DeviceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Type { get; set; } = string.Empty;

    public double Value { get; set; }

    [Required]
    [MaxLength(50)]
    public string Unit { get; set; } = string.Empty;

    [Range(0, 100)]    //sensible ranges
    public int Battery { get; set; }

    public int Signal { get; set; }

    public DateTime RecordedAt { get; set; }

    [Required]
    [MaxLength(100)]
    public string ExternalId { get; set; } = string.Empty;
}
