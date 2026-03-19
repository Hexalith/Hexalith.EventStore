using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEventStore();

WebApplication app = builder.Build();

app.UseEventStore();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Hexalith EventStore Sample Domain Service");

app.MapPost("/process", async (DomainServiceRequest request, IServiceProvider serviceProvider) => {
    return Results.Ok(await Hexalith.EventStore.Sample.DomainServiceRequestRouter.ProcessAsync(serviceProvider, request).ConfigureAwait(false));
});

app.Run();
