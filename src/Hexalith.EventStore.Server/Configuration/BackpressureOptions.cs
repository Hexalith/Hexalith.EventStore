using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Configuration options for per-aggregate backpressure (Story 4.3, FR67).
/// Bound to configuration section "EventStore:Backpressure".
/// </summary>
public record BackpressureOptions {
    /// <summary>Gets the maximum number of pending (non-terminal) commands per aggregate before backpressure rejects new commands.</summary>
    public int MaxPendingCommandsPerAggregate { get; init; } = 100;

    /// <summary>Gets the Retry-After header value in seconds returned with HTTP 429 responses.</summary>
    public int RetryAfterSeconds { get; init; } = 10;
}

/// <summary>
/// Validates that <see cref="BackpressureOptions"/> is properly configured at startup.
/// Fails fast with clear error messages for invalid configuration.
/// </summary>
public class ValidateBackpressureOptions : IValidateOptions<BackpressureOptions> {
    public ValidateOptionsResult Validate(string? name, BackpressureOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxPendingCommandsPerAggregate <= 0) {
            return ValidateOptionsResult.Fail(
                "EventStore:Backpressure:MaxPendingCommandsPerAggregate must be greater than 0.");
        }

        if (options.RetryAfterSeconds <= 0) {
            return ValidateOptionsResult.Fail(
                "EventStore:Backpressure:RetryAfterSeconds must be greater than 0.");
        }

        return ValidateOptionsResult.Success;
    }
}
