using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEventStoreClient<CounterProcessor>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Hexalith EventStore Sample Domain Service");

app.MapPost("/process", async (CommandEnvelope command, IDomainProcessor processor) => {
    DomainResult result = await processor.ProcessAsync(command, currentState: null).ConfigureAwait(false);
    return Results.Ok(result);
});

app.Run();
