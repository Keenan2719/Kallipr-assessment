using Kallipr.Telemetry.Api.Features.Telemetry;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Kallipr.Telemetry.Api.Features.Telemetry;

public static class TelemetryEndpoints
{
    public static IEndpointRouteBuilder MapTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/telemetry").WithTags("Telemetry"); //base url for all telemetry endpoints

        group.MapPost("/", IngestAsync)                    //endpoint to ingest a new reading
            .WithName("IngestReading")
            .Produces<ReadingResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapGet("/{tenantId}", QueryAsync)           //endpoint to query readings for a tenant with optional filters and pagination
            .WithName("QueryReadings")
            .Produces<PagedReadingsResponse>(StatusCodes.Status200OK);   //debugging purposes 

        return app;
    }

    private static async Task<IResult> IngestAsync(      //endpoint handler for ingesting a new reading
        IngestRequest request,                          //data from the request body
        TelemetryService service,
        ILogger<TelemetryService> logger,
        HttpContext httpContext)                          //raw request
    {
        var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                            ?? httpContext.TraceIdentifier;  //use provided correlation ID or fallback to trace identifier
        httpContext.Response.Headers["X-Correlation-Id"] = correlationId;

        var validationResults = new List<ValidationResult>(); //validate the request using data annotations on the IngestRequest class
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
        {
            var errors = validationResults  //group validation errors by field and return a structured response
                .GroupBy(v => v.MemberNames.FirstOrDefault() ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage ?? "Invalid value").ToArray());


            return Results.ValidationProblem(errors); //400 Bad Request with details on which fields were invalid
        }

        var (reading, error) = await service.IngestAsync(request); //destructure the result from the service => reading or error message

        if (error is not null)
        {
            return Results.Conflict(new ProblemDetails   //409 Conflict if a duplicate reading was detected(TenantId + ExternalId)
            {
                Title = "Duplicate reading",
                Detail = error,
                Status = StatusCodes.Status409Conflict,
                Extensions = { ["correlationId"] = correlationId }
            });
        }

        return Results.Created($"/api/telemetry/{reading!.TenantId}?externalId={reading.ExternalId}", reading); //successfully created! 201
    }

    private static async Task<IResult> QueryAsync( //endpoint handler for querying readings with filters and pagination
        string tenantId,
        [AsParameters] QueryParams query,
        TelemetryService service,
        HttpContext httpContext)
    {
        var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                            ?? httpContext.TraceIdentifier;
        httpContext.Response.Headers["X-Correlation-Id"] = correlationId;

        var result = await service.QueryAsync(tenantId, query);
        return Results.Ok(result); //200 OK with the paged list of readings matching the query
    }
}
