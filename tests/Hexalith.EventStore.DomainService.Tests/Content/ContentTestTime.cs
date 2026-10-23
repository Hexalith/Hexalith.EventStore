namespace Hexalith.EventStore.DomainService.Tests.Content;

internal sealed class ContentTestTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
