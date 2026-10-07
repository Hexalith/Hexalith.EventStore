namespace SnapshotAliasProbe;

/// <summary>One diagnostic tail event.</summary>
/// <param name="Amount">The mutation amount.</param>
/// <param name="Fail">Whether to throw after mutation.</param>
public sealed record MutationEvent(int Amount, bool Fail);
