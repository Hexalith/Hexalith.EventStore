using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Reads and decrypts an authoritative aggregate prefix for maintenance reconciliation.</summary>
internal sealed class EventStoreProjectionDeliveryHistoryReader(
    IActorProxyFactory actorProxyFactory,
    IEventPayloadProtectionService payloadProtectionService,
    IOptions<EventStoreActorOptions> actorOptions) : IProjectionDeliveryHistoryReader {
    private const int _readPageSize = 256;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProjectionEventDto>> ReadAsync(
        AggregateIdentity identity,
        long throughSequence,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegative(throughSequence);
        if (throughSequence == 0) {
            return [];
        }

        IAggregateActor aggregate = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            actorOptions.Value.AggregateActorTypeName);
        var persisted = new List<EventEnvelope>();
        long afterSequence = 0;
        while (afterSequence < throughSequence) {
            cancellationToken.ThrowIfCancellationRequested();
            EventEnvelope[] page = await aggregate
                .ReadEventsRangeAsync(afterSequence, throughSequence, _readPageSize)
                .ConfigureAwait(false);
            if (page.Length == 0) {
                break;
            }

            foreach (EventEnvelope value in page) {
                long expected = persisted.Count + 1L;
                if (value.SequenceNumber != expected) {
                    throw new ProjectionDeliveryHistoryValidationException(
                        "Authoritative projection history is not contiguous.");
                }

                persisted.Add(value);
            }

            afterSequence = persisted[^1].SequenceNumber;
        }

        if (persisted.Count != throughSequence) {
            throw new ProjectionDeliveryHistoryValidationException(
                "Authoritative projection history does not reach the persisted checkpoint.");
        }

        ProjectionEventReadabilityResult readability = await ProjectionEventWireBuilder
            .BuildAsync(payloadProtectionService, identity, [.. persisted], cancellationToken)
            .ConfigureAwait(false);
        if (readability.Events is not null) {
            return readability.Events;
        }

        if (readability.UnreadableReason == UnreadableProtectedDataReason.ProviderUnavailable) {
            throw new InvalidOperationException("Authoritative projection history is temporarily unavailable.");
        }

        throw new ProjectionDeliveryHistoryValidationException(
            "Authoritative projection history is not readable.");
    }
}
