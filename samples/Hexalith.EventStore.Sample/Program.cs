using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEventStore();

WebApplication app = builder.Build();

app.UseEventStore();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Hexalith EventStore Sample Domain Service");

app.MapPost("/process", async (DomainServiceRequest request, IDomainProcessor processor) => {
    DomainResult result = await processor.ProcessAsync(request.Command, request.CurrentState).ConfigureAwait(false);
    return Results.Ok(DomainServiceWireResult.FromDomainResult(result));
});

app.Run();
