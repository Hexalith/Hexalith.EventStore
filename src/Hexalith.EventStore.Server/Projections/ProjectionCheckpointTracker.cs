using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// DAPR state-store implementation of <see cref="IProjectionCheckpointTracker"/>.
/// </summary>
public sealed partial class ProjectionCheckpointTracker(
    DaprClient daprClient,
    IOptions<ProjectionOptions> options,
    ILogger<ProjectionCheckpointTracker> logger) : IProjectionCheckpointTracker, IProjectionCheckpointEraser, IProjectionDeliveryCheckpointStore {
    private const int MaxEtagRetries = 3;
    private const int IdentityPageSize = 100;
    private const string StateKeyPrefix = "projection-checkpoints:";
    private const string MigratedMarkerPrefix = "projection-checkpoints-migrated:";
    private const string IdentityScopeIndexKey = "projection-identities:scopes";
    private const string IdentityScopePagePrefix = "projection-identities:scopes:";
    private const string IdentityIndexPrefix = "projection-identities:index:";
    private const string IdentityPagePrefix = "projection-identities:page:";

    // Defensive upper bound on persisted PageCount. Healthy operation produces small page counts
    // (page size is 100); a corrupt persisted index with PageCount near int.MaxValue would
    // satisfy every other corruption clause yet still drive a near-infinite enumeration loop.
    private const int MaxReasonablePageCount = 1_000_000;
    private readonly IProjectionDeliveryStateStore _deliveryStateStore = new DaprProjectionDeliveryStateStore(daprClient, options);

    /// <inheritdoc/>
    public async Task<long> ReadLastDeliveredSequenceAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default) {
        ValidateIdentity(identity);

        ProjectionCheckpoint? checkpoint = await daprClient
            .GetStateAsync<ProjectionCheckpoint>(
                options.Value.CheckpointStateStoreName,
                GetStateKey(identity),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (checkpoint is null) {
            return 0;
        }

        if (!string.Equals(checkpoint.TenantId, identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(checkpoint.Domain, identity.Domain, StringComparison.Ordinal)
            || !string.Equals(checkpoint.AggregateId, identity.AggregateId, StringComparison.Ordinal)) {
            throw new InvalidOperationException("Projection checkpoint identity does not match the requested aggregate identity.");
        }

        return checkpoint.LastDeliveredSequence;
    }

    /// <inheritdoc/>
    public async Task<bool> SaveDeliveredSequenceAsync(
        AggregateIdentity identity,
        long deliveredSequence,
        CancellationToken cancellationToken = default) {
        ValidateIdentity(identity);
        ArgumentOutOfRangeException.ThrowIfNegative(deliveredSequence);

        string key = GetStateKey(identity);
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            try {
                (ProjectionCheckpoint? existing, string etag) = await daprClient
                    .GetStateAndETagAsync<ProjectionCheckpoint>(
                        stateStoreName,
                        key,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (existing is not null
                    && (!string.Equals(existing.TenantId, identity.TenantId, StringComparison.Ordinal)
                        || !string.Equals(existing.Domain, identity.Domain, StringComparison.Ordinal)
                        || !string.Equals(existing.AggregateId, identity.AggregateId, StringComparison.Ordinal))) {
                    throw new InvalidOperationException("Projection checkpoint identity does not match the requested aggregate identity.");
                }

                // Skip the round-trip when the proposed sequence is already covered by the
                // persisted checkpoint. This avoids burning ETag retries (and emitting a
                // misleading CheckpointSaveExhausted warning) when concurrent fan-out triggers
                // collide on a key that does not actually need to advance.
                if (existing is not null && existing.LastDeliveredSequence >= deliveredSequence) {
                    return true;
                }

                long maxSequence = Math.Max(existing?.LastDeliveredSequence ?? 0, deliveredSequence);
                var checkpoint = new ProjectionCheckpoint(
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    maxSequence,
                    DateTimeOffset.UtcNow);

                bool saved = await daprClient
                    .TrySaveStateAsync(
                        stateStoreName,
                        key,
                        checkpoint,
                        etag,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (saved) {
                    return true;
                }

                logger.LogDebug(
                    "ETag mismatch while updating projection checkpoint '{CheckpointKey}', retry {Attempt}.",
                    key,
                    attempt + 1);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (attempt < MaxEtagRetries - 1) {
                logger.LogDebug(
                    ex,
                    "Retry {Attempt} while updating projection checkpoint '{CheckpointKey}'.",
                    attempt + 1,
                    key);
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> TryEraseAsync(
        AggregateIdentity identity,
        string etag,
        CancellationToken cancellationToken = default) {
        ValidateIdentity(identity);
        ArgumentNullException.ThrowIfNull(etag);

        return await daprClient
            .TryDeleteStateAsync(
                options.Value.CheckpointStateStoreName,
                GetStateKey(identity),
                etag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<long> ReadDeliveredSequenceAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default) {
        ValidateIdentity(identity);
        ValidateProjectionName(projectionName);

        string stateStoreName = options.Value.CheckpointStateStoreName;
        string scopedKey = GetProjectionScopedStateKey(identity, projectionName);

        ProjectionDeliveryStateReadResult deliveryRead = await _deliveryStateStore
            .ReadAsync(identity, projectionName, cancellationToken)
            .ConfigureAwait(false);
        ProjectionDeliveryState? deliveryState = deliveryRead.State;
        ProjectionCheckpoint? scoped = deliveryState is null
            ? await daprClient
                .GetStateAsync<ProjectionCheckpoint>(stateStoreName, scopedKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
            : new ProjectionCheckpoint(
                deliveryState.TenantId,
                deliveryState.Domain,
                deliveryState.AggregateId,
                deliveryState.LastDeliveredSequence,
                deliveryState.UpdatedAt);

        if (scoped is not null) {
            if (!string.Equals(scoped.TenantId, identity.TenantId, StringComparison.Ordinal)
                || !string.Equals(scoped.Domain, identity.Domain, StringComparison.Ordinal)
                || !string.Equals(scoped.AggregateId, identity.AggregateId, StringComparison.Ordinal)) {
                throw new InvalidOperationException("Projection checkpoint identity does not match the requested aggregate identity.");
            }

            return scoped.LastDeliveredSequence;
        }

        // Scoped key absent: consult the migration marker to distinguish "never migrated" from
        // "migrated then erased". A present marker means an operator (or the eraser) intentionally
        // removed the projection-scoped checkpoint, so we must NOT fall back to the legacy value.
        ProjectionCheckpointMigrationMarker? marker = await daprClient
            .GetStateAsync<ProjectionCheckpointMigrationMarker>(
                stateStoreName,
                GetMigratedMarkerKey(identity, projectionName),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (marker is not null) {
            return 0;
        }

        // Never migrated: lazily migrate from the legacy aggregate-wide checkpoint. The legacy key
        // is only read here and is NEVER deleted, so other projections of the same aggregate keep
        // migrating from it independently.
        ProjectionCheckpoint? legacy = await daprClient
            .GetStateAsync<ProjectionCheckpoint>(stateStoreName, GetStateKey(identity), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        long migratedValue = 0;
        if (legacy is not null
            && string.Equals(legacy.TenantId, identity.TenantId, StringComparison.Ordinal)
            && string.Equals(legacy.Domain, identity.Domain, StringComparison.Ordinal)
            && string.Equals(legacy.AggregateId, identity.AggregateId, StringComparison.Ordinal)
            && legacy.LastDeliveredSequence > 0) {
            migratedValue = legacy.LastDeliveredSequence;

            // Conditionally seed the projection-scoped key from the legacy high-water mark. Read the
            // current ETag (absent → empty) and TrySaveStateAsync; a concurrent migration that won
            // the race leaves a non-null value, in which case we adopt the persisted value.
            (ProjectionCheckpoint? currentScoped, string scopedEtag) = await daprClient
                .GetStateAndETagAsync<ProjectionCheckpoint>(stateStoreName, scopedKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (currentScoped is null) {
                _ = await daprClient
                    .TrySaveStateAsync(
                        stateStoreName,
                        scopedKey,
                        new ProjectionCheckpoint(
                            identity.TenantId,
                            identity.Domain,
                            identity.AggregateId,
                            migratedValue,
                            DateTimeOffset.UtcNow),
                        scopedEtag,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            else {
                migratedValue = currentScoped.LastDeliveredSequence;
            }
        }

        // Persist the migration marker so a later erase-then-read returns 0 instead of re-migrating
        // the legacy value. The marker is written even when the legacy checkpoint is absent/zero so
        // the (cheap) legacy read is not repeated on every subsequent read.
        await MarkMigratedAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);

        return migratedValue;
    }

    /// <inheritdoc/>
    public async Task<bool> SaveDeliveredSequenceAsync(
        AggregateIdentity identity,
        string projectionName,
        long deliveredSequence,
        CancellationToken cancellationToken = default) {
        ValidateIdentity(identity);
        ValidateProjectionName(projectionName);
        ArgumentOutOfRangeException.ThrowIfNegative(deliveredSequence);

        string key = GetProjectionScopedStateKey(identity, projectionName);
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            try {
                ProjectionDeliveryStateReadResult deliveryRead = await _deliveryStateStore
                    .ReadAsync(identity, projectionName, cancellationToken)
                    .ConfigureAwait(false);
                if (deliveryRead.Classification == ProjectionDeliveryStateClassification.Current) {
                    if (deliveryRead.State!.LastDeliveredSequence >= deliveredSequence) {
                        await MarkMigratedAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
                        return true;
                    }

                    // A v2 row can advance only through a fenced completion transition that also
                    // persists exact receipts and the cumulative prefix fingerprint. The legacy
                    // checkpoint-only path must never erase those fields or invent completion.
                    return false;
                }

                if (deliveryRead.Classification is ProjectionDeliveryStateClassification.SchemaRegression
                    or ProjectionDeliveryStateClassification.Unsupported) {
                    return false;
                }

                ProjectionDeliveryWriterProtocol? protocol = await _deliveryStateStore
                    .ReadWriterProtocolAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (protocol?.IsCurrent == true) {
                    // After cutover, no five-field writer may create or advance a scoped row.
                    return false;
                }

                ProjectionCheckpoint? existing;
                string etag;
                if (deliveryRead.State is null) {
                    (existing, etag) = await daprClient
                        .GetStateAndETagAsync<ProjectionCheckpoint>(
                            stateStoreName,
                            key,
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                else {
                    existing = new ProjectionCheckpoint(
                        deliveryRead.State.TenantId,
                        deliveryRead.State.Domain,
                        deliveryRead.State.AggregateId,
                        deliveryRead.State.LastDeliveredSequence,
                        deliveryRead.State.UpdatedAt);
                    etag = deliveryRead.Etag;
                }

                if (existing is not null
                    && (!string.Equals(existing.TenantId, identity.TenantId, StringComparison.Ordinal)
                        || !string.Equals(existing.Domain, identity.Domain, StringComparison.Ordinal)
                        || !string.Equals(existing.AggregateId, identity.AggregateId, StringComparison.Ordinal))) {
                    throw new InvalidOperationException("Projection checkpoint identity does not match the requested aggregate identity.");
                }

                // Skip the round-trip when the proposed sequence is already covered by the persisted
                // projection-scoped checkpoint (mirrors the aggregate-wide save). Migration is still
                // finalized so a later erase-then-read cannot regress to the legacy value.
                if (existing is not null && existing.LastDeliveredSequence >= deliveredSequence) {
                    await MarkMigratedAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
                    return true;
                }

                long maxSequence = Math.Max(existing?.LastDeliveredSequence ?? 0, deliveredSequence);
                var checkpoint = new ProjectionCheckpoint(
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    maxSequence,
                    DateTimeOffset.UtcNow);

                bool saved = await daprClient
                    .TrySaveStateAsync(
                        stateStoreName,
                        key,
                        checkpoint,
                        etag,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (saved) {
                    // A save implies migration is complete: finalize the marker so a later
                    // erase-then-read returns 0 rather than falling back to the legacy value.
                    await MarkMigratedAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
                    return true;
                }

                logger.LogDebug(
                    "ETag mismatch while updating projection-scoped checkpoint '{CheckpointKey}', retry {Attempt}.",
                    key,
                    attempt + 1);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (attempt < MaxEtagRetries - 1) {
                logger.LogDebug(
                    ex,
                    "Retry {Attempt} while updating projection-scoped checkpoint '{CheckpointKey}'.",
                    attempt + 1,
                    key);
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> TryEraseAsync(
        AggregateIdentity identity,
        string projectionName,
        string etag,
        CancellationToken cancellationToken = default) {
        ValidateIdentity(identity);
        ValidateProjectionName(projectionName);
        ArgumentNullException.ThrowIfNull(etag);

        // Erase the projection-scoped checkpoint only.
        bool erased = await daprClient
            .TryDeleteStateAsync(
                options.Value.CheckpointStateStoreName,
                GetProjectionScopedStateKey(identity, projectionName),
                etag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Ensure the migration marker is present after a successful erase (or idempotent absence) so a
        // subsequent read returns 0 instead of re-migrating the retained legacy aggregate-wide value.
        // Do NOT write the marker when a newer value was refused (erased == false): the present value must
        // still be observable to a read. Ensuring the marker here closes the window where a prior lazy
        // migration seeded the scoped key but failed to persist its marker.
        if (erased) {
            await MarkMigratedAsync(identity, projectionName, cancellationToken).ConfigureAwait(false);
        }

        return erased;
    }

    /// <inheritdoc/>
    public async Task<(bool Present, string Etag)> TryReadDeliveryCheckpointEtagAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default) {
        ValidateIdentity(identity);
        ValidateProjectionName(projectionName);

        // Raw ETag read of the projection-scoped key ONLY: no lazy legacy migration, no legacy fallback.
        (ProjectionCheckpoint? value, string etag) = await daprClient
            .GetStateAndETagAsync<ProjectionCheckpoint>(
                options.Value.CheckpointStateStoreName,
                GetProjectionScopedStateKey(identity, projectionName),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return (value is not null, etag);
    }

    /// <inheritdoc/>
    public async Task<(bool Present, string Etag)> TryReadDeliveryReconciliationEtagAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default) {
        ValidateIdentity(identity);
        ValidateProjectionName(projectionName);
        (ProjectionDeliveryReconciliationWork? value, string etag) = await daprClient
            .GetStateAndETagAsync<ProjectionDeliveryReconciliationWork>(
                options.Value.CheckpointStateStoreName,
                ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projectionName),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return (value is not null, etag);
    }

    /// <inheritdoc/>
    public async Task<bool> TryEraseDeliveryReconciliationAsync(
        AggregateIdentity identity,
        string projectionName,
        string etag,
        CancellationToken cancellationToken = default) {
        ValidateIdentity(identity);
        ValidateProjectionName(projectionName);
        ArgumentNullException.ThrowIfNull(etag);
        return await daprClient.TryDeleteStateAsync(
            options.Value.CheckpointStateStoreName,
            ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projectionName),
            etag,
            new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task MarkMigratedAsync(AggregateIdentity identity, string projectionName, CancellationToken cancellationToken) =>
        await daprClient
            .SaveStateAsync(
                options.Value.CheckpointStateStoreName,
                GetMigratedMarkerKey(identity, projectionName),
                new ProjectionCheckpointMigrationMarker(true),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task TrackIdentityAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) {
        ValidateIdentity(identity);

        ProjectionIdentityScope scope = new(identity.TenantId, identity.Domain);
        if (!await TryAddScopeAsync(scope, cancellationToken).ConfigureAwait(false)) {
            logger.LogWarning(
                "Projection identity scope registration exhausted ETag retries for tenant {TenantId}, domain {Domain}.",
                identity.TenantId,
                identity.Domain);
            // R3P11 — attach the offending identity components to Exception.Data so callers
            // and operators can programmatically distinguish scope-vs-identity exhaustion.
            var scopeException = new InvalidOperationException("Projection identity scope registration exhausted ETag retries.");
            scopeException.Data["TenantId"] = identity.TenantId;
            scopeException.Data["Domain"] = identity.Domain;
            scopeException.Data["ExhaustionScope"] = "ScopeIndex";
            throw scopeException;
        }

        if (!await TryAddIdentityAsync(scope, new ProjectionIdentity(identity.TenantId, identity.Domain, identity.AggregateId), cancellationToken).ConfigureAwait(false)) {
            logger.LogWarning(
                "Projection identity registration exhausted ETag retries for tenant {TenantId}, domain {Domain}, aggregate {AggregateId}.",
                identity.TenantId,
                identity.Domain,
                identity.AggregateId);
            var identityException = new InvalidOperationException("Projection identity registration exhausted ETag retries.");
            identityException.Data["TenantId"] = identity.TenantId;
            identityException.Data["Domain"] = identity.Domain;
            identityException.Data["AggregateId"] = identity.AggregateId;
            identityException.Data["ExhaustionScope"] = "IdentityIndex";
            throw identityException;
        }
    }

    // R3P5 — lifted from TrackIdentityAsync so the same defense-in-depth runs on the Read and Save
    // entry points. AggregateIdentity's ctor regex already lowercases tenant+domain and rejects
    // reserved chars under normal construction, so this guard catches bypass paths (reflection,
    // deserialization without validator, record `with`-clones) for all three operations.
    private static void ValidateIdentity(AggregateIdentity identity) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.AggregateId);
        AssertNoReservedChars(identity.TenantId, nameof(identity.TenantId));
        AssertNoReservedChars(identity.Domain, nameof(identity.Domain));
        AssertNoReservedChars(identity.AggregateId, nameof(identity.AggregateId));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AggregateIdentity> EnumerateTrackedIdentitiesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ProjectionIdentityIndex scopeIndex = await ReadStateAsync<ProjectionIdentityIndex>(IdentityScopeIndexKey, cancellationToken)
            .ConfigureAwait(false)
            ?? ProjectionIdentityIndex.Empty;
        if (IsCorruptIndex(scopeIndex)) {
            Log.TrackerCorruptionDetected(
                logger,
                TrackerReasonCodes.CorruptScopeIndex,
                IdentityScopeIndexKey,
                scopeIndex.PageCount,
                scopeIndex.LastPageCount);
            yield break;
        }

        for (int scopePageNumber = 0; scopePageNumber < scopeIndex.PageCount; scopePageNumber++) {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectionIdentityScopePage? scopePage = await ReadStateAsync<ProjectionIdentityScopePage>(
                    GetScopePageKey(scopePageNumber),
                    cancellationToken)
                .ConfigureAwait(false);
            if (scopePage is null) {
                continue;
            }

            // Defensive null coalesce: a single corrupted/legacy persisted record with a null
            // array (e.g. from a partial restore or schema drift) must not raise NRE and tear
            // down the entire poll tick.
            foreach (ProjectionIdentityScope scope in scopePage.Scopes ?? []) {
                ProjectionIdentityIndex identityIndex = await ReadStateAsync<ProjectionIdentityIndex>(
                        GetIdentityIndexKey(scope),
                        cancellationToken)
                    .ConfigureAwait(false)
                    ?? ProjectionIdentityIndex.Empty;
                if (IsCorruptIndex(identityIndex)) {
                    Log.TrackerCorruptionDetected(
                        logger,
                        TrackerReasonCodes.CorruptIdentityIndex,
                        GetIdentityIndexKey(scope),
                        identityIndex.PageCount,
                        identityIndex.LastPageCount);
                    continue;
                }

                for (int identityPageNumber = 0; identityPageNumber < identityIndex.PageCount; identityPageNumber++) {
                    cancellationToken.ThrowIfCancellationRequested();
                    ProjectionIdentityPage? identityPage = await ReadStateAsync<ProjectionIdentityPage>(
                            GetIdentityPageKey(scope, identityPageNumber),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (identityPage is null) {
                        continue;
                    }

                    foreach (ProjectionIdentity trackedIdentity in identityPage.Identities ?? []) {
                        yield return new AggregateIdentity(trackedIdentity.TenantId, trackedIdentity.Domain, trackedIdentity.AggregateId);
                    }
                }
            }
        }
    }

    internal static string GetStateKey(AggregateIdentity identity) {
        ArgumentNullException.ThrowIfNull(identity);
        return StateKeyPrefix + identity.ActorId;
    }

    internal static string GetProjectionScopedStateKey(AggregateIdentity identity, string projectionName) {
        ArgumentNullException.ThrowIfNull(identity);
        return StateKeyPrefix + identity.ActorId + ":" + projectionName;
    }

    internal static string GetMigratedMarkerKey(AggregateIdentity identity, string projectionName) {
        ArgumentNullException.ThrowIfNull(identity);
        return MigratedMarkerPrefix + identity.ActorId + ":" + projectionName;
    }

    // Mirrors ValidateIdentity's defense-in-depth for the projection name component: the name is
    // interpolated directly into the projection-scoped and migration-marker keys, so it must not be
    // empty and must not smuggle a reserved separator/terminator that would collide keys.
    private static void ValidateProjectionName(string projectionName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);
        AssertNoReservedChars(projectionName, nameof(projectionName));
    }

    private static void AssertNoReservedChars(string value, string parameterName) {
        // Reserved characters across the projection-identity key scheme: ':' separator, '\0' terminator,
        // '|' (used by ProjectionDiscoveryHostedService.ExtractDomain), and CR/LF which break log lines.
        if (value.AsSpan().IndexOfAny(s_reservedChars) >= 0) {
            throw new ArgumentException(
                $"{parameterName} must not contain ':', '\\0', '|', '\\r', or '\\n' — reserved by the projection-identity key scheme.",
                parameterName);
        }
    }

    private static readonly System.Buffers.SearchValues<char> s_reservedChars = System.Buffers.SearchValues.Create(":\0|\r\n");

    private static bool IsCorruptIndex(ProjectionIdentityIndex index) =>
        index.PageCount < 0
        || index.PageCount > MaxReasonablePageCount
        || index.LastPageCount < 0
        || index.LastPageCount > IdentityPageSize
        || (index.PageCount == 0 && index.LastPageCount != 0)
        || (index.PageCount > 0 && index.LastPageCount == 0);

    private static string GetIdentityIndexKey(ProjectionIdentityScope scope) =>
        IdentityIndexPrefix + scope.TenantId + ":" + scope.Domain;

    private static string GetIdentityPageKey(ProjectionIdentityScope scope, int pageNumber) =>
        IdentityPagePrefix + scope.TenantId + ":" + scope.Domain + ":" + pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string GetScopePageKey(int pageNumber) =>
        IdentityScopePagePrefix + pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // Page+index updates use a single bounded ETag retry loop on the index "anchor" plus
    // ETag-guarded page writes. Concurrent same-process or cross-replica writers cannot lose
    // data: the loser sees ETag mismatch, re-reads, and re-applies the append. After the
    // bounded retry budget is exhausted, the caller logs and returns rather than performing a
    // blind non-ETag save that could regress the index.
    private async Task<bool> TryAddScopeAsync(ProjectionIdentityScope scope, CancellationToken cancellationToken) {
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            try {
                (ProjectionIdentityIndex? indexOrNull, string indexEtag) = await daprClient
                    .GetStateAndETagAsync<ProjectionIdentityIndex>(stateStoreName, IdentityScopeIndexKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                ProjectionIdentityIndex index = indexOrNull ?? ProjectionIdentityIndex.Empty;

                bool alreadyTracked = false;
                for (int pageNumber = 0; pageNumber < index.PageCount; pageNumber++) {
                    (ProjectionIdentityScopePage? existingPageOrNull, _) = await ReadStateAndEtagAsync<ProjectionIdentityScopePage>(GetScopePageKey(pageNumber), cancellationToken)
                        .ConfigureAwait(false);
                    ProjectionIdentityScopePage existingPage = existingPageOrNull ?? ProjectionIdentityScopePage.Empty;
                    if ((existingPage.Scopes ?? []).Any(existing => existing == scope)) {
                        alreadyTracked = true;
                        break;
                    }
                }

                if (alreadyTracked) {
                    return true;
                }

                int targetPageNumber = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize
                    ? index.PageCount
                    : index.PageCount - 1;
                string pageKey = GetScopePageKey(targetPageNumber);
                (ProjectionIdentityScopePage? pageOrNull, string pageEtag) = await daprClient
                    .GetStateAndETagAsync<ProjectionIdentityScopePage>(stateStoreName, pageKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                ProjectionIdentityScopePage page = pageOrNull ?? ProjectionIdentityScopePage.Empty;
                ProjectionIdentityScope[] existingScopes = page.Scopes ?? [];
                if (existingScopes.Any(existing => existing == scope)) {
                    // R3P3 — clamp LastPageCount to IdentityPageSize. A concurrent writer may have
                    // grown existingScopes past the boundary between attempts; persisting the raw count
                    // would break the rollover predicate downstream.
                    ProjectionIdentityIndex recoveredIndex = index.PageCount == 0 || targetPageNumber >= index.PageCount
                        ? new ProjectionIdentityIndex(targetPageNumber + 1, Math.Min(existingScopes.Length, IdentityPageSize))
                        : index;
                    if (recoveredIndex == index
                        || await daprClient
                            .TrySaveStateAsync(stateStoreName, IdentityScopeIndexKey, recoveredIndex, indexEtag, cancellationToken: cancellationToken)
                            .ConfigureAwait(false)) {
                        return true;
                    }

                    continue;
                }

                if (existingScopes.Length >= IdentityPageSize) {
                    // R3P1 — orphan recovery for "page filled to IdentityPageSize but index says
                    // LastPageCount < IdentityPageSize". A previous attempt's page-save succeeded but
                    // the index-save failed, leaving the index understating the page's actual size.
                    // Without this clamp, every retry recomputes the same targetPageNumber against the
                    // stale LastPageCount and hits this `continue` again — exhausting retries and
                    // throwing for every new scope in the affected scope-index.
                    if (targetPageNumber == index.PageCount - 1 && index.LastPageCount < IdentityPageSize) {
                        ProjectionIdentityIndex correctedIndex = index with { LastPageCount = IdentityPageSize };
                        _ = await daprClient
                            .TrySaveStateAsync(stateStoreName, IdentityScopeIndexKey, correctedIndex, indexEtag, cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    }

                    continue;
                }

                ProjectionIdentityScope[] updatedScopes = existingScopes.Append(scope).ToArray();

                bool pageSaved = await daprClient
                    .TrySaveStateAsync(stateStoreName, pageKey, new ProjectionIdentityScopePage(updatedScopes), pageEtag, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (!pageSaved) {
                    continue;
                }

                ProjectionIdentityIndex updatedIndex = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize
                    ? new ProjectionIdentityIndex(index.PageCount + 1, 1)
                    : index with { LastPageCount = updatedScopes.Length };

                bool indexSaved = await daprClient
                    .TrySaveStateAsync(stateStoreName, IdentityScopeIndexKey, updatedIndex, indexEtag, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (indexSaved) {
                    return true;
                }
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (attempt < MaxEtagRetries - 1) {
                logger.LogDebug(
                    ex,
                    "Retry {Attempt} while registering projection scope '{TenantId}:{Domain}'.",
                    attempt + 1,
                    scope.TenantId,
                    scope.Domain);
            }
        }

        return false;
    }

    private async Task<bool> TryAddIdentityAsync(ProjectionIdentityScope scope, ProjectionIdentity identity, CancellationToken cancellationToken) {
        string stateStoreName = options.Value.CheckpointStateStoreName;
        string indexKey = GetIdentityIndexKey(scope);
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            try {
                (ProjectionIdentityIndex? indexOrNull, string indexEtag) = await daprClient
                    .GetStateAndETagAsync<ProjectionIdentityIndex>(stateStoreName, indexKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                ProjectionIdentityIndex index = indexOrNull ?? ProjectionIdentityIndex.Empty;

                bool alreadyTracked = false;
                for (int pageNumber = 0; pageNumber < index.PageCount; pageNumber++) {
                    (ProjectionIdentityPage? existingPageOrNull, _) = await ReadStateAndEtagAsync<ProjectionIdentityPage>(GetIdentityPageKey(scope, pageNumber), cancellationToken)
                        .ConfigureAwait(false);
                    ProjectionIdentityPage existingPage = existingPageOrNull ?? ProjectionIdentityPage.Empty;
                    if ((existingPage.Identities ?? []).Any(existing => existing == identity)) {
                        alreadyTracked = true;
                        break;
                    }
                }

                if (alreadyTracked) {
                    return true;
                }

                int targetPageNumber = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize
                    ? index.PageCount
                    : index.PageCount - 1;
                string pageKey = GetIdentityPageKey(scope, targetPageNumber);
                (ProjectionIdentityPage? pageOrNull, string pageEtag) = await daprClient
                    .GetStateAndETagAsync<ProjectionIdentityPage>(stateStoreName, pageKey, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                ProjectionIdentityPage page = pageOrNull ?? ProjectionIdentityPage.Empty;
                ProjectionIdentity[] existingIdentities = page.Identities ?? [];
                if (existingIdentities.Any(existing => existing == identity)) {
                    // R3P3 — clamp LastPageCount to IdentityPageSize so a concurrent writer that grew
                    // the page past the boundary between attempts cannot produce an index whose
                    // LastPageCount exceeds the rollover predicate.
                    ProjectionIdentityIndex recoveredIndex = index.PageCount == 0 || targetPageNumber >= index.PageCount
                        ? new ProjectionIdentityIndex(targetPageNumber + 1, Math.Min(existingIdentities.Length, IdentityPageSize))
                        : index;
                    if (recoveredIndex == index
                        || await daprClient
                            .TrySaveStateAsync(stateStoreName, indexKey, recoveredIndex, indexEtag, cancellationToken: cancellationToken)
                            .ConfigureAwait(false)) {
                        return true;
                    }

                    continue;
                }

                if (existingIdentities.Length >= IdentityPageSize) {
                    // R3P1 — orphan recovery: page filled to IdentityPageSize but index says
                    // LastPageCount < IdentityPageSize. A previous attempt's page-save succeeded but
                    // its index-save failed; without this clamp every retry recomputes the same
                    // targetPageNumber against the stale LastPageCount and hits this `continue`
                    // forever, exhausting retries and throwing for every new aggregate in the scope.
                    if (targetPageNumber == index.PageCount - 1 && index.LastPageCount < IdentityPageSize) {
                        ProjectionIdentityIndex correctedIndex = index with { LastPageCount = IdentityPageSize };
                        _ = await daprClient
                            .TrySaveStateAsync(stateStoreName, indexKey, correctedIndex, indexEtag, cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    }

                    continue;
                }

                ProjectionIdentity[] updatedIdentities = existingIdentities.Append(identity).ToArray();

                bool pageSaved = await daprClient
                    .TrySaveStateAsync(stateStoreName, pageKey, new ProjectionIdentityPage(updatedIdentities), pageEtag, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (!pageSaved) {
                    continue;
                }

                ProjectionIdentityIndex updatedIndex = index.PageCount == 0 || index.LastPageCount >= IdentityPageSize
                    ? new ProjectionIdentityIndex(index.PageCount + 1, 1)
                    : index with { LastPageCount = updatedIdentities.Length };

                bool indexSaved = await daprClient
                    .TrySaveStateAsync(stateStoreName, indexKey, updatedIndex, indexEtag, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (indexSaved) {
                    return true;
                }
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (attempt < MaxEtagRetries - 1) {
                logger.LogDebug(
                    ex,
                    "Retry {Attempt} while registering projection identity '{TenantId}:{Domain}:{AggregateId}'.",
                    attempt + 1,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId);
            }
        }

        return false;
    }

    private async Task<T?> ReadStateAsync<T>(string key, CancellationToken cancellationToken) =>
        await daprClient
            .GetStateAsync<T>(
                options.Value.CheckpointStateStoreName,
                key,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    private async Task<(T? Value, string Etag)> ReadStateAndEtagAsync<T>(string key, CancellationToken cancellationToken) =>
        await daprClient
            .GetStateAndETagAsync<T>(
                options.Value.CheckpointStateStoreName,
                key,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    // Persisted-schema records — internal (not private) so test assemblies with
    // InternalsVisibleTo can construct/inspect them when probing storage interactions.
    // Not part of the public API surface; serialization shape is owned here.
    internal sealed record ProjectionIdentityIndex(int PageCount, int LastPageCount) {
        public static ProjectionIdentityIndex Empty { get; } = new(0, 0);
    }

    internal sealed record ProjectionIdentityScope(string TenantId, string Domain);

    internal sealed record ProjectionIdentityScopePage(ProjectionIdentityScope[] Scopes) {
        public static ProjectionIdentityScopePage Empty { get; } = new([]);
    }

    internal sealed record ProjectionIdentity(string TenantId, string Domain, string AggregateId);

    internal sealed record ProjectionIdentityPage(ProjectionIdentity[] Identities) {
        public static ProjectionIdentityPage Empty { get; } = new([]);
    }

    // Persisted marker recording that a projection-scoped checkpoint has been migrated in from (or
    // initialized alongside) the legacy aggregate-wide checkpoint. Its presence — not its value —
    // signals that an absent projection-scoped key is an intentional erase, so reads must not fall
    // back to the legacy aggregate-wide value.
    internal sealed record ProjectionCheckpointMigrationMarker(bool Migrated);

    private static partial class Log {
        [LoggerMessage(
            EventId = 1144,
            Level = LogLevel.Warning,
            Message = "Projection tracker corruption detected: ReasonCode={ReasonCode}, StateKey={StateKey}, PageCount={PageCount}, LastPageCount={LastPageCount}, Stage=ProjectionTrackerCorruption")]
        public static partial void TrackerCorruptionDetected(ILogger logger, string reasonCode, string stateKey, int pageCount, int lastPageCount);
    }
}
