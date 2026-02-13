namespace Hexalith.EventStore.Testing.Assertions;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

using Xunit;

/// <summary>
/// Static assertion helpers for verifying <see cref="DomainResult"/> instances in tests.
/// </summary>
public static class DomainResultAssertions
{
    /// <summary>
    /// Verifies the result is successful with the expected event count.
    /// </summary>
    /// <param name="result">The domain result to verify.</param>
    /// <param name="expectedCount">The expected number of events.</param>
    public static void ShouldBeSuccess(DomainResult result, int expectedCount)
    {
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "Expected DomainResult to be a success, but it was not.");
        Assert.Equal(expectedCount, result.Events.Count);
    }

    /// <summary>
    /// Verifies the result contains rejection events.
    /// </summary>
    /// <param name="result">The domain result to verify.</param>
    public static void ShouldBeRejection(DomainResult result)
    {
        Assert.NotNull(result);
        Assert.True(result.IsRejection, "Expected DomainResult to be a rejection, but it was not.");
    }

    /// <summary>
    /// Verifies the result has no events (no-op).
    /// </summary>
    /// <param name="result">The domain result to verify.</param>
    public static void ShouldBeNoOp(DomainResult result)
    {
        Assert.NotNull(result);
        Assert.True(result.IsNoOp, "Expected DomainResult to be a no-op, but it was not.");
    }

    /// <summary>
    /// Verifies the result contains an event of the specified type.
    /// </summary>
    /// <typeparam name="TEvent">The expected event type.</typeparam>
    /// <param name="result">The domain result to verify.</param>
    public static void ShouldContainEvent<TEvent>(DomainResult result)
        where TEvent : IEventPayload
    {
        Assert.NotNull(result);
        Assert.Contains(result.Events, e => e is TEvent);
    }
}
