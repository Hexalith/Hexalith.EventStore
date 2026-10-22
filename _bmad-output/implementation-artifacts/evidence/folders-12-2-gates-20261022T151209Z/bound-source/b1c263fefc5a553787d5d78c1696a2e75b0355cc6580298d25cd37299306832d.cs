
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Configuration;
/// <summary>
/// Configuration options for per-tenant rate limiting.
/// Bound from the "EventStore:RateLimiting" configuration section.
/// </summary>
public record RateLimitingOptions {
    /// <summary>
    /// Gets the maximum number of requests permitted per window per tenant.
    /// </summary>
    public int PermitLimit { get; init; } = 1000;

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

    /// <summary>
    /// Gets per-tenant permit limit overrides. Tenants listed here use their specific limit
    /// instead of <see cref="PermitLimit"/>. Resolution: TenantPermitLimits[tenantId] > PermitLimit.
    /// </summary>
    public Dictionary<string, int> TenantPermitLimits { get; init; } = [];

    /// <summary>
    /// Gets the maximum number of requests permitted per window per consumer.
    /// Consumer identity is derived from the JWT "sub" claim.
    /// </summary>
    public int ConsumerPermitLimit { get; init; } = 100;

    /// <summary>
    /// Gets the per-consumer sliding window duration in seconds.
    /// Default is 1 second per NFR34 (100 commands/second/consumer).
    /// </summary>
    public int ConsumerWindowSeconds { get; init; } = 1;

    /// <summary>
    /// Gets the number of segments for the per-consumer sliding window.
    /// With SegmentsPerWindow=1 this is effectively a fixed window (not truly sliding).
    /// This is intentional for the "per second" NFR34 requirement — users who want smoother
    /// sliding behavior can increase segments (e.g., ConsumerWindowSeconds=10,
    /// ConsumerSegmentsPerWindow=10, ConsumerPermitLimit=1000).
    /// </summary>
    public int ConsumerSegmentsPerWindow { get; init; } = 1;

    /// <summary>
    /// Gets per-consumer permit limit overrides. Consumers listed here use their specific limit
    /// instead of <see cref="ConsumerPermitLimit"/>. Resolution: ConsumerPermitLimits[consumerId] > ConsumerPermitLimit.
    /// Usage example: { "anonymous": 10 } to explicitly throttle unauthenticated traffic.
    /// </summary>
    public Dictionary<string, int> ConsumerPermitLimits { get; init; } = [];
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

        foreach (KeyValuePair<string, int> entry in options.TenantPermitLimits) {
            if (entry.Value <= 0) {
                return ValidateOptionsResult.Fail(
                    $"EventStore:RateLimiting:TenantPermitLimits['{entry.Key}'] must be greater than 0, but was {entry.Value}.");
            }
        }

        if (options.ConsumerPermitLimit <= 0) {
            return ValidateOptionsResult.Fail(
                "EventStore:RateLimiting:ConsumerPermitLimit must be greater than 0.");
        }

        if (options.ConsumerWindowSeconds <= 0) {
            return ValidateOptionsResult.Fail(
                "EventStore:RateLimiting:ConsumerWindowSeconds must be greater than 0.");
        }

        if (options.ConsumerSegmentsPerWindow < 1) {
            return ValidateOptionsResult.Fail(
                "EventStore:RateLimiting:ConsumerSegmentsPerWindow must be at least 1.");
        }

        foreach (KeyValuePair<string, int> entry in options.ConsumerPermitLimits) {
            if (entry.Value <= 0) {
                return ValidateOptionsResult.Fail(
                    $"EventStore:RateLimiting:ConsumerPermitLimits['{entry.Key}'] must be greater than 0, but was {entry.Value}.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
