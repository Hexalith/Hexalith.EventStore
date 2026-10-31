using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Sample.BlazorUI.Services;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Routing;
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
builder.Services.AddSampleUiGatewayClient(builder.Configuration);
builder.Services.AddEventStoreDevelopmentCallerCatalog(builder.Configuration, "sample-blazor-ui");

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

// Outbound path to the EventStore gateway via this app's DAPR sidecar (mirrors Admin.UI D13).
// BaseAddress targets THIS app's DAPR sidecar; the platform invocation handler owns the target
// app-id so DAPR routes it to eventstore. A literal localhost base address keeps the global
// AddServiceDiscovery() default a no-op. DAPR forwards the Authorization bearer header unchanged,
// so EventStore JWT/RBAC/tenant enforcement is preserved. Both projection queries and command
// submissions flow through this single typed gateway client — the UI hosts no generic command path.

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment()) {
    _ = app.UseHsts();
}

app.UseHttpsRedirection();
app.UseEventStoreDevelopmentCallerCatalog();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<Hexalith.EventStore.Sample.BlazorUI.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
