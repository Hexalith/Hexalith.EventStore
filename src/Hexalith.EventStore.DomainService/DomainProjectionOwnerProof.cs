using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Owns additive read-only proof routing; no domain handler or repair/status API is invoked.</summary>
public static class DomainProjectionOwnerProof
{
    /// <summary>Binds enrolled replay selections to the existing coordinated receipt without changing manifest bytes.</summary>
    internal static ReadModelBatch BindRebuildBatch(IServiceProvider services, ProjectionDispatchRequest request, ReadModelBatch batch,
        DomainProjectionIdentityOptions identity)
    {
        if (services.GetServices<IDomainProjectionHandler>().Any(handler => handler.Domain == request.Request.Domain)) return batch;
        if (request.Request.RebuildSourceHeadSequence == 0) return batch;
        if (!UsesGuardedCatalog(services, request, identity)) return batch;
        DomainProjectionOwnerProofRegistration[] enrolled = [.. services.GetServices<DomainProjectionOwnerProofRegistration>()
            .Where(item => item.Domain == request.Request.Domain && request.ProjectionTypes.Contains(item.ProjectionType, StringComparer.Ordinal))];
        if (enrolled.Length == 0) return batch;
        if (enrolled.GroupBy(static item => item.ProjectionType, StringComparer.Ordinal).Any(static group => group.Count() != 1)
            || enrolled.Any(static item => !item.Capability.IsSupported))
            throw new ArgumentException("Invalid coordinated replay enrollment.");
        ProjectionOwnerSelection[] selections = [.. batch.Operations.Where(static operation => operation.Kind == ReadModelBatchOperationKind.Write)
            .Select(static operation => ReadModelOwnerSelectionMetadata.Read(operation.CanonicalValue)).OfType<ProjectionOwnerSelection>()
            .Distinct().OrderBy(static item => item.ProjectionName, StringComparer.Ordinal)];
        long head = request.Request.RebuildSourceHeadSequence ?? request.Request.Events.LastOrDefault()?.SequenceNumber ?? 0;
        string? headMessageId = request.Request.RebuildSourceHeadMessageId ?? request.Request.Events.LastOrDefault()?.MessageId;
        if (head <= 0 || request.Request.Events.LastOrDefault() is { } last && (last.SequenceNumber != head || last.MessageId != headMessageId)
            || enrolled.Any(registration => selections.Count(selection => selection.ProjectionName == registration.ProjectionType) != 1)
            || selections.Any(selection => selection.AppliedSequence != head || selection.BatchId != request.DispatchId
                || headMessageId is not null && selection.MessageId != headMessageId
                || selection.ReceiptProjectionName != "rebuild" || !request.ProjectionTypes.Contains(selection.ProjectionName, StringComparer.Ordinal)))
        {
            if (!ProjectionReplayTransportContext.HasOwners(request.Request)) return batch;
            throw new ArgumentException("A replay selection is missing from the complete immutable manifest.");
        }
        if (ProjectionReplayTransportContext.HasOwners(request.Request)
            && (headMessageId is null || selections.Length != enrolled.Length
                || !ProjectionReplayTransportContext.MatchesOwners(request.Request, selections)
                || batch.Operations.Any(static operation => operation.Concurrency.Mode != ReadModelBatchConcurrencyMode.ExpectedETag)))
            throw new ArgumentException("A guarded replay must preserve its admitted generation and conditional ownership.");
        var bound = new ReadModelBatch(batch.Scope, batch.Operations) { OwnerSelections = selections };
        _ = ReadModelOwnerSelectionMetadata.FromCoordinatedBatch(bound);
        // Ordinary operator rebuilds retain their configured wider manifest profile.
        // Such output remains unqualified for guarded completion without a bounded receipt.
        if (!ProjectionReplayTransportContext.HasOwners(request.Request)
            && ReadModelBatchFingerprint.BuildCanonicalManifest(batch).Length > 1_048_576) return batch;
        return bound;
    }

    /// <summary>Reads only a supported exact route from the same fingerprint issued by this service.</summary>
    public static async Task<ProjectionOwnerProofResult> ReadAsync(IServiceProvider services,
        ProjectionOwnerProofRequest request, DomainProjectionCatalogRegistry catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        _ = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
        DomainProjectionOwnerProofRegistration[] registrations = [.. services.GetServices<DomainProjectionOwnerProofRegistration>()
            .Where(item => item.Domain == request.Domain && item.ProjectionType == request.ProjectionName)];
        if (registrations.Length != 1 || !registrations[0].Capability.IsSupported
            || registrations[0].Capability.HandlerSemantics == "shared-epoch-v1"
            || services.GetServices<IDomainProjectionHandler>().Any(handler => handler.Domain == request.Domain)
            || !AuthorizesCurrentCatalog(services, request)
            || services.GetService<IReadModelOwnerProofStore>() is not { } proofStore)
            return new(null, ProjectionDispatchReasonCodes.UnsupportedCapability, 0, 0);
        return await proofStore.ReadOwnerProofAsync(request, registrations[0].Binding, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the original terminal receipt for one admitted single-event batch.</summary>
    public static async Task<ProjectionOwnerProofResult> ReadAppliedReceiptAsync(IServiceProvider services,
        ProjectionAppliedReceiptRequest request, DomainProjectionCatalogRegistry catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scope);
        ArgumentNullException.ThrowIfNull(catalog);
        ProjectionOwnerProofRequest scope = request.Scope;
        _ = new AggregateIdentity(scope.TenantId, scope.Domain, scope.AggregateId);
        DomainProjectionOwnerProofRegistration[] registrations = [.. services.GetServices<DomainProjectionOwnerProofRegistration>()
            .Where(item => item.Domain == scope.Domain && item.ProjectionType == scope.ProjectionName)];
        if (registrations.Length != 1 || !registrations[0].Capability.IsSupported
            || !SupportsEventReceipts(services, scope, registrations[0].Capability)
            || services.GetServices<IDomainProjectionHandler>().Any(handler => handler.Domain == scope.Domain)
            || !AuthorizesCurrentCatalog(services, scope)
            || services.GetService<IReadModelAppliedReceiptStore>() is not { } store)
            return new(null, ProjectionDispatchReasonCodes.UnsupportedCapability, 0, 0);
        return await store.ReadAppliedReceiptAsync(request, registrations[0].Binding, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Explicitly reconciles an original partial event manifest; current proof never calls this operation.</summary>
    public static async Task<ProjectionOwnerProofResult> ReconcileAppliedReceiptAsync(IServiceProvider services,
        ProjectionAppliedReceiptRequest request, DomainProjectionCatalogRegistry catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scope);
        ArgumentNullException.ThrowIfNull(catalog);
        ProjectionOwnerProofRequest scope = request.Scope;
        _ = new AggregateIdentity(scope.TenantId, scope.Domain, scope.AggregateId);
        DomainProjectionOwnerProofRegistration[] registrations = [.. services.GetServices<DomainProjectionOwnerProofRegistration>()
            .Where(item => item.Domain == scope.Domain && item.ProjectionType == scope.ProjectionName)];
        if (registrations.Length != 1 || !registrations[0].Capability.IsSupported
            || !SupportsEventReceipts(services, scope, registrations[0].Capability)
            || services.GetServices<IDomainProjectionHandler>().Any(handler => handler.Domain == scope.Domain)
            || !AuthorizesCurrentCatalog(services, scope)
            || services.GetService<IReadModelAppliedReceiptRecoveryStore>() is not { } store)
            return new(null, ProjectionDispatchReasonCodes.UnsupportedCapability, 0, 0);
        return await store.ReconcileAppliedReceiptAsync(request, registrations[0].Binding, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Adds only explicitly enrolled supported proof descriptors to one issued catalog generation.</summary>
    public static AdminOperationalIndexMetadata.Response BindMetadata(AdminOperationalIndexMetadata.Response response,
        IEnumerable<DomainProjectionOwnerProofRegistration> registrations)
        => BindMetadata(response, registrations, null);

    /// <summary>Declines guard enrollment when a coexisting legacy output lacks stable prior catalog coverage.</summary>
    public static AdminOperationalIndexMetadata.Response BindMetadata(AdminOperationalIndexMetadata.Response response,
        IEnumerable<DomainProjectionOwnerProofRegistration> registrations,
        IEnumerable<IDomainProjectionHandler>? legacyProjectionHandlers)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(registrations);
        HashSet<string> legacyDomains = new((legacyProjectionHandlers ?? []).Select(static handler => handler.Domain), StringComparer.Ordinal);
        DomainProjectionOwnerProofRegistration[] enrolled = [.. registrations.Where(item => !legacyDomains.Contains(item.Domain))];
        if (enrolled.Length == 0 || response.CatalogFingerprint is null || response.AppId is null || response.ServiceVersion is null) return response;
        if (enrolled.GroupBy(static item => (item.Domain, item.ProjectionType)).Any(static group => group.Count() != 1)
            || enrolled.Any(static item => !item.Capability.IsSupported))
            throw new InvalidOperationException("Invalid source guard route enrollment.");
        AdminOperationalIndexMetadata.DomainMetadata[] domains = [.. response.Domains.Select(domain => domain with
        {
            SourceGuardCapabilities = enrolled.Where(item => item.Domain == domain.Domain
                    && (domain.NamedProjectionTypes ?? []).Contains(item.ProjectionType, StringComparer.Ordinal))
                .ToDictionary(static item => item.ProjectionType, static item => item.Capability, StringComparer.Ordinal),
        })];
        ProjectionDispatchRoute[] routes = [.. domains.SelectMany(domain => (domain.NamedProjectionTypes ?? [])
            .Select(type => new ProjectionDispatchRoute(domain.Domain, type)
            {
                SourceGuard = domain.SourceGuardCapabilities?.GetValueOrDefault(type),
            }))];
        return response with
        {
            Domains = domains,
            CatalogFingerprint = ProjectionRouteCatalogFingerprint.Compute(response.AppId, response.ServiceVersion,
                routes, domains.SelectMany(domain => (domain.OrdinaryProjectionTypes ?? domain.NamedProjectionTypes ?? [])
                    .Select(type => new ProjectionDispatchRoute(domain.Domain, type)))),
        };
    }
    /// <summary>Binds explicit descriptors when recomputing a catalog on a restarted replica.</summary>
    internal static IEnumerable<ProjectionDispatchRoute> BindRoutes(IServiceProvider services, IEnumerable<ProjectionDispatchRoute> routes)
    {
        Dictionary<(string Domain, string ProjectionType), ProjectionSourceGuardCapability> descriptors = services
            .GetServices<DomainProjectionOwnerProofRegistration>()
            .Where(item => !services.GetServices<IDomainProjectionHandler>().Any(handler => handler.Domain == item.Domain))
            .ToDictionary(static item => (item.Domain, item.ProjectionType), static item => item.Capability);
        return routes.Select(route => route with { SourceGuard = descriptors.GetValueOrDefault((route.Domain, route.ProjectionType)) });
    }

    /// <summary>Accepts the independently fingerprinted historical catalog only for its compatible unguarded semantics.</summary>
    internal static bool MatchesCatalog(IServiceProvider services, string appId, string version,
        IEnumerable<ProjectionDispatchRoute> routes, IEnumerable<ProjectionDispatchRoute> ordinary, string fingerprint)
    {
        ProjectionDispatchRoute[] all = [.. routes];
        ProjectionDispatchRoute[] enabled = [.. ordinary];
        return ProjectionRouteCatalogFingerprint.Compute(appId, version, all, enabled) == fingerprint
            || ProjectionRouteCatalogFingerprint.Compute(appId, version, BindRoutes(services, all), enabled) == fingerprint;
    }

    /// <summary>Distinguishes issued guarded route identities without accepting descriptors on an old catalog.</summary>
    internal static bool UsesGuardedCatalog(IServiceProvider services, ProjectionDispatchRequest request, DomainProjectionIdentityOptions identity)
    {
        IAsyncDomainProjectionHandler[] handlers = [.. services.GetServices<IAsyncDomainProjectionHandler>()
            .Where(handler => handler.Domain == request.Request.Domain)];
        ProjectionDispatchRoute[] routes = [.. handlers.Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        ProjectionDispatchRoute[] enabled = [.. handlers.Where(static handler => handler.OrdinaryDeliveryEnabled)
            .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        return ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion,
            BindRoutes(services, routes), enabled) == request.CatalogFingerprint
            && ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion, routes, enabled) != request.CatalogFingerprint;
    }

    private static bool SupportsEventReceipts(IServiceProvider services, ProjectionOwnerProofRequest scope,
        ProjectionSourceGuardCapability capability)
        => capability.HandlerSemantics == "single-event-v1"
            || capability.HandlerSemantics == "mapped-incremental-replay-v1"
                && services.GetServices<IAsyncDomainProjectionHandler>().Count(handler => handler.Domain == scope.Domain
                    && handler.ProjectionType == scope.ProjectionName && handler is IAsyncDomainProjectionIncrementalReplayHandler) == 1;

    private static bool AuthorizesCurrentCatalog(IServiceProvider services, ProjectionOwnerProofRequest scope)
    {
        DomainProjectionIdentityOptions? identity = services.GetService<IOptions<DomainProjectionIdentityOptions>>()?.Value;
        if (identity is null) return false;
        identity.Validate();
        IAsyncDomainProjectionHandler[] handlers = [.. services.GetServices<IAsyncDomainProjectionHandler>()];
        if (handlers.Count(handler => handler.Domain == scope.Domain && handler.ProjectionType == scope.ProjectionName) != 1)
            return false;
        bool Matches(IEnumerable<IAsyncDomainProjectionHandler> selected)
        {
            IAsyncDomainProjectionHandler[] inventory = [.. selected];
            return ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion,
                BindRoutes(services, inventory.Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))),
                inventory.Where(static handler => handler.OrdinaryDeliveryEnabled)
                    .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))) == scope.CatalogFingerprint;
        }
        return Matches(handlers.Where(handler => handler.Domain == scope.Domain)) || Matches(handlers);
    }
}
