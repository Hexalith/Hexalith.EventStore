
using System.Collections.Concurrent;

using Hexalith.EventStore.Server.Actors.Authorization;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>
/// Fake RBAC validator actor for testing. Records all invocations for assertion.
/// Configurable results and exceptions for simulating various actor behaviors.
/// </summary>
public class FakeRbacValidatorActor : IRbacValidatorActor {
    private readonly ConcurrentQueue<RbacValidationRequest> _receivedRequests = new();

    /// <summary>Gets the list of received requests for assertion.</summary>
    public IReadOnlyCollection<RbacValidationRequest> ReceivedRequests => [.. _receivedRequests];

    /// <summary>Gets or sets the result to return from ValidatePermissionAsync.</summary>
    public ActorValidationResponse? ConfiguredResult { get; set; }

    /// <summary>Gets or sets the exception to throw from ValidatePermissionAsync.</summary>
    public Exception? ConfiguredException { get; set; }

    /// <summary>Clears recorded requests and restores default behavior.</summary>
    public void Reset() {
        _receivedRequests.Clear();
        ConfiguredResult = null;
        ConfiguredException = null;
    }

    /// <inheritdoc/>
    public Task<ActorValidationResponse> ValidatePermissionAsync(RbacValidationRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        _receivedRequests.Enqueue(request);

        if (ConfiguredException is not null) {
            throw ConfiguredException;
        }

        return Task.FromResult(ConfiguredResult ?? new ActorValidationResponse(true));
    }
}
