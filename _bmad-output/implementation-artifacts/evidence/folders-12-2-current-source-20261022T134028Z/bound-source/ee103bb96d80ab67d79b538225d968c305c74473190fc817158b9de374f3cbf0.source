using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Validates trusted idempotency admission configuration.</summary>
internal sealed class ValidateIdempotencyAdmissionOptions(IHostEnvironment? environment = null)
    : IValidateOptions<IdempotencyAdmissionOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, IdempotencyAdmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.DigestKeySource))
        {
            return ValidateOptionsResult.Fail("The idempotency digest-key source is invalid.");
        }

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.ActiveDigestKeyVersion))
        {
            return ValidateOptionsResult.Fail(
                "Enabled idempotency admission requires one active digest-key version.");
        }

        var versions = new HashSet<string>(StringComparer.Ordinal) { options.ActiveDigestKeyVersion };
        if (options.ReaderDigestKeyVersions.Any(version =>
            string.IsNullOrWhiteSpace(version) || !versions.Add(version)))
        {
            return ValidateOptionsResult.Fail(
                "Retained idempotency reader versions must be named, distinct, and exclude the active version.");
        }

        if (options.DigestKeySource == IdempotencyDigestKeySource.DaprSecret)
        {
            if (versions.Contains("generation"))
            {
                return ValidateOptionsResult.Fail(
                    "The secret map key 'generation' is reserved and cannot be an active or reader digest-key version.");
            }

            return string.IsNullOrWhiteSpace(options.DigestKeySecretStoreName)
                || string.IsNullOrWhiteSpace(options.DigestKeySecretName)
                || string.IsNullOrWhiteSpace(options.DigestKeySecretGeneration)
                || options.DigestKeys.Count > 0
                ? ValidateOptionsResult.Fail(
                    "Secret-backed idempotency admission requires store, map, and generation metadata and forbids inline keys.")
                : ValidateOptionsResult.Success;
        }

        if (environment is null
            || (!environment.IsDevelopment()
                && !environment.IsEnvironment("Test")
                && !environment.IsEnvironment("Testing")))
        {
            return ValidateOptionsResult.Fail(
                "Configuration-backed idempotency digest keys are permitted only in Development or test environments.");
        }

        if (options.DigestKeys.Count != versions.Count
            || versions.Any(version =>
                !options.DigestKeys.TryGetValue(version, out string? encodedKey)
                || !TryDecodeStrongKey(encodedKey)))
        {
            return ValidateOptionsResult.Fail(
                "Configured idempotency admission requires exactly one strong key for every active and reader version.");
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
