namespace Hexalith.EventStore.FoldedSnapshotFixture;

/// <summary>Mutable collections, lifecycle and history used for SDK restore/capture parity only.</summary>
public sealed class FoldedOrderState
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the order items.</summary>
    public Dictionary<string, string> Items { get; set; } = [];
    /// <summary>Gets or sets the item-change history.</summary>
    public List<string> Tags { get; set; } = [];
    /// <summary>Gets or sets the lifecycle state.</summary>
    public bool Closed { get; set; }
    /// <summary>Gets or sets membership-independent history.</summary>
    public bool HasItemHistory { get; set; }
    /// <summary>Gets or sets the original creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Gets or sets a fixture-only optional Apply failure.</summary>
    public bool ThrowOnApply { get; set; }

    /// <summary>Folds the exact persisted event into every supported state member.</summary>
    public void Apply(FoldedOrderChanged payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (ThrowOnApply) throw new InvalidOperationException("fixture-apply-refused");
        switch (payload.Operation)
        {
            case "create": Name = payload.Value; CreatedAt = DateTimeOffset.UnixEpoch; break;
            case "set": Items[payload.Key] = payload.Value; Tags.Add(payload.Value); HasItemHistory = true; break;
            case "remove": Items.Remove(payload.Key); break;
            case "rename": Name = payload.Value; break;
            case "close": Closed = true; break;
            case "open": Closed = false; break;
            default: throw new InvalidOperationException("fixture-operation-invalid");
        }
    }

    /// <summary>Rejections preserve the complete authoritative state.</summary>
    public void Apply(FoldedOrderRejected payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (ThrowOnApply) throw new InvalidOperationException("fixture-apply-refused");
    }
}
