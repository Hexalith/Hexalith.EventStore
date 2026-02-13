namespace Hexalith.EventStore.Testing.Tests.Assertions;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Testing.Assertions;
using Hexalith.EventStore.Testing.Builders;

public class EventSequenceAssertionsTests
{
    [Fact]
    public void ShouldHaveSequentialNumbers_passes_for_sequential_envelopes()
    {
        var envelopes = new[]
        {
            new EventEnvelopeBuilder().WithSequenceNumber(1).Build(),
            new EventEnvelopeBuilder().WithSequenceNumber(2).Build(),
            new EventEnvelopeBuilder().WithSequenceNumber(3).Build(),
        };

        EventSequenceAssertions.ShouldHaveSequentialNumbers(envelopes);
    }

    [Fact]
    public void ShouldHaveSequentialNumbers_passes_for_offset_sequential()
    {
        var envelopes = new[]
        {
            new EventEnvelopeBuilder().WithSequenceNumber(5).Build(),
            new EventEnvelopeBuilder().WithSequenceNumber(6).Build(),
        };

        EventSequenceAssertions.ShouldHaveSequentialNumbers(envelopes);
    }

    [Fact]
    public void ShouldHaveSequentialNumbers_fails_for_non_sequential()
    {
        var envelopes = new[]
        {
            new EventEnvelopeBuilder().WithSequenceNumber(1).Build(),
            new EventEnvelopeBuilder().WithSequenceNumber(3).Build(),
        };

        Assert.ThrowsAny<Exception>(() => EventSequenceAssertions.ShouldHaveSequentialNumbers(envelopes));
    }
}
