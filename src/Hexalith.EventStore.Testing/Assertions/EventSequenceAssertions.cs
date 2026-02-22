
using Hexalith.EventStore.Contracts.Events;

using Xunit;

namespace Hexalith.EventStore.Testing.Assertions;
/// <summary>
/// Static assertion helpers for verifying event envelope sequence numbers.
/// </summary>
public static class EventSequenceAssertions {
    /// <summary>
    /// Verifies that the event envelopes have strictly sequential sequence numbers (N, N+1, N+2, ...).
    /// </summary>
    /// <param name="envelopes">The event envelopes to verify.</param>
    public static void ShouldHaveSequentialNumbers(IReadOnlyList<EventEnvelope> envelopes) {
        Assert.NotNull(envelopes);
        Assert.NotEmpty(envelopes);

        for (int i = 1; i < envelopes.Count; i++) {
            long expected = envelopes[i - 1].Metadata.SequenceNumber + 1;
            long actual = envelopes[i].Metadata.SequenceNumber;
            Assert.Equal(expected, actual);
        }
    }
}
