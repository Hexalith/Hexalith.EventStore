
namespace Hexalith.EventStore.Client.Registration;

/// <summary>
/// Holds the runtime activation manifest populated by <c>UseEventStore()</c>.
/// Registered as a singleton during <c>AddEventStore()</c> and populated during <c>UseEventStore()</c>.
/// Thread-safe via <see cref="Interlocked.CompareExchange(ref int, int, int)"/>.
/// </summary>
public sealed class EventStoreActivationContext {
    private volatile IReadOnlyList<EventStoreDomainActivation>? _activations;
    private int _activated; // 0 = not activated, 1 = activated

    /// <summary>
    /// Gets a value indicating whether <c>UseEventStore()</c> has been called and activation is complete.
    /// </summary>
    public bool IsActivated => _activated != 0;

    /// <summary>
    /// Gets the list of activated domain metadata records.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if <c>UseEventStore()</c> has not been called.</exception>
    public IReadOnlyList<EventStoreDomainActivation> Activations
        => _activations ?? throw new InvalidOperationException(
            "UseEventStore() has not been called. Call app.UseEventStore() after building the host.");

    /// <summary>
    /// Attempts to set the activation manifest. Returns <c>false</c> if already activated (idempotency).
    /// </summary>
    /// <param name="activations">The domain activation records to store.</param>
    /// <returns><c>true</c> if activation succeeded; <c>false</c> if already activated.</returns>
    internal bool TryActivate(IReadOnlyList<EventStoreDomainActivation> activations) {
        if (Interlocked.CompareExchange(ref _activated, 1, 0) != 0) {
            return false; // Already activated
        }

        _activations = activations;
        return true;
    }
}
