using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>Retains the first guarded replay plan through the existing private rebuild transport state seam.</summary>
internal static class DomainProjectionGuardedReplay
{
    private static ReadOnlySpan<byte> ManifestTag => "hexalith.guarded-replay.manifest.v3\0"u8;

    private static byte[] BindManifest(byte[] manifest, string requestDigest)
    {
        byte[] result = new byte[ManifestTag.Length + 64 + manifest.Length];
        ManifestTag.CopyTo(result);
        System.Text.Encoding.ASCII.GetBytes(requestDigest, result.AsSpan(ManifestTag.Length, 64));
        manifest.CopyTo(result, ManifestTag.Length + 64);
        return result;
    }

    /// <summary>Returns unchanged canonical bytes from either accepted private manifest representation.</summary>
    internal static ReadOnlyMemory<byte> OriginalManifest(byte[] bytes, string? requestDigest)
    {
        if (!bytes.AsSpan().StartsWith(ManifestTag)) return bytes; // accepted original private format
        if (bytes.Length <= ManifestTag.Length + 64 || requestDigest is not null
            && !bytes.AsSpan(ManifestTag.Length, 64).SequenceEqual(System.Text.Encoding.ASCII.GetBytes(requestDigest)))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        return bytes.AsMemory(ManifestTag.Length + 64);
    }
    /// <summary>Resolves exact enrolled routes without invoking their domain handlers.</summary>
    internal static (IAsyncDomainProjectionHandler[] Handlers, string Store)? Resolve(IServiceProvider services,
        ProjectionDispatchRequest request, ProjectionDispatchOptions options, DomainProjectionIdentityOptions identity)
    {
        if (request.Request.RebuildSourceHeadSequence == 0) return null;
        DomainProjectionEvolutionAdmission.RequireLegacy(request.Request);
        DomainProjectionOwnerProofRegistration[] enrolled = [.. services.GetServices<DomainProjectionOwnerProofRegistration>()
            .Where(item => item.Domain == request.Request.Domain && request.ProjectionTypes.Contains(item.ProjectionType, StringComparer.Ordinal))];
        if (enrolled.Length == 0) return null;
        options.Validate();
        identity.Validate();
        _ = new AggregateIdentity(request.Request.TenantId, request.Request.Domain, request.Request.AggregateId);
        IAsyncDomainProjectionHandler[] all = DomainProjectionHandlerRouteValidator.MaterializeAndValidateNamed(
            services.GetServices<IAsyncDomainProjectionHandler>(), options);
        IAsyncDomainProjectionHandler[] selected = [.. all.Where(handler => handler.Domain == request.Request.Domain
            && request.ProjectionTypes.Contains(handler.ProjectionType, StringComparer.Ordinal))];
        if (!DomainProjectionOwnerProof.UsesGuardedCatalog(services, request, identity))
        {
            if (request.Request.RebuildOwnerGenerations is not null || request.Request.RebuildOwnerIdentities is not null
                || request.Request.RebuildOwnerBindingFingerprint is not null
                || request.Request.RebuildSourceHeadMessageId is not null)
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
            return null;
        }
        if (request.Request.RebuildSourceHeadMessageId is not null && !ProjectionReplayTransportContext.HasOwners(request.Request))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        if (request.ProjectionTypes.Count == 0 || request.ProjectionTypes.Distinct(StringComparer.Ordinal).Count() != request.ProjectionTypes.Count
            || selected.Length != request.ProjectionTypes.Count || enrolled.Length != selected.Length
            || enrolled.GroupBy(static item => item.ProjectionType, StringComparer.Ordinal).Any(static group => group.Count() != 1)
            || enrolled.Any(static item => !item.Capability.IsSupported)
            || selected.Any(static handler => handler is not IAsyncDomainProjectionRebuildHandler)
            || services.GetServices<IDomainProjectionHandler>().Any(handler => handler.Domain == request.Request.Domain)
            || ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion,
                DomainProjectionOwnerProof.BindRoutes(services, all.Where(handler => handler.Domain == request.Request.Domain)
                    .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))),
                all.Where(handler => handler.Domain == request.Request.Domain && handler.OrdinaryDeliveryEnabled)
                    .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))) != request.CatalogFingerprint)
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        string[] stores = [.. enrolled.Select(static item => item.Binding.StoreName).Distinct(StringComparer.Ordinal)];
        if (stores.Length != 1 || selected.OfType<IAsyncDomainProjectionRebuildTransportHandler>()
            .Any(handler => handler.RebuildTransportStoreName != stores[0]))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        return (selected, stores[0]);
    }

    /// <summary>Reads only the original plan, including the original optimistic concurrency and values.</summary>
    internal static async Task<ReadModelBatch?> ReadAsync(IServiceProvider services, ProjectionDispatchRequest request,
        string storeName, CancellationToken cancellationToken)
    {
        IReadModelStore store = services.GetRequiredService<IReadModelStore>();
        DomainProjectionReplayManifest? reference = (await store.GetAsync<DomainProjectionReplayManifest>(storeName,
            DomainProjectionRebuildTransport.Key(request, -3), cancellationToken).ConfigureAwait(false)).Value;
        if (reference is null)
        {
            byte[]? orphan = (await store.GetAsync<byte[]>(storeName, DomainProjectionRebuildTransport.Key(request, -2),
                cancellationToken).ConfigureAwait(false)).Value;
            if (orphan is null) return null;
            // The first write is already the entire immutable plan. Recover its owners
            // from those bytes; never invoke preparation against a possibly advanced model.
            ReadModelBatch recovered;
            try
            {
                recovered = ReadModelBatchManifestCodec.DecodeCoordinated(OriginalManifest(orphan, RequestDigest(request)),
                    new(storeName, request.Request.TenantId, request.Request.Domain, request.Request.AggregateId, "rebuild", request.DispatchId));
                if (recovered.OwnerSelections is not { Count: > 0 } owners || owners.Count != request.ProjectionTypes.Count
                    || owners.Any(owner => !request.ProjectionTypes.Contains(owner.ProjectionName, StringComparer.Ordinal)
                        || owner.AppliedSequence != request.Request.RebuildSourceHeadSequence || owner.MessageId != request.DispatchId
                        || owner.BatchId != request.DispatchId || owner.ReceiptProjectionName != "rebuild")
                    || !ProjectionReplayTransportContext.MatchesOwners(request.Request, owners))
                    throw new ArgumentException("The original plan does not match its admitted owners.");
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException
                or KeyNotFoundException or FormatException or OverflowException)
            { throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired); }
            return await SaveAsync(services, request, recovered, storeName, cancellationToken).ConfigureAwait(false);
        }
        if (reference.Owners is not { Length: > 0 }
            || JsonSerializer.SerializeToUtf8Bytes(reference, JsonSerializerOptions.Web).Length > 16_384
            || reference.Version is not (1 or 2)
            || ProjectionReplayTransportContext.HasOwners(request.Request)
                && !ProjectionReplayTransportContext.MatchesOwners(request.Request, reference.Owners)
            || reference.RequestDigest != (reference.Version == 1 ? LegacyRequestDigest(request, reference.Owners) : RequestDigest(request)))
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        byte[]? bytes = (await store.GetAsync<byte[]>(storeName, DomainProjectionRebuildTransport.Key(request, -2),
            cancellationToken).ConfigureAwait(false)).Value;
        if (bytes is null) throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
        ReadModelBatch batch = ReadModelBatchManifestCodec.Decode(OriginalManifest(bytes, RequestDigest(request)),
            new(storeName, request.Request.TenantId, request.Request.Domain, request.Request.AggregateId, "rebuild", request.DispatchId),
            null, reference.Owners, reference.Fingerprint);
        return batch;
    }

    /// <summary>Publishes immutable preparation references before staging any model operation.</summary>
    internal static async Task<ReadModelBatch> SaveAsync(IServiceProvider services, ProjectionDispatchRequest request,
        ReadModelBatch batch, string storeName, CancellationToken cancellationToken)
    {
        if (batch.Scope.StoreName != storeName || batch.OwnerSelections is not { Count: > 0 } owners)
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        byte[] manifest = ReadModelBatchFingerprint.BuildCanonicalManifest(batch);
        if (manifest.Length > 1_048_576)
            throw new ProjectionDispatchValidationException("delivery_proof_limit_exceeded");
        var reference = new DomainProjectionReplayManifest(2, RequestDigest(request),
            ReadModelBatchFingerprint.ComputeFromManifest(manifest), [.. owners]);
        if (JsonSerializer.SerializeToUtf8Bytes(reference, JsonSerializerOptions.Web).Length > 16_384)
            throw new ProjectionDispatchValidationException("delivery_proof_limit_exceeded");
        IReadModelStore store = services.GetRequiredService<IReadModelStore>();
        string manifestKey = DomainProjectionRebuildTransport.Key(request, -2);
        byte[] boundManifest = BindManifest(manifest, reference.RequestDigest);
        if (!await store.TrySaveAsync(storeName, manifestKey, boundManifest, string.Empty, cancellationToken).ConfigureAwait(false))
        {
            byte[]? original = (await store.GetAsync<byte[]>(storeName, manifestKey, cancellationToken).ConfigureAwait(false)).Value;
            if (original is null || !OriginalManifest(original, reference.RequestDigest).Span.SequenceEqual(manifest))
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }
        if (!await store.TrySaveAsync(storeName, DomainProjectionRebuildTransport.Key(request, -3), reference,
            string.Empty, cancellationToken).ConfigureAwait(false))
            return await ReadAsync(services, request, storeName, cancellationToken).ConfigureAwait(false)
                ?? throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
        return batch;
    }

    private static string RequestDigest(ProjectionDispatchRequest request)
    {
        using var hash = new DomainProjectionRequestDigestStream();
        JsonSerializer.Serialize(hash, request, JsonSerializerOptions.Web);
        if (ProjectionReplayTransportContext.CaptureCanonical(request.Request) is { } context)
            JsonSerializer.Serialize(hash, context, JsonSerializerOptions.Web);
        return hash.Digest.ToUpperInvariant();
    }

    private static string LegacyRequestDigest(ProjectionDispatchRequest request, ProjectionOwnerSelection[] owners)
    {
        ProjectionReplayTransportContext? context = ProjectionReplayTransportContext.Capture(request.Request);
        if (request.Request.RebuildOwnerBindingFingerprint is not null)
        {
            if (owners.Length != request.ProjectionTypes.Count
                || owners.Select(static owner => owner.ProjectionName).Distinct(StringComparer.Ordinal).Count() != owners.Length
                || request.ProjectionTypes.Any(route => !owners.Any(owner => owner.ProjectionName == route)))
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            context = new(1, request.DispatchId,
                request.ProjectionTypes.ToDictionary(static route => route,
                    route => owners.Single(owner => owner.ProjectionName == route).OwnerGeneration, StringComparer.Ordinal),
                request.ProjectionTypes.ToDictionary(static route => route,
                    route => owners.Single(owner => owner.ProjectionName == route).OwnerIdentity, StringComparer.Ordinal));
        }
        using var hash = new DomainProjectionRequestDigestStream();
        JsonSerializer.Serialize(hash, request, JsonSerializerOptions.Web);
        if (context is not null) JsonSerializer.Serialize(hash, context, JsonSerializerOptions.Web);
        return hash.Digest.ToUpperInvariant();
    }

    /// <summary>Reclaims the original manifest after terminal proof, retaining the bounded no-reprepare reference.</summary>
    internal static async Task<bool> ReclaimAsync(IServiceProvider services, ProjectionDispatchRequest completed,
        ProjectionDispatchOptions options, DomainProjectionIdentityOptions identity, CancellationToken cancellationToken)
    {
        var guarded = Resolve(services, completed, options, identity);
        if (guarded is null || completed.Request.Events.Length != 0 || completed.RebuildTransport is not null
            || completed.Request.RebuildSourceHeadSequence is not > 0
            || completed.Request.RebuildSourceHeadMessageId != completed.DispatchId) return false;
        IReadModelStore store = services.GetRequiredService<IReadModelStore>();
        IReadModelConditionalEraser eraser = services.GetRequiredService<IReadModelConditionalEraser>();
        IReadModelOwnerProofStore proofs = services.GetRequiredService<IReadModelOwnerProofStore>();
        string manifestKey = DomainProjectionRebuildTransport.Key(completed, -2);
        string referenceKey = DomainProjectionRebuildTransport.Key(completed, -3);
        ReadModelEntry<DomainProjectionReplayManifest> reference = await store.GetAsync<DomainProjectionReplayManifest>(guarded.Value.Store,
            referenceKey, cancellationToken).ConfigureAwait(false);
        if (reference.Value is null)
            return (await store.GetAsync<byte[]>(guarded.Value.Store, manifestKey, cancellationToken).ConfigureAwait(false)).Value is null;
        DomainProjectionReplayManifest original = reference.Value;
        if (original.Version is not (1 or 2) || original.Owners.Length != completed.ProjectionTypes.Count
            || original.Owners.Select(static owner => owner.ProjectionName).Distinct(StringComparer.Ordinal).Count() != original.Owners.Length)
            return false;
        if (!ProjectionReplayTransportContext.MatchesOwners(completed.Request, original.Owners)) return false;
        foreach (ProjectionOwnerSelection owner in original.Owners)
        {
            if (!completed.ProjectionTypes.Contains(owner.ProjectionName, StringComparer.Ordinal)
                || owner.AppliedSequence != completed.Request.RebuildSourceHeadSequence || owner.MessageId != completed.DispatchId
                || owner.BatchId != completed.DispatchId || owner.ReceiptProjectionName != "rebuild") return false;
            DomainProjectionOwnerProofRegistration enrolled = services.GetServices<DomainProjectionOwnerProofRegistration>()
                .Single(item => item.Domain == completed.Request.Domain && item.ProjectionType == owner.ProjectionName);
            ProjectionOwnerProofResult selected = await proofs.ReadOwnerProofAsync(new(completed.Request.TenantId, completed.Request.Domain,
                completed.Request.AggregateId, owner.ProjectionName, completed.CatalogFingerprint), enrolled.Binding, cancellationToken).ConfigureAwait(false);
            if (selected.Proof?.Selection != owner || selected.Proof.ReceiptFingerprint != original.Fingerprint) return false;
        }
        // The coordinated SDK batch has a separate private manifest. Retain the
        // original cleanup work until both owning stores prove reclamation, even
        // for a terminal retry whose guarded replay manifest was already removed.
        var batchScope = new ReadModelBatchScope(guarded.Value.Store, completed.Request.TenantId,
            completed.Request.Domain, completed.Request.AggregateId, "rebuild", completed.DispatchId);
        string batchManifestKey = ReadModelBatchKeys.ManifestKey(batchScope.ComputeScopeHash());
        ReadModelEntry<JsonDocument> batchManifest = await store.GetAsync<JsonDocument>(guarded.Value.Store,
            batchManifestKey, cancellationToken).ConfigureAwait(false);
        if (batchManifest.Value is { } document)
        {
            using (document)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(document.RootElement.GetRawText());
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ManifestSerializationBytes, bytes.Length);
                if (bytes.Length > 1_048_576 || ReadModelBatchFingerprint.ComputeFromManifest(bytes) != original.Fingerprint
                    || batchManifest.ETag is not { Length: > 0 }
                    || !await eraser.TryEraseAsync(guarded.Value.Store, batchManifestKey, batchManifest.ETag,
                        cancellationToken).ConfigureAwait(false)) return false;
            }
        }
        ReadModelEntry<byte[]> manifest = await store.GetAsync<byte[]>(guarded.Value.Store, manifestKey, cancellationToken).ConfigureAwait(false);
        if (manifest.Value is not null && (ReadModelBatchFingerprint.ComputeFromManifest(OriginalManifest(manifest.Value, null).Span) != original.Fingerprint
            || manifest.ETag is not { Length: > 0 }
            || !await eraser.TryEraseAsync(guarded.Value.Store, manifestKey, manifest.ETag, cancellationToken).ConfigureAwait(false))) return false;
        // Keep this small immutable reference. A delayed Stage must fail closed after
        // manifest reclamation instead of preparing a different plan under the same ID.
        // A lost delete response is resolved by proving this same terminal selection again.
        return true;
    }
}
