using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Actual durable DAPR installed roster; no production actor registration is made by this type.</summary>
/// <param name="host">The private namespace-scoped actor host.</param>
/// <param name="operations">Independent current private operation credential verifier; absent defaults deny.</param>
public sealed class SourcePublicationNamespaceActor(ActorHost host, ISourcePublicationOperationAuthority? operations = null) : Actor(host), ISourcePublicationNamespaceActor
{
    private const string StateKey = "source-publication-namespace-v1";
    /// <summary>Gets the private actor registration name.</summary>
    public const string ActorTypeName = "SourcePublicationNamespaceActor";
    /// <inheritdoc/>
    public async Task<SourcePublicationNamespaceState?> ReadAsync(SourcePublicationScope scope)
    {
        CheckScope(scope);
        if (operations is null || !await operations.ReadNamespaceAsync(scope).ConfigureAwait(false)) { return null; }
        var result = await ReadStateAsync(scope).ConfigureAwait(false);
        return await operations.ReadNamespaceAsync(scope).ConfigureAwait(false) ? result : null;
    }
    private async Task<SourcePublicationNamespaceState?> ReadStateAsync(SourcePublicationScope scope)
    {
        // Never certify staged data left by failed/unknown Set/SaveState acknowledgements.
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var state = await StateManager.TryGetStateAsync<SourcePublicationNamespaceState>(StateKey).ConfigureAwait(false);
        return state.HasValue ? Capture(state.Value) : null;
    }
    /// <inheritdoc/>
    public async Task<bool> InstallAsync(SourcePublicationNamespaceState installation)
    {
        ArgumentNullException.ThrowIfNull(installation); CheckScope(installation.Scope);
        var owned = Capture(installation);
        if (operations is null || !await operations.InstallNamespaceAsync(owned).ConfigureAwait(false)) { return false; }
        if (owned.Revision != 1) { throw new ArgumentException("Invalid installation revision.", nameof(installation)); }
        var current = await ReadStateAsync(owned.Scope).ConfigureAwait(false);
        if (current is not null)
        {
            return current.AuthorityRevision == owned.AuthorityRevision && current.LegacyCoverageReceipt == owned.LegacyCoverageReceipt
                && current.WriterEnforcementReceipt == owned.WriterEnforcementReceipt && current.InitialSources!.SequenceEqual(owned.InitialSources!)
                && await operations.InstallNamespaceAsync(owned).ConfigureAwait(false);
        }
        await StateManager.SetStateAsync(StateKey, owned).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false); return await operations.InstallNamespaceAsync(owned).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<bool> RegisterAsync(SourcePublicationScope scope, long expectedRevision, AggregateIdentity identity)
    {
        CheckScope(scope); ArgumentNullException.ThrowIfNull(identity);
        if (identity.TenantId != scope.Tenant || identity.Domain != scope.Domain || expectedRevision <= 0)
        { throw new ArgumentException("Invalid source registration.", nameof(identity)); }
        if (operations is null || !await operations.RegisterSourceAsync(scope, expectedRevision, identity).ConfigureAwait(false)) { return false; }
        var current = await ReadStateAsync(scope).ConfigureAwait(false);
        if (current is null) { return false; }
        if (current.Sources.Contains(identity)) { return await operations.RegisterSourceAsync(scope, expectedRevision, identity).ConfigureAwait(false); }
        if (current.Revision != expectedRevision || current.Sources.Count >= 1000) { return false; }
        var next = current with { Revision = checked(current.Revision + 1),
            Sources = Array.AsReadOnly(current.Sources.Append(identity).OrderBy(s => s.ActorId, StringComparer.Ordinal).ToArray()) };
        await StateManager.SetStateAsync(StateKey, next).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false); return await operations.RegisterSourceAsync(scope, expectedRevision, identity).ConfigureAwait(false);
    }
    private void CheckScope(SourcePublicationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (Host.Id.GetId() != scope.ActorId) { throw new ArgumentException("Namespace scope mismatch.", nameof(scope)); }
    }
    internal static SourcePublicationNamespaceState Capture(SourcePublicationNamespaceState state)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(state.Scope);
        if (state.Revision <= 0 || string.IsNullOrWhiteSpace(state.AuthorityRevision) || state.AuthorityRevision.Length > 256
            || string.IsNullOrWhiteSpace(state.LegacyCoverageReceipt) || state.LegacyCoverageReceipt.Length > 2048
            || string.IsNullOrWhiteSpace(state.WriterEnforcementReceipt) || state.WriterEnforcementReceipt.Length > 2048
            || state.Sources is null || state.Sources.Count is < 0 or > 1000)
        { throw new InvalidOperationException("Malformed installed namespace."); }
        var owned = new List<AggregateIdentity>();
        foreach (AggregateIdentity identity in state.Sources)
        {
            if (owned.Count >= 1000 || identity is null || identity.TenantId != state.Scope.Tenant || identity.Domain != state.Scope.Domain
                || owned.Contains(identity)) { throw new InvalidOperationException("Malformed namespace roster."); }
            owned.Add(identity);
        }
        if (state.Revision > 1 && state.InitialSources is null) { throw new InvalidOperationException("Missing immutable installation roster."); }
        var initial = new List<AggregateIdentity>();
        foreach (AggregateIdentity identity in state.InitialSources ?? state.Sources)
        {
            if (initial.Count >= 1000 || identity is null || !owned.Contains(identity) || initial.Contains(identity))
            { throw new InvalidOperationException("Malformed initial namespace roster."); }
            initial.Add(identity);
        }
        if (state.Revision == 1 && initial.Count != owned.Count)
        { throw new InvalidOperationException("Initial installation must contain its complete roster."); }
        return state with { Sources = Array.AsReadOnly(owned.OrderBy(s => s.ActorId, StringComparer.Ordinal).ToArray()),
            InitialSources = Array.AsReadOnly(initial.OrderBy(s => s.ActorId, StringComparer.Ordinal).ToArray()) };
    }
}
