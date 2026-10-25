using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Actual DAPR actor transport for the durable conditional index. Provider registration and ACL are explicit host work.</summary>
/// <param name="actorProxyFactory">The host-configured private actor proxy factory.</param>
public sealed class DaprSourcePublicationIndexStore(IActorProxyFactory actorProxyFactory, SourcePublicationPrimaryClient? authenticated = null) : ISourcePublicationIndexStore
{
    /// <inheritdoc/>
    public async Task<SourcePublicationIndexState?> ReadAsync(SourcePublicationScope scope, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = authenticated is null ? await Proxy(scope).ReadAsync(scope).WaitAsync(cancellationToken).ConfigureAwait(false)
                : await authenticated.InvokeAsync<SourcePublicationIndexState>(scope, "read-index", scope, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }

    /// <inheritdoc/>
    public async Task<bool> TryWriteAsync(SourcePublicationScope scope, long expectedRevision, SourcePublicationIndexState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(state);
            if (state.Scope != scope) { throw new ArgumentException("Publication scope mismatch.", nameof(state)); }
            bool result = authenticated is null ? await Proxy(scope).TryWriteAsync(new(expectedRevision, state)).WaitAsync(cancellationToken).ConfigureAwait(false)
                : await authenticated.InvokeAsync<bool>(scope, "write-index", new SourcePublicationIndexWrite(expectedRevision, state), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }

    /// <inheritdoc/>
    public async Task<SourcePublicationDispatchProgress?> ReadDispatchProgressAsync(SourcePublicationCheckpoint cut, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cut); cancellationToken.ThrowIfCancellationRequested();
        try { var result = authenticated is null ? await Proxy(cut.Scope).ReadDispatchProgressAsync(cut).WaitAsync(cancellationToken).ConfigureAwait(false)
            : await authenticated.InvokeAsync<SourcePublicationDispatchProgress>(cut.Scope, "read-dispatch", cut, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); return result; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }
    /// <inheritdoc/>
    public async Task<SourcePublicationDispatchProgress?> AdvanceDispatchProgressAsync(SourcePublicationDispatchAdvance advance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(advance); cancellationToken.ThrowIfCancellationRequested();
        try { var result = authenticated is null ? await Proxy(advance.Cut.Scope).AdvanceDispatchProgressAsync(advance).WaitAsync(cancellationToken).ConfigureAwait(false)
            : await authenticated.InvokeAsync<SourcePublicationDispatchProgress>(advance.Cut.Scope, "advance-dispatch", advance, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); return result; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }
    /// <inheritdoc/>
    public async Task<SourcePublicationReconciliationProgress?> ReadReconciliationProgressAsync(SourcePublicationCut cut, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cut); cancellationToken.ThrowIfCancellationRequested();
        try { var result = authenticated is null ? await Proxy(cut.Scope).ReadReconciliationProgressAsync(cut).WaitAsync(cancellationToken).ConfigureAwait(false)
            : await authenticated.InvokeAsync<SourcePublicationReconciliationProgress>(cut.Scope, "read-reconciliation", cut, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); return result; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }
    /// <inheritdoc/>
    public async Task<SourcePublicationReconciliationProgress?> AdvanceReconciliationProgressAsync(SourcePublicationReconciliationAdvance advance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(advance); cancellationToken.ThrowIfCancellationRequested();
        try { var result = authenticated is null ? await Proxy(advance.Cut.Scope).AdvanceReconciliationProgressAsync(advance).WaitAsync(cancellationToken).ConfigureAwait(false)
            : await authenticated.InvokeAsync<SourcePublicationReconciliationProgress>(advance.Cut.Scope, "advance-reconciliation", advance, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); return result; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }
    private ISourcePublicationIndexActor Proxy(SourcePublicationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return actorProxyFactory.CreateActorProxy<ISourcePublicationIndexActor>(new ActorId(scope.ActorId), SourcePublicationIndexActor.ActorTypeName);
    }
}
