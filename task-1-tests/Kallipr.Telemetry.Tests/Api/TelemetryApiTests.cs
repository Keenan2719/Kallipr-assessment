using System.Net;
using System.Net.Http.Json;
using Kallipr.Telemetry.Api.Data;
using Kallipr.Telemetry.Api.Features.Telemetry;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kallipr.Telemetry.Tests.Api;

public class TelemetryApiTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove all EF Core registrations for TelemetryDbContext
                var toRemove = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<TelemetryDbContext>)
                             || d.ServiceType == typeof(TelemetryDbContext))
                    .ToList();
                foreach (var d in toRemove) services.Remove(d);

                // Use the shared in-memory SQLite connection so data persists across requests
                services.AddDbContext<TelemetryDbContext>(opt =>
                    opt.UseSqlite(_connection));
            });
        });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    private static object ValidPayload(string externalId = "r-001") => new
    {
        tenantId = "acme",
        deviceId = "dev-123",
        type = "water_level",
        value = 1.23,
        unit = "m",
        battery = 62,
        signal = -85,
        recordedAt = "2025-01-10T10:15:00Z",
        externalId
    };

    [Fact]
    public async Task POST_Telemetry_ValidPayload_Returns201()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/telemetry", ValidPayload("r-happy-path"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReadingResponse>();
        Assert.NotNull(body);
        Assert.Equal("acme", body.TenantId);
        Assert.Equal("dev-123", body.DeviceId);
        Assert.Equal("water_level", body.Type);
    }

    [Fact]
    public async Task POST_Telemetry_DuplicateExternalId_Returns409()
    {
        var client = CreateClient();
        var payload = ValidPayload("r-dup-api");

        var first = await client.PostAsJsonAsync("/api/telemetry", payload);
        var second = await client.PostAsJsonAsync("/api/telemetry", payload);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task POST_Telemetry_InvalidBattery_Returns400()
    {
        var client = CreateClient();
        var payload = new
        {
            tenantId = "acme",
            deviceId = "dev-123",
            type = "water_level",
            value = 1.23,
            unit = "m",
            battery = 150, // invalid: > 100
            signal = -85,
            recordedAt = "2025-01-10T10:15:00Z",
            externalId = "r-bad-bat"
        };

        var response = await client.PostAsJsonAsync("/api/telemetry", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GET_Telemetry_ByTenant_ReturnsReadings()
    {
        var client = CreateClient();
        var postResp = await client.PostAsJsonAsync("/api/telemetry", ValidPayload("r-query-001"));
        Assert.Equal(HttpStatusCode.Created, postResp.StatusCode);

        var response = await client.GetAsync("/api/telemetry/acme");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedReadingsResponse>();
        Assert.NotNull(paged);
        Assert.True(paged.Items.Count > 0);
    }

    [Fact]
    public async Task GET_Health_Returns200()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_Telemetry_CorrelationIdPropagated()
    {
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/telemetry")
        {
            Content = JsonContent.Create(ValidPayload("r-corr-001"))
        };
        request.Headers.Add("X-Correlation-Id", "test-corr-id-123");

        var response = await client.SendAsync(request);

        Assert.Equal("test-corr-id-123", response.Headers.GetValues("X-Correlation-Id").FirstOrDefault());
    }
}
