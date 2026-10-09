using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.DomainService.Queries;
/// <summary>Owns one immutable request, exact input plan and pinned-root read-only query scope.</summary>
internal sealed class PrivateLogicalQuerySession : IDisposable
{
    private readonly LogicalQueryDescriptor _descriptor;
    private readonly EventBufferBudget _budget;
    private readonly CancellationToken _token;
    private readonly byte[] _requestHash;
    private readonly LogicalQueryReadPlanEntry[] _plan;
    private readonly List<EventBufferReservation> _graphCharges = [];
    private PrivateLogicalQueryInput? _input;
    private QueryEnvelope? _query;
    /// <summary>Takes exclusive admitted request/root ownership while the enclosing request reservation stays live.</summary>
    internal PrivateLogicalQuerySession(LogicalQueryDescriptor descriptor, QueryEnvelope query, byte[] requestHash, LogicalQueryReadPlanEntry[] plan, PrivateLogicalQueryInput input, EventBufferBudget budget, CancellationToken token)
    {
        _descriptor = descriptor;
        _query = query;
        _requestHash = requestHash;
        _plan = plan;
        _input = input;
        _budget = budget;
        _token = token;
    }

    /// <summary>Gets the exact private pinned handler request.</summary>
    internal QueryEnvelope Query => _query ?? throw Closed();

    /// <summary>Requires request/descriptor/root pins around an actual owner await.</summary>
    internal async Task RequireCurrentAsync(CancellationToken token)
    {
        RequirePins(token);
        await (_input ?? throw Closed()).RequireCurrentAsync(false, token).ConfigureAwait(false);
        RequirePins(token);
    }

    /// <summary>Checks actual current root, authorization and fresh UTC before any response disclosure.</summary>
    internal async Task CompleteAsync(CancellationToken token)
    {
        RequirePins(token);
        await (_input ?? throw Closed()).RequireCurrentAsync(true, token).ConfigureAwait(false);
        RequirePins(token);
    }

    /// <summary>Materializes only exact planned key/type/origin mappings from the admitted root.</summary>
    internal async Task<ReadModelEntry<TValue>> GetAsync<TValue>(string store, string key, CancellationToken token)
        where TValue : class
    {
        LogicalQueryInputMapping mapping = RequireRead<TValue>(store, key, token);
        DaprLogicalQueryRow row = (_input ?? throw Closed()).RequireRow(store, key, token);
        await RequireCurrentAsync(token).ConfigureAwait(false);
        if (row.Payload is null)
        {
            return new ReadModelEntry<TValue>(null, null);
        }

        if (row.ValueTypeName != mapping.ValueTypeName)
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: originating value type disagrees.");
        }

        EventBufferReservation graph = _budget.Reserve(checked(mapping.MaximumGraphBytes + 256));
        _graphCharges.Add(graph);
        EventBufferReservation copy = _budget.Reserve(row.Payload.Length);
        using var payload = new ImmutablePayload(row.Payload.ToArray(), row.Payload.Length, token, copy);
        object value = await mapping.ReadAsync(payload, RequireCurrentAsync, _budget, token).ConfigureAwait(false);
        return new ReadModelEntry<TValue>((TValue)value, null);
    }

    /// <summary>Validates a complete key/type request before any read or application materializer.</summary>
    internal LogicalQueryInputMapping RequireRead<TValue>(string store, string key, CancellationToken token)
        where TValue : class
    {
        RequirePins(token);
        LogicalQueryReadPlanEntry entry = _plan.SingleOrDefault(x => x.StoreName == store && x.LogicalKey == key) ?? throw Closed();
        LogicalQueryInputMapping mapping = _descriptor.GetMapping(entry.MappingId);
        if (mapping.ValueType != typeof(TValue))
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: requested CLR type is not the exact mapping.");
        }

        return mapping;
    }

    /// <summary>Hashes the complete private envelope under the already admitted encoding workspace.</summary>
    internal static byte[] HashRequest(QueryEnvelope query, EventBufferBudget budget, CancellationToken token) => LogicalQueryRequestCodec.Compute(query, budget, token);
    /// <summary>Refuses originating cancellation before closed scope, then checks exact mutable request bytes.</summary>
    internal void RequirePins(CancellationToken token)
    {
        _token.ThrowIfCancellationRequested();
        if (token != _token)
        {
            throw Closed();
        }

        _descriptor.RequireCurrent(token);
        byte[] current = HashRequest(Query, _budget, token);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(current, _requestHash))
            {
                throw new InvalidOperationException("QueryCapabilityChanged: private query request changed.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(current);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        QueryEnvelope? query = Interlocked.Exchange(ref _query, null);
        if (query is null)
        {
            return;
        }

        ClearOwnedRequest(query);
        CryptographicOperations.ZeroMemory(_requestHash);
        Array.Clear(_plan);
        Interlocked.Exchange(ref _input, null)?.Dispose();
        foreach (EventBufferReservation charge in _graphCharges)
        {
            charge.Dispose();
        }

        _graphCharges.Clear();
    }

    /// <summary>Clears only the privately copied payload and scope/audience reference arrays before releasing ownership.</summary>
    internal static void ClearOwnedRequest(QueryEnvelope query)
    {
        CryptographicOperations.ZeroMemory(query.Payload);
        if (query.Scopes is string[] scopes)
        {
            Array.Clear(scopes);
        }

        if (query.Audience is string[] audience)
        {
            Array.Clear(audience);
        }
    }

    private static InvalidOperationException Closed() => new("ReadModelRouteContextRequired: query session is closed or scope is undeclared.");
}
