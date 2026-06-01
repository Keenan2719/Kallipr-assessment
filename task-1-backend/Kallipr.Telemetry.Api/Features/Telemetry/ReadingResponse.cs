namespace Kallipr.Telemetry.Api.Features.Telemetry;

public record ReadingStatus(bool BatteryLow);

public record ReadingResponse(
    long Id,
    string TenantId,
    string DeviceId,
    string Type,
    double Value,
    string Unit,
    int Battery,
    int Signal,
    DateTime RecordedAt,
    string ExternalId,
    DateTime IngestedAt,   //timestamp of when WE received the reading 
    ReadingStatus Status 
);

public record PagedReadingsResponse(     //pagination for user to know which page they're on and how many items
    IReadOnlyList<ReadingResponse> Items,
    int Page,
    int PageSize
);
