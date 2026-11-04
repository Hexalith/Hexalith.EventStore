using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>CAS-serialized prepare/ready/commit/rollback authority with durable, non-expiring dispatch leases.</summary>
public sealed class RouteCatalogAuthority(IRouteCatalogStateStore store, IRouteCatalogTrustValidator trust,
    IOptions<RouteCatalogHostOptions> options, IRouteCatalogHostObserver? observer = null, IRouteCatalogQuiescenceVerifier? quiescenceVerifier = null) : IRouteCatalogAuthority
{
    // Admission is locally queued only until its fence is recorded. Remote dispatch and all
    // lifecycle/verifier work run outside this gate and retain their independent CAS checks.
    private readonly SemaphoreSlim _localAdmission = new(1, 1);
    private const int MaximumUpdateAttempts = 96;
    private readonly ILogger<RouteCatalogAuthority>? _logger;

    /// <summary>Uses optional host logging without changing the existing public constructor or acquisition authority.</summary>
    internal RouteCatalogAuthority(IRouteCatalogStateStore store, IRouteCatalogTrustValidator trust,
        IOptions<RouteCatalogHostOptions> options, IRouteCatalogHostObserver? observer,
        IRouteCatalogQuiescenceVerifier? quiescenceVerifier, ILogger<RouteCatalogAuthority>? logger)
        : this(store, trust, options, observer, quiescenceVerifier) => _logger = logger;

    /// <inheritdoc/>
    public async Task PrepareAsync(RouteCatalogActivation candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        candidate = Freeze(candidate);
        await ValidateAsync(candidate, cancellationToken).ConfigureAwait(false);
        await UpdateAsync(state =>
        {
            if (state.Pending is { } pending)
            {
                if (RouteCatalogActivationBinding.ComputeDigest(pending) == RouteCatalogActivationBinding.ComputeDigest(candidate)) return state;
                throw new RouteCatalogUnavailableException("Another activation is already prepared.");
            }
            if (state.Attempts.Any(a => a.AttemptId == candidate.AttemptId))
                throw new RouteCatalogUnavailableException("An activation attempt identity is immutable and cannot be reused after completion.");
            if (candidate.PredecessorRootDigest != state.Committed?.RootDigest
                || (state.Committed is { } committed && Catalog(candidate).Generation <= Catalog(committed).Generation)
                || candidate.AttemptId == state.Committed?.AttemptId || candidate.AttemptId == state.Previous?.AttemptId)
                throw new RouteCatalogUnavailableException("Stale predecessor, generation or activation attempt.");
            return state with { Pending = candidate, Ready = [], Attempts = [.. state.Attempts, new(candidate.AttemptId, RouteCatalogActivationBinding.ComputeDigest(candidate))] };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<RouteCatalogHostAttestation> ReadyAsync(string attemptId, CancellationToken cancellationToken = default)
    {
        RouteCatalogHostAttestation? receipt = null;
        await UpdateAsync(async state =>
        {
            RouteCatalogActivation pending = RequirePending(state, attemptId);
            await ValidateAsync(pending, cancellationToken).ConfigureAwait(false);
            if (state.Leases.Count != 0) throw new RouteCatalogUnavailableException("Prior dispatch is still draining.", RouteCatalogRefusalReason.Draining);
            RouteCatalogEnvelope envelope = Catalog(pending);
            RouteCatalogHostIdentity host = options.Value.Identity!;
            receipt = new(attemptId, RouteCatalogActivationBinding.ComputeDigest(pending), envelope.Generation,
                pending.RootDigest, envelope.RouteFacetDigest, envelope.IdempotencyFacetDigest, envelope.Profile, host);
            return state with { Ready = [.. state.Ready.Where(r => r.Host.HostId != host.HostId), receipt] };
        }, cancellationToken).ConfigureAwait(false);
        return receipt!;
    }

    /// <inheritdoc/>
    public async Task CommitAsync(string attemptId, CancellationToken cancellationToken = default)
    {
        await UpdateAsync(async state =>
        {
            if (state.Pending is null && state.Committed?.AttemptId == attemptId)
            {
                await ValidateAsync(state.Committed, cancellationToken).ConfigureAwait(false);
                return state;
            }
            RouteCatalogActivation pending = RequirePending(state, attemptId);
            await ValidateAsync(pending, cancellationToken).ConfigureAwait(false);
            string binding = RouteCatalogActivationBinding.ComputeDigest(pending);
            RouteCatalogEnvelope envelope = Catalog(pending);
            if (state.Leases.Count != 0 || state.Ready.Count != pending.RequiredHosts.Count
                || pending.RequiredHosts.Any(host => !state.Ready.Any(r => r.Host == host && r.BindingDigest == binding
                    && r.AttemptId == pending.AttemptId && r.Generation == envelope.Generation && r.RootDigest == pending.RootDigest
                    && r.RouteFacetDigest == envelope.RouteFacetDigest && r.IdempotencyFacetDigest == envelope.IdempotencyFacetDigest && r.Profile == envelope.Profile))
                || pending.PredecessorRootDigest != state.Committed?.RootDigest)
                throw new RouteCatalogUnavailableException("The complete required host set has not agreed on this candidate.");
            return state with { Epoch = checked(state.Epoch + 1), Previous = state.Committed,
                Committed = pending, Pending = null, Ready = [] };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(RouteCatalogRollbackRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await UpdateAsync(async state =>
        {
            if (state.CompletedTransitions.Contains(request)) return state;
            if (state.Epoch != request.ExpectedEpoch)
                throw new RouteCatalogUnavailableException("The lifecycle transition epoch has changed.");
            if (request.Phase == "pending")
            {
                RouteCatalogActivation pending = RequirePending(state, request.AttemptId);
                if (RouteCatalogActivationBinding.ComputeDigest(pending) != request.BindingDigest)
                    throw new RouteCatalogUnavailableException("The pending lifecycle binding differs.");
                return state with { Pending = null, Ready = [], CompletedTransitions = [.. state.CompletedTransitions, request] };
            }
            if (request.Phase != "committed" || state.Pending is not null || state.Committed is not { } committed
                || committed.AttemptId != request.AttemptId || RouteCatalogActivationBinding.ComputeDigest(committed) != request.BindingDigest
                || state.Previous is not { } previous || state.Leases.Count != 0)
                throw new RouteCatalogUnavailableException("Rollback requires the exact drained committed attempt and retained complete predecessor.");
            await ValidateAsync(previous, cancellationToken).ConfigureAwait(false);
            return state with { Epoch = checked(state.Epoch + 1), Committed = previous, Previous = null, Ready = [],
                CompletedTransitions = [.. state.CompletedTransitions, request] };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ReconcileAsync(RouteCatalogQuiescenceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await UpdateAsync(async state =>
        {
            RouteCatalogActivation committed = state.Committed ?? throw new RouteCatalogUnavailableException("No committed generation can verify quiescence.");
            if (state.Pending is { } successor)
            {
                _ = RequirePending(state, successor.AttemptId);
                // The old fleet can be genuinely stopped, with different physical coordinates
                // from the independently prepared successor. Never relabel its old identities
                // as current. Require the real successor and retain the old owner's trust too.
                if (successor.PredecessorRootDigest != committed.RootDigest || Catalog(successor).Generation <= Catalog(committed).Generation
                    || Catalog(committed).Profile != options.Value.Profile)
                    throw new RouteCatalogUnavailableException("Quiescence requires the exact trusted committed predecessor of this successor.");
                await ValidateAsync(successor, cancellationToken).ConfigureAwait(false);
                await trust.ValidateAsync(committed, RouteCatalogActivationBinding.ComputeDigest(committed), cancellationToken).ConfigureAwait(false);
            }
            else await ValidateAsync(committed, cancellationToken).ConfigureAwait(false);
            RouteCatalogDispatchFence fence = state.Leases.SingleOrDefault(l => l.LeaseId == request.LeaseId)
                ?? throw new RouteCatalogUnavailableException("The exact durable dispatch fence does not exist.");
            if (quiescenceVerifier is null || string.IsNullOrWhiteSpace(request.Proof)
                || fence.RootDigest != request.RootDigest || fence.Epoch != request.Epoch
                || committed.RootDigest != fence.RootDigest || state.Epoch != fence.Epoch
                || !committed.RequiredHosts.Any(host => host.HostId == fence.HostId))
                throw new RouteCatalogUnavailableException("Owner-verified evidence for this exact dispatch and generation is required.");
            await quiescenceVerifier.VerifyAsync(request, fence, cancellationToken).ConfigureAwait(false);
            return state with { Leases = [.. state.Leases.Where(l => l.LeaseId != fence.LeaseId)] };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<RouteCatalogDispatchLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        DispatchDiagnostics? diagnostics = DispatchDiagnostics.Create(_logger, cancellationToken);
        using (var stage = diagnostics?.Start(DispatchStageName.CatalogAdmissionWait))
        {
            await _localAdmission.WaitAsync(cancellationToken).ConfigureAwait(false);
            stage?.Complete();
        }
        try { return await AcquireCoreAsync(cancellationToken, diagnostics).ConfigureAwait(false); }
        finally { _localAdmission.Release(); }
    }

    private async Task<RouteCatalogDispatchLease> AcquireCoreAsync(CancellationToken cancellationToken, DispatchDiagnostics? diagnostics)
    {
        RequireLocalIdentity();
        RouteCatalogActivation? captured = null;
        long epoch = 0;
        string? capturedAppId = null;
        RouteCatalogStateCoordinates? capturedCoordinates = null;
        string leaseId = Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId();
        for (int attempt = 0; attempt < MaximumUpdateAttempts; attempt++)
        {
            (RouteCatalogActivationState state, _) = await ReadAdmissionStateAsync(cancellationToken, diagnostics).ConfigureAwait(false);
            bool retainedLease = state.Leases.Any(fence => fence.LeaseId == leaseId);
            if ((!retainedLease && state.Pending is not null) || state.Committed is null)
                throw new RouteCatalogUnavailableException("No complete catalog generation is serving.");
            // Own the complete candidate before awaited observations. A store/observer alias must
            // never allow a later authority change to inherit validation of the original bytes.
            RouteCatalogActivation candidate = Freeze(state.Committed);
            RouteCatalogHostIdentity identity = options.Value.Identity!;
            string profile = options.Value.Profile;
            RouteCatalogStateCoordinates? coordinates = store.Coordinates;
            RouteCatalogActivation observed = Freeze(candidate);
            await ValidateAsync(observed, cancellationToken, diagnostics).ConfigureAwait(false);
            if (!SameAdmissionActivation(candidate, observed))
                throw new RouteCatalogUnavailableException("The observed catalog activation changed during admission.");
            RequireLocalIdentity();
            if (options.Value.Identity != identity || options.Value.Profile != profile || store.Coordinates != coordinates)
                throw new RouteCatalogUnavailableException("The local catalog authority changed during admission.");
            if (retainedLease)
            {
                RequireAdmissionFence(state, leaseId, identity, candidate);
                captured = candidate;
                epoch = state.Epoch;
                capturedAppId = identity.AppId;
                capturedCoordinates = coordinates;
                break;
            }
            // Lease-only writers may have advanced the inventory while the expensive fresh owner
            // capture ran. Rebase only onto an independently read, exactly unchanged authority.
            (RouteCatalogActivationState fresh, string etag) = await ReadAdmissionStateAsync(cancellationToken, diagnostics).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (options.Value.Identity != identity || options.Value.Profile != profile || store.Coordinates != coordinates)
                throw new RouteCatalogUnavailableException("The local catalog authority changed during admission.");
            bool sameAuthority = fresh.Epoch == state.Epoch && fresh.Committed is { } committed
                && SameAdmissionActivation(candidate, Freeze(committed));
            if (sameAuthority && fresh.Leases.Any(fence => fence.LeaseId == leaseId))
            {
                // Recover an already durable admission even if a successor began draining it.
                // This creates no fence and retains the original response-loss cleanup path.
                RequireAdmissionFence(fresh, leaseId, identity, candidate);
                captured = candidate;
                epoch = fresh.Epoch;
                capturedAppId = identity.AppId;
                capturedCoordinates = coordinates;
                break;
            }
            if (fresh.Pending is not null)
                throw new RouteCatalogUnavailableException("No complete catalog generation is serving.");
            if (sameAuthority)
            {
                long sequence = checked(fresh.LastLease + 1);
                if (fresh.Leases.Count >= 4096)
                    throw new RouteCatalogUnavailableException("The durable dispatch lease inventory requires draining.");
                cancellationToken.ThrowIfCancellationRequested();
                if (options.Value.Identity != identity || options.Value.Profile != profile || store.Coordinates != coordinates)
                    throw new RouteCatalogUnavailableException("The local catalog authority changed during admission.");
                RouteCatalogActivationState next = fresh with
                {
                    LastLease = sequence,
                    Leases = [.. fresh.Leases, new RouteCatalogDispatchFence(sequence, leaseId, identity.HostId, candidate.RootDigest, fresh.Epoch)],
                };
                bool exchanged;
                using (var stage = diagnostics?.Start(DispatchStageName.CatalogCompareExchange))
                {
                    exchanged = await store.CompareExchangeAsync(next, etag, cancellationToken).ConfigureAwait(false);
                    stage?.Complete();
                }
                if (exchanged)
                {
                    captured = candidate;
                    epoch = fresh.Epoch;
                    capturedAppId = identity.AppId;
                    capturedCoordinates = coordinates;
                    break;
                }
            }
            if (attempt < MaximumUpdateAttempts - 1)
                await Task.Delay(Random.Shared.Next(1, Math.Min((attempt + 1) * 4, 50) + 1), cancellationToken).ConfigureAwait(false);
        }
        if (captured is null)
            throw new RouteCatalogUnavailableException("Concurrent catalog authority updates could not be serialized.");
        using var snapshotStage = diagnostics?.Start(DispatchStageName.CatalogSnapshot);
        var lease = new RouteCatalogDispatchLease(new(captured!, epoch, capturedAppId, capturedCoordinates), () => ReleaseAsync(leaseId))
        {
            Activation = captured,
        };
        snapshotStage?.Complete();
        return lease;
    }

    private async Task<(RouteCatalogActivationState State, string ETag)> ReadAdmissionStateAsync(CancellationToken cancellationToken, DispatchDiagnostics? diagnostics)
    {
        using var stage = diagnostics?.Start(DispatchStageName.CatalogStateRead);
        var result = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        stage?.Complete();
        return result;
    }

    private static bool SameAdmissionActivation(RouteCatalogActivation validated, RouteCatalogActivation current)
        => validated.RootDigest == current.RootDigest
            && string.Equals(validated.TrustProof, current.TrustProof, StringComparison.Ordinal)
            && validated.CanonicalCatalog.AsSpan().SequenceEqual(current.CanonicalCatalog)
            && RouteCatalogActivationBinding.ComputeDigest(validated) == RouteCatalogActivationBinding.ComputeDigest(current);

    private static void RequireAdmissionFence(RouteCatalogActivationState state, string leaseId, RouteCatalogHostIdentity identity, RouteCatalogActivation activation)
    {
        RouteCatalogDispatchFence[] own = [.. state.Leases.Where(fence => fence.LeaseId == leaseId)];
        if (own.Length != 1 || own[0].HostId != identity.HostId || own[0].RootDigest != activation.RootDigest
            || own[0].Epoch != state.Epoch || own[0].Sequence > state.LastLease)
            throw new RouteCatalogUnavailableException("The retained dispatch fence differs from the validated authority.");
    }

    /// <summary>Rechecks independently observed local facts for a native read under its existing durable lease.</summary>
    internal async Task ValidateNativeReadLeaseAsync(RouteCatalogDispatchLease lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        lease.RequireOpenForExecution();
        if (lease.Activation is not { } activation
            || lease.Snapshot.AuthorityCoordinates != store.Coordinates
            || lease.Snapshot.LocalAppId != options.Value.Identity?.AppId
            || lease.Snapshot.RootDigest != activation.RootDigest)
            throw new RouteCatalogUnavailableException("The native read scope differs from its acquired local authority.");
        await ValidateAsync(activation, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<RouteCatalogGenerationReadback> ReadbackAsync(CancellationToken cancellationToken = default)
    {
        RequireLocalIdentity();
        (RouteCatalogActivationState state, _) = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        RouteCatalogActivation committed = state.Committed ?? throw new RouteCatalogUnavailableException("No catalog has been activated.");
        await ValidateAsync(committed, cancellationToken).ConfigureAwait(false);
        RouteCatalogEnvelope envelope = Catalog(committed);
        return new(state.Epoch, envelope.Generation, committed.RootDigest, envelope.RouteFacetDigest,
            envelope.IdempotencyFacetDigest, envelope.Profile, Array.AsReadOnly(committed.RequiredHosts.ToArray()), state.Pending is null && state.Leases.Count < 4096)
        {
            AttemptId = committed.TechnicalHttpReceivers is { Count: > 0 } ? committed.AttemptId : null,
            ActivationBindingDigest = committed.TechnicalHttpReceivers is { Count: > 0 } ? RouteCatalogActivationBinding.ComputeDigest(committed) : null,
        };
    }

    private async Task ReleaseAsync(string leaseId)
    {
        // Only this completed local lease is removed. CAS contention cannot turn known
        // quiescence into an abandoned fence; backend failures still propagate for retry.
        // Release writes share the local admission gate with acquisitions, so this
        // host does not manufacture avoidable conflicts against its own new reads.
        await _localAdmission.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            int conflicts = 0;
            while (true)
            {
                (RouteCatalogActivationState state, string etag) = await store.ReadAsync(CancellationToken.None).ConfigureAwait(false);
                if (!state.Leases.Any(lease => lease.LeaseId == leaseId)) return;
                var next = state with { Leases = [.. state.Leases.Where(lease => lease.LeaseId != leaseId)] };
                if (await store.CompareExchangeAsync(next, etag, CancellationToken.None).ConfigureAwait(false)) return;
                await Task.Delay(Math.Min(++conflicts, 25), CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally { _localAdmission.Release(); }
    }

    private async Task ValidateAsync(RouteCatalogActivation activation, CancellationToken cancellationToken, DispatchDiagnostics? diagnostics = null)
    {
        RequireLocalIdentity();
        string binding;
        using (var stage = diagnostics?.Start(DispatchStageName.CatalogActivationDigest))
        {
            binding = RouteCatalogActivationBinding.ComputeDigest(activation);
            stage?.Complete();
        }
        RouteCatalogHostOptions local = options.Value;
        if (local.Identity is null || Catalog(activation).Profile != local.Profile
            || !activation.RequiredHosts.Contains(local.Identity))
            throw new RouteCatalogUnavailableException("Local host inventory, topology, authentication or profile disagrees with the exact required host set.");
        using (var stage = diagnostics?.Start(DispatchStageName.CatalogHostCapture))
        {
            if (store.Coordinates is not { } coordinates || observer is null
                || await observer.ObserveAsync(activation, coordinates, cancellationToken).ConfigureAwait(false) != local.Identity)
                throw new RouteCatalogUnavailableException("Configured host identity lacks matching independent registration, topology and authentication observations.", RouteCatalogRefusalReason.Inventory);
            stage?.Complete();
        }
        using (var stage = diagnostics?.Start(DispatchStageName.CatalogTrust))
        {
            await trust.ValidateAsync(activation, binding, cancellationToken).ConfigureAwait(false);
            stage?.Complete();
        }
    }

    private void RequireLocalIdentity()
    {
        if (options.Value.Identity is null || string.IsNullOrWhiteSpace(options.Value.Profile))
            throw new RouteCatalogUnavailableException("An independently bound local catalog host identity and profile are required.", RouteCatalogRefusalReason.Configuration);
    }

    private Task UpdateAsync(Func<RouteCatalogActivationState, RouteCatalogActivationState> update, CancellationToken cancellationToken)
        => UpdateAsync(state => Task.FromResult(update(state)), cancellationToken);

    private async Task UpdateAsync(Func<RouteCatalogActivationState, Task<RouteCatalogActivationState>> update, CancellationToken cancellationToken, DispatchDiagnostics? diagnostics = null)
    {
        for (int attempt = 0; attempt < MaximumUpdateAttempts; attempt++)
        {
            RouteCatalogActivationState state;
            string etag;
            using (var stage = diagnostics?.Start(DispatchStageName.CatalogStateRead))
            {
                (state, etag) = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
                stage?.Complete();
            }
            RouteCatalogActivationState next = await update(state).ConfigureAwait(false);
            if (ReferenceEquals(state, next)) return;
            bool exchanged;
            using (var stage = diagnostics?.Start(DispatchStageName.CatalogCompareExchange))
            {
                exchanged = await store.CompareExchangeAsync(next, etag, cancellationToken).ConfigureAwait(false);
                stage?.Complete();
            }
            if (exchanged) return;
            // Bound conflict scheduling like completed-lease release, desynchronizing competing
            // writers without locking across an awaited owner verifier. The wider
            // jitter breaks a busy fleet's repeated read/write collisions; every
            // retry still revalidates fresh state within a finite attempt bound.
            if (attempt < MaximumUpdateAttempts - 1)
                await Task.Delay(Random.Shared.Next(1, Math.Min((attempt + 1) * 4, 50) + 1), cancellationToken).ConfigureAwait(false);
        }
        throw new RouteCatalogUnavailableException("Concurrent catalog authority updates could not be serialized.");
    }

    private static RouteCatalogActivation RequirePending(RouteCatalogActivationState state, string attemptId)
    {
        if (state.Pending is not { } pending || pending.AttemptId != attemptId)
            throw new RouteCatalogUnavailableException("The activation attempt is not prepared.");
        var recorded = state.Attempts.Where(attempt => attempt.AttemptId == attemptId).ToArray();
        if (recorded.Length != 1 || recorded[0].BindingDigest != RouteCatalogActivationBinding.ComputeDigest(pending))
            throw new RouteCatalogUnavailableException("The prepared activation differs from its immutable attempt binding.");
        return pending;
    }
    private static RouteCatalogEnvelope Catalog(RouteCatalogActivation activation) => RouteCatalogCodec.Decode(activation.CanonicalCatalog, activation.RootDigest);
    private static RouteCatalogActivation Freeze(RouteCatalogActivation candidate) => candidate with
    {
        TechnicalHttpReceivers = TechnicalHttpCatalogBinding.Freeze(candidate.TechnicalHttpReceivers),
        CanonicalCatalog = candidate.CanonicalCatalog.ToArray(), RequiredHosts = Array.AsReadOnly(candidate.RequiredHosts.ToArray()),
        AuxiliaryOperations = Array.AsReadOnly((candidate.AuxiliaryOperations ?? []).Select(operation => operation with { Http = operation.Http is { } http ? http with { Grants = http.Grants is null ? null : Array.AsReadOnly(http.Grants.ToArray()) } : null }).ToArray()),
        ProjectionCapabilities = Array.AsReadOnly((candidate.ProjectionCapabilities ?? []).Select(c => c with
        { ProjectionTypes = Array.AsReadOnly(c.ProjectionTypes.ToArray()) }).ToArray()),
    };
}
