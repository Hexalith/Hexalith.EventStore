using System.Security.Cryptography;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Events;
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
    private readonly EventEvolutionCapabilityLoss _capabilityLoss;

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
        _capabilityLoss = registry.CapabilityLoss;
        _pages = new DaprLogicalEventReader(stateManager, protection, new EventLogicalViewResolver(registry, executor));
    }

    /// <summary>Builds a reader from a caller-supplied manifest pin. A missing pin stays on the typed path.</summary>
    /// <remarks>
    /// This production pin binds no upcasters and runs no registered schema or identity validators,
    /// including for zero-hop events. A required hop fails closed. Allow-listed callable binding
    /// remains blocked on trusted loader and catalog closure; the pin alone grants no V2 readiness.
    /// </remarks>
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
    internal async Task<DaprLogicalEventPage> ReadPageAsync(
        AggregateIdentity identity,
        string aggregateType,
        long startSequence,
        int maxCount,
        CancellationToken cancellationToken,
        long? expectedActorHead = null,
        long? expectedRetainedFloor = null,
        EventBufferBudget? sharedBudget = null,
        LegacyEventArrayBudget? arrayBudget = null)
    {
        DaprLogicalEventPage page = await _pages.ReadPageAsync(
            identity,
            aggregateType,
            startSequence,
            maxCount,
            cancellationToken,
            expectedActorHead,
            expectedRetainedFloor,
            sharedBudget,
            requireUnversioned: true,
            arrayBudget).ConfigureAwait(false);
        try
        {
            foreach (DaprLogicalEventView view in page.Events)
            {
                LegacyEventReadGuard.RequireUnversioned(view.Source);
            }

            return page;
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <summary>Reads a contiguous range as checked pages with an optional owned domain view.</summary>
    internal Task<DaprProductionLogicalReplay> ReadRangeAsync(
        AggregateIdentity identity,
        string aggregateType,
        long startSequence,
        int count,
        CancellationToken cancellationToken,
        long expectedActorHead,
        long? expectedRetainedFloor = null,
        bool includeDomainView = true,
        LegacyEventArrayBudget? arrayBudget = null)
        => ReadRangeAsync(identity, aggregateType, startSequence, count, cancellationToken,
            expectedActorHead, expectedRetainedFloor, includeDomainView, arrayBudget, new EventBufferBudget());

    /// <summary>Reads a contiguous range while retaining its metadata and domain copies in the supplied live-buffer budget.</summary>
    internal async Task<DaprProductionLogicalReplay> ReadRangeAsync(
        AggregateIdentity identity,
        string aggregateType,
        long startSequence,
        int count,
        CancellationToken cancellationToken,
        long expectedActorHead,
        long? expectedRetainedFloor,
        bool includeDomainView,
        LegacyEventArrayBudget? arrayBudget,
        EventBufferBudget bufferBudget)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(bufferBudget);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        cancellationToken.ThrowIfCancellationRequested();
        _capabilityLoss.RequireNoObservedLoss();
        arrayBudget ??= new LegacyEventArrayBudget(count);
        var stored = new List<EventEnvelope>(count);
        var domain = new List<EventEnvelope>(includeDomainView ? count : 0);
        var reservations = new List<EventBufferReservation>();
        bool evolved = false;
        long? pinnedFloor = expectedRetainedFloor;
        try
        {
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
                    pinnedFloor,
                    bufferBudget,
                    arrayBudget).ConfigureAwait(false);
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
                        cancellationToken.ThrowIfCancellationRequested();
                        _capabilityLoss.RequireNoObservedLoss();
                        if (view.SequenceNumber != startSequence + stored.Count)
                        {
                            throw new InvalidOperationException("AddressMismatch: logical page sequence is not a complete prefix.");
                        }

                        EventBufferReservation? metadataReservation = view.TakeMetadataReservation();
                        if (metadataReservation is not null)
                        {
                            try
                            {
                                reservations.Add(metadataReservation);
                            }
                            catch
                            {
                                metadataReservation.Dispose();
                                throw;
                            }
                        }

                        stored.Add(view.Source);
                        if (!IsZeroHopV1(view))
                        {
                            evolved = true;
                        }

                        if (includeDomainView && IsZeroHopV1(view))
                        {
                            arrayBudget.AddDomainPayload(view.Resolved.Payload.Length);
                            EventBufferReservation reservation = bufferBudget.Reserve(view.Resolved.Payload.Length);
                            reservations.Add(reservation);
                            cancellationToken.ThrowIfCancellationRequested();
                            _capabilityLoss.RequireNoObservedLoss();
                            domain.Add(CreateDomainEvent(view));
                        }
                    }
                }
                finally
                {
                    page.Dispose();
                }

                offset += size;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _capabilityLoss.RequireNoObservedLoss();
            return new DaprProductionLogicalReplay(expectedActorHead, pinnedFloor ?? 1, stored, domain, evolved, reservations);
        }
        catch
        {
            new DaprProductionLogicalReplay(expectedActorHead, pinnedFloor ?? 1, stored, domain, evolved, reservations).Dispose();
            throw;
        }
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
            && source.PayloadVersion is (null or >= 1 and <= 1024);
    }

    /// <summary>Copies the effective payload into a new envelope and leaves the stored actor value unchanged.</summary>
    internal static EventEnvelope CreateDomainEvent(DaprLogicalEventView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!IsZeroHopV1(view))
        {
            throw new InvalidOperationException("CapabilityMismatch: evolved replay requires a verified effective event route.");
        }

        EventEnvelope source = view.Source;
        byte[] payload = new byte[view.Resolved.Payload.Length];
        return CopyDomainEvent(source, view.Resolved.Payload, view.Resolved.SerializationFormat, payload);
    }

    /// <summary>Copies into an owned domain array, clearing it if copying or envelope construction fails.</summary>
    /// <param name="source">The immutable stored event metadata.</param>
    /// <param name="logicalPayload">The admitted readable payload.</param>
    /// <param name="serializationFormat">The readable payload's registered format.</param>
    /// <param name="destination">The privately owned allocation transferred to the domain envelope on success.</param>
    /// <returns>The in-memory domain envelope with the owned readable payload.</returns>
    internal static EventEnvelope CopyDomainEvent(
        EventEnvelope source,
        IReadOnlyPayload logicalPayload,
        string serializationFormat,
        byte[] destination)
    {
        try
        {
            logicalPayload.CopyTo(0, destination);
            return source with
            {
                Payload = destination,
                SerializationFormat = serializationFormat,
            };
        }
        catch
        {
            CryptographicOperations.ZeroMemory(destination);
            throw;
        }
    }
}
