using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Produces a qualified finite source-head vector from a durable installed roster and actual committed actor metadata.</summary>
/// <remarks>Independent coverage/all-writer evidence and restricted actor credentials are mandatory; no default claims completeness.</remarks>
public sealed class DaprSourcePublicationNamespaceSource(IActorProxyFactory proxies, ISourcePublicationNamespaceAuthority authority,
    TimeProvider clock) : ISourcePublicationNamespaceSource
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
                var installed = await deadline.ReadAsync(_ => actor.ReadAsync(scope)).ConfigureAwait(false);
                if (installed is null) { return null; }
                installed = await deadline.ReadAsync(_ => Task.FromResult(SourcePublicationNamespaceActor.Capture(installed))).ConfigureAwait(false);
                if (installed.Scope != scope) { return null; }
                var approved = await deadline.ReadAsync(token => authority.AuthorizeAsync(installed, token)).ConfigureAwait(false);
                if (!Current(approved)) { return null; }
                var heads = new List<SourcePublicationHead>();
                foreach (AggregateIdentity identity in installed.Sources)
                {
                    deadline.ThrowIfCancellationRequested();
                    var source = proxies.CreateActorProxy<IAggregateActor>(new(identity.ActorId), "AggregateActor");
                    AggregateStreamMetadata metadata = await deadline.ReadAsync(_ => source.GetStreamMetadataAsync()).ConfigureAwait(false);
                    if (metadata.CurrentSequence is < 0 or > 10000 || !metadata.Exists && metadata.CurrentSequence != 0) { return null; }
                    heads.Add(new(identity, metadata.CurrentSequence));
                }
                // Reconfirm each monotonic committed head; reservations/global maxima never enter this vector.
                foreach (SourcePublicationHead head in heads)
                {
                    var source = proxies.CreateActorProxy<IAggregateActor>(new(head.Identity.ActorId), "AggregateActor");
                    AggregateStreamMetadata current = await deadline.ReadAsync(_ => source.GetStreamMetadataAsync()).ConfigureAwait(false);
                    if (current.CurrentSequence != head.Head || !current.Exists && current.CurrentSequence != 0) { return null; }
                }
                var final = await deadline.ReadAsync(_ => actor.ReadAsync(scope)).ConfigureAwait(false);
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
    private bool Current(SourcePublicationNamespaceAuthorization? result) => result is not null
        && !string.IsNullOrWhiteSpace(result.AuthorityRevision) && result.AuthorityRevision.Length <= 200 && result.ValidUntil > clock.GetUtcNow();
}
