using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Queries;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Default fail-closed normal-delivery coordinator for named projection handlers.</summary>
internal sealed partial class NamedProjectionDispatchCoordinator : INamedProjectionDispatchCoordinator {
    private INamedProjectionRouteCatalog routeCatalog { get; }
    private IProjectionDeliveryCheckpointStore checkpointStore { get; }
    private IProjectionLifecycleGateway lifecycleGateway { get; }
    private IActorProxyFactory actorProxyFactory { get; }
    private DaprClient daprClient { get; }
    private IHttpClientFactory httpClientFactory { get; }
    private IOptions<ProjectionDispatchOptions> options { get; }
    private ILogger<NamedProjectionDispatchCoordinator> logger { get; }
    private IProjectionDeliveryRetryScheduler? retryScheduler { get; }
    private TimeProvider? timeProvider { get; }
    private INamedProjectionCatalogRefresher? catalogRefresher { get; }
    private IProjectionDeliveryIdempotencyCoordinator? idempotencyCoordinator { get; }
    private IOptions<EventStoreActorOptions>? actorOptions { get; }
    private IProjectionDeliveryHistoryReader? historyReader { get; }
    private Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority { get; }

    /// <summary>Creates the guarded runtime implementation.</summary>
    public NamedProjectionDispatchCoordinator(
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority authority,
        INamedProjectionRouteCatalog routeCatalog,
        IProjectionDeliveryCheckpointStore checkpointStore,
        IProjectionLifecycleGateway lifecycleGateway,
        IActorProxyFactory actorProxyFactory,
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<ProjectionDispatchOptions> options,
        ILogger<NamedProjectionDispatchCoordinator> logger,
        IProjectionDeliveryRetryScheduler? retryScheduler = null,
        TimeProvider? timeProvider = null,
        INamedProjectionCatalogRefresher? catalogRefresher = null,
        IProjectionDeliveryIdempotencyCoordinator? idempotencyCoordinator = null,
        IOptions<EventStoreActorOptions>? actorOptions = null,
        IProjectionDeliveryHistoryReader? historyReader = null)
        : this(routeCatalog, checkpointStore, lifecycleGateway, actorProxyFactory, daprClient, httpClientFactory, options, logger, retryScheduler, timeProvider, catalogRefresher, idempotencyCoordinator, actorOptions, historyReader, authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
    }

    /// <summary>Friend-only protocol composition seam.</summary>
    internal NamedProjectionDispatchCoordinator(
        INamedProjectionRouteCatalog routeCatalog,
        IProjectionDeliveryCheckpointStore checkpointStore,
        IProjectionLifecycleGateway lifecycleGateway,
        IActorProxyFactory actorProxyFactory,
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<ProjectionDispatchOptions> options,
        ILogger<NamedProjectionDispatchCoordinator> logger,
        IProjectionDeliveryRetryScheduler? retryScheduler = null,
        TimeProvider? timeProvider = null,
        INamedProjectionCatalogRefresher? catalogRefresher = null,
        IProjectionDeliveryIdempotencyCoordinator? idempotencyCoordinator = null,
        IOptions<EventStoreActorOptions>? actorOptions = null,
        IProjectionDeliveryHistoryReader? historyReader = null,
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority = null)
    {
        this.routeCatalog = routeCatalog;
        this.checkpointStore = checkpointStore;
        this.lifecycleGateway = lifecycleGateway;
        this.actorProxyFactory = actorProxyFactory;
        this.daprClient = daprClient;
        this.httpClientFactory = httpClientFactory;
        this.options = options;
        this.logger = logger;
        this.retryScheduler = retryScheduler;
        this.timeProvider = timeProvider;
        this.catalogRefresher = catalogRefresher;
        this.idempotencyCoordinator = idempotencyCoordinator;
        this.actorOptions = actorOptions;
        this.historyReader = historyReader;
        this.catalogAuthority = catalogAuthority;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider _timeProvider;
    private string AggregateActorTypeName => actorOptions?.Value.AggregateActorTypeName ?? nameof(AggregateActor);

    /// <inheritdoc/>
    public Task<bool> TryDispatchCompatibleAsync(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        long headSequence, string headMessageId, ProjectionEventDto[] events, IReadOnlyList<string> routes,
        CancellationToken cancellationToken)
        => TryDispatchCoreAsync(identity, new(catalog.AppId, "project/v2", identity.TenantId, identity.Domain, catalog.ServiceVersion),
            headSequence, headMessageId, events, false, cancellationToken, routes, catalog.CatalogFingerprint);

    /// <inheritdoc/>
    public Task<bool> TryDispatchCompatibleAsync(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        long headSequence, string headMessageId, ProjectionEventDto[] events, IReadOnlyList<string> routes,
        NativeSourceProof nativeSourceProof, CancellationToken cancellationToken)
        => TryDispatchCoreAsync(identity, new(catalog.AppId, "project/v2", identity.TenantId, identity.Domain, catalog.ServiceVersion),
            headSequence, headMessageId, events, false, cancellationToken, routes, catalog.CatalogFingerprint, nativeSourceProof);

    public async Task<bool> SupportsMappedOnlyStreamedDeliveryAsync(
        AggregateIdentity identity, DomainServiceRegistration registration, CancellationToken cancellationToken) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);
        NamedProjectionRouteCatalogEntry? entry = await ResolveCatalogEntryAsync(identity, registration,
            cancellationToken).ConfigureAwait(false);
        return historyReader is not null && idempotencyCoordinator is not null && retryScheduler is not null
            && entry is not null && entry.FullReplayProjectionTypes.Count == 1
            && entry.FullReplayProjectionTypes.All(type =>
                entry.MappedOnlyStreamedProjectionTypes.Contains(type, StringComparer.Ordinal))
            && entry.OrdinaryProjectionTypes.Count > 0
            && entry.OrdinaryProjectionTypes.All(type =>
                entry.FullReplayProjectionTypes.Contains(type, StringComparer.Ordinal)
                || entry.SharedEpochProjectionTypes.Contains(type, StringComparer.Ordinal)
                || entry.SingleEventProjectionTypes.Contains(type, StringComparer.Ordinal));
    }

    public async Task<bool> SupportsMappedOnlyStreamedRebuildAsync(
        AggregateIdentity identity, DomainServiceRegistration registration,
        string projectionName, CancellationToken cancellationToken) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);
        NamedProjectionRouteCatalogEntry? entry = await ResolveCatalogEntryAsync(identity, registration,
            cancellationToken).ConfigureAwait(false);
        return entry is not null
            && entry.FullReplayProjectionTypes.Count > 0
            && entry.FullReplayProjectionTypes.Contains(projectionName, StringComparer.Ordinal)
            && entry.FullReplayProjectionTypes.All(type =>
                entry.MappedOnlyStreamedProjectionTypes.Contains(type, StringComparer.Ordinal));
    }

    public async Task<ProjectionMappedReplayStageResult> StageMappedOnlyStreamedRebuildAsync(
        AggregateIdentity identity, DomainServiceRegistration registration, string operationId,
        long sourceHead, Func<Stream, CancellationToken, Task<string?>> writeSource,
        CancellationToken cancellationToken) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);
        NamedProjectionRouteCatalogEntry? entry = await ResolveCatalogEntryAsync(identity, registration,
            cancellationToken).ConfigureAwait(false);
        if (entry is null || entry.FullReplayProjectionTypes.Count == 0
            || entry.FullReplayProjectionTypes.Any(type =>
                !entry.MappedOnlyStreamedProjectionTypes.Contains(type, StringComparer.Ordinal))) {
            return new(new NamedProjectionRebuildResult(false, false, [], []), null);
        }

        string[] routes = [.. entry.FullReplayProjectionTypes.Order(StringComparer.Ordinal)];
        NamedProjectionRebuildResult Failure(ProjectionDispatchStatus status, string reason)
            => new(true, false, [.. routes.Select(route => new ProjectionDispatchOutcome(
                route, status, null, reason))], routes);
        try {
            using var measure = new ProjectionRebuildHashCountStream();
            string? unreadable = await writeSource(measure, cancellationToken).ConfigureAwait(false);
            if (unreadable is not null)
                return new(Failure(ProjectionDispatchStatus.Failed, unreadable), null);
            long countLong = (measure.Length + ProjectionRebuildTransportReference.PartBytes - 1)
                / ProjectionRebuildTransportReference.PartBytes;
            if (countLong < 1 || countLong > ProjectionRebuildTransportReference.MaximumParts)
                return new(Failure(ProjectionDispatchStatus.Failed,
                    StreamReplayReasonCodes.RebuildPrefixSafetyLimitExceeded), null);
            int count = checked((int)countLong);
            string digest = measure.Digest;
            ProjectionDispatchRequest header = new(
                new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, []) {
                    RebuildSourceHeadSequence = sourceHead,
                }, routes, operationId, entry.CatalogFingerprint) {
                    RebuildTransport = new ProjectionRebuildTransportReference(count, digest),
                };
            if (catalogLease is not null)
            {
                _ = RouteCatalogProjectionPolicy.Outputs(catalogLease.Snapshot, identity.TenantId, identity.Domain,
                    registration.AppId, "project/v2", routes);
                RouteCatalogProjectionPolicy.Auxiliary(catalogLease.Snapshot, identity.Domain, registration.AppId,
                    "project/rebuild/part/v1", Contracts.Authorization.EventStoreOperationIdentifiers.DomainServiceProject);
            }
            HttpClient uploadClient = httpClientFactory.CreateClient();
            async Task UploadAsync(int ordinal, byte[] data, CancellationToken token) {
                using HttpRequestMessage upload = daprClient.CreateInvokeMethodRequest(
                    registration.AppId, "project/rebuild/part/v1",
                    new ProjectionRebuildTransportPartRequest(header, ordinal, data));
                upload.Content?.Dispose();
                upload.Content = ProjectionWireJsonSerializer.CreateContent(new ProjectionRebuildTransportPartRequest(header, ordinal, data), daprClient.JsonSerializerOptions);
                using HttpResponseMessage response = await RouteCatalogRemoteCall.InvokeAsync(() => uploadClient!.SendAsync(upload, token)).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new IOException("Mapped replay part upload was rejected.");
            }
            using var parts = new ProjectionRebuildPartUploadStream(UploadAsync);
            unreadable = await writeSource(parts, cancellationToken).ConfigureAwait(false);
            if (unreadable is not null)
                return new(Failure(ProjectionDispatchStatus.Failed, unreadable), null);
            await parts.CompleteAsync(cancellationToken).ConfigureAwait(false);
            if (parts.PartCount != count || parts.WrittenLength != measure.Length
                || !string.Equals(parts.Digest, digest, StringComparison.Ordinal))
                return new(Failure(ProjectionDispatchStatus.Failed,
                    ProjectionDispatchReasonCodes.MalformedOutcome), null);

            ProjectionDispatchResponse? response = await InvokeAsync(registration.AppId,
                "project/rebuild/stage/v1", header, options.Value, cancellationToken).ConfigureAwait(false);
            return new(MappedResult(response, routes), header);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) {
            return new(Failure(ProjectionDispatchStatus.Indeterminate,
                ProjectionDispatchReasonCodes.HandlerFailure), null);
        }
    }

    public async Task<NamedProjectionRebuildResult> InvokeMappedOnlyStreamedRebuildAsync(
        AggregateIdentity identity, DomainServiceRegistration registration,
        ProjectionDispatchRequest header, ProjectionMappedReplayAction action,
        CancellationToken cancellationToken) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(header.Request.TenantId, identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(header.Request.Domain, identity.Domain, StringComparison.Ordinal)
            || !string.Equals(header.Request.AggregateId, identity.AggregateId, StringComparison.Ordinal)
            || header.RebuildTransport is null)
            throw new ArgumentException("Mapped replay header does not match the owner.", nameof(header));
        if (catalogLease is not null && !catalogLease.Snapshot.Envelope.Routes.Any(route => route.AppId == registration.AppId
            && route.Method == "project/v2" && route.ContractVersion == (registration.Version ?? "v1")
            && (route.ResolverTenantPattern is null or "*" || route.ResolverTenantPattern == identity.TenantId)
            && catalogLease.Snapshot.Envelope.Declarations.Any(declaration => declaration.RouteId == route.RouteId
                && declaration.Kind == "projection" && declaration.Domain == identity.Domain)))
            throw new RouteCatalogUnavailableException("The mapped replay target has no committed executable version and tenant scope.");
        string method = action switch {
            ProjectionMappedReplayAction.Commit => "project/rebuild/commit/v1",
            ProjectionMappedReplayAction.Verify => "project/rebuild/verify/v1",
            _ => "project/rebuild/abort/v1",
        };
        ProjectionDispatchResponse? response = await InvokeAsync(registration.AppId, method,
            header, options.Value, cancellationToken).ConfigureAwait(false);
        return MappedResult(response, [.. header.ProjectionTypes]);
    }

    private NamedProjectionRebuildResult MappedResult(
        ProjectionDispatchResponse? response, string[] routes) {
        if (response is null)
            return new(true, false, [.. routes.Select(route => new ProjectionDispatchOutcome(
                route, ProjectionDispatchStatus.Indeterminate, null,
                ProjectionDispatchReasonCodes.HandlerFailure))], routes);
        Dictionary<string, ProjectionDispatchOutcome> valid = ValidateOutcomes(
            response, routes, options.Value);
        ProjectionDispatchOutcome[] outcomes = [.. routes.Select(route =>
            valid.TryGetValue(route, out ProjectionDispatchOutcome? outcome)
                ? outcome
                : new ProjectionDispatchOutcome(route, ProjectionDispatchStatus.Indeterminate,
                    null, ProjectionDispatchReasonCodes.MalformedOutcome))];
        return new(true, outcomes.All(static outcome => outcome.Status is
            ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted),
            outcomes, routes);
    }

    /// <inheritdoc/>
    public async Task<NamedProjectionRebuildResult> AcquireRebuildLifecyclesAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        string operationId,
        CancellationToken cancellationToken) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        NamedProjectionRouteCatalogEntry? catalogEntry = await ResolveCatalogEntryAsync(
                identity,
                registration,
                cancellationToken)
            .ConfigureAwait(false);
        if (catalogEntry is null) {
            return new NamedProjectionRebuildResult(Owned: false, Succeeded: true, [], []);
        }

        string[] requiredRoutes = [.. catalogEntry.ProjectionTypes.Order(StringComparer.Ordinal)];
        var acquired = new List<string>(requiredRoutes.Length);
        try {
            foreach (string projectionType in requiredRoutes) {
                if (!await RouteCatalogRemoteCall.InvokeAsync(() => lifecycleGateway!.BeginRebuildAsync(identity, projectionType, operationId, cancellationToken)).ConfigureAwait(false)) {
                    ProjectionDispatchOutcome[] denied = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                        route,
                        ProjectionDispatchStatus.Retryable,
                        null,
                        ProjectionDispatchReasonCodes.DeliveryInProgress))];
                    return new NamedProjectionRebuildResult(Owned: true, Succeeded: false, denied, acquired);
                }

                acquired.Add(projectionType);
            }

            ProjectionDispatchOutcome[] outcomes = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                route,
                ProjectionDispatchStatus.Completed,
                null,
                null))];
            return new NamedProjectionRebuildResult(Owned: true, Succeeded: true, outcomes, acquired);
        }
        catch {
            await ReleaseRebuildLifecyclesAsync(identity, operationId, acquired).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<NamedProjectionRebuildResult> TryRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        long sourceHeadSequence,
        string operationId,
        CancellationToken cancellationToken) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(projectionEvents);
        if (sourceHeadSequence < projectionEvents.LongLength) {
            throw new ArgumentOutOfRangeException(nameof(sourceHeadSequence));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        ProjectionDispatchOptions dispatchOptions = options.Value;
        dispatchOptions.Validate();
        string serviceVersion = string.IsNullOrWhiteSpace(registration.Version) ? "v1" : registration.Version;
        NamedProjectionRouteCatalogEntry? catalogEntry = await ResolveCatalogEntryAsync(identity, registration, cancellationToken).ConfigureAwait(false);
        if (catalogEntry is null) { return new NamedProjectionRebuildResult(Owned: false, Succeeded: true, [], []); }

        string[] requiredRoutes = [.. catalogEntry.FullReplayProjectionTypes.Order(StringComparer.Ordinal)];
        if (requiredRoutes.Length == 0) {
            return new NamedProjectionRebuildResult(Owned: false, Succeeded: true, [], []);
        }
        bool sourceErased = false;
        if (projectionEvents.Length == 0) {
            // An empty caller prefix cannot distinguish a new/touched stream from one whose
            // actor-owned erasure completed. Re-read that durable marker at the source actor.
            IAggregateActor source = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), AggregateActorTypeName);
            AggregateStreamMetadata metadata = await RouteCatalogRemoteCall.InvokeAsync(() => source!.GetStreamMetadataAsync()).ConfigureAwait(false);
            if (sourceHeadSequence != 0 || metadata.Exists || metadata.CurrentSequence != 0 || !metadata.Erased) {
                ProjectionDispatchOutcome[] denied = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                    route, ProjectionDispatchStatus.Failed, null, ProjectionDispatchReasonCodes.MalformedOutcome))];
                return new NamedProjectionRebuildResult(Owned: true, Succeeded: false, denied, []);
            }

            sourceErased = true;
        }
        var lifecycleProjectionTypes = new List<string>(requiredRoutes.Length);
        try {
            foreach (string projectionType in requiredRoutes) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await RouteCatalogRemoteCall.InvokeAsync(() => lifecycleGateway!.BeginRebuildAsync(identity, projectionType, operationId, cancellationToken)).ConfigureAwait(false)) {
                    ProjectionDispatchOutcome[] denied = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                    route,
                    ProjectionDispatchStatus.Retryable,
                    null,
                    ProjectionDispatchReasonCodes.DeliveryInProgress))];
                    return new NamedProjectionRebuildResult(
                        Owned: true,
                        Succeeded: false,
                        denied,
                        lifecycleProjectionTypes);
                }

                lifecycleProjectionTypes.Add(projectionType);
            }

            var request = new ProjectionDispatchRequest(
                new ProjectionRequest(
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    projectionEvents) {
                    RebuildSourceHeadSequence = sourceHeadSequence,
                    RebuildSourceErased = sourceErased,
                },
                requiredRoutes,
                operationId,
                catalogEntry.CatalogFingerprint);
            ProjectionDispatchResponse? response = await InvokeAsync(
                    registration.AppId,
                    "project/rebuild/stage/v1",
                    request,
                    dispatchOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            if (response is null) {
                ProjectionDispatchOutcome[] indeterminate = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                route,
                ProjectionDispatchStatus.Indeterminate,
                null,
                ProjectionDispatchReasonCodes.HandlerFailure))];
                return new NamedProjectionRebuildResult(
                    Owned: true,
                    Succeeded: false,
                    indeterminate,
                    lifecycleProjectionTypes);
            }

            Dictionary<string, ProjectionDispatchOutcome> validOutcomes = ValidateOutcomes(
                response,
                requiredRoutes,
                dispatchOptions);
            ProjectionDispatchOutcome[] orderedOutcomes = [.. requiredRoutes.Select(route =>
            validOutcomes.TryGetValue(route, out ProjectionDispatchOutcome? outcome)
                ? outcome
                : new ProjectionDispatchOutcome(
                    route,
                    ProjectionDispatchStatus.Indeterminate,
                    null,
                    ProjectionDispatchReasonCodes.MalformedOutcome))];
            bool succeeded = orderedOutcomes.Length == requiredRoutes.Length
                && orderedOutcomes.All(static outcome =>
                    outcome.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted);
            return new NamedProjectionRebuildResult(
                Owned: true,
                succeeded,
                orderedOutcomes,
                lifecycleProjectionTypes);
        }
        catch {
            foreach (string projectionType in lifecycleProjectionTypes) {
                try {
                    bool completed = await RouteCatalogRemoteCall.InvokeAsync(() => lifecycleGateway!.CompleteRebuildAsync(identity, projectionType, operationId, CancellationToken.None)).ConfigureAwait(false);
                    if (!completed) {
                        Log.LifecycleReleaseFailed(
                            logger,
                            identity.TenantId,
                            identity.Domain,
                            identity.AggregateId,
                            projectionType,
                            operationId,
                            "completion-rejected");
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException) {
                    Log.LifecycleReleaseFailed(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        projectionType,
                        operationId,
                        exception.GetType().Name);
                }
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public Task<bool> TryDispatchHeadAsync(AggregateIdentity identity, DomainServiceRegistration registration,
        long headSequence, string headMessageId, ProjectionEventDto[] events, CancellationToken cancellationToken)
        => TryDispatchCoreAsync(identity, registration, headSequence, headMessageId, events, false, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TryDispatchHeadAsync(AggregateIdentity identity, DomainServiceRegistration registration,
        long headSequence, string headMessageId, ProjectionEventDto[] events, NativeSourceProof? nativeSourceProof,
        CancellationToken cancellationToken)
        => TryDispatchCoreAsync(identity, registration, headSequence, headMessageId, events, false, cancellationToken,
            nativeSourceProof: nativeSourceProof);

    /// <inheritdoc/>
    public async Task<bool> TryDispatchAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        EventEnvelope[] events,
        ProjectionEventDto[] projectionEvents,
        CancellationToken cancellationToken) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(projectionEvents);
        if (events.Length == 0) {
            throw new ArgumentException("Named projection delivery requires a non-empty persisted event history.", nameof(events));
        }

        if (catalogLease is not null) _ = await ResolveCatalogEntryAsync(identity, registration, cancellationToken).ConfigureAwait(false);
        EventEnvelope head = events.OrderBy(static item => item.SequenceNumber).Last();
        NativeSourceProof? proof = await CaptureNativePrefixAsync(identity, events, cancellationToken).ConfigureAwait(false);
        return await TryDispatchCoreAsync(identity, registration, head.SequenceNumber, head.MessageId,
            projectionEvents, mappedOnly: false, cancellationToken, nativeSourceProof: proof).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<bool> TryDispatchMappedOnlyAsync(
        AggregateIdentity identity, DomainServiceRegistration registration,
        long sourceHeadSequence, string headMessageId, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeadSequence);
        ArgumentException.ThrowIfNullOrWhiteSpace(headMessageId);
        return TryDispatchCoreAsync(identity, registration, sourceHeadSequence, headMessageId,
            [], mappedOnly: true, cancellationToken);
    }

    private async Task<bool> TryDispatchCoreAsync(
        AggregateIdentity identity, DomainServiceRegistration registration,
        long highestSequence, string headMessageId, ProjectionEventDto[] projectionEvents,
        bool mappedOnly, CancellationToken cancellationToken, IReadOnlyList<string>? compatibleRoutes = null,
        string? expectedCatalogFingerprint = null, NativeSourceProof? nativeSourceProof = null) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);

        ProjectionDispatchOptions dispatchOptions = options.Value;
        dispatchOptions.Validate();
        string serviceVersion = string.IsNullOrWhiteSpace(registration.Version) ? "v1" : registration.Version;
        NamedProjectionRouteCatalogEntry? catalogEntry = await ResolveCatalogEntryAsync(identity, registration, cancellationToken).ConfigureAwait(false);
        if (catalogEntry is null) { return false; }

        if (catalogEntry.OrdinaryProjectionTypes.Count == 0) return false;
        if (catalogLease is not null) ValidateCommittedCapability(catalogLease.Snapshot, identity, catalogEntry);
        if (expectedCatalogFingerprint is not null && (catalogEntry.CatalogFingerprint != expectedCatalogFingerprint
            || compatibleRoutes is null || compatibleRoutes.Count == 0
            || compatibleRoutes.Distinct(StringComparer.Ordinal).Count() != compatibleRoutes.Count
            || compatibleRoutes.Any(route => !catalogEntry.OrdinaryProjectionTypes.Contains(route, StringComparer.Ordinal)))) return false;
        string[] deliveryRoutes = [.. compatibleRoutes ?? catalogEntry.OrdinaryProjectionTypes];

        if (mappedOnly && !await SupportsMappedOnlyStreamedDeliveryAsync(identity, registration,
            cancellationToken).ConfigureAwait(false)) return false;
        if (string.IsNullOrWhiteSpace(headMessageId)) {
            // The stable dispatch identity derives from the head event's message id. Without it we
            // cannot form a valid work item; this domain is v2-owned, so defer rather than fall
            // through to v1. A later delivery trigger with a valid head reconciles this aggregate.
            Log.DispatchDeferred(
                logger,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                ProjectionDispatchReasonCodes.MalformedOutcome);
            return true;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        var proposedWork = new ProjectionDeliveryRetryWorkItem(
            ProjectionDeliveryRetryWorkItem.CreateWorkId(
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                highestSequence),
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            registration.AppId,
            serviceVersion,
            highestSequence,
            headMessageId,
            [.. catalogEntry.OrdinaryProjectionTypes.Order(StringComparer.Ordinal)],
            [],
            headMessageId,
            catalogEntry.CatalogFingerprint,
            0,
            now + dispatchOptions.RetryWorkerInterval,
            null) { NativeSourceProof = nativeSourceProof, MappedOnlyStreamed = mappedOnly,
                ReplayRecoveryBindings = ProjectionReplayRecoveryContext.Freeze(catalogEntry) };
        ProjectionDeliveryRetryWorkItem scheduledWork;
        if (retryScheduler is null) {
            scheduledWork = proposedWork;
        }
        else {
            try {
                scheduledWork = await RouteCatalogRemoteCall.InvokeAsync(() => retryScheduler!.ScheduleAsync(proposedWork, cancellationToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception exception) {
                Log.DispatchDeferred(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    ProjectionDispatchReasonCodes.PartialRetry);
                throw new InvalidOperationException(
                    "Named projection retry activation could not be persisted.",
                    exception);
            }
        }

        if (retryScheduler is not null) {
            ProjectionDeliveryRetryWorkItem? claimedWork = await RouteCatalogRemoteCall.InvokeAsync(() => retryScheduler!.TryAcquireAsync(
                    scheduledWork,
                    Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                    now,
                    dispatchOptions.RetryLeaseDuration,
                    cancellationToken)).ConfigureAwait(false);
            if (claimedWork is null) {
                return true;
            }

            scheduledWork = claimedWork;
        }

        if (!string.Equals(scheduledWork.HeadMessageId, headMessageId, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.DispatchId, headMessageId, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.CatalogFingerprint, catalogEntry.CatalogFingerprint, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.AppId, registration.AppId, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.ServiceVersion, serviceVersion, StringComparison.Ordinal)
            || scheduledWork.MappedOnlyStreamed != mappedOnly
            || !ProjectionReplayRecoveryContext.Matches(scheduledWork.ReplayRecoveryBindings, proposedWork.ReplayRecoveryBindings)) {
            await DeferAsync(
                    scheduledWork,
                    ProjectionDispatchReasonCodes.PartialRetry,
                    dispatchOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        Dictionary<string, ProjectionDeliveryFingerprintHistory>? mappedHistories = null;
        if (mappedOnly) {
            mappedHistories = new Dictionary<string, ProjectionDeliveryFingerprintHistory>(StringComparer.Ordinal);
            foreach (string projectionType in deliveryRoutes) {
                ProjectionDeliveryAcceptedPrefix? accepted = await RouteCatalogRemoteCall.InvokeAsync(() => idempotencyCoordinator!
                    .ReadAcceptedPrefixAsync(identity, projectionType, cancellationToken)).ConfigureAwait(false);
                ProjectionDeliveryFingerprintHistory history;
                if (accepted is not null && accepted.SequenceNumber + 1 == highestSequence) {
                    history = await ProjectionDeliveryFingerprint.ComputeSuffixAsync(identity,
                        projectionType, accepted.SequenceNumber, accepted.Fingerprint,
                        historyReader!.ReadPagesFromAsync(identity, accepted.SequenceNumber,
                            highestSequence, cancellationToken), 10_000, cancellationToken)
                        .ConfigureAwait(false);
                }
                else {
                    bool legacyIdentity = accepted?.Fingerprint.StartsWith("v1:", StringComparison.Ordinal)
                        ?? await RouteCatalogRemoteCall.InvokeAsync(() => idempotencyCoordinator!.UseLegacyMappedIdentityAsync(identity,
                            projectionType, cancellationToken)).ConfigureAwait(false);
                    history = await ProjectionDeliveryFingerprint.ComputeHistoryAsync(identity,
                        projectionType, historyReader!.ReadPagesAsync(identity, highestSequence,
                            cancellationToken), 10_000, cancellationToken,
                        positionSensitive: !legacyIdentity).ConfigureAwait(false);
                }
                ProjectionDeliveryEventDigest actualHead = history.Events[^1];
                if (actualHead.SequenceNumber != highestSequence
                    || !string.Equals(actualHead.MessageId, headMessageId, StringComparison.Ordinal))
                    throw new ProjectionDeliveryHistoryValidationException(
                        "Mapped normal delivery source head changed during fingerprinting.");
                mappedHistories.Add(projectionType, history);
            }
        }

        HashSet<string> pendingRoutes = new(scheduledWork.PendingRoutes, StringComparer.Ordinal);
        List<string> admittedRoutes = [];
        var settledRoutes = new HashSet<string>(StringComparer.Ordinal);
        var completedRoutes = new HashSet<string>(StringComparer.Ordinal);
        var terminalRoutes = new HashSet<string>(scheduledWork.TerminalRoutes, StringComparer.Ordinal);
        bool identityConflict = string.Equals(
            scheduledWork.LastReasonCode,
            ProjectionDispatchReasonCodes.DeliveryIdentityConflict,
            StringComparison.Ordinal);
        var reservations = new Dictionary<string, ProjectionDeliveryReservation>(StringComparer.Ordinal);
        var lifecycleLeases = new HashSet<string>(StringComparer.Ordinal);

        // Each dispatch invocation takes a unique lifecycle delivery-lease id rather than the shared,
        // stable dispatch id. Two concurrent deliveries of the same aggregate share a dispatch id, so
        // both would re-enter one Delivering lease; the loser, once denied by idempotency admission, would
        // then release the winner's shared lease and fail the winner's own completion. A per-invocation
        // token makes the loser fail closed at BeginDeliveryWrite so it never acquires or releases the
        // winner's lease. A crashed holder's lease is recovered through its bounded expiry, matching the
        // delivery reservation's own lease bound.
        string deliveryLeaseToken = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        try {
            foreach (string projectionType in deliveryRoutes
                .Where(pendingRoutes.Contains)
                .Order(StringComparer.Ordinal)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (idempotencyCoordinator is null) {
                    long deliveredSequence = await RouteCatalogRemoteCall.InvokeAsync(() => checkpointStore!.ReadDeliveredSequenceAsync(identity, projectionType, cancellationToken)).ConfigureAwait(false);
                    if (deliveredSequence > highestSequence) {
                        Log.RouteNotAdmitted(
                            logger,
                            identity.TenantId,
                            identity.Domain,
                            identity.AggregateId,
                            projectionType,
                            ProjectionReasonCodes.CheckpointDrift);

                        // A checkpoint already ahead of this head will never need this delivery. Treat the
                        // route as terminally settled for this work item (mirroring the v1 terminal no-op)
                        // so the item can converge and be deleted rather than deferring this route forever.
                        _ = settledRoutes.Add(projectionType);
                        continue;
                    }
                }

                if (!await RouteCatalogRemoteCall.InvokeAsync(() => lifecycleGateway!.BeginDeliveryWriteAsync(
                        identity,
                        projectionType,
                        deliveryLeaseToken,
                        cancellationToken)).ConfigureAwait(false)) {
                    Log.RouteNotAdmitted(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        projectionType,
                        ProjectionReasonCodes.DeliveryDeferredForErase);

                    // Lifecycle denial (concurrent delivery holding the lease, or erase in progress) may
                    // lift later; keep the route pending.
                    continue;
                }

                _ = lifecycleLeases.Add(projectionType);

                if (idempotencyCoordinator is not null) {
                    long? resumeToken = scheduledWork.ReservationFencingTokens.TryGetValue(projectionType, out long token)
                        ? token : null;
                    ProjectionDeliveryAdmissionResult admission = mappedOnly
                        ? await RouteCatalogRemoteCall.InvokeAsync(() => idempotencyCoordinator!.TryAdmitHistoryAsync(
                            identity, projectionType, mappedHistories![projectionType],
                            reclaimSafe: true, cancellationToken, resumeToken)).ConfigureAwait(false)
                        : await RouteCatalogRemoteCall.InvokeAsync(() => idempotencyCoordinator!.TryAdmitAsync(
                            identity, projectionType, projectionEvents,
                            reclaimSafe: true, cancellationToken, resumeToken)).ConfigureAwait(false);
                    ProjectionDeliveryDiagnostics.RecordAdmission(projectionType, admission);
                    if (admission.Disposition == ProjectionDeliveryAdmissionDisposition.Dispatch) {
                        admittedRoutes.Add(projectionType);
                        reservations.Add(projectionType, admission.Reservation!);
                        continue;
                    }

                    if (!await RouteCatalogRemoteCall.InvokeAsync(() => lifecycleGateway!.CompleteDeliveryWriteAsync(
                            identity,
                            projectionType,
                            deliveryLeaseToken,
                            CancellationToken.None)).ConfigureAwait(false)) {
                        throw new InvalidOperationException("Named projection delivery lifecycle lease completion was rejected.");
                    }

                    _ = lifecycleLeases.Remove(projectionType);

                    Log.RouteNotAdmitted(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        projectionType,
                        admission.ReasonCode ?? ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
                    if (admission.Disposition == ProjectionDeliveryAdmissionDisposition.AlreadyCompleted) {
                        _ = completedRoutes.Add(projectionType);
                    }
                    else if (admission.Disposition == ProjectionDeliveryAdmissionDisposition.Failed) {
                        _ = terminalRoutes.Add(projectionType);
                        identityConflict |= string.Equals(
                            admission.ReasonCode,
                            ProjectionDispatchReasonCodes.DeliveryIdentityConflict,
                            StringComparison.Ordinal);
                    }

                    continue;
                }

                admittedRoutes.Add(projectionType);
            }

            if (admittedRoutes.Count == 0) {
                await ReclaimCompletedRecoveryAsync(identity, catalogEntry, scheduledWork, completedRoutes, cancellationToken).ConfigureAwait(false);
                IReadOnlySet<string> reconciledRoutes = mappedOnly
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : await ReconcileTerminalRoutesAsync(
                        registration.AppId,
                        identity,
                        projectionEvents,
                        scheduledWork,
                        terminalRoutes.Where(deliveryRoutes.Contains).ToHashSet(StringComparer.Ordinal),
                        identityConflict,
                        dispatchOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                terminalRoutes.ExceptWith(reconciledRoutes);
                settledRoutes.UnionWith(reconciledRoutes);

                // Nothing to dispatch this round. Prune settled (drift-ahead) routes: if only lifecycle-
                // deferred routes remain this re-schedules with backoff, otherwise it deletes the item.
                await ReconcileRetryLedgerAsync(
                        scheduledWork,
                        completedRoutes,
                        terminalRoutes,
                        settledRoutes,
                        reservations,
                        identityConflict,
                        dispatchOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            ProjectionDispatchResponse? dispatchResponse;
            if (scheduledWork.ReplayRecoveryBindings is { Count: > 0 } && !mappedOnly) {
                dispatchResponse = await InvokeRecoveryBoundRoutesAsync(identity, catalogEntry, scheduledWork,
                    admittedRoutes, projectionEvents, reservations, completedRoutes, cancellationToken).ConfigureAwait(false);
            }
            else if (compatibleRoutes is not null) {
                dispatchResponse = await InvokeCompatibleRoutesAsync(identity, catalogEntry, scheduledWork,
                    admittedRoutes, projectionEvents, reservations, cancellationToken).ConfigureAwait(false);
            }
            else if (mappedOnly) {
                dispatchResponse = await InvokeMappedOrdinaryRoutesAsync(identity, registration,
                    catalogEntry, scheduledWork, admittedRoutes, mappedHistories!,
                    cancellationToken).ConfigureAwait(false);
            }
            else {
                var projectionRequest = new ProjectionRequest(
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    projectionEvents) { NativeSourceProof = scheduledWork.NativeSourceProof };
                var dispatchRequest = new ProjectionDispatchRequest(
                    projectionRequest,
                    admittedRoutes,
                    scheduledWork.DispatchId,
                    scheduledWork.CatalogFingerprint);
                dispatchResponse = await InvokeAsync(registration.AppId, "project/v2",
                    dispatchRequest, dispatchOptions, cancellationToken).ConfigureAwait(false);
            }
            if (dispatchResponse is null) {
                await DeferAsync(
                        scheduledWork,
                        ProjectionDispatchReasonCodes.PartialRetry,
                        dispatchOptions,
                        cancellationToken,
                        reservations)
                    .ConfigureAwait(false);
                return true;
            }

            Dictionary<string, ProjectionDispatchOutcome> validOutcomes = ValidateOutcomes(
                dispatchResponse,
                admittedRoutes,
                dispatchOptions);
            foreach (string projectionType in admittedRoutes) {
                if (idempotencyCoordinator is null) {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                long started = Stopwatch.GetTimestamp();
                if (!validOutcomes.TryGetValue(projectionType, out ProjectionDispatchOutcome? outcome)) {
                    Log.RouteOutcome(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        projectionType,
                        ProjectionDispatchStatus.Indeterminate,
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    continue;
                }

                if (outcome.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted) {
                    try {
                        using CancellationTokenSource? finalization = idempotencyCoordinator is null
                            ? null
                            : new CancellationTokenSource(TimeSpan.FromSeconds(30), _timeProvider);
                        CancellationToken finalizationToken = finalization?.Token ?? cancellationToken;
                        if (outcome.State is not null) {
                            await WriteLegacyActorStateAsync(identity, projectionType, outcome.State.Value, finalizationToken)
                                .ConfigureAwait(false);
                        }

                        if (idempotencyCoordinator is not null) {
                            ProjectionDeliveryCompletion completion = mappedOnly
                                ? await RouteCatalogRemoteCall.InvokeAsync(() => idempotencyCoordinator!.CompleteHistoryAsync(
                                    identity, projectionType, mappedHistories![projectionType],
                                    reservations[projectionType], finalizationToken)).ConfigureAwait(false)
                                : await RouteCatalogRemoteCall.InvokeAsync(() => idempotencyCoordinator!.CompleteAsync(
                                    identity, projectionType, projectionEvents,
                                    reservations[projectionType], finalizationToken)).ConfigureAwait(false);
                            ProjectionDeliveryDiagnostics.RecordCompletion(projectionType, completion);
                            if (completion is ProjectionDeliveryCompletion.Completed or ProjectionDeliveryCompletion.AlreadyCompleted) {
                                _ = completedRoutes.Add(projectionType);
                            }
                        }
                        else if (await RouteCatalogRemoteCall.InvokeAsync(() => checkpointStore!.SaveDeliveredSequenceAsync(identity, projectionType, highestSequence, finalizationToken)).ConfigureAwait(false)) {
                            _ = completedRoutes.Add(projectionType);
                        }
                    }
                    catch (OperationCanceledException) {
                        throw;
                    }
                    catch (Exception) {
                        // Durable handler work may already exist. Leave this projection pending for stable-id retry.
                    }
                }
                else if (outcome.Status == ProjectionDispatchStatus.Failed) {
                    if (idempotencyCoordinator is not null) {
                        using var release = new CancellationTokenSource(TimeSpan.FromSeconds(30), _timeProvider);
                        _ = await RouteCatalogRemoteCall.InvokeAsync(() => idempotencyCoordinator!.TryReleaseAsync(identity, projectionType, reservations[projectionType], release.Token)).ConfigureAwait(false);
                    }

                    _ = terminalRoutes.Add(projectionType);
                    identityConflict |= string.Equals(
                        outcome.ReasonCode,
                        ProjectionDispatchReasonCodes.DeliveryIdentityConflict,
                        StringComparison.Ordinal);
                }

                Log.RouteOutcome(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    projectionType,
                    outcome.Status,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }

            // The durable work item is the crash-recovery carrier for the stable dispatch id. Keep it
            // until every lifecycle lease has been released; deleting it first can strand this
            // invocation's delivery lease until its bounded expiry if the process stops between ledger
            // reconciliation and the finally block. A later retry then reclaims the expired lease.
            await CompleteDeliveryLifecycleLeasesAsync(
                    identity,
                    deliveryLeaseToken,
                    lifecycleLeases)
                .ConfigureAwait(false);

            IReadOnlySet<string> reconciledTerminalRoutes = mappedOnly
                ? new HashSet<string>(StringComparer.Ordinal)
                : await ReconcileTerminalRoutesAsync(
                    registration.AppId,
                    identity,
                    projectionEvents,
                    scheduledWork,
                    terminalRoutes.Where(deliveryRoutes.Contains).ToHashSet(StringComparer.Ordinal),
                    identityConflict,
                    dispatchOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            terminalRoutes.ExceptWith(reconciledTerminalRoutes);
            settledRoutes.UnionWith(reconciledTerminalRoutes);

            await ReclaimCompletedRecoveryAsync(identity, catalogEntry, scheduledWork, completedRoutes, cancellationToken).ConfigureAwait(false);

            await ReconcileRetryLedgerAsync(
                    scheduledWork,
                    completedRoutes,
                terminalRoutes,
                settledRoutes,
                reservations,
                identityConflict,
                dispatchOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        finally {
            if (lifecycleLeases.Count > 0) {
                await CompleteDeliveryLifecycleLeasesAsync(
                        identity,
                        deliveryLeaseToken,
                        lifecycleLeases)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public Task<NamedProjectionRebuildResult> CommitRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        CancellationToken cancellationToken)
        => InvokePreparedRebuildAsync(
            identity,
            registration,
            projectionEvents,
            operationId,
            "project/rebuild/commit/v1",
            cancellationToken);

    /// <inheritdoc/>
    public Task<NamedProjectionRebuildResult> AbortRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        CancellationToken cancellationToken)
        => InvokePreparedRebuildAsync(
            identity,
            registration,
            projectionEvents,
            operationId,
            "project/rebuild/abort/v1",
            cancellationToken);

    /// <inheritdoc/>
    public Task<NamedProjectionRebuildResult> VerifyRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        CancellationToken cancellationToken)
        => InvokePreparedRebuildAsync(
            identity,
            registration,
            projectionEvents,
            operationId,
            "project/rebuild/verify/v1",
            cancellationToken);

    private async Task<ProjectionDispatchResponse?> InvokeMappedOrdinaryRoutesAsync(
        AggregateIdentity identity, DomainServiceRegistration registration,
        NamedProjectionRouteCatalogEntry catalogEntry, ProjectionDeliveryRetryWorkItem workItem,
        IReadOnlyList<string> admittedRoutes,
        IReadOnlyDictionary<string, ProjectionDeliveryFingerprintHistory> admittedHistories,
        CancellationToken cancellationToken) {
        var outcomes = new List<ProjectionDispatchOutcome>(admittedRoutes.Count);
        var replayFallbackRoutes = new HashSet<string>(StringComparer.Ordinal);
        string[] perEventRoutes = [.. admittedRoutes
            .Where(type => catalogEntry.SharedEpochProjectionTypes.Contains(type, StringComparer.Ordinal)
                || catalogEntry.SingleEventProjectionTypes.Contains(type, StringComparer.Ordinal)
                || catalogEntry.IncrementalReplayProjectionTypes.Contains(type, StringComparer.Ordinal)
                    && admittedHistories[type].BaseSequence > 0)];
        if (perEventRoutes.Length > 0) {
            foreach (IGrouping<long, string> group in perEventRoutes.GroupBy(
                route => admittedHistories[route].BaseSequence)) {
                string[] groupRoutes = [.. group];
                var activeRoutes = new HashSet<string>(groupRoutes, StringComparer.Ordinal);
                long expectedSequence = group.Key + 1;
                IAsyncEnumerable<ProjectionEventDto[]> pages = group.Key == 0
                    ? historyReader!.ReadPagesAsync(identity, workItem.HeadSequence, cancellationToken)
                    : historyReader!.ReadPagesFromAsync(identity, group.Key, workItem.HeadSequence,
                        cancellationToken);
                await foreach (ProjectionEventDto[] page in pages.ConfigureAwait(false)) {
                    if (activeRoutes.Count == 0) break;
                    if (page.Length == 0 || page[0].SequenceNumber != expectedSequence)
                        throw new ProjectionDeliveryHistoryValidationException("Mapped shared delivery source has a gap.");
                    expectedSequence += page.Length;
                    var request = new ProjectionDispatchRequest(
                        new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, page),
                        [.. groupRoutes.Where(activeRoutes.Contains)], workItem.DispatchId,
                        catalogEntry.CatalogFingerprint);
                    ProjectionDispatchResponse? response = await InvokeAsync(registration.AppId,
                        "project/v2", request, options.Value, cancellationToken).ConfigureAwait(false);
                    if (response is null) return null;
                    Dictionary<string, ProjectionDispatchOutcome> valid = ValidateOutcomes(response,
                        request.ProjectionTypes, options.Value);
                    if (request.ProjectionTypes.Any(route => !valid.TryGetValue(route, out ProjectionDispatchOutcome? outcome)
                        || outcome.Status is not ProjectionDispatchStatus.Completed
                            and not ProjectionDispatchStatus.AlreadyCompleted)) {
                        string[] failed = [.. request.ProjectionTypes.Where(route => !valid.TryGetValue(route,
                            out ProjectionDispatchOutcome? outcome)
                            || outcome.Status is not ProjectionDispatchStatus.Completed
                                and not ProjectionDispatchStatus.AlreadyCompleted)];
                        if (group.Key > 0 && failed.All(route =>
                            catalogEntry.IncrementalReplayProjectionTypes.Contains(route, StringComparer.Ordinal)
                            && valid.TryGetValue(route, out ProjectionDispatchOutcome? outcome)
                            && outcome.Status == ProjectionDispatchStatus.Retryable
                            && string.Equals(outcome.ReasonCode,
                                ProjectionDispatchReasonCodes.DeliveryRebuildRequired, StringComparison.Ordinal))) {
                            foreach (string route in failed) {
                                _ = replayFallbackRoutes.Add(route);
                                _ = activeRoutes.Remove(route);
                            }
                            continue;
                        }
                        foreach (string route in perEventRoutes)
                            outcomes.Add(valid.TryGetValue(route, out ProjectionDispatchOutcome? outcome)
                                && outcome.Status is not ProjectionDispatchStatus.Completed
                                    and not ProjectionDispatchStatus.AlreadyCompleted
                                ? outcome : new ProjectionDispatchOutcome(route, ProjectionDispatchStatus.Indeterminate,
                                    null, ProjectionDispatchReasonCodes.PartialRetry));
                        foreach (string route in admittedRoutes.Except(perEventRoutes, StringComparer.Ordinal))
                            outcomes.Add(new ProjectionDispatchOutcome(route, ProjectionDispatchStatus.Indeterminate,
                                null, ProjectionDispatchReasonCodes.PartialRetry));
                        return new ProjectionDispatchResponse(ProjectionDispatchProtocol.Version, outcomes);
                    }
                }
                if (activeRoutes.Count > 0 && expectedSequence != workItem.HeadSequence + 1)
                    throw new ProjectionDeliveryHistoryValidationException("Mapped shared delivery source ended early.");
                foreach (string route in groupRoutes.Where(route => !replayFallbackRoutes.Contains(route)))
                    outcomes.Add(new ProjectionDispatchOutcome(route, ProjectionDispatchStatus.Completed, null, null));
            }
        }

        string[] replayRoutes = [.. admittedRoutes
            .Where(type => catalogEntry.FullReplayProjectionTypes.Contains(type, StringComparer.Ordinal)
                && (!perEventRoutes.Contains(type, StringComparer.Ordinal)
                    || replayFallbackRoutes.Contains(type)))];
        if (replayRoutes.Length > 0) {
            ProjectionMappedReplayStageResult stage = await StageMappedOnlyStreamedRebuildAsync(
                identity, registration, workItem.DispatchId, workItem.HeadSequence,
                (stream, token) => WriteMappedOrdinarySourceAsync(identity, workItem.HeadSequence,
                    stream, token), cancellationToken).ConfigureAwait(false);
            NamedProjectionRebuildResult result = stage.Result;
            if (stage.Result.Succeeded && stage.Header is not null) {
                NamedProjectionRebuildResult commit = await InvokeMappedOnlyStreamedRebuildAsync(identity,
                    registration, stage.Header, ProjectionMappedReplayAction.Commit,
                    cancellationToken).ConfigureAwait(false);
                NamedProjectionRebuildResult verify = await InvokeMappedOnlyStreamedRebuildAsync(identity,
                    registration, stage.Header, ProjectionMappedReplayAction.Verify,
                    cancellationToken).ConfigureAwait(false);
                result = verify.Succeeded ? verify : commit.Succeeded ? new NamedProjectionRebuildResult(
                    true, false, verify.Outcomes, verify.LifecycleProjectionTypes) : verify;
                if (!commit.Succeeded && !verify.Succeeded)
                    _ = await InvokeMappedOnlyStreamedRebuildAsync(identity, registration, stage.Header,
                        ProjectionMappedReplayAction.Abort, cancellationToken).ConfigureAwait(false);
            }
            foreach (string route in replayRoutes)
                outcomes.Add(result.Outcomes.FirstOrDefault(outcome =>
                    string.Equals(outcome.ProjectionType, route, StringComparison.Ordinal))
                    ?? new ProjectionDispatchOutcome(route, ProjectionDispatchStatus.Indeterminate,
                        null, ProjectionDispatchReasonCodes.MalformedOutcome));
        }

        foreach (string route in admittedRoutes) {
            ProjectionDeliveryFingerprintHistory admitted = admittedHistories[route];
            ProjectionDeliveryFingerprintHistory checkedHistory = admitted.BaseSequence > 0
                ? await ProjectionDeliveryFingerprint.ComputeSuffixAsync(identity, route,
                    admitted.BaseSequence, admitted.BaseFingerprint!,
                    historyReader!.ReadPagesFromAsync(identity, admitted.BaseSequence,
                        workItem.HeadSequence, cancellationToken), 10_000, cancellationToken)
                    .ConfigureAwait(false)
                : await ProjectionDeliveryFingerprint.ComputeHistoryAsync(identity, route,
                    historyReader!.ReadPagesAsync(identity, workItem.HeadSequence, cancellationToken),
                    10_000, cancellationToken,
                    positionSensitive: admitted.InitialFingerprint.StartsWith("v2:",
                        StringComparison.Ordinal)).ConfigureAwait(false);
            if (!string.Equals(checkedHistory.PrefixFingerprint,
                admittedHistories[route].PrefixFingerprint, StringComparison.Ordinal))
                throw new ProjectionDeliveryHistoryValidationException(
                    "Mapped normal delivery source changed after route dispatch.");
        }

        return new ProjectionDispatchResponse(ProjectionDispatchProtocol.Version, outcomes);
    }

    private async Task<string?> WriteMappedOrdinarySourceAsync(
        AggregateIdentity identity, long sourceHeadSequence, Stream output,
        CancellationToken cancellationToken) {
        await output.WriteAsync(new byte[] { (byte)'[' }, cancellationToken).ConfigureAwait(false);
        bool first = true;
        await foreach (ProjectionEventDto[] page in historyReader!.ReadPagesAsync(
            identity, sourceHeadSequence, cancellationToken).ConfigureAwait(false)) {
            foreach (ProjectionEventDto item in page) {
                if (!first) await output.WriteAsync(new byte[] { (byte)',' }, cancellationToken)
                    .ConfigureAwait(false);
                await ProjectionWireJsonSerializer.SerializeAsync(output, item, daprClient.JsonSerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                first = false;
            }
        }
        await output.WriteAsync(new byte[] { (byte)']' }, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task CompleteDeliveryLifecycleLeasesAsync(
        AggregateIdentity identity,
        string dispatchId,
        ISet<string> lifecycleLeases) {
        Exception? lifecycleCompletionFailure = null;
        foreach (string projectionType in lifecycleLeases.ToArray()) {
            try {
                if (!await RouteCatalogRemoteCall.InvokeAsync(() => lifecycleGateway!.CompleteDeliveryWriteAsync(
                        identity,
                        projectionType,
                        dispatchId,
                        CancellationToken.None)).ConfigureAwait(false)) {
                    lifecycleCompletionFailure ??= new InvalidOperationException(
                        "Named projection delivery lifecycle lease completion was rejected.");
                    continue;
                }

                _ = lifecycleLeases.Remove(projectionType);
            }
            catch (Exception exception) {
                lifecycleCompletionFailure ??= exception;
            }
        }

        if (lifecycleCompletionFailure is not null) {
            throw new InvalidOperationException(
                "One or more named projection delivery lifecycle leases could not be completed.",
                lifecycleCompletionFailure);
        }
    }

    private async Task<IReadOnlySet<string>> ReconcileTerminalRoutesAsync(
        string appId,
        AggregateIdentity identity,
        ProjectionEventDto[] projectionEvents,
        ProjectionDeliveryRetryWorkItem scheduledWork,
        IReadOnlySet<string> terminalRoutes,
        bool identityConflict,
        ProjectionDispatchOptions dispatchOptions,
        CancellationToken cancellationToken) {
        // An identity conflict is durable quarantine evidence. A handler's reconciliation reply
        // cannot authorize removal of bytes that disagree with the persisted delivery identity.
        if (terminalRoutes.Count == 0 || identityConflict) {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        string[] orderedRoutes = [.. terminalRoutes.Order(StringComparer.Ordinal)];
        var request = new ProjectionDispatchRequest(
            new ProjectionRequest(
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                projectionEvents) { NativeSourceProof = scheduledWork.NativeSourceProof },
            orderedRoutes,
            scheduledWork.DispatchId,
            scheduledWork.CatalogFingerprint);
        ProjectionDispatchResponse? response = await InvokeAsync(
                appId,
                "project/v2/reconcile",
                request,
                dispatchOptions,
                cancellationToken)
            .ConfigureAwait(false);
        if (response is null) {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        Dictionary<string, ProjectionDispatchOutcome> outcomes = ValidateOutcomes(
            response,
            orderedRoutes,
            dispatchOptions);
        return outcomes
            .Where(static pair => pair.Value.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted)
            .Select(static pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Reconciles the durable retry work item after one dispatch round: prunes completed and
    /// drift-settled routes, retains lifecycle-deferred and terminal routes, and deletes the item
    /// only when every route has converged. Identity conflicts require a persisted terminal row;
    /// other ledger faults leave the prior work item for the next retry tick.
    /// </summary>
    private async Task ReconcileRetryLedgerAsync(
        ProjectionDeliveryRetryWorkItem scheduledWork,
        IReadOnlySet<string> completedRoutes,
        IReadOnlySet<string> terminalRoutes,
        IReadOnlySet<string> settledRoutes,
        IReadOnlyDictionary<string, ProjectionDeliveryReservation> reservations,
        bool identityConflict,
        ProjectionDispatchOptions dispatchOptions,
        CancellationToken cancellationToken) {
        if (retryScheduler is null) {
            if (identityConflict) {
                throw new InvalidOperationException("Projection delivery identity conflict has no durable quarantine scheduler.");
            }

            return;
        }

        try {
            if (identityConflict && terminalRoutes.Count == 0) {
                throw new InvalidOperationException("Projection delivery identity conflict has no terminal route to quarantine.");
            }

            string[] remainingRoutes = [.. scheduledWork.PendingRoutes
                .Where(route => !completedRoutes.Contains(route)
                    && !terminalRoutes.Contains(route)
                    && !settledRoutes.Contains(route))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
            if (remainingRoutes.Length == 0 && terminalRoutes.Count == 0) {
                _ = await RouteCatalogRemoteCall.InvokeAsync(() => retryScheduler!.TryDeleteAsync(scheduledWork, cancellationToken)).ConfigureAwait(false);
                return;
            }

            int attempt = Math.Min(scheduledWork.Attempt + 1, dispatchOptions.MaxRetryAttempts);
            var updated = scheduledWork with {
                PendingRoutes = remainingRoutes,
                TerminalRoutes = [.. terminalRoutes.Order(StringComparer.Ordinal)],
                ReservationFencingTokens = scheduledWork.ReservationFencingTokens.Concat(reservations.Select(static pair =>
                        new KeyValuePair<string, long>(pair.Key, pair.Value.FencingToken)))
                    .Where(pair => remainingRoutes.Contains(pair.Key, StringComparer.Ordinal))
                    .GroupBy(static pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(static group => group.Key, static group => group.Last().Value, StringComparer.Ordinal),
                Attempt = attempt,
                NextDueUtc = _timeProvider.GetUtcNow() + GetRetryDelay(attempt, dispatchOptions),
                LastReasonCode = identityConflict
                    ? ProjectionDispatchReasonCodes.DeliveryIdentityConflict
                    : terminalRoutes.Count > 0
                        ? ProjectionDispatchReasonCodes.HandlerFailure
                        : ProjectionDispatchReasonCodes.PartialRetry,
            };
            bool updatedSuccessfully = await RouteCatalogRemoteCall.InvokeAsync(() => retryScheduler!.TryUpdateAsync(updated, cancellationToken)).ConfigureAwait(false);
            if (identityConflict && !updatedSuccessfully) {
                throw new InvalidOperationException("Projection delivery identity conflict could not be quarantined.");
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception) {
            if (identityConflict) {
                throw;
            }

            Log.DispatchDeferred(
                logger,
                scheduledWork.TenantId,
                scheduledWork.Domain,
                scheduledWork.AggregateId,
                ProjectionDispatchReasonCodes.PartialRetry);
        }
    }

    private async Task DeferAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        string reasonCode,
        ProjectionDispatchOptions dispatchOptions,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, ProjectionDeliveryReservation>? reservations = null) {
        if (retryScheduler is null) {
            return;
        }

        int attempt = Math.Min(workItem.Attempt + 1, dispatchOptions.MaxRetryAttempts);
        try {
            _ = await RouteCatalogRemoteCall.InvokeAsync(() => retryScheduler!.TryUpdateAsync(
                workItem with {
                    ReservationFencingTokens = reservations is null
                        ? workItem.ReservationFencingTokens
                        : workItem.ReservationFencingTokens.Concat(reservations.Select(static pair =>
                            new KeyValuePair<string, long>(pair.Key, pair.Value.FencingToken)))
                            .GroupBy(static pair => pair.Key, StringComparer.Ordinal)
                            .ToDictionary(static group => group.Key, static group => group.Last().Value, StringComparer.Ordinal),
                    Attempt = attempt,
                    NextDueUtc = _timeProvider.GetUtcNow() + GetRetryDelay(attempt, dispatchOptions),
                    LastReasonCode = reasonCode,
                },
                cancellationToken)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception) {
            // A ledger fault while recording backoff must not surface as a dropped delivery; the
            // work item stays at its prior due time and is reconciled on a later trigger/tick.
            Log.DispatchDeferred(
                logger,
                workItem.TenantId,
                workItem.Domain,
                workItem.AggregateId,
                reasonCode);
        }
    }

    private static TimeSpan GetRetryDelay(int attempt, ProjectionDispatchOptions dispatchOptions) {
        double multiplier = Math.Pow(2, Math.Max(0, attempt - 1));
        double ticks = Math.Min(
            dispatchOptions.RetryMaxDelay.Ticks,
            dispatchOptions.RetryBaseDelay.Ticks * multiplier);
        return TimeSpan.FromTicks((long)ticks);
    }

    private async Task<NamedProjectionRebuildResult> InvokePreparedRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        string methodName,
        CancellationToken cancellationToken) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(projectionEvents);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ProjectionDispatchOptions dispatchOptions = options.Value;
        dispatchOptions.Validate();
        NamedProjectionRouteCatalogEntry? catalogEntry = await ResolveCatalogEntryAsync(
                identity,
                registration,
                cancellationToken)
            .ConfigureAwait(false);
        if (catalogEntry is null) {
            return new NamedProjectionRebuildResult(Owned: false, Succeeded: true, [], []);
        }

        string[] requiredRoutes = [.. catalogEntry.FullReplayProjectionTypes.Order(StringComparer.Ordinal)];
        if (requiredRoutes.Length == 0) {
            return new NamedProjectionRebuildResult(Owned: false, Succeeded: true, [], []);
        }

        bool sourceErased = false;
        if (projectionEvents.Length == 0) {
            IAggregateActor source = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId), AggregateActorTypeName);
            AggregateStreamMetadata metadata = await RouteCatalogRemoteCall.InvokeAsync(() => source!.GetStreamMetadataAsync()).ConfigureAwait(false);
            if (metadata.Exists || metadata.CurrentSequence != 0 || !metadata.Erased) {
                ProjectionDispatchOutcome[] denied = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                    route, ProjectionDispatchStatus.Failed, null, ProjectionDispatchReasonCodes.MalformedOutcome))];
                return new NamedProjectionRebuildResult(Owned: true, Succeeded: false, denied, []);
            }

            sourceErased = true;
        }

        var request = new ProjectionDispatchRequest(
            new ProjectionRequest(
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                projectionEvents) {
                RebuildSourceHeadSequence = projectionEvents.LongLength,
                RebuildSourceErased = sourceErased,
            },
            requiredRoutes,
            operationId,
            catalogEntry.CatalogFingerprint);
        ProjectionDispatchResponse? response = await InvokeAsync(
                registration.AppId,
                methodName,
                request,
                dispatchOptions,
                cancellationToken)
            .ConfigureAwait(false);
        if (response is null) {
            ProjectionDispatchOutcome[] indeterminate = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                route,
                ProjectionDispatchStatus.Indeterminate,
                null,
                ProjectionDispatchReasonCodes.HandlerFailure))];
            return new NamedProjectionRebuildResult(Owned: true, Succeeded: false, indeterminate, requiredRoutes);
        }

        Dictionary<string, ProjectionDispatchOutcome> validOutcomes = ValidateOutcomes(
            response,
            requiredRoutes,
            dispatchOptions);
        ProjectionDispatchOutcome[] orderedOutcomes = [.. requiredRoutes.Select(route =>
            validOutcomes.TryGetValue(route, out ProjectionDispatchOutcome? outcome)
                ? outcome
                : new ProjectionDispatchOutcome(
                    route,
                    ProjectionDispatchStatus.Indeterminate,
                    null,
                    ProjectionDispatchReasonCodes.MalformedOutcome))];
        bool succeeded = orderedOutcomes.All(static outcome =>
            outcome.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted);
        return new NamedProjectionRebuildResult(Owned: true, succeeded, orderedOutcomes, requiredRoutes);
    }

    private async Task<NamedProjectionRouteCatalogEntry?> ResolveCatalogEntryAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        CancellationToken cancellationToken) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);
        string serviceVersion = string.IsNullOrWhiteSpace(registration.Version) ? "v1" : registration.Version;
        if (catalogLease is not null)
        {
            var snapshot = catalogLease.Snapshot;
            var declared = snapshot.Envelope.Routes.Where(r => r.AppId == registration.AppId && r.ContractVersion == serviceVersion
                && (r.ResolverTenantPattern is null or "*" || r.ResolverTenantPattern == identity.TenantId)
                && snapshot.Envelope.Declarations.Any(d => d.RouteId == r.RouteId && d.Domain == identity.Domain && d.Kind == "projection")).ToArray();
            if (!declared.Any(r => r.Method == "project/v2"))
            {
                if (declared.Any(r => r.Method == "project")) return null; // The committed legacy method remains authoritative, regardless of metadata.
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The named projection registration is not committed.");
            }
            if (!snapshot.ProjectionCapabilities.Any(c => c.AppId == registration.AppId && c.Domain == identity.Domain && c.ContractVersion == serviceVersion))
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The named projection capability inventory is not committed.");
        }
        if (routeCatalog.Current.TryGet(
            registration.AppId,
            serviceVersion,
            identity.Domain,
            out NamedProjectionRouteCatalogEntry? catalogEntry)
            && catalogEntry is not null) {
            if (catalogLease is not null) ValidateCommittedCapability(catalogLease.Snapshot, identity, catalogEntry);
            return catalogEntry;
        }

        NamedProjectionRouteCatalogEntry? refreshed = catalogRefresher is not null
            && await catalogRefresher.RefreshAsync(registration, cancellationToken).ConfigureAwait(false)
            && routeCatalog.Current.TryGet(
                registration.AppId,
                serviceVersion,
                identity.Domain,
                out catalogEntry)
            ? catalogEntry
            : null;
        if (catalogLease is not null && refreshed is not null) ValidateCommittedCapability(catalogLease.Snapshot, identity, refreshed);
        return refreshed;
    }

    private static void ValidateCommittedCapability(Hexalith.EventStore.ServiceDefaults.Routing.CommittedRouteCatalogSnapshot snapshot,
        AggregateIdentity identity, NamedProjectionRouteCatalogEntry cached)
    {
        _ = Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogProjectionPolicy.Outputs(snapshot,
            identity.TenantId, identity.Domain, cached.AppId, "project/v2", cached.ProjectionTypes);
        var bound = snapshot.ProjectionCapabilities.SingleOrDefault(c => c.AppId == cached.AppId && c.Domain == identity.Domain
            && c.ContractVersion == cached.ServiceVersion);
        if (bound is null || bound.Fingerprint != cached.CatalogFingerprint
            || !bound.ProjectionTypes.Order(StringComparer.Ordinal).SequenceEqual(cached.ProjectionTypes.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The cached named projection capability is absent or stale for this committed generation.");
    }

    private async Task ReleaseRebuildLifecyclesAsync(
        AggregateIdentity identity,
        string operationId,
        IEnumerable<string> projectionTypes) {
        foreach (string projectionType in projectionTypes) {
            try {
                _ = await RouteCatalogRemoteCall.InvokeAsync(() => lifecycleGateway!.CompleteRebuildAsync(identity, projectionType, operationId, CancellationToken.None)).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException) {
                Log.LifecycleReleaseFailed(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    projectionType,
                    operationId,
                    exception.GetType().Name);
            }
        }
    }

    private async Task<ProjectionDispatchResponse?> InvokeAsync(
        string appId,
        string methodName,
        ProjectionDispatchRequest request,
        ProjectionDispatchOptions dispatchOptions,
        CancellationToken cancellationToken, string? replayRecoveryHeader = null) {
        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease, cancellationToken).ConfigureAwait(false);
        if (catalogLease is not null)
        {
            if (methodName == "project/v2") _ = Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogProjectionPolicy.Outputs(catalogLease.Snapshot,
                request.Request.TenantId, request.Request.Domain, appId, methodName, request.ProjectionTypes);
            else
            {
                _ = RouteCatalogProjectionPolicy.Outputs(catalogLease.Snapshot, request.Request.TenantId,
                    request.Request.Domain, appId, "project/v2", request.ProjectionTypes);
                RouteCatalogProjectionPolicy.Auxiliary(catalogLease.Snapshot, request.Request.Domain, appId,
                    methodName, Contracts.Authorization.EventStoreOperationIdentifiers.DomainServiceProject);
            }
            if (!catalogLease.Snapshot.ProjectionCapabilities.Any(c => c.AppId == appId && c.Domain == request.Request.Domain
                && c.Fingerprint == request.CatalogFingerprint && request.ProjectionTypes.All(t => c.ProjectionTypes.Contains(t, StringComparer.Ordinal))))
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("Named projection dispatch carries an uncommitted capability fingerprint.");
        }
        try {
            if (replayRecoveryHeader is null && methodName.StartsWith("project/rebuild/", StringComparison.Ordinal)
                && methodName != "project/rebuild/part/v1")
            {
                using var measure = new ProjectionRebuildHashCountStream();
                await ProjectionWireJsonSerializer.SerializeAsync(measure, request.Request.Events, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                if (measure.Length > ProjectionRebuildTransportReference.PartBytes)
                {
                    long countLong = (measure.Length + ProjectionRebuildTransportReference.PartBytes - 1)
                        / ProjectionRebuildTransportReference.PartBytes;
                    if (countLong > ProjectionRebuildTransportReference.MaximumParts) return null;
                    int count = checked((int)countLong);
                    var reference = new ProjectionRebuildTransportReference(count,
                        measure.Digest);
                    ProjectionDispatchRequest header = request with
                    {
                        Request = request.Request with { Events = [] },
                        RebuildTransport = reference
                    };
                    HttpClient uploadClient = httpClientFactory.CreateClient();
                    if (catalogLease is not null)
                        RouteCatalogProjectionPolicy.Auxiliary(catalogLease.Snapshot, request.Request.Domain, appId,
                            "project/rebuild/part/v1", Contracts.Authorization.EventStoreOperationIdentifiers.DomainServiceProject);
                    async Task UploadPartAsync(int ordinal, byte[] data, CancellationToken token)
                    {
                        using HttpRequestMessage upload = daprClient.CreateInvokeMethodRequest(appId,
                            "project/rebuild/part/v1", new ProjectionRebuildTransportPartRequest(header, ordinal, data));
                        if (ProjectionReplayTransportContext.Header(header) is { } replayContext)
                            upload.Headers.Add(ProjectionReplayTransportContext.HeaderName, replayContext);
                        upload.Content?.Dispose();
                        upload.Content = ProjectionWireJsonSerializer.CreateContent(new ProjectionRebuildTransportPartRequest(header, ordinal, data), daprClient.JsonSerializerOptions);
                        using HttpResponseMessage uploaded = await RouteCatalogRemoteCall.InvokeAsync(() => uploadClient!.SendAsync(upload, token)).ConfigureAwait(false);
                        if (!uploaded.IsSuccessStatusCode)
                            throw new IOException("Mapped replay part upload was rejected.");
                    }
                    using var uploadStream = new ProjectionRebuildPartUploadStream(UploadPartAsync);
                    await ProjectionWireJsonSerializer.SerializeAsync(uploadStream, request.Request.Events, SerializerOptions, cancellationToken)
                        .ConfigureAwait(false);
                    await uploadStream.CompleteAsync(cancellationToken).ConfigureAwait(false);
                    if (uploadStream.PartCount != count) return null;

                    request = header;
                }
            }
            using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(appId, methodName, request);
            if (ProjectionReplayTransportContext.Header(request) is { } replayContextHeader)
                httpRequest.Headers.Add(ProjectionReplayTransportContext.HeaderName, replayContextHeader);
            if (replayRecoveryHeader is not null)
                _ = httpRequest.Headers.TryAddWithoutValidation(ProjectionReplayRecoveryBinding.HeaderName, replayRecoveryHeader);
            httpRequest.Content?.Dispose();
            httpRequest.Content = ProjectionWireJsonSerializer.CreateContent(request, daprClient.JsonSerializerOptions);
            HttpClient client = httpClientFactory.CreateClient();
            using HttpResponseMessage response = await RouteCatalogRemoteCall.InvokeAsync(() => client!.SendAsync(httpRequest, cancellationToken)).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode
                || response.Content.Headers.ContentType?.MediaType is not string mediaType
                || (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                    && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))) {
                return null;
            }

            byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (payload.Length == 0 || payload.Length > dispatchOptions.MaxOutcomeEnvelopeBytes) {
                return null;
            }

            return JsonSerializer.Deserialize<ProjectionDispatchResponse>(payload, SerializerOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception) {
            return null;
        }
    }

    private static Dictionary<string, ProjectionDispatchOutcome> ValidateOutcomes(
        ProjectionDispatchResponse response,
        IReadOnlyList<string> admittedRoutes,
        ProjectionDispatchOptions dispatchOptions) {
        var valid = new Dictionary<string, ProjectionDispatchOutcome>(StringComparer.Ordinal);
        if (response.Version != ProjectionDispatchProtocol.Version
            || response.Outcomes is null
            || response.Outcomes.Count > dispatchOptions.MaxOutcomes) {
            return valid;
        }

        HashSet<string> admitted = new(admittedRoutes, StringComparer.Ordinal);
        foreach (IGrouping<string, ProjectionDispatchOutcome> group in response.Outcomes
            .Where(outcome => outcome is not null && admitted.Contains(outcome.ProjectionType))
            .GroupBy(static outcome => outcome.ProjectionType, StringComparer.Ordinal)) {
            ProjectionDispatchOutcome[] outcomes = [.. group];
            if (outcomes.Length != 1 || !IsValidOutcome(outcomes[0], dispatchOptions)) {
                continue;
            }

            valid[group.Key] = outcomes[0];
        }

        return valid;
    }

    private static bool IsValidOutcome(
        ProjectionDispatchOutcome outcome,
        ProjectionDispatchOptions dispatchOptions)
        => Enum.IsDefined(outcome.Status)
            && (outcome.State is null
                || outcome.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted)
            && (outcome.ReasonCode is null
                || (outcome.ReasonCode.Length <= dispatchOptions.MaxReasonCodeBytes
                    && outcome.ReasonCode.All(static character => character <= 0x7f)));

    private async Task WriteLegacyActorStateAsync(
        AggregateIdentity identity,
        string projectionType,
        JsonElement state,
        CancellationToken cancellationToken) {
        if (state.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            throw new InvalidOperationException("A state-bearing projection outcome must contain JSON state.");
        }

        string projectionActorId = QueryActorIdHelper.DeriveActorId(
            projectionType,
            identity.TenantId,
            identity.AggregateId,
            []);
        IProjectionWriteActor writeProxy = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(
            new ActorId(projectionActorId),
            QueryRouter.ProjectionActorTypeName);
        cancellationToken.ThrowIfCancellationRequested();
        await RouteCatalogRemoteCall.InvokeAsync(() => writeProxy!.UpdateProjectionAsync(ProjectionState.FromJsonElement(projectionType, identity.TenantId, state))).ConfigureAwait(false);

        IETagActor eTagProxy = actorProxyFactory.CreateActorProxy<IETagActor>(
            new ActorId($"{projectionType}:{identity.TenantId}"),
            ETagActor.ETagActorTypeName);
        cancellationToken.ThrowIfCancellationRequested();
        _ = await RouteCatalogRemoteCall.InvokeAsync(() => eTagProxy!.RegenerateAsync()).ConfigureAwait(false);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 4650,
            Level = LogLevel.Warning,
            Message = "Named projection route not admitted: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, ReasonCode={ReasonCode}, Stage=NamedProjectionAdmission")]
        public static partial void RouteNotAdmitted(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            string projectionType,
            string reasonCode);

        [LoggerMessage(
            EventId = 4651,
            Level = LogLevel.Information,
            Message = "Named projection outcome reconciled: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, Status={Status}, DurationMs={DurationMs}, Stage=NamedProjectionOutcome")]
        public static partial void RouteOutcome(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            string projectionType,
            ProjectionDispatchStatus status,
            double durationMs);

        [LoggerMessage(
            EventId = 4652,
            Level = LogLevel.Warning,
            Message = "Named projection dispatch deferred: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ReasonCode={ReasonCode}, Stage=NamedProjectionDispatchDeferred")]
        public static partial void DispatchDeferred(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            string reasonCode);

        [LoggerMessage(
            EventId = 4653,
            Level = LogLevel.Error,
            Message = "Named projection lifecycle release failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, OperationId={OperationId}, ReasonCode={ReasonCode}, Stage=NamedProjectionLifecycleRelease")]
        public static partial void LifecycleReleaseFailed(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            string projectionType,
            string operationId,
            string reasonCode);
    }
}
