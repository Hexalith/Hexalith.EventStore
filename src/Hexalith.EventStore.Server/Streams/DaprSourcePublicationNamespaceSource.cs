using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Produces a qualified finite source-head vector from a durable installed roster and actual committed actor metadata.</summary>
/// <remarks>Independent coverage/all-writer evidence and restricted actor credentials are mandatory; no default claims completeness.</remarks>
public sealed class DaprSourcePublicationNamespaceSource(IActorProxyFactory proxies, ISourcePublicationNamespaceAuthority authority,
    TimeProvider clock, SourcePublicationPrimaryClient? authenticated = null) : ISourcePublicationNamespaceSource
{
    /// <inheritdoc/>
    public async Task<SourcePublicationCut?> ReadAsync(SourcePublicationScope scope, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(scope);
            using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
            try
            {
                var actor = proxies.CreateActorProxy<ISourcePublicationNamespaceActor>(new(scope.ActorId), SourcePublicationNamespaceActor.ActorTypeName);
                var installed = await deadline.ReadAsync(token => authenticated is null ? actor.ReadAsync(scope)
                    : authenticated.InvokeAsync<SourcePublicationNamespaceState>(scope, "read-namespace", scope, token)).ConfigureAwait(false);
                if (installed is null) { return null; }
                installed = await deadline.ReadAsync(_ => Task.FromResult(SourcePublicationNamespaceActor.Capture(installed))).ConfigureAwait(false);
                if (installed.Scope != scope) { return null; }
                var approved = await deadline.ReadAsync(token => authority.AuthorizeAsync(installed, token)).ConfigureAwait(false);
                if (!Current(approved)) { return null; }
                var heads = new List<SourcePublicationHead>();
                var inspections = new Dictionary<AggregateIdentity, SourceProofInspection>();
                foreach (AggregateIdentity identity in installed.Sources)
                {
                    deadline.ThrowIfCancellationRequested();
                    var source = proxies.CreateActorProxy<IAggregateActor>(new(identity.ActorId), "AggregateActor");
                    SourceProofInspection inspection = await deadline.ReadAsync(_ => source.InspectSourceProofAsync()).ConfigureAwait(false);
                    if (!Valid(identity, inspection)) { return null; }
                    inspections.Add(identity, inspection);
                    SourceProof proof = inspection.Proof!;
                    heads.Add(new(identity, inspection.CurrentSequence)
                    {
                        NativeProof = new(proof.CodecVersion, proof.TenantId, proof.Domain, proof.AggregateId,
                            proof.SourceIncarnation, proof.HeadSequence, proof.HeadMessageId, proof.CumulativeDigest,
                            proof.RetainedFloor, proof.Erased),
                    });
                }
                // Compare the full native incarnation/prefix/erasure proof, not just a sequence that
                // could belong to a replaced source. This probe does not invoke stream recovery.
                foreach (SourcePublicationHead head in heads)
                {
                    var source = proxies.CreateActorProxy<IAggregateActor>(new(head.Identity.ActorId), "AggregateActor");
                    SourceProofInspection current = await deadline.ReadAsync(_ => source.InspectSourceProofAsync()).ConfigureAwait(false);
                    if (!Valid(head.Identity, current) || current != inspections[head.Identity]) { return null; }
                }
                var final = await deadline.ReadAsync(token => authenticated is null ? actor.ReadAsync(scope)
                    : authenticated.InvokeAsync<SourcePublicationNamespaceState>(scope, "read-namespace", scope, token)).ConfigureAwait(false);
                if (final is null) { return null; }
                final = await deadline.ReadAsync(_ => Task.FromResult(SourcePublicationNamespaceActor.Capture(final))).ConfigureAwait(false);
                if (final.Scope != scope || final.Revision != installed.Revision || final.AuthorityRevision != installed.AuthorityRevision
                    || final.LegacyCoverageReceipt != installed.LegacyCoverageReceipt || final.WriterEnforcementReceipt != installed.WriterEnforcementReceipt
                    || !final.InitialSources!.SequenceEqual(installed.InitialSources!) || !final.Sources.SequenceEqual(installed.Sources)) { return null; }
                var currentAuthority = await deadline.ReadAsync(token => authority.AuthorizeAsync(final, token)).ConfigureAwait(false);
                deadline.ThrowIfCancellationRequested();
                if (!Current(currentAuthority) || currentAuthority != approved) { return null; }
                return new(scope, currentAuthority!.AuthorityRevision + ":" + final.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    clock.GetUtcNow(), currentAuthority.ValidUntil, heads.AsReadOnly(), true);
            }
            catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or InvalidOperationException or ArgumentException)
            { cancellationToken.ThrowIfCancellationRequested(); return null; }
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }
    private static bool Valid(AggregateIdentity identity, SourceProofInspection inspection)
        => inspection is { Status: SourceProofInspectionStatus.Available, Exists: true, Erased: false,
                CurrentSequence: >= 0 and <= 10000, Proof: { Erased: false, RetainedFloor: 1 } proof }
            && proof.TenantId == identity.TenantId && proof.Domain == identity.Domain
            && proof.AggregateId == identity.AggregateId && proof.HeadSequence == inspection.CurrentSequence
            && SourceProofCodec.IsValid(proof, identity, inspection.CurrentSequence, 1);

    private bool Current(SourcePublicationNamespaceAuthorization? result) => result is not null
        && !string.IsNullOrWhiteSpace(result.AuthorityRevision) && result.AuthorityRevision.Length <= 200 && result.ValidUntil > clock.GetUtcNow();
}
