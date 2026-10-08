using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Actual DAPR-backed conditional publication index owner; never registered as an available default.</summary>
/// <param name="host">The runtime-scoped private technical actor.</param>
/// <param name="operations">Independent current private method/request authority; absent defaults deny.</param>
public sealed class SourcePublicationIndexActor(ActorHost host, ISourcePublicationOperationAuthority? operations = null) : Actor(host), ISourcePublicationIndexActor
{
    private const string StateKey = "source-publications-v1";
    /// <summary>Gets the exact actor registration name for a qualified private host binding.</summary>
    public const string ActorTypeName = "SourcePublicationIndexActor";

    /// <inheritdoc/>
    public async Task<SourcePublicationIndexState?> ReadAsync(SourcePublicationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (Host.Id.GetId() != scope.ActorId) { throw new ArgumentException("Publication index scope mismatch.", nameof(scope)); }
        if (operations is null || !await operations.ReadIndexAsync(scope).ConfigureAwait(false)) { return null; }
        var result = await ReadStateAsync(scope).ConfigureAwait(false);
        return await operations.ReadIndexAsync(scope).ConfigureAwait(false) ? result : null;
    }
    private async Task<SourcePublicationIndexState?> ReadStateAsync(SourcePublicationScope scope)
    {
        // A preceding failed/unknown SaveState may leave staged cached data. Only a fresh durable read may be certified.
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var state = await StateManager.TryGetStateAsync<SourcePublicationIndexState>(StateKey).ConfigureAwait(false);
        return state.HasValue ? Capture(state.Value, scope) : null;
    }

    /// <inheritdoc/>
    public async Task<bool> TryWriteAsync(SourcePublicationIndexWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(write.State);
        if (write.ExpectedRevision < 0 || write.State.Revision != checked(write.ExpectedRevision + 1))
        { throw new ArgumentException("Invalid publication compare revision.", nameof(write)); }
        if (Host.Id.GetId() != write.State.Scope.ActorId) { throw new ArgumentException("Publication index scope mismatch.", nameof(write)); }
        SourcePublicationIndexState owned = Capture(write.State, write.State.Scope);
        var request = write with { State = owned };
        if (operations is null || !await operations.WriteIndexAsync(request).ConfigureAwait(false)) { return false; }
        SourcePublicationIndexState? current = await ReadStateAsync(write.State.Scope).ConfigureAwait(false);
        if ((current?.Revision ?? 0) != write.ExpectedRevision || current?.PoisonCode is not null) { return false; }
        // Runtime actor turns serialize this compare and SaveState. Copy owned collections before persistence.
        // ReadAsync always clears cache, including after SetState/SaveState faults or a lost commit acknowledgement.
        await StateManager.SetStateAsync(StateKey, owned).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return await operations.WriteIndexAsync(request).ConfigureAwait(false);
    }
    private static SourcePublicationIndexState Capture(SourcePublicationIndexState state, SourcePublicationScope scope)
    {
        if (state.Scope != scope || state.Revision <= 0 || string.IsNullOrWhiteSpace(state.AuthorityRevision)
            || state.AuthorityRevision.Length > 256 || state.Sources is null || state.Entries is null
            || state.Sources.Count is < 0 or > 1000 || state.Entries.Count is < 0 or > 10000
            || state.PoisonCode is not (null or "publication-source-regressed" or "publication-source-incompatible"
                or "publication-reference-conflict" or "publication-source-disappeared"))
        { throw new InvalidOperationException("Malformed publication index."); }
        var heads = new List<SourcePublicationHead>();
        foreach (SourcePublicationHead head in state.Sources)
        {
            if (heads.Count >= 1000 || head is null || head.Identity is null || head.Head is < 0 or > 10000
                || head.Identity.TenantId != scope.Tenant || head.Identity.Domain != scope.Domain
                || heads.Any(h => h.Identity == head.Identity))
            { throw new InvalidOperationException("Malformed publication source vector."); }
            heads.Add(head);
        }
        var entries = new List<SourcePublicationIndexEntry>();
        foreach (SourcePublicationIndexEntry entry in state.Entries)
        {
            if (entries.Count >= 10000 || entry is null || entry.Offset != entries.Count + 1L || entry.Publication is not { } publication
                || publication.Identity is null || publication.SourceRevision is < 1 or > 10000
                || string.IsNullOrWhiteSpace(publication.PublicationId) || publication.PublicationId.Length > 256
                || string.IsNullOrWhiteSpace(publication.SourceMessageId) || publication.SourceMessageId.Length > 256
                || publication.StableFieldsDigest is not { Length: 64 } digest
                || !digest.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')
                || !heads.Any(h => h.Identity == publication.Identity && h.Head >= publication.SourceRevision)
                || entries.Any(e => e.Publication.PublicationId == publication.PublicationId
                    || e.Publication.Identity == publication.Identity && e.Publication.SourceRevision == publication.SourceRevision))
            { throw new InvalidOperationException("Malformed publication reference index."); }
            entries.Add(entry);
        }
        return state with { Sources = heads.AsReadOnly(), Entries = entries.AsReadOnly() };
    }
}
