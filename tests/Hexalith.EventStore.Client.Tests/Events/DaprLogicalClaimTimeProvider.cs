namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Controls current-key time and boundary cancellation/loss in logical claim tests.</summary>
internal sealed class DaprLogicalClaimTimeProvider : TimeProvider
{
    /// <summary>Gets or sets the observed current instant.</summary>
    internal DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
    /// <summary>Gets or sets a boundary callback.</summary>
    internal Action<int>? OnRead { get; set; }
    private int _reads;
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() { OnRead?.Invoke(++_reads); return Now; }
}
