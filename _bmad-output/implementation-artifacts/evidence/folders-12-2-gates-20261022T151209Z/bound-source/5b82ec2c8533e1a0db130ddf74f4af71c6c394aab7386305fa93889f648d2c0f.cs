namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>One native proof observation; the observation instant is separate from the persisted revision.</summary>
/// <param name="Proof">The exact native committed revision.</param>
/// <param name="ObservedAt">The actual metadata read instant.</param>
public sealed record NativeSourceObservation(NativeSourceProof Proof, DateTimeOffset ObservedAt);
