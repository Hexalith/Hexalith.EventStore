using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Composes exact supplied state creation, canonical codec and Apply with one shared evolution service.</summary>
/// <remarks>Unregistered event-only preparation. Declared graph bounds and detached codecs require deployer review.</remarks>
internal sealed class RegisteredLogicalReplayBinding
{
    private readonly Type _stateType;
    private readonly Func<CancellationToken, object> _create;
    private readonly Func<IReadOnlyPayload, CancellationToken, object> _read;
    private readonly Action<object, IBoundedPayloadWriter, CancellationToken> _write;
    private readonly Func<object, object, CancellationToken, object> _apply;
    private readonly LogicalReplayCallableBinding[] _callables;
    private readonly int _maximumStateBytes;
    private readonly int _workingGraphBytes;
    private readonly byte[] _stateAssemblyHash;
    private readonly EventManagedArtifactExecutionBinding? _stateExecution;
    private readonly string _aggregateType;
    private readonly string _domain;
    private readonly string _registryFingerprint;

    /// <summary>Seals local binding identity and measured capacities before any callback.</summary>
    /// <remarks>Working capacity bounds all simultaneously live callback graphs, including candidate, decoded round-trip state,
    /// current event and references retained by reviewed callbacks. It is not a single-object size or hostile-code allocation limit.</remarks>
    internal RegisteredLogicalReplayBinding(Type stateType, EventEvolutionService evolution, string aggregateType, string serializerId,
        ReadOnlyMemory<byte> canonicalOptions, Func<CancellationToken, object> create,
        Func<IReadOnlyPayload, CancellationToken, object> read,
        Action<object, IBoundedPayloadWriter, CancellationToken> write,
        Func<object, object, CancellationToken, object> apply, int maximumStateBytes, int workingGraphBytes,
        IReadOnlyList<EventManagedArtifactExecutionBinding?>? executions = null,
        EventManagedArtifactExecutionBinding? stateTypeExecution = null)
    {
        ArgumentNullException.ThrowIfNull(stateType);
        ArgumentNullException.ThrowIfNull(evolution);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializerId);
        evolution.RequireAggregateRoute(evolution.Domain, aggregateType, CancellationToken.None);
        _aggregateType = aggregateType;
        _domain = evolution.Domain;
        _registryFingerprint = evolution.RegistryFingerprint;
        if (maximumStateBytes is < 1 or > 64 * 1024 * 1024 || workingGraphBytes is < 1 or > 128 * 1024 * 1024
            || canonicalOptions.Length > 64 * 1024 || (executions is not null && executions.Count != 4))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStateBytes));
        }

        _stateType = stateType;
        Evolution = evolution;
        _create = create;
        _read = read;
        _write = write;
        _apply = apply;
        _maximumStateBytes = maximumStateBytes;
        _workingGraphBytes = workingGraphBytes;
        _stateExecution = stateTypeExecution;
        if (stateTypeExecution is not null)
        {
            stateTypeExecution.RequireCapabilityScope(evolution.CapabilityLoss);
            _stateAssemblyHash = stateTypeExecution.CopyHashForAssembly(stateType.Assembly);
        }
        else
        {
            using FileStream stream = File.OpenRead(stateType.Assembly.Location);
            _stateAssemblyHash = SHA256.HashData(stream);
        }

        Delegate[] callbacks = [create, read, write, apply];
        _callables = callbacks.Select((callback, index) => new LogicalReplayCallableBinding(callback, evolution.CapabilityLoss,
            executions?[index])).ToArray();
        using var pin = new EventEvolutionBinaryWriter(128 * 1024);
        pin.WriteRaw("HX-EV-DAPR-REPLAY-BINDING-1\0"u8);
        pin.WriteByte(1);
        pin.WriteString(_domain);
        pin.WriteString(_aggregateType);
        pin.WriteHash(Convert.FromHexString(_registryFingerprint));
        pin.WriteString(stateType.AssemblyQualifiedName!);
        pin.WriteHash(_stateAssemblyHash);
        pin.WriteString(serializerId);
        pin.WriteHash(EventOptionsManifestCodec.ComputeHash(canonicalOptions, []));
        pin.WriteInt32(maximumStateBytes);
        pin.WriteInt32(workingGraphBytes);
        foreach (LogicalReplayCallableBinding callable in _callables)
        {
            pin.WriteString(callable.MethodIdentity);
            pin.WriteHash(callable.Hash);
        }

        Fingerprint = pin.ComputeSha256();
        RequireCurrent(CancellationToken.None);
    }

    /// <summary>Gets the exact local binding pin retained by the operation owner.</summary>
    internal ReadOnlyMemory<byte> Fingerprint
    {
        get;
    }

    /// <summary>Gets the same evolution service used by source and effective intake.</summary>
    internal EventEvolutionService Evolution
    {
        get;
    }

    /// <summary>Bounds private intake, all simultaneous graphs, canonical copies, codec round-trip and state staging/readback.</summary>
    internal int GetPreparationCapacity(int responseBytes, int priorBytes)
    {
        long capacity = 128 * 1024L + 16384L + _workingGraphBytes + 12L * responseBytes + 14L * _maximumStateBytes + 4L * priorBytes;
        if (capacity > 128 * 1024 * 1024)
        {
            throw new InvalidOperationException("ScratchLimit: composed reconstruction capacity exceeds 128 MiB.");
        }

        return checked((int)capacity);
    }

    /// <summary>Admits all private completed-state bytes, proof decoding and simultaneous processor graphs.</summary>
    internal int GetCommandCapacity(int stateBytes, int proofBytes, int commandBytes)
        => checked(_workingGraphBytes + 14 * _maximumStateBytes + 4 * stateBytes + 12 * proofBytes + commandBytes + 12 * 1024 * 1024);

    /// <summary>Reserves reviewed graph capacity for the entire completed command invocation.</summary>
    internal EventBufferReservation ReserveCommandGraphs(EventBufferBudget budget)
        => budget.Reserve(checked(_workingGraphBytes + 2 * _maximumStateBytes));

    /// <summary>Decodes only private canonical state; its input lease expires before the actual owner fence yields.</summary>
    internal async Task<object> ReadCommandStateAsync(IReadOnlyPayload canonical, EventBufferBudget budget,
        Func<CancellationToken, Task> actualOwnerFence, CancellationToken token)
    {
        await RequireBoundaryAsync(actualOwnerFence, token).ConfigureAwait(false);
        object state;
        try
        {
            using var lease = new InvocationPayloadLease(canonical, token);
            state = _read(lease, token);
        }
        finally { token.ThrowIfCancellationRequested(); }
        await RequireBoundaryAsync(actualOwnerFence, token).ConfigureAwait(false);
        RequireType(state);
        ImmutablePayload produced;
        try { produced = await SerializeAsync(state, budget, actualOwnerFence, token).ConfigureAwait(false); }
        finally { token.ThrowIfCancellationRequested(); }
        using ImmutablePayload roundtrip = produced;
        if (canonical.Length != roundtrip.Length || !PayloadHash(canonical, budget, token).AsSpan().SequenceEqual(roundtrip.ComputeSha256()))
        { throw new InvalidOperationException("ProofMismatch: completed state is not canonical under the pinned codec."); }
        await RequireBoundaryAsync(actualOwnerFence, token).ConfigureAwait(false);
        return state;
    }

    /// <summary>Refuses a mutable admission-stage graph that differs from the privately proven canonical state.</summary>
    internal async Task RequireCommandGraphAsync(object state, IReadOnlyPayload canonical, EventBufferBudget budget,
        Func<CancellationToken, Task> fence, CancellationToken token)
    {
        RequireType(state);
        ImmutablePayload produced;
        try { produced = await SerializeAsync(state, budget, fence, token).ConfigureAwait(false); }
        finally { token.ThrowIfCancellationRequested(); }
        using ImmutablePayload encoded = produced;
        if (canonical.Length != encoded.Length || !PayloadHash(canonical, budget, token).AsSpan().SequenceEqual(encoded.ComputeSha256()))
        { throw new InvalidOperationException("ProofMismatch: admission stage changed the proven canonical state."); }
        await RequireBoundaryAsync(fence, token).ConfigureAwait(false);
    }

    private static byte[] PayloadHash(IReadOnlyPayload payload, EventBufferBudget budget, CancellationToken token)
    {
        using ImmutablePayload copy = ImmutablePayload.CopyFrom(payload, budget, token);
        return copy.ComputeSha256();
    }

    /// <summary>Checks all supplied callable images before invoking any one of them.</summary>
    internal void RequireCurrent(CancellationToken token)
    {
        Evolution.RequireActive(token);
        Evolution.RequireAggregateRoute(_domain, _aggregateType, token);
        if (Evolution.RegistryFingerprint != _registryFingerprint)
        {
            throw new InvalidOperationException("CapabilityMismatch: supplied reconstruction registry changed.");
        }

        if (_stateExecution is not null)
        {
            _stateExecution.RequireCapabilityScope(Evolution.CapabilityLoss);
            _stateExecution.RequireBoundAssembly(_stateType.Assembly);
        }
        else
        {
            using FileStream stream = File.OpenRead(_stateType.Assembly.Location);
            if (!SHA256.HashData(stream).AsSpan().SequenceEqual(_stateAssemblyHash))
            {
                throw new InvalidOperationException("CapabilityMismatch: supplied state assembly changed.");
            }
        }

        foreach (LogicalReplayCallableBinding callable in _callables)
        {
            callable.RequireCurrent(token);
        }

        Evolution.RequireActive(token);
    }

    /// <summary>Rejects a source outside the exact supplied domain/aggregate/registry binding before state callbacks.</summary>
    internal void RequireSource(DaprLogicalSourceBinding source, CancellationToken token)
    {
        RequireCurrent(token);
        if (source.Identity.Domain != _domain || source.AggregateType != _aggregateType)
        {
            throw new InvalidOperationException("CapabilityMismatch: logical source is outside the supplied state binding route.");
        }
    }

    /// <summary>Creates canonical sequence-zero state with graph capacity admitted before application code.</summary>
    internal async ValueTask<ImmutablePayload> CreateInitialAsync(EventBufferBudget budget, Func<CancellationToken, Task> sourceFence, CancellationToken token)
    {
        RequireCurrent(token);
        using EventBufferReservation graphs = budget.Reserve(checked(_workingGraphBytes + 2 * _maximumStateBytes));
        await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
        object state;
        try { state = _create(token); }
        finally { token.ThrowIfCancellationRequested(); }
        await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
        RequireType(state);
        return await SerializeAsync(state, budget, sourceFence, token).ConfigureAwait(false);
    }

    /// <summary>Folds only a completely admitted page; prior canonical bytes remain separate from every mutable candidate.</summary>
    internal async ValueTask<PrivateLogicalReplayFold> FoldAsync(PrivateLogicalReplayPage page, IReadOnlyPayload prior, DaprLogicalSourceBinding source,
        EventBufferBudget budget, Func<CancellationToken, Task> sourceFence, CancellationToken token)
    {
        RequireSource(source, token);
        await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
        using EventBufferReservation graphs = budget.Reserve(checked(_workingGraphBytes + 2 * _maximumStateBytes));
        ImmutablePayload? lastGood = ImmutablePayload.CopyFrom(prior, budget, token);
        long sequence = page.Prefix.StartSequence - 1;
        try
        {
            foreach (PrivateLogicalReplayEvent item in page.Events)
            {
                await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
                object state;
                using (var lease = new InvocationPayloadLease(lastGood, token))
                {
                    try { state = _read(lease, token); }
                    finally { token.ThrowIfCancellationRequested(); }
                }
                await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
                RequireType(state);
                object value = await Evolution.DeserializeCurrentAsync(source.Identity.Domain, source.AggregateType, item.CanonicalType,
                    item.Version, item.Format, item.Payload, cancellation => RequireBoundaryAsync(sourceFence, cancellation), token).ConfigureAwait(false);
                await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
                try
                {
                    state = _apply(state, value, token);
                }
                catch (Exception failure) when (failure is not OperationCanceledException)
                {
                    await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
                    var failed = new PrivateLogicalReplayFold(lastGood, sequence, failure, item.Sequence, item.CanonicalType);
                    lastGood = null;
                    return failed;
                }
                finally { token.ThrowIfCancellationRequested(); }

                await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
                RequireType(state);
                ImmutablePayload successor = await SerializeAsync(state, budget, sourceFence, token).ConfigureAwait(false);
                lastGood.Dispose();
                lastGood = successor;
                sequence = item.Sequence;
            }

            await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
            var result = new PrivateLogicalReplayFold(lastGood, sequence, null);
            lastGood = null;
            return result;
        }
        finally
        {
            lastGood?.Dispose();
        }
    }

    private async ValueTask<ImmutablePayload> SerializeAsync(object state, EventBufferBudget budget, Func<CancellationToken, Task> sourceFence, CancellationToken token)
    {
        ImmutablePayload canonical = await SerializeUncheckedAsync(state, budget, sourceFence, token).ConfigureAwait(false);
        try
        {
            await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
            canonical.RequireJsonState(token);
            await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
            object decoded;
            using (var lease = new InvocationPayloadLease(canonical, token))
            {
                try { decoded = _read(lease, token); }
                finally { token.ThrowIfCancellationRequested(); }
            }
            await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
            RequireType(decoded);
            using ImmutablePayload roundtrip = await SerializeUncheckedAsync(decoded, budget, sourceFence, token).ConfigureAwait(false);
            if (canonical.Length != roundtrip.Length || !canonical.ComputeSha256().AsSpan().SequenceEqual(roundtrip.ComputeSha256()))
            {
                throw new InvalidOperationException("ReplayRestartRequired: supplied state bytes are not canonical under their sealed codec.");
            }

            await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
            return canonical;
        }
        catch
        {
            canonical.Dispose();
            throw;
        }
    }

    private async ValueTask<ImmutablePayload> SerializeUncheckedAsync(object state, EventBufferBudget budget, Func<CancellationToken, Task> sourceFence, CancellationToken token)
    {
        await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
        ImmutablePayload canonical;
        using (BoundedPayloadWriter writer = BoundedPayloadWriter.CreateLegacy(_maximumStateBytes, token, budget))
        {
            try { _write(state, writer, token); }
            finally { token.ThrowIfCancellationRequested(); }
            canonical = writer.TakeCompletedPayload();
        }

        try
        {
            await RequireBoundaryAsync(sourceFence, token).ConfigureAwait(false);
            return canonical;
        }
        catch
        {
            canonical.Dispose();
            throw;
        }
    }

    private async Task RequireBoundaryAsync(Func<CancellationToken, Task> sourceFence, CancellationToken token)
    {
        RequireCurrent(token);
        await sourceFence(token).ConfigureAwait(false);
        RequireCurrent(token);
    }

    private void RequireType(object? state)
    {
        if (state is null || state.GetType() != _stateType)
        {
            throw new InvalidOperationException("ReplayRestartRequired: supplied state codec or Apply returned a foreign type.");
        }
    }
}
