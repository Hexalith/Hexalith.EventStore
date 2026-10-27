#if FLOOR
using Dapr.Client;
using Hexalith.EventStore.Server.Commands;

namespace PublishedConsumer;

/// <summary>Durably appends synthetic metadata-only effect audit bookkeeping through Dapr.</summary>
/// <param name="client">The Dapr-owned application state client.</param>
public sealed class FixtureEffectAuditSink(DaprClient client) : ITrustedEffectAuditSink
{
    /// <inheritdoc/>
    public Task AppendAsync(TrustedEffectAuditRecord record, CancellationToken cancellationToken = default)
        => client.SaveStateAsync("statestore", "p1r:effect-audit:" + Guid.NewGuid().ToString("N"), record, cancellationToken: cancellationToken);
}
#endif
