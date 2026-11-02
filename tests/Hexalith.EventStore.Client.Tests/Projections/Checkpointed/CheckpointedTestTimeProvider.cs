namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>A manually advanced lease clock.</summary>
internal sealed class CheckpointedTestTimeProvider : TimeProvider {
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
