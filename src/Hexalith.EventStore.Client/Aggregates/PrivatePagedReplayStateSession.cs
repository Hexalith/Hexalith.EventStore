using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Owns bounded canonical prior, successor and timeline bytes for one in-process page lease.</summary>
/// <remarks>
/// This unregistered local kernel provides no durable CAS, request/response pin, source proof or continuation.
/// A qualified owner must reserve the working graph, proof and transport capacities in the same budget,
/// validate the scope and serializer binding, and reconcile its Dapr transition before granting progress.
/// </remarks>
internal sealed class PrivatePagedReplayStateSession : IPagedReplayStateSession, IDisposable
{
    private const int MaximumStateBytes = 64 * 1024 * 1024;
    private const int OwnerOverheadBytes = 128;
    private readonly object _gate = new();
    private readonly PrivateReplayPageScope _scope;
    private readonly string _serializerId;
    private readonly EventBufferBudget _budget;
    private readonly CancellationToken _operationCancellation;
    private readonly Action<IReadOnlyPayload, CancellationToken> _validateState;
    private readonly TimeProvider _timeProvider;
    private readonly long _leaseStart;
    private readonly TimeSpan _leaseDuration;
    private readonly List<ImmutablePayload> _borrowedCopies = [];
    private readonly List<ImmutablePayload> _timelineStates = [];
    private readonly List<EventBufferReservation> _callerReservations = [];
    private readonly byte[] _priorHandle = [];
    private readonly byte[] _successorHandle = [];
    private EventBufferReservation? _metadataReservation;
    private ImmutablePayload? _prior;
    private ImmutablePayload? _successor;
    private long _timelineStateBytes;
    private bool _active = true;
    private bool _writing;
    private bool _validating;
    private bool _contractViolation;
    private bool _sealed;
    private bool _disposed;

    /// <summary>Captures and validates a private prior under one admitted local scope and serializer.</summary>
    /// <remarks>The origin token is forwarded unchanged to validation; callbacks receive only an expiring copy facade.</remarks>
    internal PrivatePagedReplayStateSession(PrivateReplayPageScope scope, string serializerId,
        ReadOnlyMemory<byte> priorCanonicalState, Action<IReadOnlyPayload, CancellationToken> validateState,
        EventBufferBudget budget, CancellationToken operationCancellation,
        long committedTimelineStateBytes = 0, TimeProvider? timeProvider = null, TimeSpan? leaseDuration = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializerId);
        ArgumentNullException.ThrowIfNull(validateState);
        ArgumentNullException.ThrowIfNull(budget);
        _scope = scope;
        _serializerId = serializerId;
        _budget = budget;
        _operationCancellation = operationCancellation;
        _validateState = validateState;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _leaseDuration = leaseDuration ?? TimeSpan.FromMinutes(15);
        _leaseStart = _timeProvider.GetTimestamp();
        if (_leaseDuration <= TimeSpan.Zero || _leaseDuration > TimeSpan.FromMinutes(15)
            || committedTimelineStateBytes is < 0 or > MaximumStateBytes
            || (!scope.IncludeTimeline && committedTimelineStateBytes != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        _timelineStateBytes = committedTimelineStateBytes;
        operationCancellation.ThrowIfCancellationRequested();
        long metadataBytes = 1024;
        foreach (string value in new[] { scope.TenantId, scope.Domain, scope.AggregateType,
            scope.AggregateId, scope.OperationId, serializerId })
        {
            metadataBytes = checked(metadataBytes + 2L * value.Length);
            _ = new UTF8Encoding(false, true).GetByteCount(value);
        }

        if (metadataBytes > 512 * 1024)
        {
            throw new InvalidOperationException("MetadataLimit: private replay scope exceeds 512 KiB.");
        }

        try
        {
            _metadataReservation = budget.Reserve((int)metadataBytes);
            _priorHandle = RandomNumberGenerator.GetBytes(32);
            _successorHandle = RandomNumberGenerator.GetBytes(32);
            _prior = CaptureState(priorCanonicalState, operationCancellation);
            Validate(_prior);
            RequireActive(operationCancellation);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets a detached opaque handle valid only for this scope, instance, generation and page lease.</summary>
    internal byte[] CreatePriorHandle()
    {
        lock (_gate)
        {
            RequireActive(CancellationToken.None);
            return CopyBorrowedBytes(_priorHandle);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>> ReadPriorAsync(byte[] handle, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            RequireActive(cancellationToken);
            RequireHandle(handle, _priorHandle);
            byte[] copy = AllocateBorrowedCopy(_prior!.Length);
            _prior.CopyTo(0, copy);
            RequireActive(cancellationToken);
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(copy);
        }
    }

    /// <inheritdoc/>
    /// <remarks>All capture and validation is synchronous, before any ValueTask is returned.</remarks>
    public ValueTask<byte[]> WriteSuccessorAsync(byte[] priorHandle, ReadOnlyMemory<byte> canonicalState,
        string serializerId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            RequireActive(cancellationToken);
            RequireHandle(priorHandle, _priorHandle);
            if (_writing || _validating)
            {
                _contractViolation = true;
                throw new InvalidOperationException("ReplayRestartRequired: a replay successor capture cannot reenter.");
            }

            if (_successor is not null)
            {
                throw new InvalidOperationException("ReplayRestartRequired: this page already has a private successor.");
            }

            if (!string.Equals(serializerId, _serializerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("ReplayRestartRequired: successor serializer differs from the admitted serializer.");
            }

            _writing = true;
            ImmutablePayload? captured = null;
            try
            {
                captured = CaptureState(canonicalState, cancellationToken);
                Validate(captured);
                RequireActive(cancellationToken);
                if (_contractViolation)
                {
                    throw new InvalidOperationException("ReplayRestartRequired: state validation violated the page lease.");
                }

                if (_scope.PageCount == 0)
                {
                    RequireEqual(captured, _prior!);
                }
                else if (_scope.IncludeTimeline)
                {
                    if (_timelineStates.Count != _scope.PageCount)
                    {
                        throw new InvalidOperationException("ReplayRestartRequired: the timeline does not contain the complete admitted page.");
                    }

                    RequireEqual(captured, _timelineStates[^1]);
                }

                byte[] handle = CopyBorrowedBytes(_successorHandle);
                _successor = captured;
                captured = null;
                return ValueTask.FromResult(handle);
            }
            catch
            {
                captured?.Dispose();
                AbortCore();
                throw;
            }
            finally
            {
                _writing = false;
            }
        }
    }

    /// <inheritdoc/>
    public void AppendTimelineEntry(long sequence, ReadOnlySpan<byte> canonicalPostApplyState)
    {
        lock (_gate)
        {
            RequireActive(CancellationToken.None);
            if (_writing || _validating || _successor is not null)
            {
                _contractViolation = true;
                throw new InvalidOperationException("ReplayRestartRequired: timeline is already sealed or capturing a successor.");
            }

            ImmutablePayload? entry = null;
            try
            {
                if (!_scope.IncludeTimeline || _timelineStates.Count >= _scope.PageCount
                    || sequence < _scope.PageStart || sequence - _scope.PageStart != _timelineStates.Count)
                {
                    throw new InvalidOperationException("ReplayRestartRequired: timeline entry is outside the admitted contiguous page.");
                }

                if (canonicalPostApplyState.Length > MaximumStateBytes - _timelineStateBytes)
                {
                    throw new InvalidOperationException("TimelineLimit: cumulative canonical timeline states exceed 64 MiB.");
                }

                // The upstream page reservation covers the caller's working/serialization span.
                entry = CopyOwned(canonicalPostApplyState);
                Validate(entry);
                RequireActive(CancellationToken.None);
                if (_contractViolation)
                {
                    throw new InvalidOperationException("ReplayRestartRequired: timeline validation violated the page lease.");
                }

                _timelineStates.Add(entry);
                entry = null;
                _timelineStateBytes += canonicalPostApplyState.Length;
            }
            catch
            {
                entry?.Dispose();
                AbortCore();
                throw;
            }
        }
    }

    /// <summary>Ends the borrowed handler lease while retaining sealed bytes for the qualified owner's reconciliation.</summary>
    /// <remarks>This local seal is preparation only and grants no durable progress or final result.</remarks>
    internal void SealInvocation(byte[] successorHandle)
    {
        lock (_gate)
        {
            RequireActive(CancellationToken.None);
            RequireHandle(successorHandle, _successorHandle);
            if (_writing || _successor is null || _contractViolation)
            {
                throw new InvalidOperationException("ReplayRestartRequired: no valid private successor is prepared.");
            }

            _sealed = true;
            _active = false;
            ClearBorrowedCopies();
        }
    }

    /// <summary>Lends the sealed private successor to an owner using its independent recovery token.</summary>
    internal InvocationPayloadLease BorrowSealedSuccessor(CancellationToken recoveryCancellation)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            recoveryCancellation.ThrowIfCancellationRequested();
            if (!_sealed || _successor is null)
            {
                throw new InvalidOperationException("ReplayRestartRequired: the page has no sealed successor.");
            }

            return new InvocationPayloadLease(_successor, recoveryCancellation);
        }
    }

    /// <summary>Lends immutable last-good prior bytes to the owner, including after page failure or caller cancellation.</summary>
    internal InvocationPayloadLease BorrowRetainedPrior(CancellationToken recoveryCancellation)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            recoveryCancellation.ThrowIfCancellationRequested();
            return new InvocationPayloadLease(_prior!, recoveryCancellation);
        }
    }

    /// <summary>Discards an uncommitted page without changing its privately retained last-good prior.</summary>
    internal void AbortInvocation()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_sealed)
            {
                throw new InvalidOperationException("ReplayRestartRequired: sealed bytes remain retained for owner reconciliation.");
            }

            AbortCore();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            AbortCore();
            _prior?.Dispose();
            _prior = null;
            foreach (EventBufferReservation caller in _callerReservations) { caller.Dispose(); }
            _callerReservations.Clear();
            CryptographicOperations.ZeroMemory(_priorHandle);
            CryptographicOperations.ZeroMemory(_successorHandle);
            _metadataReservation?.Dispose();
            _metadataReservation = null;
        }
    }

    private ImmutablePayload CaptureState(ReadOnlyMemory<byte> state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state.Length > MaximumStateBytes)
        {
            throw new InvalidOperationException("StateLimit: canonical replay state exceeds 64 MiB.");
        }

        if (!MemoryMarshal.TryGetArray(state, out ArraySegment<byte> segment))
        {
            throw new InvalidOperationException("ScratchLimit: canonical state storage capacity cannot be measured.");
        }

        EventBufferReservation caller = _budget.Reserve(segment.Array?.Length ?? 0);
        try
        {
            ImmutablePayload owned = CopyOwned(state.Span);
            try
            {
                _callerReservations.Add(caller);
                return owned;
            }
            catch
            {
                owned.Dispose();
                throw;
            }
        }
        catch
        {
            caller.Dispose();
            throw;
        }
    }

    private ImmutablePayload CopyOwned(ReadOnlySpan<byte> source)
    {
        EventBufferReservation reservation = _budget.Reserve(checked(source.Length + OwnerOverheadBytes));
        byte[]? owner = null;
        try
        {
            owner = source.ToArray();
            return new ImmutablePayload(owner, owner.Length, CancellationToken.None, reservation);
        }
        catch
        {
            if (owner is not null) { CryptographicOperations.ZeroMemory(owner); }
            reservation.Dispose();
            throw;
        }
    }

    private byte[] CopyBorrowedBytes(ReadOnlySpan<byte> source)
    {
        byte[] copy = AllocateBorrowedCopy(source.Length);
        source.CopyTo(copy);
        return copy;
    }

    private byte[] AllocateBorrowedCopy(int length)
    {
        EventBufferReservation reservation = _budget.Reserve(checked(length + OwnerOverheadBytes));
        byte[]? copy = null;
        ImmutablePayload? owner = null;
        try
        {
            copy = new byte[length];
            owner = new ImmutablePayload(copy, length, CancellationToken.None, reservation);
            _borrowedCopies.Add(owner);
            return copy;
        }
        catch
        {
            if (owner is not null) { owner.Dispose(); }
            else
            {
                if (copy is not null) { CryptographicOperations.ZeroMemory(copy); }
                reservation.Dispose();
            }

            throw;
        }
    }

    private void Validate(ImmutablePayload state)
    {
        byte[] before = state.ComputeSha256();
        _validating = true;
        try
        {
            using var lease = new InvocationPayloadLease(state, _operationCancellation);
            _operationCancellation.ThrowIfCancellationRequested();
            _validateState(lease, _operationCancellation);
            byte[] after = state.ComputeSha256();
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(before, after))
                {
                    throw new InvalidOperationException("ReplayRestartRequired: validation changed private canonical state.");
                }
            }
            finally { CryptographicOperations.ZeroMemory(after); }
        }
        finally
        {
            _validating = false;
            CryptographicOperations.ZeroMemory(before);
        }
    }

    private static void RequireEqual(ImmutablePayload candidate, ImmutablePayload expected)
    {
        byte[] candidateHash = candidate.ComputeSha256();
        byte[] expectedHash = expected.ComputeSha256();
        try
        {
            if (candidate.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash))
            {
                throw new InvalidOperationException("ReplayRestartRequired: successor disagrees with the terminal canonical state.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(candidateHash);
            CryptographicOperations.ZeroMemory(expectedHash);
        }
    }

    private static void RequireHandle(byte[] handle, byte[] expected)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(handle, expected))
        {
            throw new InvalidOperationException("ReplayRestartRequired: private state handle is foreign or stale.");
        }
    }

    private void RequireActive(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_operationCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            AbortCoreUnlessSealed();
            _operationCancellation.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();
        }
        TimeSpan elapsed = _timeProvider.GetElapsedTime(_leaseStart);
        if (!_active || elapsed < TimeSpan.Zero || elapsed >= _leaseDuration)
        {
            AbortCoreUnlessSealed();
            throw new InvalidOperationException("ReplayRestartRequired: private page lease is closed or expired.");
        }
    }

    private void AbortCoreUnlessSealed()
    {
        if (!_sealed) { AbortCore(); }
    }

    private void AbortCore()
    {
        _active = false;
        ClearBorrowedCopies();
        _successor?.Dispose();
        _successor = null;
        foreach (ImmutablePayload entry in _timelineStates) { entry.Dispose(); }
        _timelineStates.Clear();
    }

    private void ClearBorrowedCopies()
    {
        foreach (ImmutablePayload copy in _borrowedCopies) { copy.Dispose(); }
        _borrowedCopies.Clear();
    }
}
