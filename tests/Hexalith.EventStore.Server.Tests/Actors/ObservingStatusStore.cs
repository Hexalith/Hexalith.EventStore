using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Observes the committed actor state immediately before an advisory command status is written.</summary>
/// <param name="inner">The in-memory status store that retains the observed status.</param>
/// <param name="onWrite">The observer invoked before the status write.</param>
internal sealed class ObservingStatusStore(InMemoryCommandStatusStore inner, Action<CommandStatusRecord> onWrite) : ICommandStatusStore
{
    /// <inheritdoc />
    public Task WriteStatusAsync(string tenantId, string messageId, CommandStatusRecord status, CancellationToken cancellationToken = default)
    {
        onWrite(status);
        return inner.WriteStatusAsync(tenantId, messageId, status, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CommandStatusRecord?> ReadStatusAsync(string tenantId, string messageId, CancellationToken cancellationToken = default)
        => inner.ReadStatusAsync(tenantId, messageId, cancellationToken);
}
