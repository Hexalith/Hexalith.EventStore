using Hexalith.EventStore.Sample.BlazorUI.Services;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.SignalR;

using Microsoft.FluentUI.AspNetCore.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Blazor Server with interactive server-side rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Fluent UI v4 components
builder.Services.AddFluentUIComponents();

// EventStore API authentication for protected EventStore/query endpoints.
builder.Services.AddSingleton<EventStoreApiAccessTokenProvider>();
builder.Services.AddTransient<EventStoreApiAuthorizationHandler>();

// EventStore SignalR client — receives real-time "changed" signals
builder.Services.AddSingleton(sp => new EventStoreSignalRClientOptions {
    HubUrl = builder.Configuration["EventStore:SignalR:HubUrl"]
        ?? "https://eventstore/hubs/projection-changes",
    AccessTokenProvider = async () => await sp.GetRequiredService<EventStoreApiAccessTokenProvider>()
        .GetAccessTokenAsync()
        .ConfigureAwait(false),
    ConfigureHttpConnection = connectionOptions => {
        // In development, accept the EventStore's dev certificate for SignalR hub connections.
        // HttpMessageHandlerFactory covers HTTP negotiate/long-polling/SSE transports.
        // WebSocketConfiguration covers the WebSocket transport (SignalR's default) — its TLS
        // stack is separate and NOT affected by HttpMessageHandlerFactory.
        if (builder.Environment.IsDevelopment()) {
            connectionOptions.HttpMessageHandlerFactory = handler => {
                if (handler is HttpClientHandler clientHandler) {
                    clientHandler.ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                }

                return handler;
            };
            connectionOptions.WebSocketConfiguration = ws => ws.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }
    },
});
builder.Services.AddSingleton<EventStoreSignalRClient>();
builder.Services.AddHostedService<SignalRClientStartup>();

// HttpClient for querying EventStore via Aspire service discovery
builder.Services.AddHttpClient("EventStoreApi", client =>
    client.BaseAddress = new Uri(builder.Configuration["EventStore:EventStoreUrl"]
        ?? "https://eventstore"))
    .AddHttpMessageHandler<EventStoreApiAuthorizationHandler>();

// Counter query service (shared across all patterns)
builder.Services.AddScoped<CounterQueryService>();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment()) {
    _ = app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<Hexalith.EventStore.Sample.BlazorUI.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
