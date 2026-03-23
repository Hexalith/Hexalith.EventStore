using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.SignalR;

using Microsoft.FluentUI.AspNetCore.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

// Blazor Server with interactive server-side rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Fluent UI v4 components
builder.Services.AddFluentUIComponents();

// Authentication & authorization
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer();
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Admin API authentication for protected Admin.Server endpoints
builder.Services.AddSingleton<AdminApiAccessTokenProvider>();
builder.Services.AddTransient<AdminApiAuthorizationHandler>();

// Admin user context for role-based UI rendering
builder.Services.AddScoped<AdminUserContext>();
builder.Services.AddScoped<ThemeState>();

// Admin API client for streams, health, and tenants
builder.Services.AddScoped<AdminStreamApiClient>();

// Dashboard polling refresh service
builder.Services.AddScoped<DashboardRefreshService>();

// Viewport detection for responsive layout
builder.Services.AddScoped<ViewportService>();

// Topology cache for sidebar tree
builder.Services.AddScoped<TopologyCacheService>();

// SignalR client for real-time projection change signals
string signalRHubUrl = builder.Configuration["EventStore:SignalR:HubUrl"]
    ?? "https+http://commandapi/hubs/projection-changes";
builder.Services.AddSingleton(new EventStoreSignalRClientOptions { HubUrl = signalRHubUrl });
builder.Services.AddSingleton<EventStoreSignalRClient>();

// HttpClient for querying Admin.Server via Aspire service discovery
builder.Services.AddHttpClient("AdminApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["EventStore:AdminServer:BaseUrl"]
        ?? "https://admin-server");
    client.Timeout = TimeSpan.FromSeconds(5);
})
    .AddHttpMessageHandler<AdminApiAuthorizationHandler>();

WebApplication app = builder.Build();

_ = app.UseExceptionHandler();

if (!app.Environment.IsDevelopment()) {
    _ = app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<Hexalith.EventStore.Admin.UI.Components.App>()
    .AddInteractiveServerRenderMode();

// Start SignalR connection gracefully — polling handles data refresh if SignalR is unavailable
try
{
    EventStoreSignalRClient signalRClient = app.Services.GetRequiredService<EventStoreSignalRClient>();
    await signalRClient.StartAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "SignalR connection failed on startup. Polling will handle data refresh.");
}

await app.RunAsync().ConfigureAwait(false);
