namespace Hexalith.EventStore.Server.Commands;

/// <summary>Platform-owned append-only privileged audit sink for trusted effects.</summary>
public interface ITrustedEffectAuditSink
{
    /// <summary>Durably appends one metadata-only entry, or throws before privileged mutation proceeds.</summary>
    Task AppendAsync(TrustedEffectAuditRecord record, CancellationToken cancellationToken = default);
}
