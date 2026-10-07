namespace SnapshotAliasProbe;

/// <summary>Demonstrates the unresolved caller-owned typed snapshot alias.</summary>
public sealed class MutationState
{
    /// <summary>Gets the accumulated mutation.</summary>
    public int Total { get; private set; }

    /// <summary>Mutates, then optionally throws.</summary>
    /// <param name="value">The diagnostic tail event.</param>
    public void Apply(MutationEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Total += value.Amount;
        if (value.Fail) { throw new InvalidOperationException("diagnostic-after-mutation"); }
    }
}
