
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Configuration;
/// <summary>
/// Configuration options for per-aggregate backpressure (FR67).
/// Bound to configuration section "EventStore:Backpressure".
/// When an aggregate's in-flight command count exceeds <see cref="MaxPendingCommandsPerAggregate"/>,
/// new commands targeting that aggregate are rejected with HTTP 429 before entering the pipeline.
/// </summary>
public record BackpressureOptions {
    /// <summary>
    /// Gets the maximum number of pending (in-flight) commands allowed per aggregate before
    /// backpressure triggers an HTTP 429 rejection. Setting to 0 disables backpressure entirely.
    /// </summary>
    public int MaxPendingCommandsPerAggregate { get; init; } = 100;
}

/// <summary>
/// Validates that <see cref="BackpressureOptions"/> is properly configured at startup.
/// Fails fast with a clear error message for invalid configuration.
/// </summary>
public class ValidateBackpressureOptions : IValidateOptions<BackpressureOptions> {
    public ValidateOptionsResult Validate(string? name, BackpressureOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxPendingCommandsPerAggregate < 0) {
            return ValidateOptionsResult.Fail(
                "EventStore:Backpressure:MaxPendingCommandsPerAggregate must be 0 (disabled) or greater.");
        }

        return ValidateOptionsResult.Success;
    }
}
