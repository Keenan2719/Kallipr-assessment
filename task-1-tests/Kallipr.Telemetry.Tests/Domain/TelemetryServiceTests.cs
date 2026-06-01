using Kallipr.Telemetry.Api.Configuration;
using Kallipr.Telemetry.Api.Data;
using Kallipr.Telemetry.Api.Features.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kallipr.Telemetry.Tests.Domain;

public class TelemetryServiceTests                  //Create => Do => Assert 
{
    private static TelemetryDbContext CreateDb()  //create a new in-memory database context for testing
    {
        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelemetryDbContext(options);
    }

    private static TelemetryService CreateService(TelemetryDbContext db, int batteryLowThreshold = 20) //create a new instance of the TelemetryService with the provided db context and battery threshold for testing
    {
        var settings = Options.Create(new TelemetrySettings { BatteryLowThreshold = batteryLowThreshold });
        var logger = NullLogger<TelemetryService>.Instance;
        return new TelemetryService(db, settings, logger);
    }

    private static IngestRequest ValidRequest(string externalId = "r-001") => new() //create a valid ingest request for testing
    {
        TenantId = "acme",
        DeviceId = "dev-123",
        Type = "water_level",
        Value = 1.23,
        Unit = "m",
        Battery = 62,
        Signal = -85,
        RecordedAt = new DateTime(2025, 1, 10, 10, 15, 0, DateTimeKind.Utc),
        ExternalId = externalId
    };

    [Fact]
    public async Task Ingest_ValidReading_PersistsAndReturnsResponse()  //test that a valid reading is ingested successfully, persisted to the database, and returns the expected response
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var (reading, error) = await service.IngestAsync(ValidRequest());

        Assert.Null(error);
        Assert.NotNull(reading);
        Assert.Equal("acme", reading.TenantId);
        Assert.Equal("dev-123", reading.DeviceId);
        Assert.Equal("water_level", reading.Type);
        Assert.Equal(1.23, reading.Value);
        Assert.Equal(62, reading.Battery);
        Assert.Equal(1, await db.Readings.CountAsync());
    }

    [Fact]
    public async Task Ingest_DuplicateExternalId_ReturnsError()  //test that ingesting a reading with a duplicate external ID returns an error
    {
        using var db = CreateDb();
        var service = CreateService(db);

        await service.IngestAsync(ValidRequest("r-dup"));
        var (reading, error) = await service.IngestAsync(ValidRequest("r-dup"));

        Assert.Null(reading);
        Assert.NotNull(error);
        Assert.Equal(1, await db.Readings.CountAsync());
    }

    [Fact]
    public async Task Ingest_BatteryBelowThreshold_SetsBatteryLowTrue()  //test that a reading with a battery level below the threshold sets the BatteryLow status to true
    {
        using var db = CreateDb();
        var service = CreateService(db, batteryLowThreshold: 20);

        var request = ValidRequest();
        request.Battery = 15;
        var (reading, _) = await service.IngestAsync(request);

        Assert.True(reading!.Status.BatteryLow);
    }

    [Fact]
    public async Task Ingest_BatteryAtOrAboveThreshold_SetsBatteryLowFalse() //test that a reading with a battery level at or above the threshold sets the BatteryLow status to false
    {
        using var db = CreateDb();
        var service = CreateService(db, batteryLowThreshold: 20);

        var request = ValidRequest();
        request.Battery = 20;
        var (reading, _) = await service.IngestAsync(request);

        Assert.False(reading!.Status.BatteryLow);
    }

    [Fact]
    public async Task Query_FilterByDeviceId_ReturnsOnlyMatchingReadings()  //test that querying by device ID returns only the readings that match the specified device ID
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var r1 = ValidRequest("r-1"); r1.DeviceId = "dev-A";
        var r2 = ValidRequest("r-2"); r2.DeviceId = "dev-B";
        await service.IngestAsync(r1);
        await service.IngestAsync(r2);

        var result = await service.QueryAsync("acme", new QueryParams { DeviceId = "dev-A" });

        Assert.Single(result.Items);
        Assert.Equal("dev-A", result.Items[0].DeviceId);
    }

    [Fact]
    public async Task Query_FilterByType_ReturnsOnlyMatchingReadings()  //test that querying by type returns only the readings that match the specified type
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var r1 = ValidRequest("r-1"); r1.Type = "water_level";
        var r2 = ValidRequest("r-2"); r2.Type = "temperature";
        await service.IngestAsync(r1);
        await service.IngestAsync(r2);

        var result = await service.QueryAsync("acme", new QueryParams { Type = "temperature" });

        Assert.Single(result.Items);
        Assert.Equal("temperature", result.Items[0].Type);
    }

    [Fact]
    public async Task Query_FilterByTimeRange_ReturnsOnlyReadingsInRange()  //test that querying by time range returns only the readings that fall within the specified range
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var r1 = ValidRequest("r-1"); r1.RecordedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var r2 = ValidRequest("r-2"); r2.RecordedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await service.IngestAsync(r1);
        await service.IngestAsync(r2);

        var from = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await service.QueryAsync("acme", new QueryParams { From = from });

        Assert.Single(result.Items);
        Assert.Equal("r-2", result.Items[0].ExternalId);
    }

    [Fact]
    public async Task Query_Pagination_ReturnsCorrectPage()  //test that querying with pagination returns the correct page of results
    {
        using var db = CreateDb();
        var service = CreateService(db);

        for (int i = 1; i <= 5; i++)
            await service.IngestAsync(ValidRequest($"r-{i}"));

        var result = await service.QueryAsync("acme", new QueryParams { Page = 2, PageSize = 2 });

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task Query_DifferentTenant_IsolatesData() //test that querying for a different tenant does not return readings from other tenants, ensuring data isolation between tenants
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var r1 = ValidRequest("r-1"); r1.TenantId = "acme";
        var r2 = ValidRequest("r-2"); r2.TenantId = "other";
        await service.IngestAsync(r1);
        await service.IngestAsync(r2);

        var result = await service.QueryAsync("acme", new QueryParams());

        Assert.Single(result.Items);
        Assert.Equal("acme", result.Items[0].TenantId);
    }
}
