
using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Fake implementation of <see cref="IDeadLetterPublisher"/> for unit testing.
/// Thread-safe for concurrent test scenarios. Captures all published dead-letter messages
/// and supports configurable failure mode.
/// </summary>
public sealed class FakeDeadLetterPublisher : IDeadLetterPublisher {
    private readonly ConcurrentBag<(AggregateIdentity Identity, DeadLetterMessage Message)> _messages = [];
    private string? _failureMessage;

    /// <inheritdoc/>
    public Task<bool> PublishDeadLetterAsync(
        AggregateIdentity identity,
        DeadLetterMessage message,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (_failureMessage is not null) {
            return Task.FromResult(false);
        }

        _messages.Add((identity, message));
        return Task.FromResult(true);
    }

    /// <summary>
    /// Configures all publish calls to return false (simulating dead-letter publication failure).
    /// </summary>
    /// <param name="failureMessage">The failure reason for diagnostics.</param>
    public void SetupFailure(string failureMessage = "Dead-letter pub/sub unavailable") => _failureMessage = failureMessage;

    /// <summary>
    /// Returns all published dead-letter messages.
    /// </summary>
    public IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> GetDeadLetterMessages()
        => [.. _messages];

    /// <summary>
    /// Returns dead-letter messages filtered by tenant ID.
    /// </summary>
    /// <param name="tenantId">The tenant ID to filter by.</param>
    public IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> GetDeadLetterMessagesForTenant(string tenantId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return [.. _messages.Where(m => m.Identity.TenantId == tenantId)];
    }

    /// <summary>
    /// Finds a dead-letter message by correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID to search for.</param>
    /// <returns>The matching message tuple, or null if not found.</returns>
    public (AggregateIdentity Identity, DeadLetterMessage Message)? GetDeadLetterMessageByCorrelationId(string correlationId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        (AggregateIdentity Identity, DeadLetterMessage Message) match = _messages
            .FirstOrDefault(m => m.Message.CorrelationId == correlationId);
        return match.Message is null ? null : match;
    }

    /// <summary>
    /// Asserts that no dead-letter messages have been published.
    /// Throws if any messages exist.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when dead-letter messages were found.</exception>
    public void AssertNoDeadLetters() {
        if (!_messages.IsEmpty) {
            throw new InvalidOperationException(
                $"Expected no dead-letter messages, but found {_messages.Count} message(s).");
        }
    }

    /// <summary>
    /// Clears all captured messages and failure setup.
    /// </summary>
    public void Reset() {
        _messages.Clear();
        _failureMessage = null;
    }
}
