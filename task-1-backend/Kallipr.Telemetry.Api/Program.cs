using Kallipr.Telemetry.Api.Configuration;
using Kallipr.Telemetry.Api.Data;
using Kallipr.Telemetry.Api.Features.Telemetry;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()   //temp logger during app startup before we have fully built the host and can read config
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try                                 //wrapped in a try-catch => error detected, exit clean and log as fatal
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg //configure Serilog from appsettings and replace EF Core command logging to Warning to reduce noise
        .ReadFrom.Configuration(ctx.Configuration) //read config from appsettings
        .ReadFrom.Services(services)              //allow Serilog to resolve services like ILogger<TelemetryService> for dependency injection in our service and endpoints
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

    // Configuration
    builder.Services.Configure<TelemetrySettings>(
        builder.Configuration.GetSection(TelemetrySettings.SectionName));

    // Database
    var dbPath = builder.Configuration.GetConnectionString("Sqlite") ?? "telemetry.db";
    builder.Services.AddDbContext<TelemetryDbContext>(opt =>
        opt.UseSqlite($"Data Source={dbPath}"));

    // Services
    builder.Services.AddScoped<TelemetryService>();

    // Health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<TelemetryDbContext>("sqlite");

    // CORS for the UI
    builder.Services.AddCors(opt =>
        opt.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    var app = builder.Build();

    // Apply migrations at startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();  //get the db context from the service provider
        if (db.Database.IsRelational())
            db.Database.Migrate();
        else
            db.Database.EnsureCreated();
    }

    app.UseSerilogRequestLogging(opt =>
    {
        opt.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
        opt.EnrichDiagnosticContext = (diag, ctx) =>
        {
            diag.Set("TenantId", ctx.Request.RouteValues.GetValueOrDefault("tenantId") ?? "");
            diag.Set("CorrelationId", ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? ctx.TraceIdentifier);
        };
    });

    app.UseCors();  //enable CORS with the default policy defined above

    app.MapHealthChecks("/health");  //health check endpoint
    app.MapTelemetryEndpoints();  //map the telemetry endpoints defined in TelemetryEndpoints.cs

    app.Run();  //start the app 
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    Log.CloseAndFlush();
}

// Expose for WebApplicationFactory in tests
public partial class Program { }
