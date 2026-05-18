using Dapr;
using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// DAPR state-store implementation for projection rebuild checkpoint progress.
/// </summary>
public sealed partial class ProjectionRebuildCheckpointStore(
    DaprClient daprClient,
    IOptions<ProjectionOptions> options,
    ILogger<ProjectionRebuildCheckpointStore> logger) : IProjectionRebuildCheckpointStore {
    // P12-7P (pass-7): bumped from 3 to 5 to reduce CheckpointConflict failures when N>3 concurrent
    // operators/transitions hit the same active-index key. Bigger retry budget plus jitter
    // mitigates the ETag-contention hotspot without sharding the index.
    private const int MaxEtagRetries = 5;
    private const string StateKeyPrefix = "projection-rebuild-checkpoints:";
    private const string ActiveIndexKeyPrefix = "projection-rebuild-active-index:";
    private const string ActivePairIndexKey = "projection-rebuild-active-index-pairs";

    private static readonly TimeSpan[] s_retryDelays = [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(500),
    ];

    // P11-7P (pass-7): process-local TTL cache of fail-closed verdicts so multiple poller ticks
    // within the TTL window short-circuit instead of each paying the full bounded-retry budget.
    // During a brief store flap, this prevents every poller tick across every aggregate in the
    // (tenant, domain) from thundering the failed index store.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> s_failClosedCache
        = new(StringComparer.Ordinal);

    // P2-8P (pass-8): track per-(tenant, domain) last successful read time so a fail-closed
    // write that races AFTER a known-good read does not poison the cache. The previous code
    // had a window where A.success(t=0.1) + A.TryRemove(t=0.2) + B.write(t=2) left the
    // cache with B's stale fail-closed verdict despite A's recent success. The write
    // branch now skips its set if a more-recent success has been recorded since the request
    // started.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> s_lastReadSuccessAt
        = new(StringComparer.Ordinal);
    private static readonly TimeSpan s_failClosedTtl = TimeSpan.FromSeconds(5);

    static ProjectionRebuildCheckpointStore() {
        if (s_retryDelays.Length != MaxEtagRetries - 1) {
            throw new InvalidOperationException(
                $"Retry delay table length ({s_retryDelays.Length}) must equal MaxEtagRetries - 1 ({MaxEtagRetries - 1}).");
        }
    }

    /// <inheritdoc/>
    public async Task<ProjectionRebuildCheckpoint?> ReadAsync(
        ProjectionRebuildCheckpointScope scope,
        CancellationToken cancellationToken = default) {
        ValidateScope(scope);
        ProjectionRebuildCheckpoint? checkpoint = await daprClient
            .GetStateAsync<ProjectionRebuildCheckpoint>(
                options.Value.CheckpointStateStoreName,
                GetStateKey(scope),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (checkpoint is not null) {
            ValidateCheckpointScope(scope, checkpoint);
            // M17: persisted OperationId is read back into AdminOperationResult; reject malformed
            // ids so a tampered state-store row does not surface garbage to operators.
            if (!IsValidOperationId(checkpoint.OperationId)) {
                Log.CheckpointMalformedOperationId(logger, scope.Tenant, scope.Domain, scope.ProjectionName);
                return null;
            }
        }

        return checkpoint;
    }

    // P6: ULID validation per CLAUDE.md R2-A7. UniqueIdHelper.ToGuid uses a case-insensitive
    // Crockford-base32 regex internally, so this accepts both lowercase and uppercase ULIDs
    // that the rest of the system also accepts. The wasteful uppercase-clone allocation that
    // the prior implementation used has been removed.
    // P3-7P (pass-7): narrow catch filter from "all but OCE/OOM" to expected ULID-parse exception
    // types only. NullReferenceException or other programmer errors should propagate so they
    // surface real bugs instead of being silently classified as "invalid ULID".
    private static bool IsValidOperationId(string? operationId) {
        if (operationId is null) {
            return true;
        }

        if (operationId.Length != 26) {
            return false;
        }

        try {
            _ = UniqueIdHelper.ToGuid(operationId);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException) {
            // P17-8P (pass-8): include OverflowException in the catch filter. UniqueIdHelper.ToGuid
            // performs fixed-width Crockford-base32 parsing whose overflow path can throw
            // OverflowException for malformed inputs. Without this branch, malformed ULIDs surfaced
            // as 500 InternalError instead of "invalid-operation-id" 400.
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<ProjectionRebuildCheckpointSaveResult> SaveAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default,
        long? toPosition = null,
        bool isPerAggregateProgress = false) {
        ValidateScope(scope);
        ArgumentOutOfRangeException.ThrowIfNegative(lastAppliedSequence);

        string key = GetStateKey(scope);
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            // P17-6P: observe cancellation between retry iterations even when the prior
            // attempt did not throw (e.g., bare ETag conflict, !saved branch).
            cancellationToken.ThrowIfCancellationRequested();
            try {
                (ProjectionRebuildCheckpoint? existing, string etag) = await daprClient
                    .GetStateAndETagAsync<ProjectionRebuildCheckpoint>(
                        stateStoreName,
                        key,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (existing is not null) {
                    ValidateCheckpointScope(scope, existing);
                    // P-DEC6-5P (pass-5): per-aggregate progress writes bypass IsDifferentOperation
                    // because the operator-scope row is the single source of operator identity.
                    // Per-aggregate rows inherited the operator's OperationId at row creation; a
                    // subsequent operator B's Reset+Replay updates the operator-scope row's
                    // OperationId but the per-aggregate rows still carry A's. Without this carve-out
                    // B's per-aggregate writes would wedge forever (DEC7-4P).
                    // P16/DEC10/P-DEC3-5P: reject when the existing record carries an OperationId
                    // and the caller either presents a different one or omits one entirely. The
                    // pass-5 P-DEC3-5P extension removes the status filter so NotStarted rows are
                    // also protected (cheap defense-in-depth; mirror Reset-to-NotStarted race).
                    if (!isPerAggregateProgress && IsDifferentOperation(scope, existing)) {
                        Log.OperationConflict(logger, scope.Tenant, scope.Domain, scope.ProjectionName, existing.Status.ToString(), StreamReplayReasonCodes.OperationInFlight);
                        return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.OperationInFlight);
                    }

                    // P2/P14: Idempotent no-op ONLY when every observable field matches.
                    if (existing.LastAppliedSequence >= lastAppliedSequence
                        && existing.Status == status
                        && string.Equals(existing.FailureReasonCode, failureReasonCode, StringComparison.Ordinal)
                        && existing.ToPosition == toPosition) {
                        // P28-7P (pass-7 MEDIUM): skip the active-index round-trip on a true no-op
                        // (same status, same sequence, same metadata). The active-index should
                        // already reflect the existing status — the prior lifecycle write that
                        // produced `existing` was responsible for it. P-DEC1-8P
                        // ActiveRebuildIndexCleanupService is the explicit recovery path for any
                        // index/checkpoint divergence left behind by a partial prior write, so
                        // burning a GET/SET per poll-confirm tick here is wasteful.
                        // H11: surface the caller's OperationId (if any) instead of the existing
                        // operation's, so a no-op response does not leak a prior operator's id.
                        return ProjectionRebuildCheckpointSaveResult.Success(
                            string.IsNullOrWhiteSpace(scope.OperationId)
                                ? existing
                                : existing with { OperationId = scope.OperationId });
                    }

                    // H8-5P (pass-5): once IsDifferentOperation rejects same-OperationId terminal
                    // overwrites at the SaveAsync layer, an operator with a MATCHING OperationId
                    // could still flip Succeeded→Paused/Canceled and mutate audited terminal state.
                    // Reject all status changes against terminal records (only idempotent same-status
                    // no-op above is permitted). Operators wanting to start a fresh rebuild after a
                    // terminal record must route through ResetAsync (the documented trust boundary).
                    // C1-8P (= P27-7P, pass-7 MEDIUM): per-aggregate progress writes bypass the
                    // terminal-status guard. Migration-era / hand-edited per-aggregate rows with
                    // Succeeded would otherwise wedge the orchestrator's per-aggregate Save into
                    // CheckpointConflict → Interrupt() forever. The operator-scope row remains
                    // the single source of operator identity and is still terminal-protected.
                    if (!isPerAggregateProgress && IsTerminal(existing.Status)) {
                        Log.CheckpointLifecycleProtected(logger, scope.Tenant, scope.Domain, scope.ProjectionName, existing.Status.ToString(), status.ToString(), StreamReplayReasonCodes.CheckpointConflict);
                        return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointConflict);
                    }

                    // C4 + H7: SaveAsync must NOT regress lifecycle into Running from a terminal
                    // or operator-protected status. Only ResetAsync routes are permitted to flip
                    // these. Returning a typed failure lets the orchestrator stop cleanly without
                    // overwriting the operator's intent.
                    if (IsLifecycleProtected(existing.Status) && IsNonTerminalAdvancement(status)) {
                        string protectReason = existing.Status switch {
                            ProjectionRebuildStatus.Paused or ProjectionRebuildStatus.Pausing => StreamReplayReasonCodes.RebuildPaused,
                            ProjectionRebuildStatus.Canceled or ProjectionRebuildStatus.Canceling => StreamReplayReasonCodes.RebuildCanceled,
                            _ => StreamReplayReasonCodes.CheckpointConflict,
                        };
                        Log.CheckpointLifecycleProtected(logger, scope.Tenant, scope.Domain, scope.ProjectionName, existing.Status.ToString(), status.ToString(), protectReason);
                        return ProjectionRebuildCheckpointSaveResult.Failure(protectReason);
                    }

                    if (status == ProjectionRebuildStatus.Failed && lastAppliedSequence < existing.LastAppliedSequence) {
                        return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.StaleCheckpoint);
                    }
                }

                long monotonicSequence = Math.Max(existing?.LastAppliedSequence ?? 0, lastAppliedSequence);
                string? operationId = string.IsNullOrWhiteSpace(scope.OperationId)
                    ? existing?.OperationId
                    : scope.OperationId;
                var checkpoint = new ProjectionRebuildCheckpoint(
                    scope.Tenant,
                    scope.Domain,
                    scope.ProjectionName,
                    scope.AggregateId,
                    operationId,
                    monotonicSequence,
                    status,
                    DateTimeOffset.UtcNow,
                    failureReasonCode,
                    toPosition);

                // C4-5P (pass-5): Reverts pass-5-worsening — write checkpoint FIRST for ALL
                // statuses, then update the active-index on success only. The prior code wrote
                // the active-index BEFORE TrySaveStateAsync for active statuses; on ETag-retry-
                // exhaustion or CheckpointUnavailable the index entry persisted as a phantom and
                // HasActiveOperatorRebuildForDomainAsync returned true forever for (tenant, domain).
                bool saved = await daprClient
                    .TrySaveStateAsync(
                        stateStoreName,
                        key,
                        checkpoint,
                        etag,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (saved) {
                    string? activeIndexFailure = await UpdateActiveIndexForLifecycleAsync(scope, status, cancellationToken).ConfigureAwait(false);
                    if (activeIndexFailure is not null) {
                        // P13-6P: terminal-status (Succeeded/Failed/Canceled) checkpoint is already
                        // persisted at this point. Failing the SaveAsync call would leave the caller
                        // unsure whether the terminal write happened, AND the projection name would
                        // stay in the active-index forever, blocking the poller permanently. For
                        // terminal writes, treat the index-removal failure as best-effort: log
                        // loudly so operators can observe the gap, but return Success so the next
                        // lifecycle transition retries the index removal naturally. For active
                        // (Running/Pausing/Resuming/Retrying/Canceling) writes, the index must
                        // succeed (otherwise the poller cannot tell an operator rebuild is in
                        // flight), so propagate the failure.
                        if (IsTerminal(status)) {
                            Log.CheckpointNoOpIndexUpdateFailed(logger, scope.Tenant, scope.Domain, scope.ProjectionName, activeIndexFailure);
                            return ProjectionRebuildCheckpointSaveResult.Success(checkpoint);
                        }

                        return ProjectionRebuildCheckpointSaveResult.Failure(activeIndexFailure);
                    }

                    return ProjectionRebuildCheckpointSaveResult.Success(checkpoint);
                }

                Log.CheckpointConflict(logger, scope.Tenant, scope.Domain, scope.ProjectionName, scope.AggregateId ?? string.Empty, scope.OperationId ?? string.Empty, attempt + 1);

                // P18-6P: ETag conflict — short backoff before retrying so hot keys do not thrash.
                if (attempt < MaxEtagRetries - 1) {
                    await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception ex) when (IsStateStoreUnavailable(ex)) {
                Log.CheckpointUnavailable(logger, ex, scope.Tenant, scope.Domain, scope.ProjectionName, ex.GetType().Name);
                if (attempt >= MaxEtagRetries - 1) {
                    return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointUnavailable);
                }

                await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointConflict);
    }

    /// <inheritdoc/>
    public async Task<ProjectionRebuildCheckpointSaveResult> ResetAsync(
        ProjectionRebuildCheckpointScope scope,
        long lastAppliedSequence,
        ProjectionRebuildStatus status,
        string? failureReasonCode = null,
        CancellationToken cancellationToken = default,
        long? toPosition = null) {
        ValidateScope(scope);
        ArgumentOutOfRangeException.ThrowIfNegative(lastAppliedSequence);

        string key = GetStateKey(scope);
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            // P17-6P: observe cancellation between retry iterations.
            cancellationToken.ThrowIfCancellationRequested();
            try {
                (ProjectionRebuildCheckpoint? existing, string etag) = await daprClient
                    .GetStateAndETagAsync<ProjectionRebuildCheckpoint>(
                        stateStoreName,
                        key,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (existing is not null) {
                    ValidateCheckpointScope(scope, existing);
                    // DEC8: Reject when an active operation owned by a different operator is in
                    // flight. Reset is the trust boundary for terminal records, so terminal-record
                    // overwrites from a different OperationId remain allowed (sequential operator
                    // history). The active-vs-active conflict is the one we must rebuff.
                    if (IsLifecycleActive(existing.Status) && IsDifferentOperation(scope, existing)) {
                        Log.OperationConflict(logger, scope.Tenant, scope.Domain, scope.ProjectionName, existing.Status.ToString(), StreamReplayReasonCodes.OperationInFlight);
                        return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.OperationInFlight);
                    }
                }

                var checkpoint = new ProjectionRebuildCheckpoint(
                    scope.Tenant,
                    scope.Domain,
                    scope.ProjectionName,
                    scope.AggregateId,
                    string.IsNullOrWhiteSpace(scope.OperationId) ? existing?.OperationId : scope.OperationId,
                    lastAppliedSequence,
                    status,
                    DateTimeOffset.UtcNow,
                    failureReasonCode,
                    toPosition);

                bool saved = await daprClient
                    .TrySaveStateAsync(
                        stateStoreName,
                        key,
                        checkpoint,
                        etag,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (saved) {
                    string? activeIndexFailure = await UpdateActiveIndexForLifecycleAsync(scope, status, cancellationToken).ConfigureAwait(false);
                    if (activeIndexFailure is not null) {
                        // P13-6P: mirror SaveAsync — terminal Reset (rare; tests use Reset for Failed
                        // and Canceled-write paths) treats index removal as best-effort.
                        if (IsTerminal(status)) {
                            Log.CheckpointNoOpIndexUpdateFailed(logger, scope.Tenant, scope.Domain, scope.ProjectionName, activeIndexFailure);
                            return ProjectionRebuildCheckpointSaveResult.Success(checkpoint);
                        }

                        return ProjectionRebuildCheckpointSaveResult.Failure(activeIndexFailure);
                    }

                    return ProjectionRebuildCheckpointSaveResult.Success(checkpoint);
                }

                Log.CheckpointConflict(logger, scope.Tenant, scope.Domain, scope.ProjectionName, scope.AggregateId ?? string.Empty, scope.OperationId ?? string.Empty, attempt + 1);

                // P18-6P: short backoff between ETag retries.
                if (attempt < MaxEtagRetries - 1) {
                    await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception ex) when (IsStateStoreUnavailable(ex)) {
                Log.CheckpointUnavailable(logger, ex, scope.Tenant, scope.Domain, scope.ProjectionName, ex.GetType().Name);
                if (attempt >= MaxEtagRetries - 1) {
                    return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointUnavailable);
                }

                await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        return ProjectionRebuildCheckpointSaveResult.Failure(StreamReplayReasonCodes.CheckpointConflict);
    }

    /// <inheritdoc/>
    public async Task<bool> HasActiveOperatorRebuildForDomainAsync(
        string tenant,
        string domain,
        CancellationToken cancellationToken = default) {
        // P9-8P (pass-8): validate canonical key parts on tenant/domain to forbid the `:`
        // delimiter (and other reserved chars) at the API boundary. Without this, a malicious or
        // mis-configured caller could submit tenant="a:b" + domain="c" colliding with
        // tenant="a" + domain="b:c" in the process-local cache key — a cross-tenant fail-closed
        // poisoning vector.
        ValidateKeyPart(tenant, nameof(tenant));
        ValidateKeyPart(domain, nameof(domain));

        // P11-7P (pass-7): TTL cache short-circuit. If a recent fail-closed verdict is still valid
        // for this (tenant, domain), return true without re-querying the index store. Prevents
        // every poller tick across every aggregate in the domain from thrashing during a brief
        // store flap.
        // P2-8P (pass-8): capture request start time so a fail-closed write that races AFTER a
        // concurrent success does not overwrite the cleared state with stale verdict.
        DateTimeOffset requestStartedAt = DateTimeOffset.UtcNow;
        string cacheKey = string.Concat(tenant, ":", domain);
        if (s_failClosedCache.TryGetValue(cacheKey, out DateTimeOffset failClosedUntil)
            && failClosedUntil > requestStartedAt) {
            return true;
        }

        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            // P17-6P: observe cancellation between retry iterations.
            cancellationToken.ThrowIfCancellationRequested();
            try {
                string[]? activeProjections = await daprClient
                    .GetStateAsync<string[]>(
                        options.Value.CheckpointStateStoreName,
                        GetActiveIndexKey(tenant, domain),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // P11-7P (pass-7): success — drop any stale fail-closed cache entry so the next
                // poller tick uses live data.
                // P2-8P (pass-8): record the success timestamp so a concurrent fail-closed write
                // racing AFTER this success can detect it and skip its own write.
                s_lastReadSuccessAt[cacheKey] = DateTimeOffset.UtcNow;
                _ = s_failClosedCache.TryRemove(cacheKey, out _);
                return activeProjections is { Length: > 0 };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception ex) when (IsStateStoreUnavailable(ex)) {
                if (attempt >= MaxEtagRetries - 1) {
                    // Fail closed: when the index store cannot answer after bounded retries, assume
                    // an operator rebuild MAY be active so the poller skips delivery.
                    // P11-7P (pass-7): cache the fail-closed verdict for s_failClosedTtl so the
                    // next poller tick within the TTL short-circuits without re-querying.
                    Log.ActiveIndexReadFailed(logger, ex, tenant, domain, ex.GetType().Name);

                    // P2-8P (pass-8): only set the fail-closed cache if no successful read has
                    // landed since this request started. Otherwise we would overwrite a recent
                    // success with a stale fail-closed verdict, poisoning the cache for s_failClosedTtl.
                    if (!s_lastReadSuccessAt.TryGetValue(cacheKey, out DateTimeOffset lastSuccess)
                        || lastSuccess < requestStartedAt) {
                        s_failClosedCache[cacheKey] = DateTimeOffset.UtcNow + s_failClosedTtl;
                    }

                    return true;
                }

                await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<(string Tenant, string Domain)>> ListActiveRebuildIndexPairsAsync(
        CancellationToken cancellationToken = default) {
        string[]? activePairs = await daprClient
            .GetStateAsync<string[]>(
                options.Value.CheckpointStateStoreName,
                ActivePairIndexKey,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (activePairs is null || activePairs.Length == 0) {
            return [];
        }

        var pairs = new HashSet<(string Tenant, string Domain)>();
        foreach (string pair in activePairs) {
            if (TryDecodeActivePair(pair, out string? tenant, out string? domain)) {
                pairs.Add((tenant, domain));
            }
        }

        return [.. pairs];
    }

    /// <inheritdoc/>
    public async Task<int> ClearOrphanActiveRebuildIndexEntriesAsync(
        string tenant,
        string domain,
        CancellationToken cancellationToken = default) {
        // P-DEC1-8P (pass-8): canonical-key guard mirrors HasActiveOperatorRebuildForDomainAsync.
        ValidateKeyPart(tenant, nameof(tenant));
        ValidateKeyPart(domain, nameof(domain));

        string indexKey = GetActiveIndexKey(tenant, domain);
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                (string[]? existing, string etag) = await daprClient
                    .GetStateAndETagAsync<string[]>(
                        stateStoreName,
                        indexKey,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (existing is null || existing.Length == 0) {
                    return 0;
                }

                var orphans = new List<string>();
                var keep = new List<string>(existing.Length);
                foreach (string projectionName in existing) {
                    if (string.IsNullOrWhiteSpace(projectionName)) {
                        orphans.Add(projectionName ?? string.Empty);
                        continue;
                    }

                    ProjectionRebuildCheckpoint? checkpoint = await ReadAsync(
                            new ProjectionRebuildCheckpointScope(tenant, domain, projectionName, AggregateId: null, OperationId: null),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (checkpoint is null || IsTerminal(checkpoint.Status)) {
                        orphans.Add(projectionName);
                    }
                    else {
                        keep.Add(projectionName);
                    }
                }

                if (orphans.Count == 0) {
                    return 0;
                }

                var revalidatedOrphans = new List<string>(orphans.Count);
                foreach (string orphan in orphans) {
                    if (string.IsNullOrWhiteSpace(orphan)) {
                        revalidatedOrphans.Add(orphan);
                        continue;
                    }

                    ProjectionRebuildCheckpoint? checkpoint = await ReadAsync(
                            new ProjectionRebuildCheckpointScope(tenant, domain, orphan, AggregateId: null, OperationId: null),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (checkpoint is null || IsTerminal(checkpoint.Status)) {
                        revalidatedOrphans.Add(orphan);
                    }
                    else {
                        keep.Add(orphan);
                    }
                }

                if (revalidatedOrphans.Count == 0) {
                    return 0;
                }

                string[] next = [.. keep];
                bool saved = await daprClient
                    .TrySaveStateAsync(
                        stateStoreName,
                        indexKey,
                        next,
                        etag,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (saved) {
                    foreach (string orphan in revalidatedOrphans) {
                        Log.ActiveIndexOrphanCleared(logger, tenant, domain, orphan);
                    }

                    if (next.Length == 0) {
                        _ = await UpdateActivePairIndexAsync(tenant, domain, shouldExist: false, cancellationToken).ConfigureAwait(false);
                    }

                    return revalidatedOrphans.Count;
                }

                if (attempt < MaxEtagRetries - 1) {
                    await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                }
                else {
                    Log.ActiveIndexCleanupConflict(logger, tenant, domain, revalidatedOrphans.Count);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception ex) when (IsStateStoreUnavailable(ex)) {
                Log.ActiveIndexReadFailed(logger, ex, tenant, domain, ex.GetType().Name);
                if (attempt >= MaxEtagRetries - 1) {
                    return 0;
                }

                await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        return 0;
    }

    // D3-B: SaveAsync/ResetAsync write a per-(tenant, domain) index of active projection names.
    // The index is read by HasActiveOperatorRebuildForDomainAsync to determine whether any
    // operator rebuild is in flight for the (tenant, domain) pair, replacing the prior probe
    // that incorrectly assumed projectionName == domain.
    private async Task<string?> UpdateActiveIndexForLifecycleAsync(
        ProjectionRebuildCheckpointScope scope,
        ProjectionRebuildStatus status,
        CancellationToken cancellationToken) {
        bool active = IsLifecycleActive(status);
        bool terminal = IsTerminal(status);
        // P1-6P: NotStarted is NOT a deactivation. A Reset → NotStarted that proactively
        // removed the projection from the index opened a race window between the Reset
        // write and the subsequent Running write during which HasActiveOperatorRebuildForDomainAsync
        // returned false and the poller could race the rebuild. Only IsLifecycleActive
        // statuses add to the index; only terminal statuses remove from it. All other
        // transitions (NotStarted, no-op same-status) leave the index untouched.
        if (!active && !terminal) {
            return null;
        }

        string indexKey = GetActiveIndexKey(scope.Tenant, scope.Domain);
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            // P17-6P: observe cancellation between retry iterations even when the prior
            // attempt did not throw (e.g., bare ETag conflict, !saved branch).
            cancellationToken.ThrowIfCancellationRequested();
            try {
                (string[]? existing, string etag) = await daprClient
                    .GetStateAndETagAsync<string[]>(
                        stateStoreName,
                        indexKey,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var current = new HashSet<string>(existing ?? [], StringComparer.Ordinal);
                bool changed;
                if (active) {
                    changed = current.Add(scope.ProjectionName);
                }
                else {
                    changed = current.Remove(scope.ProjectionName);
                }

                if (!changed) {
                    // P16-7P (pass-7 MEDIUM): when terminal-remove sees the index as null (the read
                    // returned no entry), defensively assert the empty state via a fresh save. A
                    // concurrent path may have created an entry between our GET and this no-op
                    // return; that entry should not block the poller post-terminal. If the assert
                    // fails (ETag mismatch from a concurrent writer that just inserted), the next
                    // attempt re-reads and removes correctly.
                    if (!active && existing is null) {
                        bool savedAssert = await daprClient
                            .TrySaveStateAsync(
                                stateStoreName,
                                indexKey,
                                Array.Empty<string>(),
                                etag,
                                cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        if (savedAssert) {
                            return await UpdateActivePairIndexAsync(scope.Tenant, scope.Domain, shouldExist: false, cancellationToken).ConfigureAwait(false);
                        }

                        // ETag conflict: fall through to retry-loop tail (back off and re-read).
                        if (attempt < MaxEtagRetries - 1) {
                            await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                        }

                        continue;
                    }

                    if (active) {
                        bool savedAssert = await daprClient
                            .TrySaveStateAsync(
                                stateStoreName,
                                indexKey,
                                existing ?? current.ToArray(),
                                etag,
                                cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        if (savedAssert) {
                            return await UpdateActivePairIndexAsync(scope.Tenant, scope.Domain, shouldExist: true, cancellationToken).ConfigureAwait(false);
                        }

                        if (attempt < MaxEtagRetries - 1) {
                            await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                        }

                        continue;
                    }

                    if (terminal && existing is { Length: 0 }) {
                        return await UpdateActivePairIndexAsync(scope.Tenant, scope.Domain, shouldExist: false, cancellationToken).ConfigureAwait(false);
                    }

                    return null;
                }

                string[] next = [.. current];
                bool saved = await daprClient
                    .TrySaveStateAsync(
                        stateStoreName,
                        indexKey,
                        next,
                        etag,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (saved) {
                    return await UpdateActivePairIndexAsync(scope.Tenant, scope.Domain, shouldExist: active || next.Length > 0, cancellationToken).ConfigureAwait(false);
                }

                // P18-6P: ETag conflict — back off briefly before the next attempt so hot keys
                // do not thrash the state store. Mirrors the transient-store delay below.
                if (attempt < MaxEtagRetries - 1) {
                    await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception ex) when (IsStateStoreUnavailable(ex)) {
                Log.ActiveIndexWriteFailed(logger, ex, scope.Tenant, scope.Domain, scope.ProjectionName, ex.GetType().Name);
                if (attempt >= MaxEtagRetries - 1) {
                    return StreamReplayReasonCodes.CheckpointUnavailable;
                }

                await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        return StreamReplayReasonCodes.CheckpointConflict;
    }

    private async Task<string?> UpdateActivePairIndexAsync(
        string tenant,
        string domain,
        bool shouldExist,
        CancellationToken cancellationToken) {
        string pairKey = EncodeActivePair(tenant, domain);
        string stateStoreName = options.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                (string[]? existing, string etag) = await daprClient
                    .GetStateAndETagAsync<string[]>(
                        stateStoreName,
                        ActivePairIndexKey,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var current = new HashSet<string>(existing ?? [], StringComparer.Ordinal);
                bool changed = shouldExist
                    ? current.Add(pairKey)
                    : current.Remove(pairKey);
                if (!changed) {
                    return null;
                }

                bool saved = await daprClient
                    .TrySaveStateAsync(
                        stateStoreName,
                        ActivePairIndexKey,
                        current.ToArray(),
                        etag,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (saved) {
                    return null;
                }

                if (attempt < MaxEtagRetries - 1) {
                    await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception ex) when (IsStateStoreUnavailable(ex)) {
                Log.ActiveIndexWriteFailed(logger, ex, tenant, domain, "*", ex.GetType().Name);
                if (attempt >= MaxEtagRetries - 1) {
                    return StreamReplayReasonCodes.CheckpointUnavailable;
                }

                await Task.Delay(BoundedRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        return StreamReplayReasonCodes.CheckpointConflict;
    }

    internal static string GetStateKey(ProjectionRebuildCheckpointScope scope) {
        ArgumentNullException.ThrowIfNull(scope);
        return string.Concat(
            StateKeyPrefix,
            scope.Tenant,
            ":",
            scope.Domain,
            ":",
            scope.ProjectionName,
            ":",
            string.IsNullOrWhiteSpace(scope.AggregateId) ? "*" : scope.AggregateId);
    }

    internal static string GetActiveIndexKey(string tenant, string domain)
        => string.Concat(ActiveIndexKeyPrefix, tenant, ":", domain);

    internal static string GetActivePairIndexKey()
        => ActivePairIndexKey;

    private static string EncodeActivePair(string tenant, string domain)
        => string.Concat(tenant, ":", domain);

    private static bool TryDecodeActivePair(string value, out string tenant, out string domain) {
        tenant = string.Empty;
        domain = string.Empty;
        int separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator >= value.Length - 1) {
            return false;
        }

        tenant = value[..separator];
        domain = value[(separator + 1)..];
        try {
            ValidateKeyPart(tenant, nameof(tenant));
            ValidateKeyPart(domain, nameof(domain));
            return true;
        }
        catch (ArgumentException) {
            tenant = string.Empty;
            domain = string.Empty;
            return false;
        }
    }

    private static void ValidateScope(ProjectionRebuildCheckpointScope scope) {
        ArgumentNullException.ThrowIfNull(scope);
        ValidateKeyPart(scope.Tenant, nameof(scope.Tenant));
        ValidateKeyPart(scope.Domain, nameof(scope.Domain));
        ValidateKeyPart(scope.ProjectionName, nameof(scope.ProjectionName));
        if (scope.AggregateId is not null) {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope.AggregateId, nameof(scope.AggregateId));
            ValidateKeyPart(scope.AggregateId, nameof(scope.AggregateId));
        }

        if (!string.IsNullOrWhiteSpace(scope.OperationId)) {
            ValidateKeyPart(scope.OperationId, nameof(scope.OperationId));
            if (!IsValidOperationId(scope.OperationId)) {
                throw new ArgumentException("OperationId must be a valid Crockford-base32 ULID and must not contain I, L, O, or U.", nameof(scope.OperationId));
            }
        }
    }

    private static void ValidateKeyPart(string value, string parameterName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.AsSpan().IndexOfAny(s_reservedChars) >= 0) {
            throw new ArgumentException(
                $"{parameterName} must not contain ':', '\\0', '|', '*', '\\r', or '\\n'.",
                parameterName);
        }
    }

    // M1: add '*' to reserved chars. The state-key derivation collapses null AggregateId to "*";
    // an aggregateId literally equal to "*" would have collided with the domain-wide key.
    private static readonly System.Buffers.SearchValues<char> s_reservedChars = System.Buffers.SearchValues.Create(":\0|\r\n*");

    private static bool IsLifecycleProtected(ProjectionRebuildStatus status)
        => status is ProjectionRebuildStatus.Paused
            or ProjectionRebuildStatus.Pausing
            or ProjectionRebuildStatus.Canceled
            or ProjectionRebuildStatus.Canceling
            or ProjectionRebuildStatus.Failed
            or ProjectionRebuildStatus.Succeeded;

    private static bool IsNonTerminalAdvancement(ProjectionRebuildStatus status)
        => status is ProjectionRebuildStatus.Running
            or ProjectionRebuildStatus.Resuming
            or ProjectionRebuildStatus.NotStarted
            or ProjectionRebuildStatus.Retrying;

    private static bool IsLifecycleActive(ProjectionRebuildStatus status)
        => status is ProjectionRebuildStatus.Running
            or ProjectionRebuildStatus.Pausing
            or ProjectionRebuildStatus.Paused
            or ProjectionRebuildStatus.Resuming
            or ProjectionRebuildStatus.Retrying
            or ProjectionRebuildStatus.Canceling;

    private static bool IsTerminal(ProjectionRebuildStatus status)
        => status is ProjectionRebuildStatus.Succeeded
            or ProjectionRebuildStatus.Failed
            or ProjectionRebuildStatus.Canceled;

    // P16/DEC10: a different operation is one where the existing record carries an OperationId
    // and the caller's scope either omits one or supplies a different one. The asymmetric-null
    // form covered the "both populated" case only; this form also catches poller (no OpId)
    // racing operator (fresh OpId) and operator-vs-operator on terminal records.
    private static bool IsDifferentOperation(
        ProjectionRebuildCheckpointScope scope,
        ProjectionRebuildCheckpoint existing) {
        if (string.IsNullOrWhiteSpace(existing.OperationId)) {
            // Migration-era checkpoint rows may not have OperationId. Treat them as legacy
            // progress-only rows so an operator can take ownership through the next lifecycle write.
            return false;
        }

        return string.IsNullOrWhiteSpace(scope.OperationId)
            || !string.Equals(existing.OperationId, scope.OperationId, StringComparison.Ordinal);
    }

    private static void ValidateCheckpointScope(ProjectionRebuildCheckpointScope scope, ProjectionRebuildCheckpoint checkpoint) {
        if (!string.Equals(checkpoint.Tenant, scope.Tenant, StringComparison.Ordinal)
            || !string.Equals(checkpoint.Domain, scope.Domain, StringComparison.Ordinal)
            || !string.Equals(checkpoint.ProjectionName, scope.ProjectionName, StringComparison.Ordinal)
            || !string.Equals(checkpoint.AggregateId, scope.AggregateId, StringComparison.Ordinal)) {
            throw new InvalidOperationException("Projection rebuild checkpoint scope does not match the requested scope.");
        }
    }

    private static bool IsStateStoreUnavailable(Exception exception)
        => IsStateStoreUnavailable(exception, depth: 0, parentIsTransport: false);

    // P18: bound is exclusive; depth==0..MaxExceptionFrames-1 are examined (== MaxExceptionFrames frames).
    // Renamed from MaxExceptionUnwindDepth to make the constant name reflect actual behavior.
    private const int MaxExceptionFrames = 8;

    // P1-8P (pass-8): align classification policy with StreamsController.IsServiceUnavailable.
    // Previously this method classified raw `IOException` and `TaskCanceledException` as transient
    // at depth 0, AND recursed through arbitrary InnerException chains. That broke parity with the
    // controller (a wrapped `FileNotFoundException` from the state-store call became 503 here
    // versus 500 in the controller). The narrowed behavior:
    //   * `DaprException`, `HttpRequestException`, `SocketException` are transient at any depth.
    //   * `TaskCanceledException` is transient when caller cancellation was not requested; the
    //     explicit OCE catch blocks above preserve requested cancellation.
    //   * `IOException` is transient ONLY when wrapped under a known transport exception.
    //   * `TimeoutException` is transient ONLY when its immediate parent is a transport frame.
    //   * Recursion follows ONLY `AggregateException.InnerExceptions` — arbitrary intermediate
    //     `InvalidOperationException(inner: DaprException)` no longer masks application bugs as
    //     503. M13-5P pass-5 established this rule for the controller; P1-8P pass-8 extends it
    //     to the store.
    private static bool IsStateStoreUnavailable(Exception exception, int depth, bool parentIsTransport) {
        if (depth >= MaxExceptionFrames) {
            return false;
        }

        if (exception is DaprException or HttpRequestException or System.Net.Sockets.SocketException) {
            return true;
        }

        if (exception is TaskCanceledException) {
            return true;
        }

        if (exception is OperationCanceledException) {
            // P2-6P / controller parity: OCE-derived frames are not transient; cancellation
            // propagation is handled by the explicit `catch (OperationCanceledException) when (
            // cancellationToken.IsCancellationRequested) { throw; }` blocks above. Non-requested
            // TaskCanceledException is handled above as a transport timeout.
            return false;
        }

        // TimeoutException is transient ONLY when immediately wrapped under a confirmed transport
        // frame. Bare TimeoutException at the top level is treated as a programmer/data error so it
        // surfaces as 500 InternalError rather than masking application bugs as 503.
        if (exception is TimeoutException) {
            return parentIsTransport;
        }

        // IOException at depth > 0 (wrapped under a transport exception) remains transient, but at
        // depth 0 (raw IOException) surfaces as application failure. Mirrors controller P13-7P.
        if (exception is IOException && parentIsTransport) {
            return true;
        }

        // Only AggregateException recursion is permitted (P1-8P pass-8 alignment with controller).
        if (exception is AggregateException aggregateException) {
            foreach (Exception inner in aggregateException.InnerExceptions) {
                if (IsStateStoreUnavailable(inner, depth + 1, parentIsTransport: false)) {
                    return true;
                }
            }
        }

        return false;
    }

    // P12-7P / P32-7P (pass-7): add ±25% jitter to bounded retry delays so concurrent writers on
    // the same hot key (active-index ETag conflict) do not thundering-herd in lockstep.
    // P14-8P (pass-8): removed the Math.Min(attempt, s_retryDelays.Length - 1) clamp. The
    // static constructor invariant `s_retryDelays.Length == MaxEtagRetries - 1` and the call-site
    // invariant `attempt < MaxEtagRetries - 1` jointly guarantee the index is in range. The clamp
    // was masking a potential contract violation; without it, any future drift surfaces immediately
    // as IndexOutOfRangeException at first invocation.
    private static TimeSpan BoundedRetryDelay(int attempt) {
        TimeSpan baseDelay = s_retryDelays[attempt];
        int totalMs = (int)baseDelay.TotalMilliseconds;
        int jitterRangeMs = Math.Max(1, totalMs / 4);
        int signedJitter = System.Random.Shared.Next(-jitterRangeMs, jitterRangeMs + 1);
        return TimeSpan.FromMilliseconds(Math.Max(1, totalMs + signedJitter));
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1190,
            Level = LogLevel.Debug,
            Message = "Projection rebuild checkpoint ETag conflict: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, AggregateId={AggregateId}, OperationId={OperationId}, Attempt={Attempt}, Stage=ProjectionRebuildCheckpointConflict")]
        public static partial void CheckpointConflict(
            ILogger logger,
            string tenantId,
            string domain,
            string projectionName,
            string aggregateId,
            string operationId,
            int attempt);

        [LoggerMessage(
            EventId = 1191,
            Level = LogLevel.Warning,
            Message = "Projection rebuild checkpoint store unavailable: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, ExceptionType={ExceptionType}, Stage=ProjectionRebuildCheckpointUnavailable")]
        public static partial void CheckpointUnavailable(
            ILogger logger,
            Exception exception,
            string tenantId,
            string domain,
            string projectionName,
            string exceptionType);

        [LoggerMessage(
            EventId = 1192,
            Level = LogLevel.Warning,
            Message = "Projection rebuild checkpoint lifecycle write rejected: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, ExistingStatus={ExistingStatus}, AttemptedStatus={AttemptedStatus}, ReasonCode={ReasonCode}, Stage=ProjectionRebuildLifecycleProtected")]
        public static partial void CheckpointLifecycleProtected(
            ILogger logger,
            string tenantId,
            string domain,
            string projectionName,
            string existingStatus,
            string attemptedStatus,
            string reasonCode);

        // P26-7P (pass-7 MEDIUM): elevated to Error. A malformed persisted OperationId signals
        // a corrupt state-store row that an operator must remediate; orchestrator and poller
        // both treat the row as "no checkpoint", so any active-index entry for this projection
        // also leaks. Operators need this to escalate above routine warnings.
        [LoggerMessage(
            EventId = 1193,
            Level = LogLevel.Error,
            Message = "Projection rebuild checkpoint malformed operation id discarded: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, Stage=ProjectionRebuildCheckpointMalformedOperationId")]
        public static partial void CheckpointMalformedOperationId(
            ILogger logger,
            string tenantId,
            string domain,
            string projectionName);

        [LoggerMessage(
            EventId = 1194,
            Level = LogLevel.Warning,
            Message = "Projection rebuild checkpoint operation conflict: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, ExistingStatus={ExistingStatus}, ReasonCode={ReasonCode}, Stage=ProjectionRebuildOperationConflict")]
        public static partial void OperationConflict(
            ILogger logger,
            string tenantId,
            string domain,
            string projectionName,
            string existingStatus,
            string reasonCode);

        [LoggerMessage(
            EventId = 1196,
            Level = LogLevel.Warning,
            Message = "Projection rebuild active-index read failed: TenantId={TenantId}, Domain={Domain}, ExceptionType={ExceptionType}, Stage=ProjectionRebuildActiveIndexReadFailed")]
        public static partial void ActiveIndexReadFailed(
            ILogger logger,
            Exception exception,
            string tenantId,
            string domain,
            string exceptionType);

        [LoggerMessage(
            EventId = 1197,
            Level = LogLevel.Warning,
            Message = "Projection rebuild active-index write failed: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, ExceptionType={ExceptionType}, Stage=ProjectionRebuildActiveIndexWriteFailed")]
        public static partial void ActiveIndexWriteFailed(
            ILogger logger,
            Exception exception,
            string tenantId,
            string domain,
            string projectionName,
            string exceptionType);

        [LoggerMessage(
            EventId = 1198,
            Level = LogLevel.Warning,
            Message = "Projection rebuild checkpoint no-op active-index update failed: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, ReasonCode={ReasonCode}, Stage=ProjectionRebuildCheckpointNoOpIndexUpdateFailed")]
        public static partial void CheckpointNoOpIndexUpdateFailed(
            ILogger logger,
            string tenantId,
            string domain,
            string projectionName,
            string reasonCode);

        // P-DEC1-8P (pass-8): one log per orphan index entry cleared by the cleanup service.
        [LoggerMessage(
            EventId = 1199,
            Level = LogLevel.Warning,
            Message = "Projection rebuild active-index orphan cleared by cleanup service: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, Stage=ProjectionRebuildActiveIndexOrphanCleared")]
        public static partial void ActiveIndexOrphanCleared(
            ILogger logger,
            string tenantId,
            string domain,
            string projectionName);

        [LoggerMessage(
            EventId = 1205,
            Level = LogLevel.Warning,
            Message = "Projection rebuild active-index cleanup ETag conflict: TenantId={TenantId}, Domain={Domain}, OrphanCount={OrphanCount}, Stage=ProjectionRebuildActiveIndexCleanupConflict")]
        public static partial void ActiveIndexCleanupConflict(
            ILogger logger,
            string tenantId,
            string domain,
            int orphanCount);
    }
}
