namespace Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;

/// <summary>Authorized evidence for the open poison records visible to a caller.</summary>
/// <param name="Count">The authorized open record count.</param>
/// <param name="OldestAge">The age of the oldest authorized failure, when it exists.</param>
public sealed record DeadLetterSummary(int Count, TimeSpan? OldestAge);
