
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.CommandApi.Configuration;

/// <summary>
/// Configuration options for event store authorization.
/// Bound from the "EventStore:Authorization" configuration section.
/// When actor names are null, claims-based authorization is used (default).
/// When non-null, DAPR actor-based authorization is used (Story 17-2).
/// </summary>
public record EventStoreAuthorizationOptions {
    /// <summary>
    /// Gets the DAPR actor type name for tenant validation.
    /// When null, claims-based tenant validation is used (default).
    /// </summary>
    public string? TenantValidatorActorName { get; init; }

    /// <summary>
    /// Gets the DAPR actor type name for RBAC validation.
    /// When null, claims-based RBAC validation is used (default).
    /// </summary>
    public string? RbacValidatorActorName { get; init; }

    /// <summary>
    /// Gets the Retry-After header value (in seconds) returned when an actor-based
    /// authorization service is unavailable (503). Valid range: 1-300.
    /// </summary>
    public int RetryAfterSeconds { get; init; } = 5;
}

/// <summary>
/// Validates <see cref="EventStoreAuthorizationOptions"/> at startup.
/// Null values are valid (means claims-based default).
/// Non-null values must be non-empty, non-whitespace strings.
/// </summary>
public class ValidateEventStoreAuthorizationOptions : IValidateOptions<EventStoreAuthorizationOptions> {
    public ValidateOptionsResult Validate(string? name, EventStoreAuthorizationOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TenantValidatorActorName is not null
            && string.IsNullOrWhiteSpace(options.TenantValidatorActorName)) {
            return ValidateOptionsResult.Fail(
                "EventStore:Authorization:TenantValidatorActorName, when specified, must not be empty or whitespace.");
        }

        if (options.RbacValidatorActorName is not null
            && string.IsNullOrWhiteSpace(options.RbacValidatorActorName)) {
            return ValidateOptionsResult.Fail(
                "EventStore:Authorization:RbacValidatorActorName, when specified, must not be empty or whitespace.");
        }

        if (options.RetryAfterSeconds is < 1 or > 300) {
            return ValidateOptionsResult.Fail(
                "EventStore:Authorization:RetryAfterSeconds must be between 1 and 300 seconds.");
        }

        return ValidateOptionsResult.Success;
    }
}
