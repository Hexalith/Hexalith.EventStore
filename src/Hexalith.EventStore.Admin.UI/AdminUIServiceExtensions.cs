using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.SignalR;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI;

/// <summary>
/// Configures all Admin.UI services and middleware.
/// Extracted from Program.cs for testability with Playwright E2E fixtures.
/// </summary>
public static class AdminUIServiceExtensions
{
    /// <summary>
    /// Registers all Admin.UI services (Blazor, FluentUI, authentication, API clients, SignalR).
    /// Call <see cref="Hexalith.EventStore.ServiceDefaults.Extensions.AddServiceDefaults{TBuilder}"/>
    /// separately — tests may skip it or supply a lightweight replacement.
    /// </summary>
    public static WebApplicationBuilder AddAdminUI(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
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
        builder.Services.AddScoped<AuthenticationStateProvider, TokenAuthenticationStateProvider>();

        // Admin API authentication for protected Admin.Server endpoints
        builder.Services.AddSingleton<AdminApiAccessTokenProvider>();
        builder.Services.AddTransient<AdminApiAuthorizationHandler>();

        // Admin user context for role-based UI rendering
        builder.Services.AddScoped<AdminUserContext>();
        builder.Services.AddScoped<ThemeState>();

        // Admin API clients
        builder.Services.AddScoped<AdminStreamApiClient>();
        builder.Services.AddScoped<AdminProjectionApiClient>();
        builder.Services.AddScoped<AdminTypeCatalogApiClient>();
        builder.Services.AddScoped<AdminStorageApiClient>();
        builder.Services.AddScoped<AdminSnapshotApiClient>();
        builder.Services.AddScoped<AdminCompactionApiClient>();
        builder.Services.AddScoped<AdminBackupApiClient>();
        builder.Services.AddScoped<AdminDeadLetterApiClient>();
        builder.Services.AddScoped<AdminTenantApiClient>();
        builder.Services.AddScoped<AdminConsistencyApiClient>();
        builder.Services.AddScoped<AdminDaprApiClient>();
        builder.Services.AddScoped<AdminActorApiClient>();
        builder.Services.AddScoped<AdminPubSubApiClient>();
        builder.Services.AddScoped<AdminResiliencyApiClient>();
        builder.Services.AddScoped<AdminHealthHistoryApiClient>();

        // Dashboard polling refresh service
        builder.Services.AddScoped<DashboardRefreshService>();

        // Viewport detection for responsive layout
        builder.Services.AddScoped<ViewportService>();

        // Topology cache for sidebar tree
        builder.Services.AddScoped<TopologyCacheService>();

        // SignalR client for real-time projection change signals
        string signalRHubUrl = builder.Configuration["EventStore:SignalR:HubUrl"]
            ?? "https+http://eventstore/hubs/projection-changes";
        builder.Services.AddSingleton(new EventStoreSignalRClientOptions { HubUrl = signalRHubUrl });
        builder.Services.AddSingleton<EventStoreSignalRClient>();

        // HttpClient for querying Admin.Server via Aspire service discovery
        builder.Services.AddHttpClient("AdminApi", client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["EventStore:AdminServer:BaseUrl"]
                ?? "https://eventstore-admin");
            client.Timeout = TimeSpan.FromSeconds(5);
        })
            .AddHttpMessageHandler<AdminApiAuthorizationHandler>();

        return builder;
    }

    /// <summary>
    /// Configures the Admin.UI middleware pipeline.
    /// </summary>
    public static WebApplication UseAdminUI(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _ = app.UseExceptionHandler();

        if (!app.Environment.IsDevelopment())
        {
            _ = app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseAntiforgery();

        app.MapStaticAssets();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorComponents<Hexalith.EventStore.Admin.UI.Components.App>()
            .AddInteractiveServerRenderMode();

        return app;
    }

    /// <summary>
    /// Starts the SignalR connection gracefully. Polling handles data refresh if SignalR is unavailable.
    /// </summary>
    public static async Task StartSignalRAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        try
        {
            EventStoreSignalRClient signalRClient = app.Services.GetRequiredService<EventStoreSignalRClient>();
            await signalRClient.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "SignalR connection failed on startup. Polling will handle data refresh.");
        }
    }
}
