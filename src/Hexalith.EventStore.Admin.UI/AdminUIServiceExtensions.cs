using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.SignalR;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Hexalith.EventStore.Admin.UI;

/// <summary>
/// Configures all Admin.UI services and middleware.
/// Extracted from Program.cs for testability with Playwright E2E fixtures.
/// </summary>
public static class AdminUIServiceExtensions {
    /// <summary>
    /// Registers all Admin.UI services (Blazor, FluentUI, authentication, API clients, SignalR).
    /// Call <see cref="Hexalith.EventStore.ServiceDefaults.Extensions.AddServiceDefaults{TBuilder}"/>
    /// separately — tests may skip it or supply a lightweight replacement.
    /// </summary>
    public static WebApplicationBuilder AddAdminUI(this WebApplicationBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.Services.AddProblemDetails();

        // Blazor Server with interactive server-side rendering
        _ = builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Fluent UI v5 components
        _ = builder.Services.AddFluentUIComponents();

        // Authentication & authorization
        _ = builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer();
        _ = builder.Services.AddAuthorization();
        _ = builder.Services.AddCascadingAuthenticationState();
        _ = builder.Services.AddScoped<AuthenticationStateProvider, TokenAuthenticationStateProvider>();

        // Admin API authentication for protected Admin.Server endpoints
        _ = builder.Services.AddSingleton<AdminApiAccessTokenProvider>();
        _ = builder.Services.AddTransient<AdminApiAuthorizationHandler>();

        // Admin user context for role-based UI rendering
        _ = builder.Services.AddScoped<AdminUserContext>();
        _ = builder.Services.AddScoped<ThemeState>();

        // Admin API clients
        _ = builder.Services.AddScoped<AdminStreamApiClient>();
        _ = builder.Services.AddScoped<AdminProjectionApiClient>();
        _ = builder.Services.AddScoped<AdminTypeCatalogApiClient>();
        _ = builder.Services.AddScoped<AdminStorageApiClient>();
        _ = builder.Services.AddScoped<AdminSnapshotApiClient>();
        _ = builder.Services.AddScoped<AdminCompactionApiClient>();
        _ = builder.Services.AddScoped<AdminBackupApiClient>();
        _ = builder.Services.AddScoped<AdminDeadLetterApiClient>();
        _ = builder.Services.AddScoped<AdminTenantApiClient>();
        _ = builder.Services.AddScoped<AdminConsistencyApiClient>();
        _ = builder.Services.AddScoped<AdminDaprApiClient>();
        _ = builder.Services.AddScoped<AdminActorApiClient>();
        _ = builder.Services.AddScoped<AdminPubSubApiClient>();
        _ = builder.Services.AddScoped<AdminResiliencyApiClient>();
        _ = builder.Services.AddScoped<AdminHealthHistoryApiClient>();

        // Dashboard polling refresh service
        _ = builder.Services.AddScoped<DashboardRefreshService>();

        // Viewport detection for responsive layout
        _ = builder.Services.AddScoped<ViewportService>();

        // Topology cache for sidebar tree
        _ = builder.Services.AddScoped<TopologyCacheService>();

        // SignalR client for real-time projection change signals
        string signalRHubUrl = builder.Configuration["EventStore:SignalR:HubUrl"]
            ?? "https+http://eventstore/hubs/projection-changes";
        _ = builder.Services.AddSingleton(new EventStoreSignalRClientOptions { HubUrl = signalRHubUrl });
        _ = builder.Services.AddSingleton<EventStoreSignalRClient>();

        // HttpClient for querying Admin.Server via Aspire service discovery
        _ = builder.Services.AddHttpClient("AdminApi", client => {
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
    public static WebApplication UseAdminUI(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);
        _ = app.UseExceptionHandler();

        if (!app.Environment.IsDevelopment()) {
            _ = app.UseHsts();
        }

        _ = app.UseHttpsRedirection();
        _ = app.UseAntiforgery();

        _ = app.MapStaticAssets();

        _ = app.UseAuthentication();
        _ = app.UseAuthorization();

        _ = app.MapRazorComponents<Hexalith.EventStore.Admin.UI.Components.App>()
            .AddInteractiveServerRenderMode();

        return app;
    }

    /// <summary>
    /// Starts the SignalR connection gracefully. Polling handles data refresh if SignalR is unavailable.
    /// </summary>
    public static async Task StartSignalRAsync(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);
        try {
            EventStoreSignalRClient signalRClient = app.Services.GetRequiredService<EventStoreSignalRClient>();
            await signalRClient.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex) {
            app.Logger.LogWarning(ex, "SignalR connection failed on startup. Polling will handle data refresh.");
        }
    }
}
