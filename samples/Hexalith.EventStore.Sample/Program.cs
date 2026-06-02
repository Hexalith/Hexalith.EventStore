using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter.Projections;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The domain-service SDK provides all hosting boilerplate: service defaults, convention discovery and
// registration of the Counter/Greeting aggregates and projections, runtime activation, and the canonical
// /process, /replay-state, and /admin/operational-index-metadata endpoints. This project writes only its
// domain code plus this two-line host.
builder.AddEventStoreDomainService();

bool malformedProjectionResponse = builder.Configuration
    .GetValue<bool>("EventStore:SampleFaults:MalformedProjectResponse");

WebApplication app = builder.Build();

int malformedProjectionResponseHitCount = 0;

app.UseEventStoreDomainService();

// Domain-specific projection endpoint. Epic A3 will generalize /project into the SDK; until then each
// domain maps its own projection handler.
if (malformedProjectionResponse) {
    _ = app.MapPost("/project", () => {
        _ = Interlocked.Increment(ref malformedProjectionResponseHitCount);

        // Intentionally malformed JSON payload used for Tier 3 fail-open validation.
        return Results.Content("{\"projectionType\":", "application/json");
    });

    _ = app.MapGet("/faults/project-hit-count", () => Results.Ok(new {
        Count = Volatile.Read(ref malformedProjectionResponseHitCount),
    }));
}
else {
    // Real /project endpoint: replays events into counter projection state.
    // DaprClient.InvokeMethodAsync round-trips ProjectionRequest/Response as JSON.
    _ = app.MapPost("/project", (ProjectionRequest request)
        => Results.Ok(CounterProjectionHandler.Project(request)));
}

app.Run();
