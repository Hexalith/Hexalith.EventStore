using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Validates trusted idempotency admission configuration.</summary>
internal sealed class ValidateIdempotencyAdmissionOptions : IValidateOptions<IdempotencyAdmissionOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, IdempotencyAdmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.ActiveDigestKeyVersion)
            || !options.DigestKeys.TryGetValue(options.ActiveDigestKeyVersion, out string? encodedKey)
            || !TryDecodeStrongKey(encodedKey))
        {
            return ValidateOptionsResult.Fail(
                "Enabled idempotency admission requires an active base64 digest key of at least 32 bytes.");
        }

        foreach (KeyValuePair<string, string> key in options.DigestKeys)
        {
            if (string.IsNullOrWhiteSpace(key.Key) || !TryDecodeStrongKey(key.Value))
            {
                return ValidateOptionsResult.Fail(
                    "Every idempotency digest key version must be named and contain at least 32 bytes.");
            }
        }

        foreach (KeyValuePair<string, IdempotencyAdmissionOperationOptions> operation in options.Operations)
        {
            if (string.IsNullOrWhiteSpace(operation.Key)
                || operation.Value.DescriptorVersion <= 0
                || !Enum.IsDefined(operation.Value.RetentionTier))
            {
                return ValidateOptionsResult.Fail(
                    "Every idempotency operation must have a name, positive descriptor version, and known retention tier.");
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static bool TryDecodeStrongKey(string? encodedKey)
    {
        if (string.IsNullOrWhiteSpace(encodedKey))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(encodedKey).Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
