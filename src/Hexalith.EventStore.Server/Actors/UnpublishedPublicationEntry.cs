namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// One outstanding committed-but-unpublished command tracked by
/// <see cref="UnpublishedPublicationIndex"/>.
/// Entries stay deliberately minimal: the drain record (<see cref="UnpublishedEventsRecord"/>) or
/// the <see cref="PipelineState"/> checkpoint remains the source of truth for the persisted
/// sequence range. The entry only says <em>which</em> command still owes a publication and where
/// its checkpoint lives.
/// </summary>
/// <param name="MessageId">The command message identifier, which is also the drain tracking id.</param>
/// <param name="CorrelationId">The correlation identifier used to locate the pipeline checkpoint.</param>
/// <param name="CommittedAt">When the committing batch staged this entry.</param>
public record UnpublishedPublicationEntry(
    string MessageId,
    string CorrelationId,
    DateTimeOffset CommittedAt) {
    /// <summary>
    /// Gets a value indicating whether the entry still carries the two identities recovery needs.
    /// A malformed entry (blank message id or blank correlation id) can never be re-armed and must
    /// be pruned rather than skipped, because skipping it permanently consumes index capacity.
    /// </summary>
    public bool IsWellFormed
        => !string.IsNullOrWhiteSpace(MessageId) && !string.IsNullOrWhiteSpace(CorrelationId);
}
