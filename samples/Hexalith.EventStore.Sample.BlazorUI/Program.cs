using Hexalith.EventStore.Client.Registration;
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

// Outbound bearer-token acquisition + attachment for calls to the protected EventStore gateway.
// The UI host consumes EventStore through the platform gateway client; it exposes no REST endpoints
// of its own, so no inbound authentication scheme is registered.
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

// HttpClient for querying EventStore via DAPR service invocation (mirrors Admin.UI D13).
// BaseAddress targets THIS app's DAPR sidecar; DaprAppIdHandler tags the request with the
// target app-id so DAPR routes it to eventstore. A literal localhost base address keeps the
// global AddServiceDiscovery() default a no-op. DAPR forwards the Authorization bearer header
// unchanged, so EventStore JWT/RBAC/tenant enforcement is preserved.
string daprHttpEndpoint = builder.Configuration["DAPR_HTTP_ENDPOINT"]
    ?? $"http://localhost:{builder.Configuration["DAPR_HTTP_PORT"] ?? "3500"}";
string? daprApiToken = builder.Configuration["DAPR_API_TOKEN"];
builder.Services.AddHttpClient("EventStoreApi", client => {
    client.BaseAddress = new Uri(daprHttpEndpoint);
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .AddHttpMessageHandler<EventStoreApiAuthorizationHandler>()
    .AddHttpMessageHandler(() => new DaprAppIdHandler("eventstore", daprApiToken));

builder.Services.AddEventStoreGatewayClient(options => options.BaseAddress = new Uri(daprHttpEndpoint))
    .AddHttpMessageHandler<EventStoreApiAuthorizationHandler>()
    .AddHttpMessageHandler(() => new DaprAppIdHandler("eventstore", daprApiToken));
builder.Services.AddScoped<EventStoreProjectionQueryClient>();

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
