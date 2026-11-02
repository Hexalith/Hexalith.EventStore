namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// The authoritative EventStore stream that a checkpointed projection record follows.
/// </summary>
/// <param name="TenantId">The tenant scope the stream is persisted under.</param>
/// <param name="Domain">The EventStore domain of the stream.</param>
/// <param name="AggregateId">The aggregate identifier of the stream.</param>
public sealed record CheckpointedProjectionStream(
    string TenantId,
    string Domain,
    string AggregateId) {
    /// <summary>Gets a value indicating whether every stream identity component is present.</summary>
    public bool IsComplete
        => !string.IsNullOrWhiteSpace(TenantId)
        && !string.IsNullOrWhiteSpace(Domain)
        && !string.IsNullOrWhiteSpace(AggregateId);
}
