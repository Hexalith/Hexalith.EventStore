namespace Hexalith.EventStore.Client.Projections;

/// <summary>Bounded CAS admission epoch and active preparations for an aggregate.</summary>
/// <param name="Epoch">The admission epoch, changed before retirement.</param>
/// <param name="Closed">Blocks registration while aggregate erasure publishes its canonical fence.</param>
/// <param name="Entries">Active content-free preparation registrations.</param>
internal sealed record ReadModelRecoveryDirectory(long Epoch, ReadModelRecoveryRegistration[] Entries, bool Closed = false);
