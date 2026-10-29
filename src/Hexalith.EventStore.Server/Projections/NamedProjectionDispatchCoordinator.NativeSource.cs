using Hexalith.EventStore.ServiceDefaults.Routing;

using Dapr.Actors;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

internal sealed partial class NamedProjectionDispatchCoordinator
{
    private async Task<NativeSourceProof?> CaptureNativePrefixAsync(AggregateIdentity identity,
        EventEnvelope[] events, CancellationToken cancellationToken)
    {
        // These are the producer's original persisted envelopes, before payload evolution or
        // materialization. A later native head can never certify a different/older input.
        using var deadline = new Hexalith.EventStore.Client.Streams.AuthoritativeStreamReadDeadline(
            TimeSpan.FromSeconds(2), _timeProvider, cancellationToken, _timeProvider.GetTimestamp());
        try
        {
            return await deadline.ReadWithAdmissionAsync<NativeSourceProof?>(async captureToken =>
            {
                if (events.Any(item => Hexalith.EventStore.Contracts.Security.EventStorePayloadProtectionMetadataCarrier
                    .Read(item.Extensions).State != Hexalith.EventStore.Contracts.Security.PayloadProtectionState.Unprotected)) return null;
                captureToken.ThrowIfCancellationRequested();
                IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                    new ActorId(identity.ActorId), AggregateActorTypeName);
                var before = await RouteCatalogRemoteCall.InvokeAsync(() => actor!.InspectSourceProofAsync()).ConfigureAwait(false);
                if (before is null || before.Status != SourceProofInspectionStatus.Available || !before.Exists || before.Erased || before.Proof is not { RetainedFloor: 1, Erased: false } proof
                    || proof.HeadSequence != events.LongLength
                    || !SourceProofCodec.IsValid(proof, identity, events.LongLength, 1)) return null;
                SourceProof chain = SourceProofCodec.CreateSeed(identity, proof.SourceIncarnation);
                foreach (EventEnvelope item in events)
                {
                    captureToken.ThrowIfCancellationRequested();
                    chain = SourceProofCodec.Extend(chain, item);
                }
                if (chain != proof) return null;
                captureToken.ThrowIfCancellationRequested();
                var after = await RouteCatalogRemoteCall.InvokeAsync(() => actor!.InspectSourceProofAsync()).ConfigureAwait(false);
                return before == after ? SourceProofCodec.ToNative(proof) : null;
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return null; } // optional proof never changes ordinary legacy delivery
    }
}
