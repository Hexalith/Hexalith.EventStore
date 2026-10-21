using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// Validates <see cref="SignalROptions"/> at startup.
/// </summary>
public sealed class ValidateSignalROptions : IValidateOptions<SignalROptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, SignalROptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxGroupsPerConnection <= 0)
        {
            return ValidateOptionsResult.Fail("SignalR MaxGroupsPerConnection must be greater than zero.");
        }

        if (options.MaxDetailMetadataEntries is < 1 or > ProjectionChangedMetadataLimits.MaximumEntries)
        {
            return ValidateOptionsResult.Fail("SignalR MaxDetailMetadataEntries must be between 1 and 16.");
        }

        if (options.MaxDetailMetadataBytes is < ProjectionChangedMetadataLimits.MinimumSerializedUtf8Bytes
            or > ProjectionChangedMetadataLimits.MaximumSerializedUtf8Bytes)
            {
            return ValidateOptionsResult.Fail("SignalR MaxDetailMetadataBytes must be between 2 and 2048 serialized UTF-8 bytes.");
        }

        if (options.BackplaneRedisConnectionString is not null
            && string.IsNullOrWhiteSpace(options.BackplaneRedisConnectionString))
            {
            return ValidateOptionsResult.Fail("SignalR BackplaneRedisConnectionString must be null or a non-empty value.");
        }

        return ValidateOptionsResult.Success;
    }
}
