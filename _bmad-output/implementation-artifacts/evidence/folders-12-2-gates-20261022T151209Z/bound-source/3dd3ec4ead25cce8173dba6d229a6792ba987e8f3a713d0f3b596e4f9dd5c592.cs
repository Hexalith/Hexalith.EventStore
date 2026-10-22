namespace Hexalith.EventStore.Client.Tests.Projections;

internal sealed class ProjectionDeadlineTimeProvider(DateTimeOffset now) : TimeProvider
{
    internal DateTimeOffset UtcNow { get; set; } = now;

    internal Func<DateTimeOffset>? Read { get; set; }

    public override DateTimeOffset GetUtcNow() => Read?.Invoke() ?? UtcNow;
}
