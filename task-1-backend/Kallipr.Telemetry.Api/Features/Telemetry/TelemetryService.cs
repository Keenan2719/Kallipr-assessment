using Kallipr.Telemetry.Api.Configuration;
using Kallipr.Telemetry.Api.Data;
using Kallipr.Telemetry.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kallipr.Telemetry.Api.Features.Telemetry;

public class TelemetryService
{
    private readonly TelemetryDbContext _db;
    private readonly TelemetrySettings _settings;    //battery threshold 
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(TelemetryDbContext db, IOptions<TelemetrySettings> settings, ILogger<TelemetryService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<(ReadingResponse? Reading, string? Error)> IngestAsync(IngestRequest request)
    {
        var duplicate = await _db.Readings           //checking if a row exists with the same combo
            .AnyAsync(r => r.TenantId == request.TenantId && r.ExternalId == request.ExternalId);

        if (duplicate)                                   //if yes, return error and stop
        {
            _logger.LogWarning("Duplicate ingest rejected. TenantId={TenantId} ExternalId={ExternalId}",
                request.TenantId, request.ExternalId);
            return (null, "A reading with this externalId already exists for this tenant.");  //no save
        }

        var reading = new TelemetryReading      //take fields from request and create a new reading to save to DB
        {
            TenantId = request.TenantId,
            DeviceId = request.DeviceId,
            Type = request.Type,
            Value = request.Value,
            Unit = request.Unit,
            Battery = request.Battery,
            Signal = request.Signal,
            RecordedAt = request.RecordedAt == default ? DateTime.UtcNow : DateTime.SpecifyKind(request.RecordedAt, DateTimeKind.Utc),
            ExternalId = request.ExternalId,
            IngestedAt = DateTime.UtcNow
        };

        _db.Readings.Add(reading);                 //track and add to db
        await _db.SaveChangesAsync();              //persist changes to the database

        _logger.LogInformation(
            "Reading ingested. TenantId={TenantId} DeviceId={DeviceId} Type={Type} ExternalId={ExternalId} Battery={Battery}",
            reading.TenantId, reading.DeviceId, reading.Type, reading.ExternalId, reading.Battery);

        return (MapToResponse(reading), null);  //success, no error
    }

    public async Task<PagedReadingsResponse> QueryAsync(string tenantId, QueryParams query)
    {
        var pageSize = Math.Clamp(query.PageSize ?? 20, 1, 100);   //defaults to 20 if not provided, max 100
        var page = Math.Max(1, query.Page ?? 1);

        var q = _db.Readings.Where(r => r.TenantId == tenantId);  //all readings for the tenant

        if (!string.IsNullOrWhiteSpace(query.DeviceId))
            q = q.Where(r => r.DeviceId == query.DeviceId);  //filter by deviceId if provided

        if (!string.IsNullOrWhiteSpace(query.Type))
            q = q.Where(r => r.Type == query.Type);         //filter by type if provided

        if (query.From.HasValue)
            q = q.Where(r => r.RecordedAt >= query.From.Value.ToUniversalTime());

        if (query.To.HasValue)
            q = q.Where(r => r.RecordedAt <= query.To.Value.ToUniversalTime());

        var entities = await q
            .OrderByDescending(r => r.RecordedAt)        //newest to oldest
            .Skip((page - 1) * pageSize)                 //skip the items from previous pages
            .Take(pageSize)                              //take items for the current page
            .ToListAsync();                             //execute the query and get results

        var items = entities.Select(MapToResponse).ToList();   //db rows to response objects

        return new PagedReadingsResponse(items, page, pageSize);
    }

    private ReadingResponse MapToResponse(TelemetryReading r) =>
        new(r.Id, r.TenantId, r.DeviceId, r.Type, r.Value, r.Unit,
            r.Battery, r.Signal, r.RecordedAt, r.ExternalId, r.IngestedAt,
            new ReadingStatus(r.Battery < _settings.BatteryLowThreshold)); //if battery is below threshold, status will indicate low battery
}
