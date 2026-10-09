using System.Security.Cryptography;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Server.Queries;
/// <summary>Reads a distinct dormant query root through its owning actor state manager.</summary>
/// <remarks>No default registration, root publication, provider certificate or production readiness is supplied.</remarks>
internal sealed class DaprLogicalQueryRootOwner
{
    private readonly IActorStateManager _state;
    private readonly EventEvolutionCapabilityLoss _loss;
    private readonly string _rootKey;
    private readonly string _tenant;
    private readonly string _domain;
    private readonly string _queryType;
    private readonly string _principal;
    private readonly byte[] _compatibilityHash;
    private readonly string _route;
    private readonly string _keySpace;
    private readonly string _store;
    private readonly byte[] _backend;
    private readonly int _maximumRootBytes;
    private readonly Func<QueryEnvelope, IReadOnlyList<LogicalQueryReadPlanEntry>, CancellationToken, Task<DateTimeOffset>> _authorize;
    private readonly Func<Func<CancellationToken, Task>, CancellationToken, Task> _ownerFence;
    private readonly Func<DateTimeOffset> _authoritativeUtc;
    /// <summary>Pins the explicit actor-owned key, declared address and current principal/time authority.</summary>
    internal DaprLogicalQueryRootOwner(IActorStateManager state, string rootKey, string tenant, string domain, string queryType, string principal, ReadOnlySpan<byte> compatibilityHash, string route, string keySpace, string store, ReadOnlySpan<byte> backend, int maximumRootBytes, Func<QueryEnvelope, IReadOnlyList<LogicalQueryReadPlanEntry>, CancellationToken, Task<DateTimeOffset>> authorize, Func<Func<CancellationToken, Task>, CancellationToken, Task> ownerFence, Func<DateTimeOffset> authoritativeUtc, EventEvolutionCapabilityLoss loss)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootKey);
        if (maximumRootBytes is < 1024 or > 64 * 1024 * 1024 || backend.Length is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRootBytes));
        }

        if (compatibilityHash.Length != 32)
        {
            throw new ArgumentException("Exact query compatibility hash required.", nameof(compatibilityHash));
        }

        _queryType = queryType;
        _principal = principal;
        _compatibilityHash = compatibilityHash.ToArray();
        _loss = loss ?? throw new ArgumentNullException(nameof(loss));
        _state = state;
        _rootKey = rootKey;
        _tenant = tenant;
        _domain = domain;
        _route = route;
        _keySpace = keySpace;
        _store = store;
        _backend = backend.ToArray();
        _maximumRootBytes = maximumRootBytes;
        _authorize = authorize ?? throw new ArgumentNullException(nameof(authorize));
        _ownerFence = ownerFence ?? throw new ArgumentNullException(nameof(ownerFence));
        _authoritativeUtc = authoritativeUtc ?? throw new ArgumentNullException(nameof(authoritativeUtc));
    }

    /// <summary>Acquires one complete root after exact-key authorization, retaining overlap and actual-readback pins.</summary>
    internal async Task<PrivateLogicalQueryInput> AcquireAsync(QueryEnvelope query, IReadOnlyList<LogicalQueryReadPlanEntry> plan, EventBufferBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _loss.RequireNoObservedLoss();
        if (query.GetType() != typeof(QueryEnvelope) || query.TenantId != _tenant || query.Domain != _domain || query.QueryType != _queryType || query.UserId != _principal)
        {
            throw new InvalidOperationException("ReadModelRouteContextRequired: foreign query/principal scope.");
        }

        LogicalQueryReadPlanEntry[] keys = plan.ToArray();
        IReadOnlyList<LogicalQueryReadPlanEntry> readOnlyKeys = Array.AsReadOnly(keys);
        if (keys.Length is < 1 or > 256 || keys.Any(x => x.StoreName != _store) || keys.Select(static x => x.LogicalKey).Distinct(StringComparer.Ordinal).Count() != keys.Length)
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: query requires one declared root and distinct exact keys.");
        }

        EventBufferReservation retained = budget.Reserve(checked(_maximumRootBytes + 1024 * 1024));
        DaprLogicalQueryRoot? privateRoot = null;
        byte[]? pinnedHash = null;
        byte[]? requestHash = null;
        try
        {
            requestHash = LogicalQueryRequestCodec.Compute(query, budget, token);
            void RequireRequestPin()
            {
                byte[] current = LogicalQueryRequestCodec.Compute(query, budget, token);
                try
                {
                    if (!current.AsSpan().SequenceEqual(requestHash))
                    {
                        throw new InvalidOperationException("ReadModelRouteContextRequired: private query scope changed.");
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(current);
                }
            }

            using (EventBufferReservation overlap = budget.Reserve(checked(2 * _maximumRootBytes + 1024 * 1024)))
            {
                try
                {
                    _ = await _authorize(query, readOnlyKeys, token).ConfigureAwait(false);
                }
                finally
                {
                    token.ThrowIfCancellationRequested();
                }

                _loss.RequireNoObservedLoss();
                RequireRequestPin();
                DaprLogicalQueryRoot root = await ReadAsync(token, budget).ConfigureAwait(false);
                RequireAddress(root);
                _ = DaprLogicalQueryRootCodec.Measure(root, _maximumRootBytes);
                if (keys.Any(x => !root.Rows.Any(row => row.Key == x.LogicalKey)))
                {
                    throw new InvalidOperationException("ReadModelQueryConsistencyHold: exact-key absence lacks evidence.");
                }

                privateRoot = root with
                {
                    BackendDescriptor = root.BackendDescriptor.ToArray(),
                    Rows = root.Rows.Select(static row => row with { Payload = row.Payload?.ToArray() }).ToArray()
                };
                pinnedHash = DaprLogicalQueryRootCodec.Compute(privateRoot, _maximumRootBytes, budget);
            }

            DaprLogicalQueryRoot ownedRoot = privateRoot;
            byte[] ownedHash = pinnedHash;
            void RequirePrivatePin()
            {
                RequireRequestPin();
                byte[] privateHash = DaprLogicalQueryRootCodec.Compute(ownedRoot, _maximumRootBytes, budget);
                try
                {
                    if (!CryptographicOperations.FixedTimeEquals(ownedHash, privateHash))
                    {
                        throw new InvalidOperationException("ReadModelQueryConsistencyHold: private pinned root changed.");
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(privateHash);
                }
            }

            async Task<DateTimeOffset> FenceAsync(bool compareRoot, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                _loss.RequireNoObservedLoss();
                RequirePrivatePin();
                DateTimeOffset now = default;
                int decisions = 0;
                Task? decisionTask = null;
                async Task RunDecisionAsync(CancellationToken boundaryToken)
                {
                    try
                    {
                        _ = await _authorize(query, readOnlyKeys, boundaryToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        token.ThrowIfCancellationRequested();
                    }

                    _loss.RequireNoObservedLoss();
                    RequirePrivatePin();
                    if (compareRoot)
                    {
                        using EventBufferReservation overlap = budget.Reserve(checked(2 * _maximumRootBytes + 1024 * 1024));
                        DaprLogicalQueryRoot current = await ReadAsync(boundaryToken, budget).ConfigureAwait(false);
                        RequireAddress(current);
                        byte[] digest = DaprLogicalQueryRootCodec.Compute(current, _maximumRootBytes, budget);
                        try
                        {
                            if (!CryptographicOperations.FixedTimeEquals(ownedHash, digest))
                            {
                                throw new InvalidOperationException("ReadModelQueryConsistencyHold: actual actor root changed.");
                            }
                        }
                        finally
                        {
                            CryptographicOperations.ZeroMemory(digest);
                        }
                    }

                    try
                    {
                        _ = await _authorize(query, readOnlyKeys, boundaryToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        token.ThrowIfCancellationRequested();
                    }

                    _loss.RequireNoObservedLoss();
                    RequirePrivatePin();
                    if (compareRoot)
                    {
                        using EventBufferReservation overlap = budget.Reserve(checked(2 * _maximumRootBytes + 1024 * 1024));
                        DaprLogicalQueryRoot current = await ReadAsync(boundaryToken, budget).ConfigureAwait(false);
                        RequireAddress(current);
                        byte[] digest = DaprLogicalQueryRootCodec.Compute(current, _maximumRootBytes, budget);
                        try
                        {
                            if (!CryptographicOperations.FixedTimeEquals(ownedHash, digest))
                            {
                                throw new InvalidOperationException("ReadModelQueryConsistencyHold: final actor root changed.");
                            }
                        }
                        finally
                        {
                            CryptographicOperations.ZeroMemory(digest);
                        }
                    }

                    RequirePrivatePin();
                    try
                    {
                        now = _authoritativeUtc();
                    }
                    finally
                    {
                        token.ThrowIfCancellationRequested();
                    }

                    _loss.RequireNoObservedLoss();
                    RequirePrivatePin();
                    boundaryToken.ThrowIfCancellationRequested();
                }

                Task DecisionAsync(CancellationToken boundaryToken)
                {
                    token.ThrowIfCancellationRequested();
                    if (boundaryToken != token || Interlocked.Increment(ref decisions) != 1)
                    {
                        throw new InvalidOperationException("ReadModelQueryConsistencyHold: skipped/repeated/foreign owner decision.");
                    }

                    decisionTask = RunDecisionAsync(boundaryToken);
                    return decisionTask;
                }

                bool completeAtReturn = false;
                try
                {
                    await _ownerFence(DecisionAsync, cancellation).ConfigureAwait(false);
                    completeAtReturn = decisionTask?.IsCompletedSuccessfully == true;
                }
                finally
                {
                    try
                    {
                        if (decisionTask is not null)
                        {
                            await decisionTask.ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        token.ThrowIfCancellationRequested();
                    }
                }

                if (decisions != 1 || !completeAtReturn)
                {
                    throw new InvalidOperationException("ReadModelQueryConsistencyHold: owner fence did not complete exactly one decision.");
                }

                _loss.RequireNoObservedLoss();
                RequirePrivatePin();
                cancellation.ThrowIfCancellationRequested();
                return now;
            }

            var input = new PrivateLogicalQueryInput(privateRoot, retained, FenceAsync, token, requestHash, _compatibilityHash, () =>
            {
                CryptographicOperations.ZeroMemory(ownedHash);
                CryptographicOperations.ZeroMemory(requestHash);
                Array.Clear(keys);
            }, _loss);
            await input.RequireCurrentAsync(true, token).ConfigureAwait(false);
            return input;
        }
        catch
        {
            if (privateRoot is not null)
            {
                foreach (DaprLogicalQueryRow row in privateRoot.Rows)
                {
                    if (row.Payload is not null)
                    {
                        CryptographicOperations.ZeroMemory(row.Payload);
                    }
                }

                Array.Clear(privateRoot.Rows);
                CryptographicOperations.ZeroMemory(privateRoot.BackendDescriptor);
            }

            if (pinnedHash is not null)
            {
                CryptographicOperations.ZeroMemory(pinnedHash);
            }

            if (requestHash is not null)
            {
                CryptographicOperations.ZeroMemory(requestHash);
            }

            Array.Clear(keys);
            retained.Dispose();
            throw;
        }
    }

    private async Task<DaprLogicalQueryRoot> ReadAsync(CancellationToken token, EventBufferBudget budget)
    {
        token.ThrowIfCancellationRequested();
        _loss.RequireNoObservedLoss();
        try
        {
            await _state.ClearCacheAsync(token).ConfigureAwait(false);
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }

        _loss.RequireNoObservedLoss();
        ConditionalValue<DaprLogicalQueryRoot> read;
        try
        {
            read = await _state.TryGetStateAsync<DaprLogicalQueryRoot>(_rootKey, token).ConfigureAwait(false);
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }

        _loss.RequireNoObservedLoss();
        if (!read.HasValue || read.Value is null)
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: actor root is absent.");
        }

        DaprLogicalQueryRoot root = read.Value;
        RequireAddress(root);
        _ = DaprLogicalQueryRootCodec.Measure(root, _maximumRootBytes);
        foreach (DaprLogicalQueryRow row in root.Rows)
        {
            token.ThrowIfCancellationRequested();
            _loss.RequireNoObservedLoss();
            using EventBufferReservation evidence = budget.Reserve(64 * 1024);
            string originKey = GetOriginKey(_rootKey, row.OriginOperationId, row.Key);
            ConditionalValue<DaprLogicalQueryOrigin> origin;
            try
            {
                origin = await _state.TryGetStateAsync<DaprLogicalQueryOrigin>(originKey, token).ConfigureAwait(false);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }

            _loss.RequireNoObservedLoss();
            byte[]? hash = row.Payload is null ? null : SHA256.HashData(row.Payload);
            try
            {
                if (!origin.HasValue || origin.Value is null || origin.Value.OperationId != row.OriginOperationId || origin.Value.Tenant != _tenant || origin.Value.Domain != _domain || origin.Value.StoreName != _store || origin.Value.Key != row.Key || origin.Value.ValueTypeName != row.ValueTypeName || origin.Value.ExpiresAt != row.ExpiresAt || (origin.Value.PayloadHash is null) != (hash is null) || hash is not null && !hash.AsSpan().SequenceEqual(origin.Value.PayloadHash))
                {
                    throw new InvalidOperationException("ReadModelQueryConsistencyHold: originating operation readback is missing or different.");
                }
            }
            finally
            {
                if (hash is not null)
                {
                    CryptographicOperations.ZeroMemory(hash);
                }
            }
        }

        return root;
    }

    /// <summary>Derives an exact origin participant key without physical-key selection by query callers.</summary>
    internal static string GetOriginKey(string rootKey, string operationId, string key)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(key);
        byte[] hash = SHA256.HashData(bytes);
        try
        {
            return rootKey + ":origin:" + operationId + ":" + Convert.ToHexStringLower(hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    private void RequireAddress(DaprLogicalQueryRoot root)
    {
        if (root.Tenant != _tenant || root.Domain != _domain || root.HandlerRoute != _route || root.KeySpace != _keySpace || root.StoreName != _store || !root.BackendDescriptor.AsSpan().SequenceEqual(_backend))
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: actor root address mismatch.");
        }
    }
}
