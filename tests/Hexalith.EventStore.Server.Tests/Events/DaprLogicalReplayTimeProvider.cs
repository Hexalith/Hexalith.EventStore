namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Controls current key validity across an actor save and independent recovery readback.</summary>
internal sealed class DaprLogicalReplayTimeProvider : TimeProvider
{
    /// <summary>Gets or sets current UTC time.</summary>
    internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => Now;
}
