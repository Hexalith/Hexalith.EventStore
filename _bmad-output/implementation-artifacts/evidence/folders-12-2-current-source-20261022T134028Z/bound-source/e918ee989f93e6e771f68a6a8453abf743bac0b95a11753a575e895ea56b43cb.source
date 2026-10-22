using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>Recovers an unchanged generic handler's original immutable batch through an explicitly bound owning component.</summary>
internal static class DomainProjectionReplayRecovery
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Advertises opaque bindings after the historical catalog fingerprint has already been computed.</summary>
    internal static AdminOperationalIndexMetadata.Response BindMetadata(IServiceProvider services, AdminOperationalIndexMetadata.Response response,
        IEnumerable<IAsyncDomainProjectionHandler>? configuredHandlers = null)
    {
        DomainProjectionReplayRecoveryRegistration[] registrations = [.. services.GetServices<DomainProjectionReplayRecoveryRegistration>()];
        if (registrations.Length == 0 || response.CatalogFingerprint is null || response.AppId is null || response.ServiceVersion is null) return response;
        if (registrations.Any(static item => string.IsNullOrWhiteSpace(item.StoreName) || string.IsNullOrWhiteSpace(item.Domain)
                || string.IsNullOrWhiteSpace(item.ProjectionType))
            || registrations.GroupBy(static item => (item.Domain, item.ProjectionType)).Any(static group => group.Count() != 1))
            throw new InvalidOperationException("Invalid replay recovery host binding.");
        if (configuredHandlers is not null)
        {
            IAsyncDomainProjectionHandler[] inventory = [.. configuredHandlers];
            if (registrations.Any(item => inventory.Count(handler => handler.Domain == item.Domain && handler.ProjectionType == item.ProjectionType
                && handler is IAsyncDomainProjectionRebuildHandler { RebuildSemantics: DomainProjectionRebuildSemantics.FullReplay }
                && handler is not IAsyncDomainProjectionStreamedRebuildHandler) != 1))
                throw new InvalidOperationException("Invalid replay recovery route binding.");
        }
        registrations = [.. registrations.Where(item => response.Domains.Any(domain => domain.Domain == item.Domain))];
        if (registrations.Length == 0) return response;
        if (registrations.Any(item => !response.Domains.Any(domain => domain.Domain == item.Domain
            && (domain.FullReplayProjectionTypes ?? []).Contains(item.ProjectionType, StringComparer.Ordinal)
            && !(domain.MappedOnlyStreamedProjectionTypes ?? []).Contains(item.ProjectionType, StringComparer.Ordinal)
            && !(domain.SourceGuardCapabilities?.ContainsKey(item.ProjectionType) ?? false))))
            throw new InvalidOperationException("Invalid replay recovery route binding.");
        return response with { Domains = [.. response.Domains.Select(domain => domain with
        {
            ReplayRecoveryBindings = registrations.Where(item => item.Domain == domain.Domain
                    && (domain.FullReplayProjectionTypes ?? []).Contains(item.ProjectionType, StringComparer.Ordinal))
                .ToDictionary(static item => item.ProjectionType, item => Binding(item, response.AppId,
                    response.ServiceVersion, response.CatalogFingerprint), StringComparer.Ordinal),
        })] };
    }

    /// <summary>Executes only a separately attested staged request, preserving the public projection carriers.</summary>
    internal static async Task<ProjectionDispatchResponse> ExecuteAsync(IServiceProvider services, ProjectionDispatchRequest request,
        ProjectionDispatchOptions options, DomainProjectionIdentityOptions identity, DomainProjectionRebuildBatchAction action,
        string header, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null) throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        options.Validate(); identity.Validate();
        DomainProjectionDispatcher.ValidateRequest(request, options);
        try { _ = new Hexalith.EventStore.Contracts.Identity.AggregateIdentity(request.Request.TenantId,
            request.Request.Domain, request.Request.AggregateId); }
        catch (ArgumentException) { throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome); }
        if (action == DomainProjectionRebuildBatchAction.Reclaim)
        {
            bool reclaimed = await ReclaimAsync(services, request, options, identity, header, cancellationToken).ConfigureAwait(false);
            return new(ProjectionDispatchProtocol.Version, [.. request.ProjectionTypes.Select(route =>
                new ProjectionDispatchOutcome(route, reclaimed ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.Indeterminate,
                    null, reclaimed ? null : ProjectionDispatchReasonCodes.DeliveryReconciliationRequired))]);
        }
        try { return await ExecuteCoreAsync(services, request, options, identity, action, header, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ProjectionDispatchValidationException) { throw; }
        catch (Exception)
        {
            // Provider, local timeout and opaque persisted-state failures retain the original work.
            // Expected recovery uncertainty never exposes exception, payload or provider details.
            return new(ProjectionDispatchProtocol.Version, [.. request.ProjectionTypes.Select(route =>
                new ProjectionDispatchOutcome(route, ProjectionDispatchStatus.Indeterminate, null,
                    ProjectionDispatchReasonCodes.DeliveryReconciliationRequired))]);
        }
    }

    private static async Task<ProjectionDispatchResponse> ExecuteCoreAsync(IServiceProvider services, ProjectionDispatchRequest request,
        ProjectionDispatchOptions options, DomainProjectionIdentityOptions identity, DomainProjectionRebuildBatchAction action,
        string header, CancellationToken cancellationToken)
    {
        (IAsyncDomainProjectionHandler[] handlers, string storeName, string binding) = Resolve(services, request, options, identity, header);
        IReadModelStore store = services.GetRequiredService<IReadModelStore>();
        string key = Key(request);
        string digest = RequestDigest(request);
        ReadModelEntry<DomainProjectionReplayRecoveryManifest> read = await store.GetAsync<DomainProjectionReplayRecoveryManifest>(storeName,
            key, cancellationToken).ConfigureAwait(false);
        ReadModelBatch? batch;
        if (read.Value is { } original)
        {
            if (original.Version != 1 || original.BindingFingerprint != binding || original.RequestDigest != digest)
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            if (original.Manifest is not { Length: > 0 })
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
            batch = Decode(original, request, storeName);
        }
        else
        {
            if (action != DomainProjectionRebuildBatchAction.Stage)
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
            DomainProjectionRebuildPreparation prepared = await DomainProjectionDispatcher.PrepareRebuildBatchAsync(services,
                request, options, identity, cancellationToken).ConfigureAwait(false);
            if (prepared.Failure is not null) return prepared.Failure;
            batch = prepared.Batch;
            if (batch is null || batch.Scope.StoreName != storeName || batch.OwnerSelection is not null || batch.OwnerSelections is not null)
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
            batch = new ReadModelBatch(batch.Scope with { ProjectionType = RecoveryProjection(request) }, batch.Operations);
            byte[] manifest = ReadModelBatchFingerprint.BuildCanonicalManifest(batch);
            if (manifest.Length > 1_048_576) throw new ProjectionDispatchValidationException("delivery_proof_limit_exceeded");
            var reference = new DomainProjectionReplayRecoveryManifest(1, binding, digest,
                ReadModelBatchFingerprint.ComputeFromManifest(manifest), manifest, batch.OwnerSelections?.ToArray());
            if (!await store.TrySaveAsync(storeName, key, reference, string.Empty, cancellationToken).ConfigureAwait(false))
            {
                DomainProjectionReplayRecoveryManifest? winner = (await store.GetAsync<DomainProjectionReplayRecoveryManifest>(storeName,
                    key, cancellationToken).ConfigureAwait(false)).Value;
                if (winner is null || winner.Version != 1 || winner.BindingFingerprint != binding || winner.RequestDigest != digest
                    || winner.Manifest is not { Length: > 0 })
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
                batch = Decode(winner, request, storeName);
            }
        }
        IReadModelBatchStagingStore staging = services.GetRequiredService<IReadModelBatchStagingStore>();
        ReadModelBatchStagingResult result = action switch
        {
            DomainProjectionRebuildBatchAction.Stage => await staging.StageAsync(batch, cancellationToken).ConfigureAwait(false),
            DomainProjectionRebuildBatchAction.Commit => await staging.CommitAsync(batch, cancellationToken).ConfigureAwait(false),
            DomainProjectionRebuildBatchAction.Abort => await staging.AbortAsync(batch, cancellationToken).ConfigureAwait(false),
            _ => await staging.VerifyAsync(batch, cancellationToken).ConfigureAwait(false),
        };
        bool completed = action == DomainProjectionRebuildBatchAction.Stage
            ? result.Status is ReadModelBatchStagingStatus.Prepared or ReadModelBatchStagingStatus.Committed
            : action == DomainProjectionRebuildBatchAction.Abort ? result.Status == ReadModelBatchStagingStatus.Aborted
            : result.Status == ReadModelBatchStagingStatus.Committed;
        return new(ProjectionDispatchProtocol.Version, [.. handlers.Select(handler => new ProjectionDispatchOutcome(handler.ProjectionType,
            completed ? ProjectionDispatchStatus.Completed : result.Status == ReadModelBatchStagingStatus.Conflict
                ? ProjectionDispatchStatus.Failed : ProjectionDispatchStatus.Indeterminate, null,
            completed ? null : result.Status == ReadModelBatchStagingStatus.Conflict
                ? ProjectionDispatchReasonCodes.DeliveryIdentityConflict : ProjectionDispatchReasonCodes.DeliveryReconciliationRequired))]);
    }

    private static (IAsyncDomainProjectionHandler[] Handlers, string Store, string Binding) Resolve(IServiceProvider services,
        ProjectionDispatchRequest request, ProjectionDispatchOptions options, DomainProjectionIdentityOptions identity, string header,
        bool cleanup = false)
    {
        options.Validate(); identity.Validate();
        if (Encoding.UTF8.GetByteCount(header) > 4096 || request.RebuildTransport is not null
            || request.Request.RebuildSourceHeadSequence is not > 0 or > 10_000
            || request.Request.RebuildSourceHeadMessageId != request.DispatchId
            || (cleanup ? request.Request.Events.Length != 0 : request.Request.Events.Length == 0
                || request.Request.Events.Length != request.Request.RebuildSourceHeadSequence
                || request.Request.Events[0].SequenceNumber != 1
                || request.Request.Events[^1].SequenceNumber != request.Request.RebuildSourceHeadSequence
                || request.Request.Events[^1].MessageId != request.DispatchId))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        using JsonDocument document = ParseHeader(header);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        JsonProperty[] supplied = [.. document.RootElement.EnumerateObject()];
        IAsyncDomainProjectionHandler[] all = DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed(
            services.GetServices<IAsyncDomainProjectionHandler>(), options);
        IAsyncDomainProjectionHandler[] selected = [.. all.Where(handler => handler.Domain == request.Request.Domain
            && request.ProjectionTypes.Contains(handler.ProjectionType, StringComparer.Ordinal))];
        DomainProjectionReplayRecoveryRegistration[] registrations = [.. services.GetServices<DomainProjectionReplayRecoveryRegistration>()
            .Where(item => item.Domain == request.Request.Domain && request.ProjectionTypes.Contains(item.ProjectionType, StringComparer.Ordinal))];
        if (selected.Length != request.ProjectionTypes.Count || selected.Length == 0
            || request.ProjectionTypes.Distinct(StringComparer.Ordinal).Count() != selected.Length
            || registrations.Length != selected.Length || registrations.GroupBy(static item => item.ProjectionType).Any(static group => group.Count() != 1)
            || selected.Any(static handler => handler is not IAsyncDomainProjectionRebuildHandler
                { RebuildSemantics: DomainProjectionRebuildSemantics.FullReplay } || handler is IAsyncDomainProjectionStreamedRebuildHandler)
            || supplied.Length != selected.Length || supplied.Select(static item => item.Name).Distinct(StringComparer.Ordinal).Count() != supplied.Length
            || !DomainProjectionOwnerProof.MatchesCatalog(services, identity.AppId, identity.ServiceVersion,
                all.Where(handler => handler.Domain == request.Request.Domain).Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType)),
                all.Where(handler => handler.Domain == request.Request.Domain && handler.OrdinaryDeliveryEnabled)
                    .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType)), request.CatalogFingerprint)
            || registrations.Any(item => !supplied.Any(property => property.Name == item.ProjectionType && property.Value.ValueKind == JsonValueKind.String
                && property.Value.GetString() == Binding(item, identity.AppId, identity.ServiceVersion, request.CatalogFingerprint).BindingFingerprint)))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        string[] stores = [.. registrations.Select(static item => item.StoreName).Distinct(StringComparer.Ordinal)];
        if (stores.Length != 1 || string.IsNullOrWhiteSpace(stores[0]))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        string canonical = JsonSerializer.Serialize(supplied.OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToDictionary(static item => item.Name, static item => item.Value.GetString(), StringComparer.Ordinal), Json);
        return (selected, stores[0], Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
    }

    private static JsonDocument ParseHeader(string header)
    {
        try { return JsonDocument.Parse(header, new JsonDocumentOptions { MaxDepth = 8 }); }
        catch (JsonException) { throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability); }
    }

    private static ReadModelBatch Decode(DomainProjectionReplayRecoveryManifest original, ProjectionDispatchRequest request, string store)
    {
        try
        {
            return ReadModelBatchManifestCodec.Decode(original.Manifest,
                new(store, request.Request.TenantId, request.Request.Domain, request.Request.AggregateId, RecoveryProjection(request), request.DispatchId),
                null, original.OwnerSelections, original.Fingerprint);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException
            or KeyNotFoundException or FormatException or OverflowException)
        { throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired); }
    }

    internal static string Key(ProjectionDispatchRequest request)
        => DomainProjectionRebuildTransport.Key(request, -4) + ":" + RouteGroup(request);

    private static string RecoveryProjection(ProjectionDispatchRequest request) => "replay-recovery:" + RouteGroup(request);

    private static string RouteGroup(ProjectionDispatchRequest request) => Convert.ToHexStringLower(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(request.ProjectionTypes.Order(StringComparer.Ordinal), Json)));

    /// <summary>Compacts only an independently committed original batch, retaining a no-reprepare tombstone.</summary>
    internal static async Task<bool> ReclaimAsync(IServiceProvider services, ProjectionDispatchRequest completed,
        ProjectionDispatchOptions options, DomainProjectionIdentityOptions identity, string header, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var resolved = Resolve(services, completed, options, identity, header, cleanup: true);
            IReadModelStore store = services.GetRequiredService<IReadModelStore>();
            string key = Key(completed);
            ReadModelEntry<DomainProjectionReplayRecoveryManifest> entry = await store.GetAsync<DomainProjectionReplayRecoveryManifest>(
                resolved.Store, key, cancellationToken).ConfigureAwait(false);
            if (entry.Value is null) return true;
            if (entry.Value is not { Version: 1 } original || original.BindingFingerprint != resolved.Binding
                || original.Manifest is null || entry.ETag is not { Length: > 0 }
                || original.RequestDigest is not { Length: 64 }
                || !original.RequestDigest.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
                || !ReadModelBatchFingerprint.IsCanonical(original.Fingerprint)) return false;
            if (original.Manifest.Length == 0) return true;
            ReadModelBatch batch = Decode(original, completed, resolved.Store);
            ReadModelBatchStagingResult proof = await services.GetRequiredService<IReadModelBatchStagingStore>()
                .VerifyAsync(batch, cancellationToken).ConfigureAwait(false);
            return proof.Status == ReadModelBatchStagingStatus.Committed
                && await store.TrySaveAsync(resolved.Store, key, original with { Manifest = [], OwnerSelections = null },
                    entry.ETag, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return false; }
    }

    private static ProjectionReplayRecoveryBinding Binding(DomainProjectionReplayRecoveryRegistration registration,
        string app, string version, string catalog)
    {
        using var hash = new DomainProjectionRequestDigestStream();
        JsonSerializer.Serialize(hash, new[] { "hexalith.projection.replay-recovery.v1", app, version, catalog,
            registration.Domain, registration.ProjectionType, registration.StoreName }, Json);
        return new(1, catalog, hash.Digest);
    }

    private static string RequestDigest(ProjectionDispatchRequest request)
    {
        using var hash = new DomainProjectionRequestDigestStream();
        JsonSerializer.Serialize(hash, request, Json);
        if (ProjectionReplayTransportContext.Capture(request.Request) is { } context)
            JsonSerializer.Serialize(hash, context, Json);
        return hash.Digest;
    }
}
