using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The domain-service SDK provides all hosting boilerplate: service defaults, convention discovery and
// registration of the Counter/Greeting aggregates, projections, and query handlers, runtime activation, and
// the canonical /process, /replay-state, /query, /project, and /admin/operational-index-metadata endpoints.
// This project writes only its domain code plus this host. The /project endpoint dispatches to the discovered
// CounterProjection handler (Epic A3) — the domain no longer hand-maps it.
builder.AddEventStoreDomainService();
builder.Services.AddCounterEventSerialization();

bool malformedProjectionResponse = builder.Configuration
    .GetValue<bool>("EventStore:SampleFaults:MalformedProjectResponse");

WebApplication app = builder.Build();

// Tier-3 fault injector (opt-in via EventStore:SampleFaults:MalformedProjectResponse). Mapping /project here —
// before UseEventStoreDomainService — makes the SDK yield the route to this bespoke handler, which returns a
// malformed projection response to exercise the gateway's projection fail-open path. In normal operation the
// SDK maps /project and dispatches to the discovered CounterProjection handler.
if (malformedProjectionResponse) {
    int malformedProjectionResponseHitCount = 0;

    // An override of an SDK route must carry the SDK's own policy; the startup route inventory refuses a weaker one.
    _ = app.MapPost("/project", () => {
        _ = Interlocked.Increment(ref malformedProjectionResponseHitCount);

        // Intentionally malformed JSON payload used for Tier 3 fail-open validation.
        return Results.Content("{\"projectionType\":", "application/json");
    }).RequireAuthorization(EventStoreDomainServicePolicies.Project);

    // Not anonymous: the domain-service fallback policy requires an EventStore workload assertion.
    _ = app.MapGet("/faults/project-hit-count", () => Results.Ok(new {
        Count = Volatile.Read(ref malformedProjectionResponseHitCount),
    }));
}

app.UseEventStoreDomainService();

app.Run();
