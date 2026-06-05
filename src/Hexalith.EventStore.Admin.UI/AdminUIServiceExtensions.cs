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
        _ = builder.Services.AddScoped<DevelopmentAdminRoleState>();
        _ = builder.Services.AddScoped<AuthenticationStateProvider, TokenAuthenticationStateProvider>();

        // Admin API authentication for protected Admin.Server endpoints
        _ = builder.Services.AddScoped<AdminApiAccessTokenProvider>();
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

        // Shared tenant option provider — unions registered + authorized observed tenants
        // for the dropdowns on /commands, /events, /streams, /projections.
        _ = builder.Services.AddScoped<AdminTenantOptionsProvider>();

        // Dashboard polling refresh service
        _ = builder.Services.AddScoped<DashboardRefreshService>();

        // Viewport detection for responsive layout
        _ = builder.Services.AddScoped<ViewportService>();

        // Topology cache for sidebar tree
        _ = builder.Services.AddScoped<TopologyCacheService>();

        // SignalR client for real-time projection change signals.
        // The projection-changes hub is [Authorize]'d under the same JWT bearer scheme as the Admin.Server
        // REST API, so the connection must present a bearer token; without one the negotiate request is
        // rejected with 401 and the client silently degrades to polling. Reuse AdminApiAccessTokenProvider —
        // the identical token source already used for Admin.Server calls (validated against the same
        // issuer/audience). It is registered scoped, but this Func is invoked by the singleton SignalR client
        // outside any request scope (on connect/reconnect/refresh), so resolve it through a short-lived scope.
        string signalRHubUrl = builder.Configuration["EventStore:SignalR:HubUrl"]
            ?? "https+http://eventstore/hubs/projection-changes";
        _ = builder.Services.AddSingleton(sp => {
            IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new EventStoreSignalRClientOptions {
                HubUrl = signalRHubUrl,
                AccessTokenProvider = async () => {
                    using IServiceScope scope = scopeFactory.CreateScope();
                    AdminApiAccessTokenProvider tokenProvider = scope.ServiceProvider.GetRequiredService<AdminApiAccessTokenProvider>();
                    return await tokenProvider.GetAccessTokenAsync().ConfigureAwait(false);
                },
            };
        });
        _ = builder.Services.AddSingleton<EventStoreSignalRClient>();

        // HttpClient for invoking Admin.Server via DAPR service invocation (D13,
        // supersedes the ADR-P4 HTTP deviation). The BaseAddress targets THIS app's
        // DAPR sidecar; DaprAppIdHandler tags the request with the target app-id so
        // DAPR routes it to eventstore-admin. A literal localhost base address keeps
        // the global AddServiceDiscovery() default a no-op (no handler-ordering
        // conflict). DAPR forwards the Authorization bearer header unchanged, so
        // Admin.Server JWT/RBAC/tenant enforcement is preserved.
        string daprHttpEndpoint = builder.Configuration["DAPR_HTTP_ENDPOINT"]
            ?? $"http://localhost:{builder.Configuration["DAPR_HTTP_PORT"] ?? "3500"}";
        string? daprApiToken = builder.Configuration["DAPR_API_TOKEN"];
        _ = builder.Services.AddHttpClient("AdminApi", client => {
            client.BaseAddress = new Uri(daprHttpEndpoint);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
            .AddHttpMessageHandler<AdminApiAuthorizationHandler>()
            .AddHttpMessageHandler(() => new DaprAppIdHandler("eventstore-admin", daprApiToken));

        return builder;
    }

    /// <summary>
    /// Fails fast at startup when no DAPR sidecar is reachable. Admin.UI invokes Admin.Server
    /// exclusively through its sidecar (D13); without one, every API call would otherwise
    /// surface as an opaque request timeout during page interactions — the exact symptom that
    /// motivated this guard. Call this from the real entry point (<c>Program.cs</c>) only:
    /// test and Playwright E2E hosts build the UI via <see cref="AddAdminUI"/> without a sidecar
    /// and intentionally bypass this check.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no DAPR sidecar is detected.</exception>
    public static WebApplicationBuilder RequireDaprSidecar(this WebApplicationBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);

        // The sidecar (CommunityToolkit under `aspire run`, or the DAPR K8s injector) sets these
        // env vars on the app process. Presence of any one means a sidecar is wired. This mirrors
        // the DAPR_HTTP_PORT discovery already used by EventStore.Server's actor registration.
        bool sidecarConfigured =
            !string.IsNullOrWhiteSpace(builder.Configuration["DAPR_HTTP_ENDPOINT"])
            || !string.IsNullOrWhiteSpace(builder.Configuration["DAPR_GRPC_ENDPOINT"])
            || !string.IsNullOrWhiteSpace(builder.Configuration["DAPR_HTTP_PORT"])
            || !string.IsNullOrWhiteSpace(builder.Configuration["DAPR_GRPC_PORT"]);

        if (!sidecarConfigured) {
            throw new InvalidOperationException(
                "Admin.UI requires a DAPR sidecar to reach Admin.Server (D13: service invocation "
                + "to app-id 'eventstore-admin'), but none was detected — DAPR_HTTP_ENDPOINT, "
                + "DAPR_GRPC_ENDPOINT, DAPR_HTTP_PORT and DAPR_GRPC_PORT are all unset. "
                + "Launch the full topology through the Aspire AppHost:" + Environment.NewLine
                + "    aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj"
                + Environment.NewLine
                + "Running this project directly with `dotnet run` is unsupported: there is no sidecar "
                + "and no Admin.Server to invoke.");
        }

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
