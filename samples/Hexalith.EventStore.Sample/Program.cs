using System.Threading;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEventStore();

bool malformedProjectionResponse = builder.Configuration
    .GetValue<bool>("EventStore:SampleFaults:MalformedProjectResponse");

WebApplication app = builder.Build();

int malformedProjectionResponseHitCount = 0;

app.UseEventStore();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Hexalith EventStore Sample Domain Service");

app.MapPost("/process", async (DomainServiceRequest request, IServiceProvider serviceProvider) => {
    return Results.Ok(await Hexalith.EventStore.Sample.DomainServiceRequestRouter.ProcessAsync(serviceProvider, request).ConfigureAwait(false));
});

if (malformedProjectionResponse) {
    app.MapPost("/project", () => {
        _ = Interlocked.Increment(ref malformedProjectionResponseHitCount);

        // Intentionally malformed JSON payload used for Tier 3 fail-open validation.
        return Results.Content("{\"projectionType\":", "application/json");
    });

    app.MapGet("/faults/project-hit-count", () => Results.Ok(new {
        Count = Volatile.Read(ref malformedProjectionResponseHitCount),
    }));
}

app.Run();
