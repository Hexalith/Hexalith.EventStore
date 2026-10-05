using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Reads addressed actor history through the shared logical page reader for one caller pin.</summary>
/// <remarks>
/// A page is returned only after the page reader accepts the digest, prefix, head, floor, and ETag.
/// Logical readback shows that Dapr returned the same application value. It does not attest physical bytes.
/// </remarks>
internal sealed class DaprProductionLogicalEventReader
{
    private const int PageSize = 256;
    private readonly IActorStateManager _stateManager;
    private readonly DaprLogicalEventReader _pages;
    private readonly string _pinnedFingerprint;

    /// <summary>Binds the page reader to the caller's registry, chain, and pin.</summary>
    internal DaprProductionLogicalEventReader(
        IActorStateManager stateManager,
        IEventPayloadProtectionService protection,
        EventDomainRegistry registry,
        EventUpcastChainExecutor executor,
        string pinnedFingerprint)
    {
        ArgumentNullException.ThrowIfNull(stateManager);
        ArgumentNullException.ThrowIfNull(protection);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentException.ThrowIfNullOrWhiteSpace(pinnedFingerprint);
        if (!ReferenceEquals(registry, executor.Registry))
        {
            throw new InvalidOperationException("CapabilityMismatch: logical resolver and upcast executor use different registries.");
        }

        if (!string.Equals(registry.Fingerprint, pinnedFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("CapabilityMismatch: the local registry fingerprint differs from the caller pin.");
        }

        _stateManager = stateManager;
        _pinnedFingerprint = pinnedFingerprint;
        _pages = new DaprLogicalEventReader(stateManager, protection, new EventLogicalViewResolver(registry, executor));
    }

    /// <summary>Builds a reader from a caller-supplied manifest pin. A missing pin stays on the typed path.</summary>
    /// <remarks>No upcaster is invented. A required hop fails closed until an allow-listed callable is supplied.</remarks>
    internal static DaprProductionLogicalEventReader? FromCallerPin(
        IActorStateManager stateManager,
        IEventPayloadProtectionService protection,
        EventEvolutionManifestCandidate? candidate)
    {
        if (candidate is null)
        {
            return null;
        }

        var executor = new EventUpcastChainExecutor(
            candidate.Registry,
            new Dictionary<(string Type, int Version), RegisteredEventUpcaster>(),
            static (_, _, _, _, _, _) => { });
        return new DaprProductionLogicalEventReader(
            stateManager,
            protection,
            candidate.Registry,
            executor,
            candidate.PinnedFingerprint);
    }

    /// <summary>Gets the caller pin that admitted this reader.</summary>
    internal string PinnedFingerprint => _pinnedFingerprint;

    /// <summary>Reads one logical page only after the shared digest, prefix, head, floor, and ETag checks.</summary>
    internal Task<DaprLogicalEventPage> ReadPageAsync(
        AggregateIdentity identity,
        string aggregateType,
        long startSequence,
        int maxCount,
        CancellationToken cancellationToken,
        long? expectedActorHead = null,
        long? expectedRetainedFloor = null,
        EventBufferBudget? sharedBudget = null)
        => _pages.ReadPageAsync(
            identity,
            aggregateType,
            startSequence,
            maxCount,
            cancellationToken,
            expectedActorHead,
            expectedRetainedFloor,
            sharedBudget);

    /// <summary>Reads a contiguous range as checked pages and copies the in-memory domain view before disposal.</summary>
    internal async Task<DaprProductionLogicalReplay> ReadRangeAsync(
        AggregateIdentity identity,
        string aggregateType,
        long startSequence,
        int count,
        CancellationToken cancellationToken,
        long expectedActorHead,
        long? expectedRetainedFloor = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        cancellationToken.ThrowIfCancellationRequested();
        var stored = new List<EventEnvelope>(count);
        var domain = new List<EventEnvelope>(count);
        bool evolved = false;
        long? pinnedFloor = expectedRetainedFloor;
        for (int offset = 0; offset < count;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int size = Math.Min(PageSize, count - offset);
            DaprLogicalEventPage page = await ReadPageAsync(
                identity,
                aggregateType,
                startSequence + offset,
                size,
                cancellationToken,
                expectedActorHead,
                pinnedFloor).ConfigureAwait(false);
            try
            {
                pinnedFloor ??= page.RetainedFloor;
                if (page.Events.Count != size
                    || page.ActorHead != expectedActorHead
                    || page.RetainedFloor != pinnedFloor
                    || page.StartSequence != startSequence + offset)
                {
                    throw new InvalidOperationException("ReplayRestartRequired: the logical page did not contain the complete prefix.");
                }

                foreach (DaprLogicalEventView view in page.Events)
                {
                    if (view.SequenceNumber != startSequence + stored.Count)
                    {
                        throw new InvalidOperationException("AddressMismatch: logical page sequence is not a complete prefix.");
                    }

                    stored.Add(view.Source);
                    if (!IsZeroHopV1(view))
                    {
                        evolved = true;
                    }

                    domain.Add(CreateDomainEvent(view));
                }
            }
            finally
            {
                page.Dispose();
            }

            offset += size;
        }

        return new DaprProductionLogicalReplay(expectedActorHead, pinnedFloor ?? 1, stored, domain, evolved);
    }

    /// <summary>Reads the stored aggregate type for a sequence the page reader will authenticate again.</summary>
    internal async Task<string> ReadStoredAggregateTypeAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequenceNumber);
        cancellationToken.ThrowIfCancellationRequested();
        ConditionalValue<EventEnvelope> stored;
        try
        {
            stored = await _stateManager.TryGetStateAsync<EventEnvelope>(
                $"{identity.EventStreamKeyPrefix}{sequenceNumber}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new EventDeserializationException(sequenceNumber, identity.ActorId, error);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!stored.HasValue)
        {
            throw new MissingEventException(sequenceNumber, identity.TenantId, identity.Domain, identity.AggregateId);
        }

        if (string.IsNullOrWhiteSpace(stored.Value.AggregateType))
        {
            throw new InvalidOperationException("AddressMismatch: actor event identity or sequence disagrees with its key.");
        }

        return stored.Value.AggregateType;
    }

    /// <summary>Reports a stored V1 event whose current registry version needs no upcast hop.</summary>
    internal static bool IsZeroHopV1(DaprLogicalEventView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        EventEnvelope source = view.Source;
        return view.Resolved.SourceVersion == view.Resolved.CurrentVersion
            && source.MetadataVersion == 1
            && source.EventContractType is null
            && source.PayloadVersion is null;
    }

    /// <summary>Copies the effective payload into a new envelope and leaves the stored actor value unchanged.</summary>
    internal static EventEnvelope CreateDomainEvent(DaprLogicalEventView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        EventEnvelope source = view.Source;
        byte[] payload = new byte[view.Resolved.Payload.Length];
        view.Resolved.Payload.CopyTo(0, payload);
        if (IsZeroHopV1(view))
        {
            return source with
            {
                Payload = payload,
                SerializationFormat = view.Resolved.SerializationFormat,
            };
        }

        return source with
        {
            Payload = payload,
            EventTypeName = view.Resolved.CanonicalType,
            SerializationFormat = view.Resolved.SerializationFormat,
            MetadataVersion = 1,
            EventContractType = null,
            PayloadVersion = null,
        };
    }
}
