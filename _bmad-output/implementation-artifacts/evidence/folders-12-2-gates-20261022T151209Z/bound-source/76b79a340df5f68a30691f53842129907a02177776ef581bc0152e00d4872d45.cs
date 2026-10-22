namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>A time provider whose current instant only moves when a test advances it.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    /// <summary>Gets or sets the current UTC instant.</summary>
    public DateTimeOffset Now { get; set; } = start;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => Now;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="delta">The elapsed time.</param>
    public void Advance(TimeSpan delta) => Now += delta;
}
