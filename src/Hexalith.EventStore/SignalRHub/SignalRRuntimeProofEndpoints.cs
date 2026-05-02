using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// Development-only runtime proof endpoints for deterministic SignalR backplane validation.
/// </summary>
public static class SignalRRuntimeProofEndpoints {
    private const string ConfigurationKey = "EventStore:SignalR:RuntimeProof:Enabled";

    /// <summary>
    /// Maps test-only SignalR runtime proof endpoints when explicitly enabled in Development.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application.</returns>
    public static WebApplication MapSignalRRuntimeProofEndpoints(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment()
            || !app.Configuration.GetValue<bool>(ConfigurationKey)) {
            return app;
        }

        RouteGroupBuilder group = app.MapGroup("/_test/signalr/runtime-proof");

        _ = group.MapGet("/identity", (IConfiguration configuration, IWebHostEnvironment environment) => {
            string? redis = GetBackplaneRedisConnectionString(configuration);
            bool signalREnabled = configuration.GetValue<bool>("EventStore:SignalR:Enabled");

            return Results.Ok(new SignalRRuntimeProofIdentity(
                InstanceName: configuration["EventStore:SignalR:RuntimeProof:InstanceName"]
                    ?? Environment.GetEnvironmentVariable("EventStore__SignalR__RuntimeProof__InstanceName")
                    ?? Environment.MachineName,
                ProcessId: Environment.ProcessId,
                MachineName: Environment.MachineName,
                EnvironmentName: environment.EnvironmentName,
                SignalREnabled: signalREnabled,
                BackplaneRedisConnectionString: RedactSecrets(redis),
                Timestamp: DateTimeOffset.UtcNow));
        });

        _ = group.MapPost(
            "/broadcast",
            async (
                SignalRRuntimeProofBroadcastRequest? request,
                IProjectionChangedBroadcaster broadcaster,
                CancellationToken cancellationToken) => {
                if (request is null) {
                    return Results.BadRequest("request body is required.");
                }

                if (string.IsNullOrWhiteSpace(request.ProjectionType)
                    || string.IsNullOrWhiteSpace(request.TenantId)) {
                    return Results.BadRequest("projectionType and tenantId are required.");
                }

                if (request.ProjectionType.Contains(':', StringComparison.Ordinal)
                    || request.TenantId.Contains(':', StringComparison.Ordinal)) {
                    return Results.BadRequest("projectionType and tenantId must not contain colons.");
                }

                await broadcaster
                    .BroadcastChangedAsync(request.ProjectionType, request.TenantId, cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(new SignalRRuntimeProofBroadcastResult(
                    ProjectionType: request.ProjectionType,
                    TenantId: request.TenantId,
                    GroupName: $"{request.ProjectionType}:{request.TenantId}",
                    ProcessId: Environment.ProcessId,
                    Timestamp: DateTimeOffset.UtcNow));
            });

        return app;
    }

    private static string? GetBackplaneRedisConnectionString(IConfiguration configuration) {
        string? configured = configuration["EventStore:SignalR:BackplaneRedisConnectionString"];
        if (!string.IsNullOrWhiteSpace(configured)) {
            return configured;
        }

        string? environment = Environment.GetEnvironmentVariable("EVENTSTORE_SIGNALR_REDIS");
        return string.IsNullOrWhiteSpace(environment) ? null : environment;
    }

    private static string? RedactSecrets(string? connectionString) {
        if (string.IsNullOrWhiteSpace(connectionString)) {
            return connectionString;
        }

        string[] segments = connectionString.Split(',');
        for (int i = 0; i < segments.Length; i++) {
            int eq = segments[i].IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0) {
                continue;
            }

            string key = segments[i][..eq].Trim();
            if (key.Equals("password", StringComparison.OrdinalIgnoreCase)
                || key.Equals("user", StringComparison.OrdinalIgnoreCase)) {
                segments[i] = $"{key}=***";
            }
        }

        return string.Join(',', segments);
    }
}

/// <summary>
/// Runtime identity emitted by Development-only SignalR proof endpoints.
/// </summary>
/// <param name="InstanceName">Configured proof instance name.</param>
/// <param name="ProcessId">Operating system process id.</param>
/// <param name="MachineName">Machine name.</param>
/// <param name="EnvironmentName">ASP.NET Core environment name.</param>
/// <param name="SignalREnabled">Whether SignalR is enabled in runtime configuration.</param>
/// <param name="BackplaneRedisConnectionString">Effective Redis backplane connection string.</param>
/// <param name="Timestamp">UTC timestamp when the identity was captured.</param>
public sealed record SignalRRuntimeProofIdentity(
    string InstanceName,
    int ProcessId,
    string MachineName,
    string EnvironmentName,
    bool SignalREnabled,
    string? BackplaneRedisConnectionString,
    DateTimeOffset Timestamp);

/// <summary>
/// Broadcast request accepted only by Development-only SignalR proof endpoints.
/// </summary>
/// <param name="ProjectionType">Projection type to broadcast.</param>
/// <param name="TenantId">Tenant id to broadcast.</param>
public sealed record SignalRRuntimeProofBroadcastRequest(string ProjectionType, string TenantId);

/// <summary>
/// Broadcast result emitted by Development-only SignalR proof endpoints.
/// </summary>
/// <param name="ProjectionType">Projection type broadcast.</param>
/// <param name="TenantId">Tenant id broadcast.</param>
/// <param name="GroupName">SignalR group name.</param>
/// <param name="ProcessId">Origin process id.</param>
/// <param name="Timestamp">UTC timestamp after the broadcast call completed.</param>
public sealed record SignalRRuntimeProofBroadcastResult(
    string ProjectionType,
    string TenantId,
    string GroupName,
    int ProcessId,
    DateTimeOffset Timestamp);

