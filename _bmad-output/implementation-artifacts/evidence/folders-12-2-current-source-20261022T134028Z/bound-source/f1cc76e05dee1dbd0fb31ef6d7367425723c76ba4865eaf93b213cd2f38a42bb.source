
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Testing.Assertions;
using Hexalith.EventStore.Testing.Builders;

namespace Hexalith.EventStore.Testing.Tests.Assertions;

public class EventSequenceAssertionsTests {
    [Fact]
    public void ShouldHaveSequentialNumbers_passes_for_sequential_envelopes() {
        EventEnvelope[] envelopes = new[]
        {
            new EventEnvelopeBuilder().WithSequenceNumber(1).Build(),
            new EventEnvelopeBuilder().WithSequenceNumber(2).Build(),
            new EventEnvelopeBuilder().WithSequenceNumber(3).Build(),
        };

        EventSequenceAssertions.ShouldHaveSequentialNumbers(envelopes);
    }

    [Fact]
    public void ShouldHaveSequentialNumbers_passes_for_offset_sequential() {
        EventEnvelope[] envelopes = new[]
        {
            new EventEnvelopeBuilder().WithSequenceNumber(5).Build(),
            new EventEnvelopeBuilder().WithSequenceNumber(6).Build(),
        };

        EventSequenceAssertions.ShouldHaveSequentialNumbers(envelopes);
    }

    [Fact]
    public void ShouldHaveSequentialNumbers_fails_for_non_sequential() {
        EventEnvelope[] envelopes = new[]
        {
            new EventEnvelopeBuilder().WithSequenceNumber(1).Build(),
            new EventEnvelopeBuilder().WithSequenceNumber(3).Build(),
        };

        _ = Assert.ThrowsAny<Exception>(() => EventSequenceAssertions.ShouldHaveSequentialNumbers(envelopes));
    }
}
