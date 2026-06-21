using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// Configuration options for the EventStore SignalR hub.
/// Bound from configuration section <c>EventStore:SignalR</c>.
/// </summary>
public class SignalROptions {
    /// <summary>
    /// Gets a value indicating whether SignalR real-time notifications are enabled.
    /// Default: <c>false</c> (disabled). Must be explicitly enabled in configuration.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the Redis connection string for the SignalR backplane (multi-instance deployments).
    /// If <c>null</c>, falls back to environment variable <c>EVENTSTORE_SIGNALR_REDIS</c>.
    /// If neither is set, the backplane is disabled (single-instance mode).
    /// </summary>
    public string? BackplaneRedisConnectionString { get; init; }

    /// <summary>
    /// Gets the maximum number of SignalR groups a single client connection can join.
    /// Prevents resource exhaustion from flooding. Default: 50.
    /// </summary>
    public int MaxGroupsPerConnection { get; init; } = 50;

    /// <summary>
    /// Gets the maximum number of key/value entries carried in a metadata-rich
    /// projection-changed detail broadcast. Entries beyond this cap are clipped (the
    /// broadcast still fires). Keeps the channel metadata-only and bounded. Default: 16.
    /// </summary>
    public int MaxDetailMetadataEntries { get; init; } = 16;

    /// <summary>
    /// Gets the maximum total UTF-8 byte size (sum of all keys and values) carried in a
    /// metadata-rich projection-changed detail broadcast. Entries that would exceed this cap
    /// are clipped (the broadcast still fires). Keeps the channel metadata-only and bounded.
    /// Default: 2048.
    /// </summary>
    public int MaxDetailMetadataBytes { get; init; } = 2048;
}

/// <summary>
/// Validates <see cref="SignalROptions"/> at startup.
/// </summary>
public sealed class ValidateSignalROptions : IValidateOptions<SignalROptions> {
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, SignalROptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxGroupsPerConnection <= 0) {
            return ValidateOptionsResult.Fail("SignalR MaxGroupsPerConnection must be greater than zero.");
        }

        if (options.MaxDetailMetadataEntries <= 0) {
            return ValidateOptionsResult.Fail("SignalR MaxDetailMetadataEntries must be greater than zero.");
        }

        if (options.MaxDetailMetadataBytes <= 0) {
            return ValidateOptionsResult.Fail("SignalR MaxDetailMetadataBytes must be greater than zero.");
        }

        if (options.BackplaneRedisConnectionString is not null
            && string.IsNullOrWhiteSpace(options.BackplaneRedisConnectionString)) {
            return ValidateOptionsResult.Fail("SignalR BackplaneRedisConnectionString must be null or a non-empty value.");
        }

        return ValidateOptionsResult.Success;
    }
}
