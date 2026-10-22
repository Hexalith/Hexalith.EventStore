using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Real pre-persistence registration into explicitly configured installed scopes; missing scope qualification blocks configured writers.</summary>
public sealed class DaprSourcePublicationWriterRegistration : ISourcePublicationWriterRegistration
{
    private readonly SourcePublicationScope[] _scopes;
    private readonly IActorProxyFactory _proxies;
    private readonly ISourcePublicationNamespaceAuthority _authority;
    private readonly TimeProvider _clock;
    /// <summary>Copies bounded explicit host scope configuration; it does not install or qualify legacy coverage.</summary>
    public DaprSourcePublicationWriterRegistration(IReadOnlyList<SourcePublicationScope> scopes, IActorProxyFactory proxies,
        ISourcePublicationNamespaceAuthority authority, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        var owned = new List<SourcePublicationScope>();
        foreach (var scope in scopes)
        {
            if (owned.Count >= 1000 || scope is null || owned.Contains(scope)) { throw new ArgumentException("Invalid writer scopes.", nameof(scopes)); }
            owned.Add(scope);
        }
        _scopes = owned.ToArray(); _proxies = proxies; _authority = authority; _clock = clock;
    }
    /// <inheritdoc/>
    public async Task RegisterBeforeWriteAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(identity);
            using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), _clock, cancellationToken, _clock.GetTimestamp());
            foreach (SourcePublicationScope scope in _scopes.Where(s => s.Tenant == identity.TenantId && s.Domain == identity.Domain))
            {
                var actor = _proxies.CreateActorProxy<ISourcePublicationNamespaceActor>(new(scope.ActorId), SourcePublicationNamespaceActor.ActorTypeName);
                bool registered = false;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var state = await deadline.ReadAsync(_ => actor.ReadAsync(scope)).ConfigureAwait(false);
                    if (state is null) { throw new InvalidOperationException("Namespace is not installed."); }
                    state = await deadline.ReadAsync(_ => Task.FromResult(SourcePublicationNamespaceActor.Capture(state))).ConfigureAwait(false);
                    var authorized = await deadline.ReadAsync(token => _authority.AuthorizeAsync(state, token)).ConfigureAwait(false);
                    if (state.Scope != scope || authorized is null || string.IsNullOrWhiteSpace(authorized.AuthorityRevision)
                        || authorized.ValidUntil <= _clock.GetUtcNow()) { throw new InvalidOperationException("Namespace writer qualification unavailable."); }
                    if (!await deadline.ReadAsync(_ => actor.RegisterAsync(scope, state.Revision, identity)).ConfigureAwait(false)) { continue; }
                    var persisted = await deadline.ReadAsync(_ => actor.ReadAsync(scope)).ConfigureAwait(false);
                    if (persisted is null) { throw new InvalidOperationException("Namespace registration outcome unknown."); }
                    persisted = await deadline.ReadAsync(_ => Task.FromResult(SourcePublicationNamespaceActor.Capture(persisted))).ConfigureAwait(false);
                    var finalAuthorization = await deadline.ReadAsync(token => _authority.AuthorizeAsync(persisted, token)).ConfigureAwait(false);
                    deadline.ThrowIfCancellationRequested();
                    if (finalAuthorization is not null && finalAuthorization.AuthorityRevision == authorized.AuthorityRevision
                        && finalAuthorization.ValidUntil > _clock.GetUtcNow() && persisted.Scope == scope && persisted.AuthorityRevision == state.AuthorityRevision
                        && persisted.LegacyCoverageReceipt == state.LegacyCoverageReceipt && persisted.WriterEnforcementReceipt == state.WriterEnforcementReceipt
                        && persisted.Sources.Contains(identity)) { registered = true; break; }
                    throw new InvalidOperationException("Namespace registration outcome unknown.");
                }
                if (!registered) { throw new InvalidOperationException("Namespace registration conflicted."); }
            }
            deadline.ThrowIfCancellationRequested();
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }
}
