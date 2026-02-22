
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.CommandApi.Configuration;
/// <summary>
/// Configuration options for per-tenant rate limiting.
/// Bound from the "EventStore:RateLimiting" configuration section.
/// </summary>
public record RateLimitingOptions {
    /// <summary>
    /// Gets the maximum number of requests permitted per window per tenant.
    /// </summary>
    public int PermitLimit { get; init; } = 100;

    /// <summary>
    /// Gets the sliding window duration in seconds.
    /// </summary>
    public int WindowSeconds { get; init; } = 60;

    /// <summary>
    /// Gets the number of segments the window is divided into.
    /// </summary>
    public int SegmentsPerWindow { get; init; } = 6;

    /// <summary>
    /// Gets the maximum number of requests to queue when the limit is reached.
    /// 0 means immediate rejection (no queuing).
    /// </summary>
    public int QueueLimit { get; init; } = 0;
}

/// <summary>
/// Validates that <see cref="RateLimitingOptions"/> is properly configured at startup.
/// Fails fast with clear error messages for invalid configuration.
/// </summary>
public class ValidateRateLimitingOptions : IValidateOptions<RateLimitingOptions> {
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PermitLimit <= 0) {
            return ValidateOptionsResult.Fail(
                "EventStore:RateLimiting:PermitLimit must be greater than 0.");
        }

        if (options.WindowSeconds <= 0) {
            return ValidateOptionsResult.Fail(
                "EventStore:RateLimiting:WindowSeconds must be greater than 0.");
        }

        if (options.SegmentsPerWindow < 1) {
            return ValidateOptionsResult.Fail(
                "EventStore:RateLimiting:SegmentsPerWindow must be at least 1.");
        }

        if (options.QueueLimit < 0) {
            return ValidateOptionsResult.Fail(
                "EventStore:RateLimiting:QueueLimit must be 0 or greater.");
        }

        return ValidateOptionsResult.Success;
    }
}
