// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V079-V106, V127-V132.
namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
internal sealed class AdapterTestClock : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddTicks(_ticks);
    internal void Advance(TimeSpan duration) => _ticks += duration.Ticks;
}
