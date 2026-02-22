namespace Hexalith.EventStore.Testing.Tests.Assertions;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Testing.Assertions;
using Hexalith.EventStore.Testing.Builders;

public class EventEnvelopeAssertionsTests {
    [Fact]
    public void ShouldHaveValidMetadata_passes_for_valid_envelope() {
        EventEnvelope envelope = new EventEnvelopeBuilder().Build();

        EventEnvelopeAssertions.ShouldHaveValidMetadata(envelope);
    }

    [Fact]
    public void ShouldHaveValidMetadata_fails_for_empty_event_type() {
        EventEnvelope envelope = new EventEnvelopeBuilder()
            .WithEventTypeName(" ")
            .Build();

        Assert.ThrowsAny<Exception>(() => EventEnvelopeAssertions.ShouldHaveValidMetadata(envelope));
    }
}
