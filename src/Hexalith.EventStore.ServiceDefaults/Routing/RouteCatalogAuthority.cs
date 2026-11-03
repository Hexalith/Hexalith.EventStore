using System.Diagnostics;

using Hexalith.EventStore.Contracts.Routing;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>CAS-serialized prepare/ready/commit/rollback authority with durable, non-expiring dispatch leases.</summary>
[method: Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
public sealed class RouteCatalogAuthority(IRouteCatalogStateStore store, IRouteCatalogTrustValidator trust,
    IOptions<RouteCatalogHostOptions> options, IRouteCatalogHostObserver? observer = null, IRouteCatalogQuiescenceVerifier? quiescenceVerifier = null,
    IHostEnvironment? environment = null) : IRouteCatalogAuthority, IDisposable
{
    /// <summary>Preserves the original packaged constructor signature for existing compiled consumers.</summary>
    /// <param name="store">The fresh durable authority store.</param>
    /// <param name="trust">The independent trust validator.</param>
    /// <param name="options">The effective local host options.</param>
    /// <param name="observer">The independent local host observer.</param>
    /// <param name="quiescenceVerifier">The independently owned quiescence verifier.</param>
    public RouteCatalogAuthority(IRouteCatalogStateStore store, IRouteCatalogTrustValidator trust,
        IOptions<RouteCatalogHostOptions> options, IRouteCatalogHostObserver? observer,
        IRouteCatalogQuiescenceVerifier? quiescenceVerifier)
        : this(store, trust, options, observer, quiescenceVerifier, null) { }

    // Admission is locally queued only until its fence is recorded. Remote dispatch and all
    // lifecycle/verifier work run outside this gate and retain their independent CAS checks.
    private readonly SemaphoreSlim _localAdmission = new(1, 1);
    private const int MaximumUpdateAttempts = 96;
    private readonly RouteCatalogValidatedBytesMemo _validatedBytes = new();

    /// <summary>Releases only private computational byte storage, without removing or reconciling any durable dispatch fence.</summary>
    public void Dispose() => _validatedBytes.Dispose();

    /// <inheritdoc/>
    public async Task PrepareAsync(RouteCatalogActivation candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
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
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
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
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
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
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
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
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
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
        await _localAdmission.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
            // Always mark the open acquisition: release serializes with admission, and a
            // reentrant callback release must not queue behind its own held gate.
            using var admissionTurn = RouteCatalogLocalAdmissionTurn.Enter(this);
            using var decoding = BeginPrivateDecodeScope();
            long started = Stopwatch.GetTimestamp();
            bool completed = false;
            try
            {
                RouteCatalogDispatchLease lease = await AcquireCoreAsync(cancellationToken).ConfigureAwait(false);
                completed = true;
                return lease;
            }
            finally
            {
                if (decoding is not null)
                {
                    try
                    {
                        Activity.Current?.AddEvent(new ActivityEvent("private.route_catalog.decode_scope", tags: new ActivityTagsCollection
                        {
                            ["enabled"] = true, ["completed"] = completed, ["request_canceled"] = cancellationToken.IsCancellationRequested,
                            ["capture_count"] = decoding.CaptureCount, ["cache_hits"] = decoding.CacheHits,
                            ["document_count"] = decoding.Count, ["retained_bytes"] = decoding.RetainedBytes,
                            ["host_memo_enabled"] = decoding.HasHostMemo,
                            ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                        }));
                    }
                    catch (Exception) { /* Private metadata diagnostics cannot change admission or lease ownership. */ }
                }
            }
        }
        finally { _localAdmission.Release(); }
    }

#if DEBUG
    private static bool ScopedDecodeBuildEnabled => true;
#else
    private static bool ScopedDecodeBuildEnabled => false;
#endif

    private bool PrivateScopedDecodeEnabled() => ScopedDecodeBuildEnabled && environment?.IsDevelopment() == true
        && options.Value is { ScopedDecodeReuse: true, DevelopmentFixture: true, Profile: "Development", RequireTechnicalHttpBinding: true };

    private bool PrivateHeldMetadataEnabled() => ScopedDecodeBuildEnabled && environment?.IsDevelopment() == true
        && options.Value is { ReuseHeldMetadataDispatch: true, DevelopmentFixture: true, Profile: "Development", RequireTechnicalHttpBinding: true };

    /// <summary>Freshly validates the issuer's frozen activation before using its genuine receiver scope for exact metadata transport.</summary>
    internal async Task<RouteCatalogHeldMetadataDispatch?> GetHeldMetadataDispatchAsync(HttpRequestMessage request,
        string appId, string method, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (method != "api/v1/streams/read/workload" || request.Method.Method != "POST" || !PrivateHeldMetadataEnabled()) return null;
        var scopes = RouteCatalogRemoteCall.CaptureMetadataScopes(this);
        if (scopes is null) return null;
        var held = new RouteCatalogHeldMetadataDispatch(this, scopes, cancellationToken);
        bool returned = false;
        try
        {
            if (!held.MatchesLocal(options.Value.Identity?.AppId, store.Coordinates))
                throw new RouteCatalogUnavailableException("Nested metadata scopes disagree on local issuer, generation or coordinates.");
            if (!RouteCatalogMetadataRequest.IsGateway(held.Snapshot, appId, method, request.Method)) return null;
            held.Request = await RouteCatalogMetadataRequest.CaptureAsync(request, appId, method, held.Token).ConfigureAwait(false);
            if (held.Request is null) return null;
            using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
            using var decoding = BeginPrivateDecodeScope();
            var activation = System.Text.Json.JsonSerializer.Deserialize<RouteCatalogActivation>(held.Activation,
                System.Text.Json.JsonSerializerOptions.Web) ?? throw new RouteCatalogUnavailableException("The private issuer activation is unavailable.");
            if (activation.RootDigest != held.Snapshot.RootDigest
                || RouteCatalogActivationBinding.ComputeDigest(activation) != held.Snapshot.CompleteActivationBindingDigest)
                throw new RouteCatalogUnavailableException("The private issuer activation binding changed.");
            await ValidateAsync(activation, held.Token).ConfigureAwait(false);
            held.RequireOpen();
            if (!held.MatchesLocal(options.Value.Identity?.AppId, store.Coordinates))
                throw new RouteCatalogUnavailableException("The local metadata issuer configuration changed.");
            returned = true;
            return held;
        }
        finally { if (!returned) held.Dispose(); }
    }

    private bool PrivateHostMemoEnabled() => PrivateScopedDecodeEnabled() && !_validatedBytes.IsClosed
        && options.Value.RetainValidatedCatalogBytes;

    private RouteCatalogDecodeScope? BeginPrivateDecodeScope() => !PrivateScopedDecodeEnabled() ? null
        : PrivateHostMemoEnabled() ? RouteCatalogDecodeScope.EnterWithMemo(_validatedBytes) : RouteCatalogDecodeScope.Enter();

    private async Task<RouteCatalogDispatchLease> AcquireCoreAsync(CancellationToken cancellationToken)
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
        var snapshot = new CommittedRouteCatalogSnapshot(captured!, epoch, options.Value.Identity!.AppId, store.Coordinates);
        if (PrivateHeldMetadataEnabled())
        {
            byte[] activation = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(captured!, System.Text.Json.JsonSerializerOptions.Web);
            if (activation.Length <= 4 * 1024 * 1024)
                return new(snapshot, () => ReleaseAsync(leaseId), this, activation) { Activation = captured };
        }
        return new(snapshot, () => ReleaseAsync(leaseId)) { Activation = captured };
    }

    /// <summary>Rechecks independently observed local facts for a native read under its existing durable lease.</summary>
    internal async Task ValidateNativeReadLeaseAsync(RouteCatalogDispatchLease lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
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
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
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
        // Release writes share the local admission gate with acquisitions, so this
        // host does not manufacture avoidable conflicts against its own new reads.
        // A release called by an open validation callback keeps the original CAS path:
        // queuing behind that same callback would deadlock. Its write still forces a
        // fresh acquisition retry. Closed or unrelated flows cannot use this exception.
        bool queued = !RouteCatalogLocalAdmissionTurn.IsHeldBy(this);
        if (queued) await _localAdmission.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try { await ReleaseCoreAsync(leaseId).ConfigureAwait(false); }
        finally { if (queued) _localAdmission.Release(); }
    }

    private async Task ReleaseCoreAsync(string leaseId)
    {
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
        using var decoding = PrivateHostMemoEnabled() ? RouteCatalogDecodeScope.EnterWithMemo(_validatedBytes) : null;
        // Only this completed local lease is removed. CAS contention cannot turn known
        // quiescence into an abandoned fence; backend failures still propagate for retry.
        // The caller, ReleaseAsync, owns the only local admission gate acquisition.
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
        long phase = RouteCatalogDecodeScope.Current is null ? 0 : Stopwatch.GetTimestamp();
        RequireLocalIdentity();
        string binding = RouteCatalogActivationBinding.ComputeDigest(activation);
        phase = ReportDecodePhase("binding", phase);
        RouteCatalogHostOptions local = options.Value;
        if (local.Identity is null || Catalog(activation).Profile != local.Profile
            || !activation.RequiredHosts.Contains(local.Identity))
            throw new RouteCatalogUnavailableException("Local host inventory, topology, authentication or profile disagrees with the exact required host set.");
        phase = ReportDecodePhase("profile", phase);
        if (store.Coordinates is not { } coordinates || observer is null
            || await observer.ObserveAsync(activation, coordinates, cancellationToken).ConfigureAwait(false) != local.Identity)
            throw new RouteCatalogUnavailableException("Configured host identity lacks matching independent registration, topology and authentication observations.", RouteCatalogRefusalReason.Inventory);
        phase = ReportDecodePhase("observer", phase);
        await trust.ValidateAsync(activation, binding, cancellationToken).ConfigureAwait(false);
        _ = ReportDecodePhase("trust", phase);
    }

    private static long ReportDecodePhase(string phase, long started)
    {
        if (started == 0) return 0;
        try
        {
            Activity.Current?.AddEvent(new ActivityEvent("private.route_catalog.validate_phase", tags: new ActivityTagsCollection
            { ["phase"] = phase, ["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds }));
        }
        catch (Exception) { /* Private timing observation cannot change validation. */ }
        return Stopwatch.GetTimestamp();
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
        for (int attempt = 0; attempt < MaximumUpdateAttempts; attempt++)
        {
            (RouteCatalogActivationState state, string etag) = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
            RouteCatalogActivationState next = await update(state).ConfigureAwait(false);
            if (ReferenceEquals(state, next) || await store.CompareExchangeAsync(next, etag, cancellationToken).ConfigureAwait(false)) return;
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
