using System.Diagnostics;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Diagnostics;

using Microsoft.Extensions.Logging;
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
        : this(store, trust, options, observer, quiescenceVerifier, (IHostEnvironment?)null) { }

    // Admission is locally queued only until its fence is recorded. Remote dispatch and all
    // lifecycle/verifier work run outside this gate and retain their independent CAS checks.
    private readonly SemaphoreSlim _localAdmission = new(1, 1);
    private const int MaximumUpdateAttempts = 96;
    private readonly RouteCatalogValidatedBytesMemo _validatedBytes = new();
    private readonly RouteCatalogCanonicalHashMemo _canonicalHashes = new();
    private readonly RouteCatalogBindingMemo _bindingComputations = new();
    private readonly RouteCatalogImageDigestMemo _imageDigests = new();
    private readonly object _snapshotGate = new();
    private bool _disposed;
    private CachedSnapshot? _cachedSnapshot;

    private sealed record CachedSnapshot(
        long Epoch,
        string? LocalAppId,
        RouteCatalogStateCoordinates? Coordinates,
        string BindingDigest,
        RouteCatalogActivation Activation,
        CommittedRouteCatalogSnapshot Snapshot);

    /// <summary>Releases only private computational byte storage, without removing or reconciling any durable dispatch fence.</summary>
    public void Dispose()
    {
        lock (_snapshotGate)
        {
            _disposed = true;
            _cachedSnapshot = null;
        }
        _canonicalHashes.Dispose();
        _validatedBytes.Dispose();
        _bindingComputations.Dispose();
        _imageDigests.Dispose();
    }
    private readonly ILogger<RouteCatalogAuthority>? _logger;

    /// <summary>Uses optional host logging without changing the existing public constructor or acquisition authority.</summary>
    internal RouteCatalogAuthority(IRouteCatalogStateStore store, IRouteCatalogTrustValidator trust,
        IOptions<RouteCatalogHostOptions> options, IRouteCatalogHostObserver? observer,
        IRouteCatalogQuiescenceVerifier? quiescenceVerifier, ILogger<RouteCatalogAuthority>? logger, IHostEnvironment? hostEnvironment = null)
        : this(store, trust, options, observer, quiescenceVerifier, hostEnvironment) => _logger = logger;

    /// <inheritdoc/>
    public async Task PrepareAsync(RouteCatalogActivation candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
        using var bindingBoundary = RouteCatalogBindingScope.Suppress();
        using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
        candidate = Freeze(candidate);
        string candidateBinding = await ValidateAsync(candidate, cancellationToken).ConfigureAwait(false);
        await UpdateAsync(state =>
        {
            if (state.Pending is { } pending)
            {
                if (RouteCatalogActivationBinding.ComputeDigest(pending) == candidateBinding) return state;
                throw new RouteCatalogUnavailableException("Another activation is already prepared.");
            }
            if (state.Attempts.Any(a => a.AttemptId == candidate.AttemptId))
                throw new RouteCatalogUnavailableException("An activation attempt identity is immutable and cannot be reused after completion.");
            if (candidate.PredecessorRootDigest != state.Committed?.RootDigest
                || (state.Committed is { } committed && Catalog(candidate).Generation <= Catalog(committed).Generation)
                || candidate.AttemptId == state.Committed?.AttemptId || candidate.AttemptId == state.Previous?.AttemptId)
                throw new RouteCatalogUnavailableException("Stale predecessor, generation or activation attempt.");
            return state with { Pending = candidate, Ready = [], Attempts = [.. state.Attempts, new(candidate.AttemptId, candidateBinding)] };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<RouteCatalogHostAttestation> ReadyAsync(string attemptId, CancellationToken cancellationToken = default)
    {
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
        using var bindingBoundary = RouteCatalogBindingScope.Suppress();
        using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
        RouteCatalogHostAttestation? receipt = null;
        await UpdateAsync(async state =>
        {
            RouteCatalogActivation pending = Freeze(RequirePending(state, attemptId));
            string binding = await ValidateAsync(pending, cancellationToken).ConfigureAwait(false);
            if (state.Leases.Count != 0) throw new RouteCatalogUnavailableException("Prior dispatch is still draining.", RouteCatalogRefusalReason.Draining);
            RouteCatalogEnvelope envelope = Catalog(pending);
            RouteCatalogHostIdentity host = options.Value.Identity!;
            receipt = new(attemptId, binding, envelope.Generation,
                pending.RootDigest, envelope.RouteFacetDigest, envelope.IdempotencyFacetDigest, envelope.Profile, host);
            return state with { Ready = [.. state.Ready.Where(r => r.Host.HostId != host.HostId), receipt] };
        }, cancellationToken).ConfigureAwait(false);
        return receipt!;
    }

    /// <inheritdoc/>
    public async Task CommitAsync(string attemptId, CancellationToken cancellationToken = default)
    {
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
        using var bindingBoundary = RouteCatalogBindingScope.Suppress();
        using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
        await UpdateAsync(async state =>
        {
            if (state.Pending is null && state.Committed?.AttemptId == attemptId)
            {
                await ValidateAsync(Freeze(state.Committed), cancellationToken).ConfigureAwait(false);
                return state;
            }
            RouteCatalogActivation pending = Freeze(RequirePending(state, attemptId));
            string binding = await ValidateAsync(pending, cancellationToken).ConfigureAwait(false);
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
        using var bindingBoundary = RouteCatalogBindingScope.Suppress();
        using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
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
            await ValidateAsync(Freeze(previous), cancellationToken).ConfigureAwait(false);
            return state with { Epoch = checked(state.Epoch + 1), Committed = previous, Previous = null, Ready = [],
                CompletedTransitions = [.. state.CompletedTransitions, request] };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ReconcileAsync(RouteCatalogQuiescenceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
        using var bindingBoundary = RouteCatalogBindingScope.Suppress();
        using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
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
                await ValidateAsync(Freeze(successor), cancellationToken).ConfigureAwait(false);
                await trust.ValidateAsync(Freeze(committed), RouteCatalogActivationBinding.ComputeDigest(committed), cancellationToken).ConfigureAwait(false);
            }
            else await ValidateAsync(Freeze(committed), cancellationToken).ConfigureAwait(false);
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

    /// <summary>Borrows an open durable dispatch lease already held by this exact authority on the current execution flow, or acquires a fresh lease.</summary>
    public static Task<RouteCatalogDispatchLease> AcquireOrBorrowScopeAsync(IRouteCatalogAuthority authority, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authority);
        if (authority is RouteCatalogAuthority concrete && concrete.TryBorrowActiveLease(cancellationToken) is { } borrowed)
            return Task.FromResult(borrowed);
        return authority.AcquireAsync(cancellationToken);
    }

    internal RouteCatalogDispatchLease? TryBorrowActiveLease(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RouteCatalogDispatchLease> active = RouteCatalogRemoteCall.Current;
        if (active.Count == 0) return null;
        RouteCatalogDispatchLease? parent = null;
        for (int index = active.Count - 1; index >= 0; index--)
        {
            RouteCatalogDispatchLease candidate = active[index].Root;
            if (candidate.IsIssuedBy(this))
            {
                parent = candidate;
                break;
            }
        }
        if (parent is null) return null;
        RouteCatalogRemoteCall.RequireOpenReceiverScopes(parent);
        RequireLocalIdentity();
        RouteCatalogHostOptions local = options.Value;
        if (parent.Activation is not { } activation
            || parent.Snapshot.LocalAppId != local.Identity!.AppId
            || parent.Snapshot.AuthorityCoordinates != store.Coordinates
            || !string.Equals(parent.Snapshot.Envelope.Profile, local.Profile, StringComparison.Ordinal)
            || !activation.RequiredHosts.Contains(local.Identity))
            throw new RouteCatalogUnavailableException("The local catalog authority changed during the active dispatch scope.");
        return RouteCatalogDispatchLease.CreateBorrowed(parent);
    }

    /// <inheritdoc/>
    public async Task<RouteCatalogDispatchLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        DispatchDiagnostics? diagnostics = DispatchDiagnostics.Create(_logger, cancellationToken);
        await WaitForLocalAdmissionAsync(cancellationToken, diagnostics).ConfigureAwait(false);
        RouteCatalogAdmissionGate? admission = null;
        try
        {
            using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
            using var bindingBoundary = RouteCatalogBindingScope.Suppress();
            using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
            using var admissionTurn = RouteCatalogLocalAdmissionTurn.Enter(this);
            admission = new(this, _localAdmission, admissionTurn);
            // Select once. Later option drift cannot change which acquired phase owns release.
            bool shortAdmission = PrivateScopedDecodeEnabled();
            using var decoding = BeginPrivateDecodeScope();
            using var bindingComputation = BeginPrivateBindingScope();
            using var imageComputation = BeginPrivateImageScope();
            long started = Stopwatch.GetTimestamp();
            bool completed = false;
            try
            {
                RouteCatalogDispatchLease lease = await AcquireCoreAsync(cancellationToken, diagnostics, admission, shortAdmission).ConfigureAwait(false);
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
        finally
        {
            if (admission is null) _localAdmission.Release();
            else admission.Dispose();
        }
    }

    private async Task WaitForLocalAdmissionAsync(CancellationToken cancellationToken, DispatchDiagnostics? diagnostics)
    {
        using var stage = diagnostics?.Start(DispatchStageName.CatalogAdmissionWait);
        await _localAdmission.WaitAsync(cancellationToken).ConfigureAwait(false);
        stage?.Complete();
    }

#if DEBUG
    private static bool ScopedDecodeBuildEnabled => true;
#else
    private static bool ScopedDecodeBuildEnabled => false;
#endif

    private bool PrivateScopedDecodeEnabled() => options.Value.ScopedDecodeReuse;

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
            using var bindingBoundary = RouteCatalogBindingScope.Suppress();
            using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
            using var decoding = BeginPrivateDecodeScope();
            using var bindingComputation = BeginPrivateBindingScope();
            using var imageComputation = BeginPrivateImageScope();
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
        : PrivateHostMemoEnabled() ? RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(_validatedBytes, _canonicalHashes) : RouteCatalogDecodeScope.Enter();

    private RouteCatalogBindingScope? BeginPrivateBindingScope() => !PrivateScopedDecodeEnabled() ? null
        : RouteCatalogBindingScope.Enter(PrivateHostMemoEnabled() && !_bindingComputations.IsClosed ? _bindingComputations : null);

    private RouteCatalogImageDigestScope? BeginPrivateImageScope() => !PrivateScopedDecodeEnabled() ? null
        : RouteCatalogImageDigestScope.Enter(PrivateHostMemoEnabled() && !_imageDigests.IsClosed ? _imageDigests : null);

    /// <summary>Borrows only this open authority's selected image computations for synchronous receiver checks, without observing authority.</summary>
    internal RouteCatalogImageDigestScope? BeginPrivateReceiverImageScope() => PrivateHostMemoEnabled() && !_imageDigests.IsClosed
        ? RouteCatalogImageDigestScope.Enter(_imageDigests) : null;

    /// <summary>Verifies fresh committed catalog authority for read-only metadata/stream-read paths without mutating the durable lease inventory.</summary>
    internal async Task<RouteCatalogDispatchLease> AcquireReadOnlyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DispatchDiagnostics? diagnostics = DispatchDiagnostics.Create(_logger, cancellationToken);
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
        using var bindingBoundary = RouteCatalogBindingScope.Suppress();
        using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
        using var decoding = BeginPrivateDecodeScope();
        using var bindingComputation = BeginPrivateBindingScope();
        using var imageComputation = BeginPrivateImageScope();
        RequireLocalIdentity();
        (RouteCatalogActivationState state, _) = await ReadAdmissionStateAsync(cancellationToken, diagnostics).ConfigureAwait(false);
        if (state.Pending is not null || state.Committed is null)
            throw new RouteCatalogUnavailableException("No complete catalog generation is serving.");
        if (state.Leases.Count >= 4096)
            throw new RouteCatalogUnavailableException("The durable dispatch lease inventory requires draining.");
        RouteCatalogActivation candidate = Freeze(state.Committed);
        RouteCatalogHostIdentity identity = options.Value.Identity!;
        string profile = options.Value.Profile;
        RouteCatalogStateCoordinates? coordinates = store.Coordinates;
        RouteCatalogActivation observed = Freeze(candidate);
        string candidateBinding = await ValidateAsync(observed, cancellationToken, diagnostics).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!SameAdmissionActivation(candidate, candidateBinding, observed))
            throw new RouteCatalogUnavailableException("The observed catalog activation changed during read verification.");
        (RouteCatalogActivationState fresh, _) = await ReadAdmissionStateAsync(cancellationToken, diagnostics).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (fresh.Pending is not null || fresh.Committed is null || fresh.Epoch != state.Epoch
            || !SameAdmissionActivation(candidate, candidateBinding, Freeze(fresh.Committed)))
            throw new RouteCatalogUnavailableException("The committed catalog generation changed during read verification.");
        if (fresh.Leases.Count >= 4096)
            throw new RouteCatalogUnavailableException("The durable dispatch lease inventory requires draining.");
        RequireLocalIdentity();
        if (options.Value.Identity != identity || options.Value.Profile != profile || store.Coordinates != coordinates)
            throw new RouteCatalogUnavailableException("The local catalog authority changed during read verification.");
        using var snapshotStage = diagnostics?.Start(DispatchStageName.CatalogSnapshot);
        CommittedRouteCatalogSnapshot snapshot = GetOrCreateSnapshot(candidate, fresh.Epoch, identity.AppId, coordinates, candidateBinding);
        snapshotStage?.Complete();
        return new RouteCatalogDispatchLease(snapshot, static () => Task.CompletedTask)
        {
            Activation = candidate,
        };
    }

    private CommittedRouteCatalogSnapshot GetOrCreateSnapshot(
        RouteCatalogActivation captured,
        long epoch,
        string? capturedAppId,
        RouteCatalogStateCoordinates? capturedCoordinates,
        string capturedBinding)
    {
        if (!PrivateHostMemoEnabled())
        {
            lock (_snapshotGate) _cachedSnapshot = null;
            return new CommittedRouteCatalogSnapshot(captured, epoch, capturedAppId, capturedCoordinates, capturedBinding);
        }

        CachedSnapshot? cached;
        lock (_snapshotGate)
        {
            if (_disposed)
            {
                return new CommittedRouteCatalogSnapshot(captured, epoch, capturedAppId, capturedCoordinates, capturedBinding);
            }
            cached = _cachedSnapshot;
        }

        if (cached is not null
            && cached.Epoch == epoch
            && string.Equals(cached.LocalAppId, capturedAppId, StringComparison.Ordinal)
            && cached.Coordinates == capturedCoordinates
            && string.Equals(cached.BindingDigest, capturedBinding, StringComparison.Ordinal)
            && SameAdmissionActivation(cached.Activation, cached.BindingDigest, captured))
        {
            return cached.Snapshot;
        }

        var created = new CommittedRouteCatalogSnapshot(captured, epoch, capturedAppId, capturedCoordinates, capturedBinding);
        lock (_snapshotGate)
        {
            if (!_disposed && PrivateHostMemoEnabled())
            {
                _cachedSnapshot = new CachedSnapshot(epoch, capturedAppId, capturedCoordinates, capturedBinding, captured, created);
            }
            else
            {
                _cachedSnapshot = null;
            }
        }
        return created;
    }

    private async Task<RouteCatalogDispatchLease> AcquireCoreAsync(CancellationToken cancellationToken, DispatchDiagnostics? diagnostics,
        RouteCatalogAdmissionGate admission, bool shortAdmission)
    {
        RequireLocalIdentity();
        RouteCatalogActivation? captured = null;
        string? capturedBinding = null;
        long epoch = 0;
        string? capturedAppId = null;
        RouteCatalogStateCoordinates? capturedCoordinates = null;
        string leaseId = Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId();
        for (int attempt = 0; attempt < MaximumUpdateAttempts; attempt++)
        {
            if (shortAdmission && !admission.IsHeld)
            {
                await WaitForLocalAdmissionAsync(cancellationToken, diagnostics).ConfigureAwait(false);
                admission.EnterAfterWait();
            }
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
            // Own every input before releasing the initial phase. Each observation remains
            // fresh and ordered; independent local acquisitions may observe concurrently.
            if (shortAdmission) admission.Release();
            RouteCatalogActivation observed = Freeze(candidate);
            // Validation binds this copy before exposing it to either observer. The private
            // candidate has the same initial bytes and is retained for this attempt alone.
            string candidateBinding = await ValidateAsync(observed, cancellationToken, diagnostics).ConfigureAwait(false);
            if (shortAdmission)
            {
                await WaitForLocalAdmissionAsync(cancellationToken, diagnostics).ConfigureAwait(false);
                admission.EnterAfterWait();
            }
            try
            {
                if (!SameAdmissionActivation(candidate, candidateBinding, observed))
                    throw new RouteCatalogUnavailableException("The observed catalog activation changed during admission.");
                RequireLocalIdentity();
                if (options.Value.Identity != identity || options.Value.Profile != profile || store.Coordinates != coordinates)
                    throw new RouteCatalogUnavailableException("The local catalog authority changed during admission.");
                if (retainedLease)
                {
                    RequireAdmissionFence(state, leaseId, identity, candidate);
                    captured = candidate;
                    capturedBinding = candidateBinding;
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
                    && SameAdmissionActivation(candidate, candidateBinding, Freeze(committed));
                if (sameAuthority && fresh.Leases.Any(fence => fence.LeaseId == leaseId))
                {
                    // Recover an already durable admission even if a successor began draining it.
                    // This creates no fence and retains the original response-loss cleanup path.
                    RequireAdmissionFence(fresh, leaseId, identity, candidate);
                    captured = candidate;
                    capturedBinding = candidateBinding;
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
                        capturedBinding = candidateBinding;
                        epoch = fresh.Epoch;
                        capturedAppId = identity.AppId;
                        capturedCoordinates = coordinates;
                        break;
                    }
                }
            }
            finally { if (shortAdmission) admission.Release(); }
            if (attempt < MaximumUpdateAttempts - 1)
                await Task.Delay(Random.Shared.Next(1, Math.Min((attempt + 1) * 4, 50) + 1), cancellationToken).ConfigureAwait(false);
        }
        if (captured is null || capturedBinding is null)
            throw new RouteCatalogUnavailableException("Concurrent catalog authority updates could not be serialized.");
        using var snapshotStage = diagnostics?.Start(DispatchStageName.CatalogSnapshot);
        CommittedRouteCatalogSnapshot snapshot = GetOrCreateSnapshot(captured, epoch, capturedAppId, capturedCoordinates, capturedBinding);
        RouteCatalogDispatchLease lease;
        if (PrivateHeldMetadataEnabled()
            && System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(captured, System.Text.Json.JsonSerializerOptions.Web) is { Length: <= 4 * 1024 * 1024 } activation)
            lease = new(snapshot, () => ReleaseAsync(leaseId), this, activation) { Activation = captured };
        else
            lease = new(snapshot, () => ReleaseAsync(leaseId), this) { Activation = captured };
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

    private static bool SameAdmissionActivation(RouteCatalogActivation validated, string validatedBinding, RouteCatalogActivation current)
        => validated.RootDigest == current.RootDigest
            && string.Equals(validated.TrustProof, current.TrustProof, StringComparison.Ordinal)
            && validated.CanonicalCatalog.AsSpan().SequenceEqual(current.CanonicalCatalog)
            && validatedBinding == RouteCatalogActivationBinding.ComputeDigest(current);

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
        using var decodingBoundary = RouteCatalogDecodeScope.Suppress();
        using var bindingBoundary = RouteCatalogBindingScope.Suppress();
        using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
        using var decoding = BeginPrivateDecodeScope();
        using var bindingComputation = BeginPrivateBindingScope();
        using var imageComputation = BeginPrivateImageScope();
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
        using var bindingBoundary = RouteCatalogBindingScope.Suppress();
        using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
        RequireLocalIdentity();
        (RouteCatalogActivationState state, _) = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        RouteCatalogActivation committed = Freeze(state.Committed ?? throw new RouteCatalogUnavailableException("No catalog has been activated."));
        string binding = await ValidateAsync(committed, cancellationToken).ConfigureAwait(false);
        RouteCatalogEnvelope envelope = Catalog(committed);
        return new(state.Epoch, envelope.Generation, committed.RootDigest, envelope.RouteFacetDigest,
            envelope.IdempotencyFacetDigest, envelope.Profile, Array.AsReadOnly(committed.RequiredHosts.ToArray()), state.Pending is null && state.Leases.Count < 4096)
        {
            AttemptId = committed.TechnicalHttpReceivers is { Count: > 0 } ? committed.AttemptId : null,
            ActivationBindingDigest = committed.TechnicalHttpReceivers is { Count: > 0 } ? binding : null,
        };
    }

    private async Task ReleaseAsync(string leaseId)
    {
        // Release writes share the local admission gate with acquisitions, so this
        // host does not manufacture avoidable conflicts against its own new reads.
        // A release called from a genuinely held admission phase keeps the original CAS path:
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
        using var bindingBoundary = RouteCatalogBindingScope.Suppress();
        using var imageBoundary = RouteCatalogImageDigestScope.Suppress();
        using var decoding = PrivateHostMemoEnabled() ? RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(_validatedBytes, _canonicalHashes) : null;
        using var bindingComputation = PrivateHostMemoEnabled() && !_bindingComputations.IsClosed
            ? RouteCatalogBindingScope.Enter(_bindingComputations) : null;
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

    private async Task<string> ValidateAsync(RouteCatalogActivation activation, CancellationToken cancellationToken, DispatchDiagnostics? diagnostics = null)
    {
        long phase = RouteCatalogDecodeScope.Current is null ? 0 : Stopwatch.GetTimestamp();
        RequireLocalIdentity();
        string binding;
        using (var stage = diagnostics?.Start(DispatchStageName.CatalogActivationDigest))
        {
            binding = RouteCatalogActivationBinding.ComputeDigest(activation);
            stage?.Complete();
        }
        phase = ReportDecodePhase("binding", phase);
        RouteCatalogHostOptions local = options.Value;
        if (local.Identity is null || RouteCatalogCodec.ReadProfile(activation.CanonicalCatalog, activation.RootDigest) != local.Profile
            || !activation.RequiredHosts.Contains(local.Identity))
            throw new RouteCatalogUnavailableException("Local host inventory, topology, authentication or profile disagrees with the exact required host set.");
        phase = ReportDecodePhase("profile", phase);
        using (var stage = diagnostics?.Start(DispatchStageName.CatalogHostCapture))
        {
            if (store.Coordinates is not { } coordinates || observer is null
                || (observer is ObservedRouteCatalogHost observedHost
                    ? await observedHost.ObserveWithValidatedBindingAsync(activation, binding, coordinates, cancellationToken).ConfigureAwait(false)
                    : await observer.ObserveAsync(activation, coordinates, cancellationToken).ConfigureAwait(false)) != local.Identity)
                throw new RouteCatalogUnavailableException("Configured host identity lacks matching independent registration, topology and authentication observations.", RouteCatalogRefusalReason.Inventory);
            stage?.Complete();
        }
        phase = ReportDecodePhase("observer", phase);
        using (var stage = diagnostics?.Start(DispatchStageName.CatalogTrust))
        {
            await trust.ValidateAsync(activation, binding, cancellationToken).ConfigureAwait(false);
            stage?.Complete();
        }
        _ = ReportDecodePhase("trust", phase);
        return binding;
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
