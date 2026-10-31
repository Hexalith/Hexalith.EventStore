using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Common protected Dapr readback and lifecycle endpoints; caller authentication never substitutes for candidate trust.</summary>
public static class RouteCatalogEndpointExtensions
{
    /// <summary>Shared workload scheme, using the existing JWT and receiver-sidecar contracts.</summary>
    public const string WorkloadScheme = "EventStoreRouteCatalogWorkload";
    /// <summary>Exact protected shared-authority readback method.</summary>
    public const string ReadbackMethod = "internal/routing/catalog-generation";

    /// <summary>Recognizes only exact canonical control methods and their mapped HTTP verbs.</summary>
    public static bool IsControlMethod(string method, HttpMethod verb) =>
        method == ReadbackMethod && verb == HttpMethod.Get
        || verb == HttpMethod.Post && method is "internal/routing/catalog/prepare" or "internal/routing/catalog/ready"
            or "internal/routing/catalog/commit" or "internal/routing/catalog/rollback" or "internal/routing/catalog/reconcile";

    /// <summary>Maps generation readback and explicit prepare/ready/commit/rollback coordination.</summary>
    public static WebApplication MapEventStoreRouteCatalog(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hexalith.EventStore.RouteCatalog.Lifecycle");
        string readPolicy = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(WorkloadScheme, EventStoreOperationIdentifiers.RouteCatalogRead);
        string activationPolicy = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(WorkloadScheme, EventStoreOperationIdentifiers.RouteCatalogActivate);
        app.MapGet("/" + ReadbackMethod, async (IRouteCatalogAuthority authority, CancellationToken token) =>
            await ExecuteAsync(logger, async () => Results.Ok(await authority.ReadbackAsync(token).ConfigureAwait(false))).ConfigureAwait(false))
            .WithMetadata(RouteCatalogControlEndpointMetadata.Instance).RequireAuthorization(readPolicy);
        app.MapPost("/internal/routing/catalog/prepare", async (RouteCatalogActivation candidate, IRouteCatalogAuthority authority, CancellationToken token) =>
            await ExecuteAsync(logger, async () => { await authority.PrepareAsync(candidate, token).ConfigureAwait(false); return Results.NoContent(); }).ConfigureAwait(false))
            .WithMetadata(RouteCatalogControlEndpointMetadata.Instance).RequireAuthorization(activationPolicy);
        app.MapPost("/internal/routing/catalog/ready", async (RouteCatalogLifecycleRequest request, IRouteCatalogAuthority authority, CancellationToken token) =>
            await ExecuteAsync(logger, async () => Results.Ok(await authority.ReadyAsync(request.Identity, token).ConfigureAwait(false))).ConfigureAwait(false))
            .WithMetadata(RouteCatalogControlEndpointMetadata.Instance).RequireAuthorization(activationPolicy);
        app.MapPost("/internal/routing/catalog/commit", async (RouteCatalogLifecycleRequest request, IRouteCatalogAuthority authority, CancellationToken token) =>
            await ExecuteAsync(logger, async () => { await authority.CommitAsync(request.Identity, token).ConfigureAwait(false); return Results.NoContent(); }).ConfigureAwait(false))
            .WithMetadata(RouteCatalogControlEndpointMetadata.Instance).RequireAuthorization(activationPolicy);
        app.MapPost("/internal/routing/catalog/rollback", async (RouteCatalogRollbackRequest request, IRouteCatalogAuthority authority, CancellationToken token) =>
            await ExecuteAsync(logger, async () => { await authority.RollbackAsync(request, token).ConfigureAwait(false); return Results.NoContent(); }).ConfigureAwait(false))
            .WithMetadata(RouteCatalogControlEndpointMetadata.Instance).RequireAuthorization(activationPolicy);
        app.MapPost("/internal/routing/catalog/reconcile", async (RouteCatalogQuiescenceRequest request, IRouteCatalogAuthority authority, CancellationToken token) =>
            await ExecuteAsync(logger, async () => { await authority.ReconcileAsync(request, token).ConfigureAwait(false); return Results.NoContent(); }).ConfigureAwait(false))
            .WithMetadata(RouteCatalogControlEndpointMetadata.Instance).RequireAuthorization(activationPolicy);
        return app;
    }

    private static async Task<IResult> ExecuteAsync(ILogger logger, Func<Task<IResult>> action)
    {
        try { return await action().ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { RouteCatalogRefusalDiagnostics.Report(logger, "lifecycle", exception); return Results.StatusCode(503); }
    }
}
