
using System.Collections.Concurrent;

using Hexalith.EventStore.Server.Actors.Authorization;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>
/// Fake tenant validator actor for testing. Records all invocations for assertion.
/// Configurable results and exceptions for simulating various actor behaviors.
/// </summary>
public class FakeTenantValidatorActor : ITenantValidatorActor {
    private readonly ConcurrentQueue<TenantValidationRequest> _receivedRequests = new();

    /// <summary>Gets the list of received requests for assertion.</summary>
    public IReadOnlyCollection<TenantValidationRequest> ReceivedRequests => [.. _receivedRequests];

    /// <summary>Gets or sets the result to return from ValidateTenantAccessAsync.</summary>
    public ActorValidationResponse? ConfiguredResult { get; set; }

    /// <summary>Gets or sets the exception to throw from ValidateTenantAccessAsync.</summary>
    public Exception? ConfiguredException { get; set; }

    /// <inheritdoc/>
    public Task<ActorValidationResponse> ValidateTenantAccessAsync(TenantValidationRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        _receivedRequests.Enqueue(request);

        if (ConfiguredException is not null) {
            throw ConfiguredException;
        }

        return Task.FromResult(ConfiguredResult ?? new ActorValidationResponse(true));
    }
}
