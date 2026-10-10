#if P1R_CANDIDATE
using Dapr.Client;
using Hexalith.EventStore.Server.Commands;

/// <summary>Persists bounded, metadata-only qualification audit entries under unique keys.</summary>
internal sealed class FixtureTrustedEffectAuditSink(DaprClient client) : ITrustedEffectAuditSink
{
    /// <inheritdoc/>
    public Task AppendAsync(TrustedEffectAuditRecord record, CancellationToken cancellationToken = default)
        => client.SaveStateAsync("statestore", "p1r-qualification-audit-" + Guid.NewGuid().ToString("N"),
            record, cancellationToken: cancellationToken);
}
#endif
