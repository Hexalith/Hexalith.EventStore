
using System.Collections.Concurrent;

using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>
/// Test double for <see cref="IETagActor"/>.
/// Records invocations and returns configurable results.
/// Follows the same pattern as <see cref="FakeProjectionActor"/>.
/// </summary>
public class FakeETagActor : IETagActor {
    private readonly ConcurrentQueue<string> _regeneratedETags = new();
    private readonly ConcurrentQueue<DateTimeOffset> _receivedNotifications = new();

    /// <summary>Gets or sets the ETag value returned by <see cref="GetCurrentETagAsync"/>.</summary>
    public string? ConfiguredETag { get; set; }

    /// <summary>Gets or sets the exception to throw from <see cref="RegenerateAsync"/>.</summary>
    public Exception? ConfiguredException { get; set; }

    /// <summary>Gets the number of times <see cref="RegenerateAsync"/> was called.</summary>
    public int RegenerateCount => _regeneratedETags.Count;

    /// <summary>Gets the list of regenerated ETag values for assertion.</summary>
    public IReadOnlyCollection<string> RegeneratedETags => [.. _regeneratedETags];

    /// <summary>Gets markers representing notifications received by the actor.</summary>
    public IReadOnlyCollection<DateTimeOffset> ReceivedNotifications => [.. _receivedNotifications];

    /// <inheritdoc/>
    public Task<string?> GetCurrentETagAsync() => Task.FromResult(ConfiguredETag);

    /// <inheritdoc/>
    public Task<string> RegenerateAsync() {
        if (ConfiguredException is not null) {
            throw ConfiguredException;
        }

        string newETag = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        _receivedNotifications.Enqueue(DateTimeOffset.UtcNow);
        _regeneratedETags.Enqueue(newETag);
        ConfiguredETag = newETag;
        return Task.FromResult(newETag);
    }

    /// <summary>
    /// Resets all state for test isolation.
    /// </summary>
    public void Reset() {
        ConfiguredETag = null;
        ConfiguredException = null;
        while (_receivedNotifications.TryDequeue(out _)) { }
        while (_regeneratedETags.TryDequeue(out _)) { }
    }
}
