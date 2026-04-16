namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// A diff between two aggregate state versions.
/// </summary>
/// <param name="FromSequence">The starting sequence number.</param>
/// <param name="ToSequence">The ending sequence number.</param>
/// <param name="ChangedFields">The list of fields that changed between the two versions.</param>
public record AggregateStateDiff(long FromSequence, long ToSequence, IReadOnlyList<FieldChange> ChangedFields) {
    /// <summary>Gets the list of fields that changed between the two versions.</summary>
    public IReadOnlyList<FieldChange> ChangedFields { get; } = ChangedFields ?? throw new ArgumentNullException(nameof(ChangedFields));
}
