using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// Development-only runtime proof endpoints for deterministic SignalR backplane validation.
/// </summary>
public static class SignalRRuntimeProofEndpoints {
    private const string ConfigurationKey = "EventStore:SignalR:RuntimeProof:Enabled";
    private const int MaxGroupScopeLength = 64;

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
                IOptions<SignalROptions> options,
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

                    string? groupScope = string.IsNullOrWhiteSpace(request.GroupScope) ? null : request.GroupScope;
                    if (groupScope is not null && groupScope.Contains(':', StringComparison.Ordinal)) {
                        return Results.BadRequest("groupScope must not contain colons.");
                    }

                    if (groupScope is not null && groupScope.Length > MaxGroupScopeLength) {
                        return Results.BadRequest($"groupScope must not exceed {MaxGroupScopeLength} characters.");
                    }

                    if (request.Metadata?.Any(entry => entry.Key is null || entry.Value is null) == true) {
                        return Results.BadRequest("metadata keys and values are required.");
                    }

                    IReadOnlyDictionary<string, string> metadata = BoundMetadata(request.Metadata, options.Value);
                    bool hasDetail = groupScope is not null || request.Metadata is not null;
                    string groupName = ProjectionChangedHub.BuildGroupName(request.ProjectionType, request.TenantId, groupScope);

                    if (hasDetail) {
                        var detail = new ProjectionChangedDetail(
                            request.ProjectionType,
                            request.TenantId,
                            groupScope,
                            metadata);

                        await broadcaster.BroadcastChangedAsync(detail, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else {
                        await broadcaster
                            .BroadcastChangedAsync(request.ProjectionType, request.TenantId, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    return Results.Ok(new SignalRRuntimeProofBroadcastResult(
                        ProjectionType: request.ProjectionType,
                        TenantId: request.TenantId,
                        GroupScope: groupScope,
                        GroupName: groupName,
                        MetadataCount: metadata.Count,
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

    private static IReadOnlyDictionary<string, string> BoundMetadata(
        IReadOnlyDictionary<string, string>? metadata,
        SignalROptions options)
        => ProjectionChangedMetadataLimits.Bound(metadata, options.MaxDetailMetadataEntries, options.MaxDetailMetadataBytes);
}
