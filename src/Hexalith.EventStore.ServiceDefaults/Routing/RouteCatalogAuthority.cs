using Hexalith.EventStore.Contracts.Routing;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>CAS-serialized prepare/ready/commit/rollback authority with durable, non-expiring dispatch leases.</summary>
public sealed class RouteCatalogAuthority(IRouteCatalogStateStore store, IRouteCatalogTrustValidator trust,
    IOptions<RouteCatalogHostOptions> options, IRouteCatalogHostObserver? observer = null, IRouteCatalogQuiescenceVerifier? quiescenceVerifier = null) : IRouteCatalogAuthority
{
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
            if (state.Leases.Count != 0) throw new RouteCatalogUnavailableException("Prior dispatch is still draining.");
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
            await ValidateAsync(committed, cancellationToken).ConfigureAwait(false);
            RouteCatalogDispatchFence fence = state.Leases.SingleOrDefault(l => l.LeaseId == request.LeaseId)
                ?? throw new RouteCatalogUnavailableException("The exact durable dispatch fence does not exist.");
            if (quiescenceVerifier is null || string.IsNullOrWhiteSpace(request.Proof)
                || fence.RootDigest != request.RootDigest || fence.Epoch != request.Epoch
                || committed.RootDigest != fence.RootDigest || state.Epoch != fence.Epoch)
                throw new RouteCatalogUnavailableException("Owner-verified evidence for this exact dispatch and generation is required.");
            await quiescenceVerifier.VerifyAsync(request, fence, cancellationToken).ConfigureAwait(false);
            return state with { Leases = [.. state.Leases.Where(l => l.LeaseId != fence.LeaseId)] };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<RouteCatalogDispatchLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        RequireLocalIdentity();
        RouteCatalogActivation? captured = null;
        long epoch = 0;
        string leaseId = Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId();
        await UpdateAsync(async state =>
        {
            if (state.Leases.Any(l => l.LeaseId == leaseId) && state.Committed is { } retained)
            {
                await ValidateAsync(retained, cancellationToken).ConfigureAwait(false);
                captured = retained;
                epoch = state.Epoch;
                return state;
            }
            if (state.Pending is not null || state.Committed is null)
                throw new RouteCatalogUnavailableException("No complete catalog generation is serving.");
            await ValidateAsync(state.Committed, cancellationToken).ConfigureAwait(false);
            captured = state.Committed;
            epoch = state.Epoch;
            long sequence = checked(state.LastLease + 1);
            if (state.Leases.Count >= 4096) throw new RouteCatalogUnavailableException("The durable dispatch lease inventory requires draining.");
            return state with { LastLease = sequence, Leases = [.. state.Leases, new RouteCatalogDispatchFence(sequence, leaseId, options.Value.Identity!.HostId, state.Committed.RootDigest, state.Epoch)] };
        }, cancellationToken).ConfigureAwait(false);
        return new(new(captured!, epoch, options.Value.Identity!.AppId, store.Coordinates), () => ReleaseAsync(leaseId));
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

    private async Task ValidateAsync(RouteCatalogActivation activation, CancellationToken cancellationToken)
    {
        RequireLocalIdentity();
        string binding = RouteCatalogActivationBinding.ComputeDigest(activation);
        RouteCatalogHostOptions local = options.Value;
        if (local.Identity is null || Catalog(activation).Profile != local.Profile
            || !activation.RequiredHosts.Contains(local.Identity))
            throw new RouteCatalogUnavailableException("Local host inventory, topology, authentication or profile disagrees with the exact required host set.");
        if (store.Coordinates is not { } coordinates || observer is null
            || await observer.ObserveAsync(activation, coordinates, cancellationToken).ConfigureAwait(false) != local.Identity)
            throw new RouteCatalogUnavailableException("Configured host identity lacks matching independent registration, topology and authentication observations.", RouteCatalogRefusalReason.Inventory);
        await trust.ValidateAsync(activation, binding, cancellationToken).ConfigureAwait(false);
    }

    private void RequireLocalIdentity()
    {
        if (options.Value.Identity is null || string.IsNullOrWhiteSpace(options.Value.Profile))
            throw new RouteCatalogUnavailableException("An independently bound local catalog host identity and profile are required.", RouteCatalogRefusalReason.Configuration);
    }

    private Task UpdateAsync(Func<RouteCatalogActivationState, RouteCatalogActivationState> update, CancellationToken cancellationToken)
        => UpdateAsync(state => Task.FromResult(update(state)), cancellationToken);

    private async Task UpdateAsync(Func<RouteCatalogActivationState, Task<RouteCatalogActivationState>> update, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 32; attempt++)
        {
            (RouteCatalogActivationState state, string etag) = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
            RouteCatalogActivationState next = await update(state).ConfigureAwait(false);
            if (ReferenceEquals(state, next) || await store.CompareExchangeAsync(next, etag, cancellationToken).ConfigureAwait(false)) return;
        }
        throw new RouteCatalogUnavailableException("Concurrent catalog authority updates could not be serialized.");
    }

    private static RouteCatalogActivation RequirePending(RouteCatalogActivationState state, string attemptId)
        => state.Pending is { } pending && pending.AttemptId == attemptId ? pending
            : throw new RouteCatalogUnavailableException("The activation attempt is not prepared.");
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
