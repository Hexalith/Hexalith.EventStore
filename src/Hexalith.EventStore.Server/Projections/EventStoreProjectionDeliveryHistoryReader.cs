using Hexalith.EventStore.ServiceDefaults.Routing;

using Dapr.Actors;
using Dapr.Actors.Client;
using System.Runtime.CompilerServices;

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
    // One valid event can carry a near-1-MiB stored payload. Keep each Dapr actor
    // response below common transport limits for mapped normal delivery and retry.
    private const int _readPageSize = 1;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProjectionEventDto>> ReadAsync(
        AggregateIdentity identity,
        long throughSequence,
        CancellationToken cancellationToken = default) {
        var readable = new List<ProjectionEventDto>();
        await foreach (ProjectionEventDto[] page in ReadPagesAsync(identity, throughSequence, cancellationToken)
            .ConfigureAwait(false)) {
            readable.AddRange(page);
        }

        return readable;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ProjectionEventDto[]> ReadPagesAsync(
        AggregateIdentity identity,
        long throughSequence,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        await foreach (ProjectionEventDto[] page in ReadPagesFromAsync(identity, 0,
            throughSequence, cancellationToken).ConfigureAwait(false)) {
            yield return page;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ProjectionEventDto[]> ReadPagesFromAsync(
        AggregateIdentity identity,
        long afterSequence,
        long throughSequence,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(throughSequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(afterSequence, throughSequence);
        if (throughSequence == afterSequence) {
            yield break;
        }

        IAggregateActor aggregate = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            actorOptions.Value.AggregateActorTypeName);
        while (afterSequence < throughSequence) {
            cancellationToken.ThrowIfCancellationRequested();
            EventEnvelope[] page = await RouteCatalogRemoteCall.InvokeAsync(() => aggregate
                .ReadEventsRangeAsync(afterSequence, throughSequence, _readPageSize))
                .ConfigureAwait(false);
            long nextSequence = ProjectionStreamPageValidation.Validate(
                identity,
                afterSequence,
                throughSequence,
                _readPageSize,
                page);
            ProjectionEventReadabilityResult readability = await ProjectionEventWireBuilder
                .BuildAsync(payloadProtectionService, identity, page, cancellationToken)
                .ConfigureAwait(false);
            if (readability.Events is null) {
                if (readability.UnreadableReason == UnreadableProtectedDataReason.ProviderUnavailable) {
                    throw new InvalidOperationException("Authoritative projection history is temporarily unavailable.");
                }

                throw new ProjectionDeliveryHistoryValidationException(
                    "Authoritative projection history is not readable.");
            }

            if (readability.Events.Length != page.Length) {
                throw new ProjectionDeliveryHistoryValidationException(
                    "Authoritative projection history did not decode every event.");
            }

            yield return readability.Events;
            afterSequence = nextSequence;
        }
    }
}
